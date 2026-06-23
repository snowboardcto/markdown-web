using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;
using TheMarkdownWeb.Agent;
using TheMarkdownWeb.Rendering;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.4 AC1/AC2 — end-to-end flow from <see cref="MarkdownDiscoveryService"/> discovery
/// through the personalization gateway to the render sink, using injected fakes throughout.
/// No window, no Show, no real network.
///
/// Verifies:
///   (a) A <c>PageMarkdown</c> discovery result flows through the gateway and into the render sink.
///   (b) Personalization is applied (the gateway's transform result is what the sink receives).
///   (c) A gateway fallback (Basic pass-through) still reaches the render sink unchanged.
///   (d) <c>NoMarkdown</c>/<c>Blocked</c> results do NOT reach the render sink.
/// </summary>
public class DiscoveryRenderFlowTests
{
    private static readonly Uri DiscoveryUri = new("https://example.com/docs/intro");

    // ── Fake infrastructure ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fake <see cref="HttpMessageHandler"/> that returns canned responses by exact URL.
    /// Any unregistered URL returns 404 with an empty body.
    /// </summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (int StatusCode, string ContentType, string Body)> _responses = new();

        public void Register(string url, int statusCode, string contentType, string body)
            => _responses[url] = (statusCode, contentType, body);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.ToString() ?? string.Empty;
            if (_responses.TryGetValue(url, out var resp))
            {
                var msg = new HttpResponseMessage((HttpStatusCode)resp.StatusCode)
                {
                    Content = new StringContent(resp.Body, System.Text.Encoding.UTF8, resp.ContentType),
                };
                return Task.FromResult(msg);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(string.Empty),
            });
        }
    }

    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    /// <summary>
    /// An <see cref="ILlmClient"/> that prefixes page markdown with a known marker so tests
    /// can verify the gateway's output (not the raw markdown) reached the render sink.
    /// </summary>
    private sealed class PrefixingLlmClient : ILlmClient
    {
        private readonly string _prefix;
        public PrefixingLlmClient(string prefix) => _prefix = prefix;

        public Task<LlmResult> CompleteAsync(
            string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
            => Task.FromResult(LlmResult.Success(_prefix + pageMarkdown));
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

    // ── Tests ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_PageMarkdown_PersonalizationApplied_OutputReachesSink()
    {
        // Arrange
        const string rawMarkdown = "# Hello from discovery";
        const string prefix = "[PERSONALIZED] ";

        var handler = new FakeHandler();
        // Step1a: GET with Accept:text/markdown → returns text/markdown directly.
        handler.Register(DiscoveryUri.ToString(), 200, "text/markdown", rawMarkdown);

        using var httpClient = new HttpClient(handler);
        var discovery = new MarkdownDiscoveryService(httpClient);

        var store = new InMemorySecretStore("fake-key-for-ci");
        var llmClient = new PrefixingLlmClient(prefix);
        var engine = new PersonalityEngine(llmClient, store);
        var customPersona = new Persona("custom", "Custom", "Custom system.", IsPassThrough: false);
        var gateway = new PersonalizationGateway(engine, () => customPersona);

        // Act: discover, then run the dispatch → gateway → render path synchronously/sequentially.
        DiscoveryResult result = await discovery.DiscoverAsync(DiscoveryUri);

        // Capture what the PageMarkdown sink would receive.
        string? capturedRaw = null;
        Uri? capturedSourceUrl = null;
        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (md, url) => { capturedRaw = md; capturedSourceUrl = url; },
            onLlmsIndex: _ => { },
            onNoMarkdown: _ => { },
            onBlocked: (_, __) => { });

        // The dispatch must have routed to PageMarkdown.
        Assert.NotNull(capturedRaw);
        Assert.Equal(rawMarkdown, capturedRaw);

        // Now simulate the gateway application (as BeginDiscoveryAsync does it).
        string rendered = await gateway.ResolveMarkdownAsync(capturedRaw!, capturedSourceUrl!, CancellationToken.None);

        // Assert: gateway applied the prefix transformation.
        Assert.StartsWith(prefix, rendered, StringComparison.Ordinal);
        Assert.Contains(rawMarkdown, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverAsync_PageMarkdown_BasicPassThrough_RawMarkdownReachesSinkUnchanged()
    {
        // Arrange: Basic persona (pass-through) — gateway returns raw markdown byte-identical.
        const string rawMarkdown = "# Site Docs\n\nSome content here.";

        var handler = new FakeHandler();
        handler.Register(DiscoveryUri.ToString(), 200, "text/markdown", rawMarkdown);

        using var httpClient = new HttpClient(handler);
        var discovery = new MarkdownDiscoveryService(httpClient);

        // Basic persona is pass-through; LLM should never be called.
        var store = new InMemorySecretStore(); // no key → NeedsKey; engine falls back to original
        var llmClient = new PrefixingLlmClient("[NEVER-CALLED] ");
        var engine = new PersonalityEngine(llmClient, store);
        var gateway = new PersonalizationGateway(engine, () => Persona.Basic);

        // Act
        DiscoveryResult result = await discovery.DiscoverAsync(DiscoveryUri);
        string? capturedRaw = null;
        Uri? capturedSourceUrl = null;
        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (md, url) => { capturedRaw = md; capturedSourceUrl = url; },
            onLlmsIndex: _ => { },
            onNoMarkdown: _ => { },
            onBlocked: (_, __) => { });

        Assert.NotNull(capturedRaw);
        string rendered = await gateway.ResolveMarkdownAsync(capturedRaw!, capturedSourceUrl!, CancellationToken.None);

        // Assert: Basic pass-through returns the raw markdown unchanged.
        Assert.Equal(rawMarkdown, rendered);
    }

    [Fact]
    public async Task DiscoverAsync_NoMarkdown_DoesNotInvokeRenderSink()
    {
        // Arrange: all cascade steps return 404 → NoMarkdown.
        var handler = new FakeHandler();
        // No URLs registered → everything 404.

        using var httpClient = new HttpClient(handler);
        var discovery = new MarkdownDiscoveryService(httpClient);

        bool renderSinkInvoked = false;
        bool noMarkdownSinkInvoked = false;
        Uri? noMarkdownUrl = null;

        // Act
        DiscoveryResult result = await discovery.DiscoverAsync(DiscoveryUri);
        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { renderSinkInvoked = true; },
            onLlmsIndex: _ => { },
            onNoMarkdown: url => { noMarkdownSinkInvoked = true; noMarkdownUrl = url; },
            onBlocked: (_, __) => { });

        // Assert
        Assert.False(renderSinkInvoked, "The render sink must NOT be called for NoMarkdown.");
        Assert.True(noMarkdownSinkInvoked, "The NoMarkdown sink must be called when no markdown is found.");
        Assert.NotNull(noMarkdownUrl);
    }

    [Fact]
    public async Task DiscoverAsync_Blocked_DoesNotInvokeRenderSink()
    {
        // Arrange: step1a returns 403 → Blocked.
        var handler = new FakeHandler();
        handler.Register(DiscoveryUri.ToString(), 403, "text/html", "<html><body>Forbidden</body></html>");

        using var httpClient = new HttpClient(handler);
        var discovery = new MarkdownDiscoveryService(httpClient);

        bool renderSinkInvoked = false;
        bool blockedSinkInvoked = false;

        // Act
        DiscoveryResult result = await discovery.DiscoverAsync(DiscoveryUri);
        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { renderSinkInvoked = true; },
            onLlmsIndex: _ => { },
            onNoMarkdown: _ => { },
            onBlocked: (_, __) => { blockedSinkInvoked = true; });

        // Assert
        Assert.False(renderSinkInvoked, "The render sink must NOT be called for Blocked.");
        Assert.True(blockedSinkInvoked, "The Blocked sink must be called on 403.");
    }

    [Fact]
    public async Task DiscoverAsync_LlmsIndex_InvokesLlmsSink_NotRenderSink()
    {
        // Arrange: step1 returns HTML (no markdown), step2 sibling returns 404,
        //          step3 /llms.txt returns a valid markdown site index.
        const string llmsBody = "# Site Index\n\n[Docs](https://example.com/docs.md)";
        var handler = new FakeHandler();

        // step1a: returns HTML, no markdown
        handler.Register(DiscoveryUri.ToString(), 200, "text/html",
            "<html><head><title>Docs</title></head><body>Hello</body></html>");

        // step2: .md sibling → 404 (not registered → 404)

        // step3: /llms.txt → valid llms.txt
        handler.Register("https://example.com/llms.txt", 200, "text/markdown", llmsBody);

        using var httpClient = new HttpClient(handler);
        var discovery = new MarkdownDiscoveryService(httpClient);

        bool renderSinkInvoked = false;
        DiscoveryResult.LlmsIndex? capturedIndex = null;

        // Act
        DiscoveryResult result = await discovery.DiscoverAsync(DiscoveryUri);
        DiscoveryOutcomeDispatcher.Dispatch(
            result,
            onPageMarkdown: (_, __) => { renderSinkInvoked = true; },
            onLlmsIndex: idx => { capturedIndex = idx; },
            onNoMarkdown: _ => { },
            onBlocked: (_, __) => { });

        // Assert: the llms.txt result surfaces as LlmsIndex, NOT as a page render.
        Assert.False(renderSinkInvoked, "LlmsIndex must NOT invoke the page render sink.");
        Assert.NotNull(capturedIndex);
        Assert.Equal(llmsBody, capturedIndex!.Body);
    }

    [StaFact] // WPF FlowDocumentScrollViewer requires STA.
    public void ShowNoMarkdown_AfterDiscoveryDispatch_SetsDistinctDocumentState()
    {
        // Arrange: simulate the dispatch path that ends in ShowNoMarkdown.
        var scroll = new FlowDocumentScrollViewer();
        var host = new ContentHostController(
            scroll,
            new FlowDocumentRenderer(),
            new NullImageLoader(),
            _ => Task.CompletedTask);

        // First render a real page so there's content to overwrite.
        host.ShowMarkdown("# Previous Page", new Uri("https://example.com/prev.md"));
        Assert.NotNull(host.Host.Document);

        // Act: simulate the NoMarkdown dispatch outcome.
        var noMarkdown = new DiscoveryResult.NoMarkdown(DiscoveryUri);
        DiscoveryOutcomeDispatcher.Dispatch(
            noMarkdown,
            onPageMarkdown: (_, __) => { },
            onLlmsIndex: _ => { },
            onNoMarkdown: url => host.ShowNoMarkdown(url),
            onBlocked: (_, __) => { });

        // Assert: the document changed to the NoMarkdown state.
        Assert.NotNull(host.Host.Document);
        Assert.False(host.IsBroken);
        string name = System.Windows.Automation.AutomationProperties.GetName(host.Host.Document!);
        Assert.Equal("No markdown available", name);
    }
}
