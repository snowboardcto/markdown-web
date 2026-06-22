using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using TheMarkdownWeb.Rendering;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.3 AC4 (render level) — the per-persona structural difference SURVIVES to the rendered
/// <see cref="FlowDocument"/>. A <c>[StaFact]</c> (WPF objects → STA): each persona's keyed output is fed
/// through a real <see cref="ContentHostController"/> over a real <see cref="FlowDocumentRenderer"/>; the
/// two hosted documents' text differ — the cozy marker appears in the cozy doc and NOT the terminal one,
/// and vice-versa. The expected marker is derived from <see cref="PersonaRegistry.Seed"/>'s prompt via the
/// SAME <see cref="KeyedLlmClient.Marker"/> function (no hardcoded prompt wording). Construct-not-Show; no
/// pixels; no socket; no real model. Mirrors <see cref="PersonalityRerenderSeamTests"/> / ContentHostTests.
/// </summary>
public class StructuralRenderDiffersTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");
    private const string Source = "# Source\n\nbody";

    // ---- local fakes -----------------------------------------------------------------------------

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

    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    private static ContentHostController NewHost() =>
        new(new FlowDocumentScrollViewer(), new FlowDocumentRenderer(), new NullImageLoader(), _ => Task.CompletedTask);

    private static string DocumentText(FlowDocument doc)
        => new TextRange(doc.ContentStart, doc.ContentEnd).Text;

    private static Persona ById(string id) => PersonaRegistry.Seed.Single(p => p.Id == id);

    private static async Task<string> Resolve(Persona persona)
    {
        var gateway = new PersonalizationGateway(
            new PersonalityEngine(new KeyedLlmClient(), new InMemorySecretStore(seed: "real-key")),
            () => persona);
        return await gateway.ResolveMarkdownAsync(Source, PageUrl, CancellationToken.None);
    }

    [StaFact] // AC4 — cozy-output and terminal-output render to two DIFFERENT FlowDocuments.
    public async Task CozyAndTerminal_RenderToDifferentFlowDocuments()
    {
        Persona cozy = ById("cozy");
        Persona terminal = ById("terminal");

        string cozyMarkdown = await Resolve(cozy);
        string terminalMarkdown = await Resolve(terminal);

        ContentHostController cozyHost = NewHost();
        ContentHostController terminalHost = NewHost();
        cozyHost.ShowMarkdown(cozyMarkdown, PageUrl);
        terminalHost.ShowMarkdown(terminalMarkdown, PageUrl);

        string cozyText = DocumentText(cozyHost.Host.Document!);
        string terminalText = DocumentText(terminalHost.Host.Document!);

        // The two rendered documents differ — the structural difference survives to the render.
        Assert.NotEqual(cozyText, terminalText);

        // Each carries its OWN persona marker, absent from the other (derived via the same Marker fn).
        string cozyMarker = KeyedLlmClient.Marker(cozy.SystemPrompt);
        string terminalMarker = KeyedLlmClient.Marker(terminal.SystemPrompt);
        Assert.Contains(cozyMarker, cozyText);
        Assert.DoesNotContain(cozyMarker, terminalText);
        Assert.Contains(terminalMarker, terminalText);
        Assert.DoesNotContain(terminalMarker, cozyText);
    }
}
