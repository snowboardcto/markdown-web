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
/// AC2 ([StaFact] surface) — the App wiring renders the GATEWAY'S OUTPUT, not the raw fetched markdown.
/// Constructs the real App seam (a <see cref="PersonalizationGateway"/> over the real
/// <see cref="PersonalityEngine"/> + a fake <see cref="ILlmClient"/>, and a real
/// <see cref="ContentHostController"/> over a real <see cref="FlowDocumentRenderer"/> into a
/// <see cref="FlowDocumentScrollViewer"/>), drives a fetched markdown THROUGH the gateway into
/// <c>ShowMarkdown</c>, and asserts the hosted <see cref="FlowDocument"/> is non-empty and reflects the
/// resolved markdown: Basic pass-through reflects the ORIGINAL (byte-identical to the Epic-3 render); a
/// fake-transform persona reflects the TRANSFORMED markdown. Construct-not-Show; no window shown; no
/// pixels; no socket. RED until App+Agent types exist.
/// </summary>
public class PersonalizationSeamTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");

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
    {
        var range = new TextRange(doc.ContentStart, doc.ContentEnd);
        return range.Text;
    }

    [StaFact] // AC2 — Basic pass-through: the gateway output rendered into the host reflects the ORIGINAL markdown.
    public async Task AppSeam_Basic_RendersGatewayOutput_ReflectingOriginal()
    {
        var engine = new PersonalityEngine(new FakeLlmClient(LlmResult.Success("# UNUSED")), new InMemorySecretStore(seed: "k"));
        var gateway = new PersonalizationGateway(engine, () => Persona.Basic);
        ContentHostController host = NewHost();

        const string fetched = "# Faithful Heading\n\nThe original body.";
        string resolved = await gateway.ResolveMarkdownAsync(fetched, PageUrl, CancellationToken.None);
        host.ShowMarkdown(resolved, PageUrl);

        FlowDocument? doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.NotEmpty(doc!.Blocks);
        Assert.Contains("Faithful Heading", DocumentText(doc));
    }

    [StaFact] // AC2 — a transform persona: the host renders the TRANSFORMED markdown the gateway resolved.
    public async Task AppSeam_TransformPersona_RendersGatewayOutput_ReflectingTransformed()
    {
        var engine = new PersonalityEngine(new FakeLlmClient(LlmResult.Success("# Transformed Heading\n\nReworked.")), new InMemorySecretStore(seed: "k"));
        var persona = new Persona("custom", "Custom", "transform", IsPassThrough: false);
        var gateway = new PersonalizationGateway(engine, () => persona);
        ContentHostController host = NewHost();

        string resolved = await gateway.ResolveMarkdownAsync("# Original\n\nbody", PageUrl, CancellationToken.None);
        host.ShowMarkdown(resolved, PageUrl);

        FlowDocument? doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.Contains("Transformed Heading", DocumentText(doc));
        Assert.DoesNotContain("Original", DocumentText(doc));
    }
}
