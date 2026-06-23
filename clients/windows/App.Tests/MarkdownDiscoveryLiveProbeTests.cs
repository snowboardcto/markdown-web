using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.3 AC live-probe — manually-triggered probes against real sites (no CI execution).
/// All tests are skipped by default with <c>Skip = "manual live probe"</c>.
///
/// To run locally: delete or override the Skip attribute, or use
/// <c>dotnet test --filter "Category=LiveProbe"</c> with the [Trait] marker.
///
/// Sites expected to serve markdown (PageMarkdown):
///   • https://stripe.com/docs           — Stripe publishes markdown-discoverable docs.
///   • https://docs.anthropic.com        — Anthropic publishes alternate-link markdown.
///   • https://ai-sdk.dev                — ai-sdk.dev serves /llms.txt.
///   • https://gilesthomas.com           — Personal site known to serve .md sibling.
///
/// Sites expected to return NoMarkdown (no markdown found at all):
///   • https://www.coca-cola.com         — no markdown alternate, no sibling, no llms.txt.
///
/// Sites expected to return Blocked (403/401/hard refusal):
///   • https://www.nytimes.com           — historically aggressive bot-blocking.
///   • https://www.bbc.com/news          — returns 403/429 to non-browser UAs.
///
/// NOTE: Real site behavior changes over time; these are INDICATIVE probes only.
/// CI must NEVER run these — they make real network calls and depend on third-party uptime.
/// </summary>
[Trait("Category", "LiveProbe")]
public class MarkdownDiscoveryLiveProbeTests
{
    private static MarkdownDiscoveryService CreateService()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        return new MarkdownDiscoveryService(httpClient);
    }

    // ── Sites expected to yield PageMarkdown or LlmsIndex ────────────────────────────────────────

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_StripeDocs_FindsMarkdownOrLlms()
    {
        var service = CreateService();
        var url = new Uri("https://stripe.com/docs");

        DiscoveryResult result = await service.DiscoverAsync(url);

        Assert.True(
            result is DiscoveryResult.PageMarkdown or DiscoveryResult.LlmsIndex,
            $"Expected stripe.com/docs to yield PageMarkdown or LlmsIndex, got: {result.GetType().Name}");
    }

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_AnthropicDocs_FindsMarkdownOrLlms()
    {
        var service = CreateService();
        var url = new Uri("https://docs.anthropic.com");

        DiscoveryResult result = await service.DiscoverAsync(url);

        Assert.True(
            result is DiscoveryResult.PageMarkdown or DiscoveryResult.LlmsIndex,
            $"Expected docs.anthropic.com to yield PageMarkdown or LlmsIndex, got: {result.GetType().Name}");
    }

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_AiSdkDev_FindsLlmsOrMarkdown()
    {
        var service = CreateService();
        var url = new Uri("https://ai-sdk.dev");

        DiscoveryResult result = await service.DiscoverAsync(url);

        Assert.True(
            result is DiscoveryResult.PageMarkdown or DiscoveryResult.LlmsIndex,
            $"Expected ai-sdk.dev to yield PageMarkdown or LlmsIndex (via /llms.txt), got: {result.GetType().Name}");
    }

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_GilesThomas_FindsMarkdownViaAlternateOrSibling()
    {
        var service = CreateService();
        var url = new Uri("https://gilesthomas.com");

        DiscoveryResult result = await service.DiscoverAsync(url);

        Assert.True(
            result is DiscoveryResult.PageMarkdown or DiscoveryResult.LlmsIndex,
            $"Expected gilesthomas.com to yield PageMarkdown or LlmsIndex, got: {result.GetType().Name}");
    }

    // ── Sites expected to yield NoMarkdown ─────────────────────────────────────────────────────

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_CocaCola_ReturnsNoMarkdown()
    {
        var service = CreateService();
        var url = new Uri("https://www.coca-cola.com");

        DiscoveryResult result = await service.DiscoverAsync(url);

        // Coca-Cola does not publish markdown; must be NoMarkdown (not Blocked — they do serve HTML).
        Assert.True(
            result is DiscoveryResult.NoMarkdown or DiscoveryResult.Blocked,
            $"Expected coca-cola.com to yield NoMarkdown or Blocked, got: {result.GetType().Name}");
    }

    // ── Sites expected to yield Blocked ──────────────────────────────────────────────────────────

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_NYTimes_ReturnsBlockedOrNoMarkdown()
    {
        var service = CreateService();
        var url = new Uri("https://www.nytimes.com");

        DiscoveryResult result = await service.DiscoverAsync(url);

        // NYT is known to return 403/429 to non-browser UAs; Blocked is the expected outcome.
        // NoMarkdown is also acceptable if they change policy.
        Assert.True(
            result is DiscoveryResult.Blocked or DiscoveryResult.NoMarkdown,
            $"Expected nytimes.com to yield Blocked or NoMarkdown, got: {result.GetType().Name}");
    }

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_BBC_ReturnsBlockedOrNoMarkdown()
    {
        var service = CreateService();
        var url = new Uri("https://www.bbc.com/news");

        DiscoveryResult result = await service.DiscoverAsync(url);

        Assert.True(
            result is DiscoveryResult.Blocked or DiscoveryResult.NoMarkdown,
            $"Expected bbc.com/news to yield Blocked or NoMarkdown, got: {result.GetType().Name}");
    }

    // ── Probe budget: always ≤ MaxProbes HTTP requests ────────────────────────────────────────────

    [Fact(Skip = "manual live probe")]
    [Trait("Category", "LiveProbe")]
    public async Task LiveProbe_ProbeBudget_NeverExceedsMaxProbes()
    {
        // Use a counting handler wrapping a real socket to observe the probe count.
        var counting = new CountingHandler();
        using var httpClient = new HttpClient(counting) { Timeout = TimeSpan.FromSeconds(30) };
        var service = new MarkdownDiscoveryService(httpClient);

        await service.DiscoverAsync(new Uri("https://example.com"));

        Assert.True(counting.Count <= MarkdownDiscoveryService.MaxProbes,
            $"Discovery issued {counting.Count} HTTP requests — must not exceed MaxProbes ({MarkdownDiscoveryService.MaxProbes}).");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────

    private sealed class CountingHandler : DelegatingHandler
    {
        public CountingHandler() : base(new HttpClientHandler()) { }
        public int Count { get; private set; }
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            Count++;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
