using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.3 AC4 — two personas → structurally-DIFFERENT rendered markdown for the SAME source.
/// Pure <c>[Fact]</c>s (plain CLR; no WPF, no STA, no socket, no key, no model) over the REAL
/// <see cref="PersonalizationGateway"/> + the REAL <see cref="PersonalityEngine"/> + a systemPrompt-KEYED
/// fake <see cref="ILlmClient"/> (Q-Marker form (a) — output is a PURE function of the received prompt).
/// The expected marker is computed from <see cref="PersonaRegistry.Seed"/>'s prompt via the SAME
/// <see cref="KeyedLlmClient.Marker"/> function — NO hardcoded prompt wording, so the proof survives
/// later prompt-wording refinement:
///   • cozy-output != terminal-output for the same source (the load-bearing assertion);
///   • each output == Marker(seed.SystemPrompt) + "\n\n" + source (the keyed fake's exact shape);
///   • both Outcome == Transformed.
///
/// App.Tests cannot see Agent.Tests internals → the keyed fake + in-memory store are declared locally
/// (the established 4-2 local-double pattern). This compiles against existing types and PASSES on the
/// pipeline; the RED 4.3 tests are the AC2 distinctness asserts (Agent.Tests), not these.
/// </summary>
public class StructuralDifferenceTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");
    private const string Source = "# Source\n\nbody";

    // ---- local fakes (App.Tests cannot see Agent.Tests internals) ------------------------------

    /// <summary>The systemPrompt-keyed fake (Q-Marker form (a)). Output is a PURE function of the prompt.</summary>
    private sealed class KeyedLlmClient : ILlmClient
    {
        public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
            => Task.FromResult(LlmResult.Success(Marker(systemPrompt) + "\n\n" + pageMarkdown));

        public static string Marker(string systemPrompt)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt ?? string.Empty));
            return "k" + Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
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

    private static Persona ById(string id) => PersonaRegistry.Seed.Single(p => p.Id == id);

    /// <summary>Resolves the source through a gateway over <c>() =&gt; persona</c> with a key present.</summary>
    private static async Task<string> Resolve(Persona persona)
    {
        var gateway = new PersonalizationGateway(
            new PersonalityEngine(new KeyedLlmClient(), new InMemorySecretStore(seed: "real-key")),
            () => persona);
        return await gateway.ResolveMarkdownAsync(Source, PageUrl, CancellationToken.None);
    }

    [Fact] // AC4 — cozy-output != terminal-output for the SAME source (the marker differs).
    public async Task CozyAndTerminal_ProduceDifferentMarkdown_ForSameSource()
    {
        string cozyOut = await Resolve(ById("cozy"));
        string terminalOut = await Resolve(ById("terminal"));

        Assert.NotEqual(cozyOut, terminalOut);
    }

    [Fact] // AC4 — each output is exactly Marker(seed.SystemPrompt) + "\n\n" + source (deterministic shape).
    public async Task EachOutput_EqualsMarkerPlusSource_DerivedFromRegistryPrompt()
    {
        Persona cozy = ById("cozy");
        Persona terminal = ById("terminal");

        string cozyOut = await Resolve(cozy);
        string terminalOut = await Resolve(terminal);

        Assert.Equal(KeyedLlmClient.Marker(cozy.SystemPrompt) + "\n\n" + Source, cozyOut);
        Assert.Equal(KeyedLlmClient.Marker(terminal.SystemPrompt) + "\n\n" + Source, terminalOut);
    }

    [Fact] // AC4 — both personas yield Transformed (non-empty valid markdown, source preserved).
    public async Task BothPersonas_YieldTransformedOutcome()
    {
        var cozyGateway = new PersonalizationGateway(
            new PersonalityEngine(new KeyedLlmClient(), new InMemorySecretStore(seed: "real-key")),
            () => ById("cozy"));
        var terminalGateway = new PersonalizationGateway(
            new PersonalityEngine(new KeyedLlmClient(), new InMemorySecretStore(seed: "real-key")),
            () => ById("terminal"));

        string cozyOut = await cozyGateway.ResolveMarkdownAsync(Source, PageUrl, CancellationToken.None);
        string terminalOut = await terminalGateway.ResolveMarkdownAsync(Source, PageUrl, CancellationToken.None);

        Assert.Equal(PersonalizationOutcome.Transformed, cozyGateway.LastOutcome);
        Assert.Equal(PersonalizationOutcome.Transformed, terminalGateway.LastOutcome);
        Assert.Contains(Source, cozyOut);     // the source body is preserved (marker is a prefix).
        Assert.Contains(Source, terminalOut);
    }
}
