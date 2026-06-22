using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using TheMarkdownWeb.Rendering;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.2 AC2 ([StaFact] App-seam) — a selection change re-renders the HELD page INTO THE HOST through
/// the gateway, with NO re-fetch (the coordinator has no fetcher). Mirrors
/// <see cref="PersonalizationSeamTests"/>: a real <see cref="ContentHostController"/> over a real
/// <see cref="FlowDocumentRenderer"/> into a <see cref="FlowDocumentScrollViewer"/>, a real
/// <see cref="PersonalizationGateway"/> over the real <see cref="PersonalityEngine"/> + a fake
/// <see cref="ILlmClient"/>, and the <see cref="PersonalityRerenderCoordinator"/> whose render-sink is
/// <c>host.ShowMarkdown</c>. SetCurrentPage holds the RAW; switching to Cozy + RerenderAsync hosts the
/// transformed markdown; switching back to Basic hosts the byte-identical original. Construct-not-Show;
/// no pixels; no socket. RED until Step 5 adds the coordinator.
/// </summary>
public class PersonalityRerenderSeamTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");

    private static readonly Persona Cozy =
        new("cozy", "Cozy Reader", "You are the Cozy Reader.", IsPassThrough: false);

    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly LlmResult _result;
        public FakeLlmClient(LlmResult result) => _result = result;
        public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
            => Task.FromResult(_result);
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

    private static ContentHostController NewHost() =>
        new(new FlowDocumentScrollViewer(), new FlowDocumentRenderer(), new NullImageLoader(), _ => Task.CompletedTask);

    private static string DocumentText(FlowDocument doc)
        => new TextRange(doc.ContentStart, doc.ContentEnd).Text;

    [StaFact] // AC2 — switching persona re-renders the HELD page through the gateway INTO the host (no re-fetch).
    public async Task SwitchPersona_RerendersHeldPageIntoHost_TransformedThenBackToOriginal()
    {
        Persona current = Persona.Basic;
        var engine = new PersonalityEngine(
            new FakeLlmClient(LlmResult.Success("# Cozy Heading\n\nReworked.")),
            new InMemorySecretStore(seed: "real-key"));
        var gateway = new PersonalizationGateway(engine, () => current);
        ContentHostController host = NewHost();
        var coordinator = new PersonalityRerenderCoordinator(gateway, (md, url) => host.ShowMarkdown(md, url));

        const string heldRaw = "# Original Heading\n\nThe source body.";
        coordinator.SetCurrentPage(heldRaw, PageUrl);

        // Switch to Cozy -> the host reflects the TRANSFORMED markdown (from the held raw).
        current = Cozy;
        await coordinator.RerenderAsync();

        FlowDocument? doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.Contains("Cozy Heading", DocumentText(doc!));
        Assert.DoesNotContain("Original Heading", DocumentText(doc!));

        // Switch back to Basic -> the host reflects the byte-identical ORIGINAL (from the held raw).
        current = Persona.Basic;
        await coordinator.RerenderAsync();

        doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.Contains("Original Heading", DocumentText(doc!));
        Assert.DoesNotContain("Cozy Heading", DocumentText(doc!));
    }
}
