using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.2 AC2 (the LOAD-BEARING AC) + AC5 — the re-render-in-place coordinator. Pure <c>[Fact]</c>s
/// (plain CLR; no WPF, no STA, no socket, no fetcher) over a real <see cref="PersonalizationGateway"/>
/// (over the real <see cref="PersonalityEngine"/> + a fake <see cref="ILlmClient"/>) and a RECORDING
/// render-sink:
///   • <c>SetCurrentPage(raw,url)</c> then <c>RerenderAsync</c> -> the sink receives the gateway output;
///   • RE-PERSONALIZE FROM HELD RAW: Basic -> Cozy -> Basic returns BYTE-IDENTICAL raw (the held value is
///     the RAW source, never a transform-of-a-transform);
///   • NO-RE-FETCH BY CONSTRUCTION: the coordinator's only ctor takes
///     <c>(PersonalizationGateway, Action&lt;string,Uri&gt;)</c> — there is NO fetcher/HttpClient/endpoint
///     dependency, so a switch CANNOT issue a GET (asserted by the ctor signature + a reflection sweep of
///     the type's fields for any fetch-shaped dependency);
///   • <c>RerenderAsync</c> before any <c>SetCurrentPage</c> -> safe no-op (no sink call, no throw);
///   • LAST-WINS: two rapid <c>RerenderAsync</c> (the first awaiting a GATED slow gateway) -> only the
///     latest result reaches the sink (the stale generation is dropped);
///   • a fresh <c>SetCurrentPage</c> invalidates a pending re-render (the generation-bump rule — a
///     navigation supersedes an in-flight switch);
///   • AC5: <c>LastOutcome</c>/<c>LastNotice</c> are passed through from the gateway (PassThrough for Basic,
///     NeedsKey for a no-key transform persona, FellBack for a failing fake LLM).
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.App):
///
///   public sealed class PersonalityRerenderCoordinator {
///       public PersonalityRerenderCoordinator(PersonalizationGateway gateway, Action&lt;string,Uri&gt; renderSink);
///       public void SetCurrentPage(string rawMarkdown, Uri pageUrl);
///       public Task RerenderAsync(CancellationToken ct = default);
///       public PersonalizationOutcome LastOutcome { get; }
///       public string? LastNotice { get; } }
///
/// RED until Step 5 adds <c>App/PersonalityRerenderCoordinator.cs</c>.
/// </summary>
public class PersonalityRerenderCoordinatorTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");

    private static readonly Persona Cozy =
        new("cozy", "Cozy Reader", "You are the Cozy Reader.", IsPassThrough: false);

    // ---- local fakes ---------------------------------------------------------------------------

    private sealed class CountingLlmClient : ILlmClient
    {
        private readonly LlmResult _result;
        public CountingLlmClient(LlmResult result) => _result = result;
        public int Calls { get; private set; }
        public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_result);
        }
    }

    /// <summary>A fake whose completion BLOCKS on a per-call TaskCompletionSource so a test can order
    /// two in-flight re-renders (the last-wins proof). Each call returns a DISTINCT canned markdown.</summary>
    private sealed class GatedLlmClient : ILlmClient
    {
        private readonly Queue<(TaskCompletionSource<bool> Gate, string Markdown)> _gates = new();
        public void Enqueue(TaskCompletionSource<bool> gate, string markdown) => _gates.Enqueue((gate, markdown));
        public async Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
        {
            (TaskCompletionSource<bool> gate, string markdown) = _gates.Dequeue();
            await gate.Task.ConfigureAwait(false);
            return LlmResult.Success(markdown);
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private string? _key;
        public InMemorySecretStore(string? seed = null) => _key = seed;
        public bool HasApiKey => !string.IsNullOrEmpty(_key);
        public string? GetApiKey() => _key;
        public void SetApiKey(string key) => _key = key;
        public void ClearApiKey() => _key = null;
    }

    /// <summary>Records every (markdown, url) the coordinator pushes; <see cref="Last"/> is the latest.</summary>
    private sealed class RecordingSink
    {
        public List<(string Markdown, Uri Url)> Calls { get; } = new();
        public Action<string, Uri> Sink => (md, url) => Calls.Add((md, url));
        public string? Last => Calls.Count == 0 ? null : Calls[^1].Markdown;
        public int Count => Calls.Count;
    }

    private static PersonalizationGateway Gateway(ILlmClient llm, ISecretStore store, Func<Persona> persona)
        => new(new PersonalityEngine(llm, store), persona);

    // ---- tests ---------------------------------------------------------------------------------

    [Fact] // AC2 — SetCurrentPage + RerenderAsync pushes the gateway output to the sink.
    public async Task Rerender_AfterSetCurrentPage_PushesGatewayOutputToSink()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# Cozy"));
        Persona current = Cozy;
        var gateway = Gateway(llm, new InMemorySecretStore(seed: "real-key"), () => current);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);

        coordinator.SetCurrentPage("# Held\n\nbody", PageUrl);
        await coordinator.RerenderAsync();

        Assert.Equal(1, sink.Count);
        Assert.Equal("# Cozy", sink.Last);
        Assert.Equal(PageUrl, sink.Calls[^1].Url);
    }

    [Fact] // AC2 — NO RE-FETCH BY CONSTRUCTION: the coordinator holds no fetcher-shaped dependency.
    public void Coordinator_HasNoFetcherDependency_ByConstruction()
    {
        Type t = typeof(PersonalityRerenderCoordinator);

        // (a) The only public ctor takes exactly (PersonalizationGateway, Action<string,Uri>) — no fetcher.
        ConstructorInfo[] ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Single(ctors);
        Type[] paramTypes = ctors[0].GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Equal(new[] { typeof(PersonalizationGateway), typeof(Action<string, Uri>) }, paramTypes);

        // (b) No field is a fetch-shaped dependency (MarkdownFetcher / HttpClient / a fetch delegate).
        FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (FieldInfo f in fields)
        {
            string typeName = f.FieldType.FullName ?? f.FieldType.Name;
            Assert.DoesNotContain("MarkdownFetcher", typeName);
            Assert.DoesNotContain("HttpClient", typeName);
            // A Func<Uri,...>/endpoint-shaped delegate would be a smuggled fetcher; the render sink is
            // Action<string,Uri>, never a Func returning a fetch result.
            Assert.DoesNotContain("FetchResult", typeName);
        }
    }

    [Fact] // AC2 — re-personalize FROM HELD RAW: Basic -> Cozy -> Basic returns the byte-identical original.
    public async Task Rerender_BasicCozyBasic_ReturnsByteIdenticalRaw_FromHeldSource()
    {
        // A single fake returns "# Cozy" for any transform call; Basic short-circuits before the LLM.
        var llm = new CountingLlmClient(LlmResult.Success("# Cozy"));
        Persona current = Persona.Basic;
        var gateway = Gateway(llm, new InMemorySecretStore(seed: "real-key"), () => current);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);

        const string heldRaw = "# Original\n\nthe source body";
        coordinator.SetCurrentPage(heldRaw, PageUrl);

        // Basic -> the held RAW, byte-identical (pass-through; LLM never called).
        await coordinator.RerenderAsync();
        Assert.Equal(heldRaw, sink.Last);

        // Cozy -> the canned transform OF the held RAW.
        current = Cozy;
        await coordinator.RerenderAsync();
        Assert.Equal("# Cozy", sink.Last);

        // Basic again -> the held RAW again, byte-identical (NOT a transform-of-a-transform).
        current = Persona.Basic;
        await coordinator.RerenderAsync();
        Assert.Equal(heldRaw, sink.Last);

        // The LLM was called exactly once (only for the single Cozy render); Basic never called it.
        Assert.Equal(1, llm.Calls);
    }

    [Fact] // AC2 — RerenderAsync before any SetCurrentPage is a safe no-op (no sink call, no throw).
    public async Task Rerender_WithNothingHeld_IsNoOp()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# UNUSED"));
        var gateway = Gateway(llm, new InMemorySecretStore(seed: "real-key"), () => Cozy);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);

        await coordinator.RerenderAsync(); // nothing held yet

        Assert.Equal(0, sink.Count);
        Assert.Equal(0, llm.Calls);
    }

    [Fact] // AC2 — LAST-WINS: a superseded re-render's result is dropped; only the latest reaches the sink.
    public async Task Rerender_LastWins_DropsSupersededResult()
    {
        var gated = new GatedLlmClient();
        var firstGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        gated.Enqueue(firstGate, "# FIRST (stale)");
        gated.Enqueue(secondGate, "# SECOND (winner)");

        Persona current = Cozy;
        var gateway = Gateway(gated, new InMemorySecretStore(seed: "real-key"), () => current);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);
        coordinator.SetCurrentPage("# Held", PageUrl);

        // Start two re-renders; both block on their gates. The SECOND claims the newer generation.
        Task first = coordinator.RerenderAsync();
        Task second = coordinator.RerenderAsync();

        // Release the SECOND (newer) first, then the FIRST (stale).
        secondGate.SetResult(true);
        firstGate.SetResult(true);
        await Task.WhenAll(first, second);

        // Only the winner reached the sink; the stale generation was dropped.
        Assert.Equal("# SECOND (winner)", sink.Last);
        Assert.DoesNotContain(sink.Calls, c => c.Markdown == "# FIRST (stale)");
    }

    [Fact] // AC2 — a fresh SetCurrentPage (a navigation) invalidates a pending re-render (the generation bump).
    public async Task SetCurrentPage_InvalidatesPendingRerender()
    {
        var gated = new GatedLlmClient();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        gated.Enqueue(gate, "# STALE SWITCH");

        Persona current = Cozy;
        var gateway = Gateway(gated, new InMemorySecretStore(seed: "real-key"), () => current);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);
        coordinator.SetCurrentPage("# Page A", PageUrl);

        // A switch re-render starts and blocks awaiting the gateway.
        Task pending = coordinator.RerenderAsync();

        // A navigation lands: SetCurrentPage bumps the generation, superseding the in-flight switch.
        coordinator.SetCurrentPage("# Page B", new Uri("https://themarkdownweb.com/b.md"));

        // Release the stale switch; its result must be DROPPED (it would otherwise overwrite page B's render).
        gate.SetResult(true);
        await pending;

        Assert.DoesNotContain(sink.Calls, c => c.Markdown == "# STALE SWITCH");
    }

    [Fact] // AC5 — Basic re-render publishes PassThrough + null notice (instant; LLM never called).
    public async Task Rerender_Basic_PublishesPassThrough_NullNotice()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# UNUSED"));
        var gateway = Gateway(llm, new InMemorySecretStore(seed: "real-key"), () => Persona.Basic);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);
        coordinator.SetCurrentPage("# Held", PageUrl);

        await coordinator.RerenderAsync();

        Assert.Equal(PersonalizationOutcome.PassThrough, coordinator.LastOutcome);
        Assert.Null(coordinator.LastNotice);
        Assert.Equal(0, llm.Calls);
    }

    [Fact] // AC4 / AC5 — a non-Basic persona with NO key publishes NeedsKey + a key-free notice; original rendered.
    public async Task Rerender_NonBasic_NoKey_PublishesNeedsKey_AndRendersOriginal()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# SHOULD NOT BE USED"));
        var gateway = Gateway(llm, new InMemorySecretStore(seed: null), () => Cozy); // empty store
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);
        const string heldRaw = "# Original";
        coordinator.SetCurrentPage(heldRaw, PageUrl);

        await coordinator.RerenderAsync();

        Assert.Equal(PersonalizationOutcome.NeedsKey, coordinator.LastOutcome);
        Assert.False(string.IsNullOrWhiteSpace(coordinator.LastNotice), "NeedsKey must surface a non-empty notice.");
        Assert.Equal(heldRaw, sink.Last); // the HELD ORIGINAL is rendered (never blank/broken).
        Assert.Equal(0, llm.Calls);       // no provider call without a key.
    }

    [Fact] // AC5 — a non-Basic persona with a key + a FAILING fake LLM publishes FellBack + renders the original.
    public async Task Rerender_NonBasic_LlmFails_PublishesFellBack_AndRendersOriginal()
    {
        var llm = new CountingLlmClient(LlmResult.Failure("provider error"));
        var gateway = Gateway(llm, new InMemorySecretStore(seed: "real-key"), () => Cozy);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);
        const string heldRaw = "# Original";
        coordinator.SetCurrentPage(heldRaw, PageUrl);

        await coordinator.RerenderAsync();

        Assert.Equal(PersonalizationOutcome.FellBack, coordinator.LastOutcome);
        Assert.False(string.IsNullOrWhiteSpace(coordinator.LastNotice), "FellBack must surface a non-empty notice.");
        Assert.Equal(heldRaw, sink.Last); // the held original is rendered on fallback.
    }
}
