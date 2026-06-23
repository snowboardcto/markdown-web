using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.3 AC1–AC8 — deterministic <c>[Fact]</c>/<c>[Theory]</c> tests for
/// <see cref="MarkdownDiscoveryService"/> using a configurable fake <see cref="HttpMessageHandler"/>.
/// No live network. Mirrors the <see cref="MarkdownFetcherTests"/> stub-handler pattern exactly.
/// </summary>
public class MarkdownDiscoveryServiceTests
{
    private const string ValidMarkdown = "# Hello World\n\nThis is markdown content.";
    private const string ValidMarkdownWithLink = "# Index\n\n[Page](https://example.com/page.md)";
    private static readonly Uri PageUrl = new("https://example.com/docs/intro");

    // ── Step 1a: direct content-negotiation hit ────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_Step1_DirectMarkdown_ReturnsPageMarkdown()
    {
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/markdown", ValidMarkdown),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        var pm = Assert.IsType<DiscoveryResult.PageMarkdown>(result);
        Assert.Equal(ValidMarkdown, pm.Markdown);
        // Step 1 hit means only 1 request was issued.
        Assert.Equal(1, handler.RequestCount);
    }

    // ── Step 1b: alternate-link hit ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_Step1b_AlternateLink_ReturnsPageMarkdown()
    {
        string htmlWithAlternate = "<html><head>" +
            "<link rel=\"alternate\" type=\"text/markdown\" href=\"https://example.com/docs/intro.md\">" +
            "</head><body><p>Content</p></body></html>";

        var altUrl = "https://example.com/docs/intro.md";
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/html", htmlWithAlternate),
            [altUrl] = (HttpStatusCode.OK, "text/markdown", ValidMarkdown),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        var pm = Assert.IsType<DiscoveryResult.PageMarkdown>(result);
        Assert.Equal(ValidMarkdown, pm.Markdown);
    }

    // ── Step 2: .md sibling hit ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_Step2_MdSibling_ReturnsPageMarkdown()
    {
        string siblingUrl = "https://example.com/docs/intro.md";
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            // Step 1: non-markdown response (no alternate link in head)
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/html", "<html><head></head><body></body></html>"),
            // Step 2: .md sibling returns markdown
            [siblingUrl] = (HttpStatusCode.OK, "text/markdown", ValidMarkdown),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        var pm = Assert.IsType<DiscoveryResult.PageMarkdown>(result);
        Assert.Equal(ValidMarkdown, pm.Markdown);
    }

    // ── Step 3: /llms.txt hit ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_Step3_LlmsText_ReturnsLlmsIndex_NotPageMarkdown()
    {
        string llmsUrl = "https://example.com/llms.txt";
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/html", "<html><head></head><body></body></html>"),
            ["https://example.com/docs/intro.md"] = (HttpStatusCode.NotFound, "text/plain", ""),
            [llmsUrl] = (HttpStatusCode.OK, "text/markdown", ValidMarkdownWithLink),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        // Must be LlmsIndex — NOT PageMarkdown (the /llms.txt is a site index, not the page body).
        var idx = Assert.IsType<DiscoveryResult.LlmsIndex>(result);
        Assert.Equal(ValidMarkdownWithLink, idx.Body);
    }

    // ── First-hit-wins: early hit means later steps are NOT requested ──────────────────────────────

