using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC4 + AC6 (fetcher half) — the App-side <c>MarkdownFetcher</c> issues a GET carrying
/// <c>Accept: text/markdown</c>, returns the body string on a 2xx <c>text/markdown</c> response,
/// and NEVER throws — every failure (non-2xx, wrong content-type, empty/oversized body,
/// <c>HttpRequestException</c>, cancellation) is surfaced as a <c>FetchResult.Failure</c>.
///
/// NO real socket: a stub <see cref="HttpMessageHandler"/> captures the outgoing request and
/// returns a canned response, injected via <c>new HttpClient(stubHandler)</c>.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public readonly record struct FetchResult
///   {
///       public bool IsSuccess { get; }
///       public string? Markdown { get; }
///       public string? FailureReason { get; }
///       public static FetchResult Success(string markdown);
///       public static FetchResult Failure(string reason);
///   }
///   public sealed class MarkdownFetcher
///   {
///       public MarkdownFetcher(HttpClient http);
///       public Task<FetchResult> FetchAsync(string url, CancellationToken ct = default);
///   }
/// </summary>
public class MarkdownFetcherTests
{
    private const string Url = "https://h/x.md";

    [Fact] // AC4 — outgoing request is GET and carries Accept: text/markdown.
    public async Task FetchAsync_IssuesGet_WithAcceptTextMarkdownHeader()
    {
        var handler = new StubHandler((request, _) =>
            Canned(HttpStatusCode.OK, "# Hello", "text/markdown", "utf-8"));
        var fetcher = new MarkdownFetcher(new HttpClient(handler));

        await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Contains(
            handler.LastRequest.Headers.Accept,
            h => string.Equals(h.MediaType, "text/markdown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] // AC4 — 200 text/markdown -> Success carrying the exact body.
    public async Task FetchAsync_Returns_Success_With_Body_On200TextMarkdown()
    {
        var handler = new StubHandler((_, __) =>
            Canned(HttpStatusCode.OK, "# Hello", "text/markdown", "utf-8"));
        var fetcher = new MarkdownFetcher(new HttpClient(handler));

        FetchResult result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.True(result.IsSuccess, "200 with text/markdown body must be a success.");
        Assert.Equal("# Hello", result.Markdown);
    }

    [Theory] // AC6 — non-2xx (incl. endpoint 404 missing slug, and 5xx) -> Failure, no throw.
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FetchAsync_Returns_Failure_On_NonSuccessStatus(HttpStatusCode status)
    {
        var handler = new StubHandler((_, __) => Canned(status, "nope", "text/markdown", "utf-8"));
        var fetcher = new MarkdownFetcher(new HttpClient(handler));

        FetchResult result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess, $"HTTP {(int)status} must map to Failure (-> Broken).");
        Assert.False(string.IsNullOrEmpty(result.FailureReason), "Failure must carry a non-empty reason.");
    }

    [Fact] // AC6 — 200 but Content-Type is NOT text/markdown (e.g. live SWA returns text/html) -> Failure.
    public async Task FetchAsync_Returns_Failure_On200WithWrongContentType()
    {
        var handler = new StubHandler((_, __) =>
            Canned(HttpStatusCode.OK, "<html></html>", "text/html", "utf-8"));
        var fetcher = new MarkdownFetcher(new HttpClient(handler));

        FetchResult result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess, "A 200 whose Content-Type is not text/markdown must be a Failure, not Loaded.");
    }

    [Fact] // AC6 — 200 text/markdown but EMPTY body -> Failure.
    public async Task FetchAsync_Returns_Failure_On200WithEmptyBody()
    {
        var handler = new StubHandler((_, __) =>
            Canned(HttpStatusCode.OK, string.Empty, "text/markdown", "utf-8"));
        var fetcher = new MarkdownFetcher(new HttpClient(handler));

        FetchResult result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess, "An empty body must be treated as a Failure (-> Broken).");
    }

    [Fact] // AC6 — oversized body -> Failure (the fetcher caps body size per the story).
    public async Task FetchAsync_Returns_Failure_On_OversizedBody()
    {
        // A body far larger than any reasonable markdown page; the fetcher must reject it, not OOM.
        string huge = new string('a', 64 * 1024 * 1024); // 64 MiB
        var handler = new StubHandler((_, __) =>
            Canned(HttpStatusCode.OK, huge, "text/markdown", "utf-8"));
        var fetcher = new MarkdownFetcher(new HttpClient(handler));

        FetchResult result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess, "An oversized body must be rejected as a Failure (-> Broken).");
    }

    [Fact] // AC6 — handler throws HttpRequestException (DNS/refused/TLS) -> Failure, NOT propagated.
    public async Task FetchAsync_Returns_Failure_When_HandlerThrowsHttpRequestException()
    {
        var handler = new StubHandler((_, __) => throw new HttpRequestException("connection refused"));
        var fetcher = new MarkdownFetcher(new HttpClient(handler));

        FetchResult result = await fetcher.FetchAsync(Url, CancellationToken.None);

        Assert.False(result.IsSuccess, "A network exception must be caught and returned as Failure, never propagated.");
    }

    [Fact] // AC6 — pre-cancelled token honored: Failure (no crash), no unhandled exception escapes.
    public async Task FetchAsync_Returns_Failure_When_TokenAlreadyCancelled()
    {
        var handler = new StubHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Canned(HttpStatusCode.OK, "# Hello", "text/markdown", "utf-8");
        });
        var fetcher = new MarkdownFetcher(new HttpClient(handler));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        FetchResult result = await fetcher.FetchAsync(Url, cts.Token);

        Assert.False(result.IsSuccess, "A cancelled fetch must return Failure (-> Broken) without throwing out of FetchAsync.");
    }

    private static HttpResponseMessage Canned(HttpStatusCode status, string body, string mediaType, string charset)
    {
        var content = new StringContent(body, System.Text.Encoding.UTF8, mediaType);
        // StringContent already sets charset=utf-8; the explicit value here documents the intent.
        content.Headers.ContentType!.CharSet = charset;
        return new HttpResponseMessage(status) { Content = content };
    }

    /// <summary>
    /// Stub message handler: records the last request and returns a caller-supplied canned response
    /// (or throws what the factory throws). No socket is opened — this replaces the entire transport.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
            => _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request, cancellationToken));
        }
    }
}
