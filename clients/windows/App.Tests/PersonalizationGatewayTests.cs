using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC2 — the App-side <see cref="PersonalizationGateway"/> over the REAL <see cref="PersonalityEngine"/>
/// with a FAKE <see cref="ILlmClient"/> + an in-memory <see cref="ISecretStore"/>. Proves the render-time
/// seam: with <see cref="Persona.Basic"/> (pass-through) <c>ResolveMarkdownAsync</c> returns the fetched
/// markdown BYTE-IDENTICAL (the AC2 seam no-regression — the render stays Epic-3 faithful) and the LLM is
/// NEVER called; with a non-pass-through persona + a fake returning canned markdown it returns the
/// transformed markdown; on failure it returns the ORIGINAL. The gateway is total (it returns the engine's
/// always-renderable Markdown). RED until App+Agent types exist.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.App):
///
///   public sealed class PersonalizationGateway {
///       public PersonalizationGateway(PersonalityEngine engine, Func&lt;Persona&gt; selectedPersona);
///       public Task&lt;string&gt; ResolveMarkdownAsync(string fetchedMarkdown, Uri pageUrl, CancellationToken ct); }
/// </summary>
public class PersonalizationGatewayTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");

    // ---- fakes (App.Tests cannot see Agent.Tests' internals; declare local doubles) -------------

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

    private sealed class InMemorySecretStore : ISecretStore
    {
        private string? _key;
        public InMemorySecretStore(string? seed = null) => _key = seed;
        public bool HasApiKey => !string.IsNullOrEmpty(_key);
        public string? GetApiKey() => _key;
        public void SetApiKey(string key) => _key = key;
        public void ClearApiKey() => _key = null;
    }

    private static Persona CustomPersona() =>
        new("custom", "Custom", "You are a transform.", IsPassThrough: false);

    // ---- tests ---------------------------------------------------------------------------------

    [Fact] // AC2 — Basic pass-through returns the fetched markdown unchanged (byte-identical); LLM never called.
    public async Task ResolveMarkdownAsync_Basic_ReturnsFetchedMarkdown_Unchanged_AndNeverCallsLlm()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# SHOULD NOT BE USED"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "any-key"));
        var gateway = new PersonalizationGateway(engine, () => Persona.Basic);

        const string fetched = "# H\n\npara";
        string resolved = await gateway.ResolveMarkdownAsync(fetched, PageUrl, CancellationToken.None);

        Assert.Equal(fetched, resolved); // byte-identical: the seam is additive, the render stays Epic-3 faithful.
        Assert.Equal(0, llm.Calls);
    }

    [Fact] // AC2 — a non-pass-through persona + canned transform -> the gateway returns the transformed markdown.
    public async Task ResolveMarkdownAsync_TransformPersona_ReturnsTransformedMarkdown()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# Transformed"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));
        var gateway = new PersonalizationGateway(engine, CustomPersona);

        string resolved = await gateway.ResolveMarkdownAsync("# H\n\npara", PageUrl, CancellationToken.None);

        Assert.Equal("# Transformed", resolved);
        Assert.Equal(1, llm.Calls);
    }

    [Fact] // AC2 / AC4 — on LLM failure the gateway returns the ORIGINAL fetched markdown (total).
    public async Task ResolveMarkdownAsync_OnFailure_ReturnsOriginal()
    {
        var llm = new CountingLlmClient(LlmResult.Failure("provider error"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));
        var gateway = new PersonalizationGateway(engine, CustomPersona);

        const string fetched = "# H\n\npara";
        string resolved = await gateway.ResolveMarkdownAsync(fetched, PageUrl, CancellationToken.None);

        Assert.Equal(fetched, resolved);
    }
}