    [Fact]
    public async Task DiscoverAsync_Step1Hit_MeansStep2And3NotRequested()
    {
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/markdown", ValidMarkdown),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        await service.DiscoverAsync(PageUrl);

        // Only 1 request (step 1 hit) — step 2 and step 3 were never issued.
        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains(".md"));
        Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("llms.txt"));
    }

    // ── HTML served as .md: rejected (zero false positives) ──────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_HtmlServedAsMarkdown_IsRejected()
    {
        string htmlBody = "<!doctype html><html><head></head><body><p>soft 404</p></body></html>";
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            // text/markdown Content-Type but HTML body — must be rejected
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/markdown", htmlBody),
            ["https://example.com/docs/intro.md"] = (HttpStatusCode.NotFound, "text/plain", ""),
            ["https://example.com/llms.txt"] = (HttpStatusCode.NotFound, "text/plain", ""),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        Assert.IsType<DiscoveryResult.NoMarkdown>(result);
    }

    // ── 403 → Blocked (distinct from NoMarkdown) ──────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_Step1_403_ReturnsBlocked()
    {
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.Forbidden, "text/html", "Forbidden"),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        var blocked = Assert.IsType<DiscoveryResult.Blocked>(result);
        Assert.Equal(403, blocked.StatusCode);
    }

    // ── All-miss → NoMarkdown ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_AllMiss_ReturnsNoMarkdown()
    {
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/html", "<html><head></head><body></body></html>"),
            ["https://example.com/docs/intro.md"] = (HttpStatusCode.NotFound, "text/plain", ""),
            ["https://example.com/llms.txt"] = (HttpStatusCode.NotFound, "text/plain", ""),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        Assert.IsType<DiscoveryResult.NoMarkdown>(result);
    }

    // ── Probe budget cap ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_WorstCase_AllMiss_IssuesAtMostMaxProbesGETs()
    {
        // Worst-case all-miss: step1 (HTML, no alternate) + step2 (404) + step3 (404) = 3 GETs.
        // The cascade also tries the alternate link if found; with no alternate link: 3 GETs max.
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/html", "<html><head></head><body></body></html>"),
            ["https://example.com/docs/intro.md"] = (HttpStatusCode.NotFound, "text/plain", ""),
            ["https://example.com/llms.txt"] = (HttpStatusCode.NotFound, "text/plain", ""),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        await service.DiscoverAsync(PageUrl);

        Assert.True(handler.RequestCount <= MarkdownDiscoveryService.MaxProbes,
            $"Expected at most {MarkdownDiscoveryService.MaxProbes} GETs, got {handler.RequestCount}.");
    }

    // ── Honest User-Agent on every request ────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_SetsHonestUserAgent_OnStep1Request()
    {
        string? capturedUserAgent = null;
        var handler = new CapturingHandler((request, _) =>
        {
            capturedUserAgent = request.Headers.UserAgent?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidMarkdown, Encoding.UTF8, "text/markdown"),
            };
            return Task.FromResult(response);
        });

        var service = new MarkdownDiscoveryService(new HttpClient(handler));
        await service.DiscoverAsync(PageUrl);

        Assert.False(string.IsNullOrWhiteSpace(capturedUserAgent),
            "A User-Agent header must be set on every discovery request.");
        Assert.Contains("MarkdownLens", capturedUserAgent);
        // Must NOT be a browser UA.
        Assert.DoesNotContain("Mozilla", capturedUserAgent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chrome", capturedUserAgent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Safari", capturedUserAgent, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cancellation → defined result, no throw ────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_WithPreCancelledToken_ReturnsDefinedResult_NoThrow()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>());
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl, cts.Token);

        // Must return a defined result, never throw.
        Assert.NotNull(result);
    }

    // ── Null/relative/non-http(s) inputs → Invalid or NoMarkdown ──────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_NullInput_ReturnsInvalidOrNoMarkdown_NoThrow()
    {
        var service = new MarkdownDiscoveryService(new HttpClient(new NullHandler()));
        var ex = await Record.ExceptionAsync(() => service.DiscoverAsync(null!));
        Assert.Null(ex);
    }

    [Fact]
    public async Task DiscoverAsync_RelativeUri_ReturnsInvalid()
    {
        var service = new MarkdownDiscoveryService(new HttpClient(new NullHandler()));
        var relUri = new Uri("/docs/intro", UriKind.Relative);

        DiscoveryResult result = await service.DiscoverAsync(relUri);

        Assert.IsType<DiscoveryResult.Invalid>(result);
    }

    [Fact]
    public async Task DiscoverAsync_FtpUri_ReturnsInvalid()
    {
        var service = new MarkdownDiscoveryService(new HttpClient(new NullHandler()));

        DiscoveryResult result = await service.DiscoverAsync(new Uri("ftp://example.com/x.md"));

        Assert.IsType<DiscoveryResult.Invalid>(result);
    }

    // ── Network error → defined result, no throw ──────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_NetworkError_ReturnsNoMarkdown_NoThrow()
    {
        var handler = new ThrowingHandler(new HttpRequestException("network failure"));
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        // Must return a defined result — not throw.
        Assert.IsType<DiscoveryResult.NoMarkdown>(result);
    }

    // ── Soft-404 doctype sniff for step-2 sibling ─────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverAsync_Step2_SoftNotFound_HtmlAsMd_Continues_ToStep3()
    {
        // The .md sibling URL returns HTML (SPA catch-all) — must be rejected, not accepted.
        string htmlSoft404 = "<!doctype html><html><head></head><body>Not Found</body></html>";
        var handler = new MultiResponseHandler(new Dictionary<string, (HttpStatusCode, string, string)>
        {
            [PageUrl.ToString()] = (HttpStatusCode.OK, "text/html", "<html><head></head><body></body></html>"),
            ["https://example.com/docs/intro.md"] = (HttpStatusCode.OK, "text/html", htmlSoft404),
            ["https://example.com/llms.txt"] = (HttpStatusCode.NotFound, "text/plain", ""),
        });
        var service = new MarkdownDiscoveryService(new HttpClient(handler));

        DiscoveryResult result = await service.DiscoverAsync(PageUrl);

        // Soft-404 at step 2 must not be accepted; step 3 runs; all miss → NoMarkdown.
        Assert.IsType<DiscoveryResult.NoMarkdown>(result);
    }

    // ── Fake handler implementations ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Multi-response handler: returns a canned <c>(statusCode, contentType, body)</c> per URL prefix.
    /// Records all requested URLs and their count.
    /// </summary>
    private sealed class MultiResponseHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode status, string contentType, string body)> _responses;

        public MultiResponseHandler(Dictionary<string, (HttpStatusCode, string, string)> responses)
            => _responses = responses;

        public int RequestCount { get; private set; }
        public List<string> RequestedUrls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            string url = request.RequestUri?.ToString() ?? string.Empty;
            RequestedUrls.Add(url);

            // Look up by exact URL; fall back to 404.
            if (_responses.TryGetValue(url, out var canned))
            {
                var content = new StringContent(canned.body, Encoding.UTF8, canned.contentType);
                return Task.FromResult(new HttpResponseMessage(canned.status) { Content = content });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "text/plain"),
            });
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _factory;

        public CapturingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
            => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _factory(request, cancellationToken);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _ex;
        public ThrowingHandler(Exception ex) => _ex = ex;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw _ex;
    }

    private sealed class NullHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(string.Empty),
            });
    }
}
