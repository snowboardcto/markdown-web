using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// AC1 / AC4 / AC5 — the REAL <see cref="AnthropicLlmClient"/> over a stub <see cref="StubHandler"/>
/// (NO real socket). Proves the pinned Anthropic Messages API request shape (the no-server-rewrite /
/// NFR-5 proof: exactly ONE POST to <c>{BaseUrl}/v1/messages</c>, to the PROVIDER host, carrying the
/// reader's key in <c>x-api-key</c> + <c>anthropic-version: 2023-06-01</c>, with the page markdown in
/// the body), the 2xx success parse (first <c>content[].type=="text"</c> <c>.text</c>), and the
/// totality table (401/403/429/500, malformed/no-text 2xx, HttpRequestException, cancellation, no-key
/// -> <see cref="LlmResult.Failure"/>, NEVER throws). The sentinel key leaks into NO FailureReason.
/// RED until the Agent module exists.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.Agent):
///
///   public sealed class AnthropicLlmClient : ILlmClient {
///       public AnthropicLlmClient(HttpClient http, ISecretStore secretStore, AnthropicOptions? options = null); }
///   public sealed record AnthropicOptions {
///       string BaseUrl = "https://api.anthropic.com"; string Model = "claude-sonnet-4-6";
///       int MaxTokens = 8192; string AnthropicVersion = "2023-06-01"; }
///
/// POST {BaseUrl}/v1/messages, headers x-api-key + anthropic-version + content-type json,
/// body { model, max_tokens, system:&lt;systemPrompt&gt;, messages:[{role:"user", content:&lt;pageMarkdown framed&gt;}] }.
/// 2xx -> first content[].type=="text" .text as markdown (blank/missing -> Failure). Everything else -> Failure.
/// </summary>
public class AnthropicLlmClientTests
{
    private const string SystemPrompt = "You are a helpful transform.";
    private const string PageMarkdown = "# Heading\n\nThe page body to transform.";

    private static ReaderContext Ctx() => new(PageUrl: "https://h/x.md", PreferredLanguage: "en");

    private static HttpResponseMessage Canned(HttpStatusCode status, string body, string mediaType = "application/json")
    {
        var content = new StringContent(body, System.Text.Encoding.UTF8, mediaType);
        return new HttpResponseMessage(status) { Content = content };
    }

    // A well-formed Anthropic 200 with a single text content block.
    private const string OkBody = "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\"," +
        "\"content\":[{\"type\":\"text\",\"text\":\"# Transformed\"}]}";

    // ----- AC5: request shape (the no-server-rewrite proof) --------------------------------------

    [Fact] // AC5 — exactly ONE POST to {BaseUrl}/v1/messages, to the provider host, with the pinned headers + body.
    public async Task CompleteAsync_Issues_One_Post_ToProvider_WithKeyAndVersionAndBody()
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, OkBody));
        var store = new InMemorySecretStore(seed: TestKeys.SentinelKey);
        var client = new AnthropicLlmClient(new HttpClient(handler), store);

        await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.Equal(1, handler.RequestCount); // exactly ONE outgoing request.
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);

        Uri uri = handler.LastRequest.RequestUri!;
        Assert.Equal("api.anthropic.com", uri.Host); // the PROVIDER host, never a Markdown-Web host.
        Assert.Equal("/v1/messages", uri.AbsolutePath);

        // x-api-key carries EXACTLY the reader's key from the store.
        Assert.True(handler.LastRequest.Headers.TryGetValues("x-api-key", out var keyValues),
            "request must carry the x-api-key header.");
        Assert.Equal(TestKeys.SentinelKey, keyValues!.Single());

        // anthropic-version is pinned.
        Assert.True(handler.LastRequest.Headers.TryGetValues("anthropic-version", out var versionValues),
            "request must carry the anthropic-version header.");
        Assert.Equal("2023-06-01", versionValues!.Single());

        // content-type is json.
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);

        // the body carries the page markdown + the system prompt + the model/max_tokens scaffolding.
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains(PageMarkdown, handler.LastRequestBody!);
        Assert.Contains(SystemPrompt, handler.LastRequestBody!);
        Assert.Contains("max_tokens", handler.LastRequestBody!);
        Assert.Contains("model", handler.LastRequestBody!);
    }

    [Fact] // AC5 — the request host is the configured provider, NEVER a Markdown-Web host.
    public async Task CompleteAsync_TargetsProviderHost_NeverMarkdownWeb()
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, OkBody));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        string host = handler.LastRequest!.RequestUri!.Host;
        Assert.DoesNotContain("themarkdownweb", host, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("api.anthropic.com", host);
    }

    [Fact] // AC1 — a custom BaseUrl is honored (the request goes to the configured provider base).
    public async Task CompleteAsync_Honors_ConfiguredBaseUrl()
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, OkBody));
        var options = new AnthropicOptions { BaseUrl = "https://example-provider.test" };
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"), options);

        await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Uri uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("example-provider.test", uri.Host);
        Assert.Equal("/v1/messages", uri.AbsolutePath);
    }

    // ----- AC1 / AC5: success parse -------------------------------------------------------------

    [Fact] // AC1 / AC5 — a well-formed 200 -> Success carrying the first text block's .text.
    public async Task CompleteAsync_Returns_Success_With_FirstTextBlock_On200()
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, OkBody));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.True(result.IsSuccess, "a 200 with a text content block must be a Success.");
        Assert.Equal("# Transformed", result.Markdown);
    }

    [Fact] // AC1 — multiple content blocks: take the FIRST text block.
    public async Task CompleteAsync_Takes_FirstTextBlock_When_MultipleBlocks()
    {
        const string body = "{\"content\":[{\"type\":\"text\",\"text\":\"# First\"}," +
            "{\"type\":\"text\",\"text\":\"# Second\"}]}";
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, body));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("# First", result.Markdown);
    }

    // ----- AC4: totality (no throw, every path -> Failure) --------------------------------------

    [Theory] // AC4 / table rows 3,7,8 — non-2xx (401/403/429/500) -> Failure, no throw.
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData((HttpStatusCode)429)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CompleteAsync_Returns_Failure_On_NonSuccessStatus(HttpStatusCode status)
    {
        var handler = new StubHandler((_, __) => Canned(status, "{\"error\":\"nope\"}"));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.False(result.IsSuccess, $"HTTP {(int)status} must map to Failure (no throw).");
        Assert.False(string.IsNullOrEmpty(result.FailureReason), "Failure must carry a non-empty reason.");
    }

    [Fact] // AC4 / table row 9 — 2xx with malformed/garbage JSON -> Failure.
    public async Task CompleteAsync_Returns_Failure_On_MalformedJson()
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, "this is not json {{{"));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.False(result.IsSuccess, "a 200 with unparseable JSON must be a Failure.");
    }

    [Theory] // AC4 / table row 10 — 2xx but no usable text block (empty content[] / non-text block) -> Failure.
    [InlineData("{\"content\":[]}")]
    [InlineData("{\"content\":[{\"type\":\"tool_use\",\"id\":\"t\",\"name\":\"x\",\"input\":{}}]}")]
    [InlineData("{\"role\":\"assistant\"}")]
    public async Task CompleteAsync_Returns_Failure_On_NoTextBlock(string body)
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, body));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.False(result.IsSuccess, "a 200 with no text content block must be a Failure.");
    }

    [Theory] // AC4 / table row 11 — 2xx text block but blank/whitespace text -> Failure.
    [InlineData("{\"content\":[{\"type\":\"text\",\"text\":\"\"}]}")]
    [InlineData("{\"content\":[{\"type\":\"text\",\"text\":\"   \"}]}")]
    public async Task CompleteAsync_Returns_Failure_On_BlankTextBlock(string body)
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, body));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.False(result.IsSuccess, "a 200 whose text is blank/whitespace must be a Failure (never an empty doc).");
    }

    [Fact] // AC4 / table row 4 — handler throws HttpRequestException (DNS/TLS/refused) -> Failure, not propagated.
    public async Task CompleteAsync_Returns_Failure_When_HandlerThrowsHttpRequestException()
    {
        var handler = new StubHandler((_, __) => throw new HttpRequestException("connection refused"));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.False(result.IsSuccess, "a network exception must be caught and returned as Failure, never propagated.");
    }

    [Fact] // AC4 / table rows 5-6 — pre-cancelled token -> Failure, no OperationCanceledException escapes.
    public async Task CompleteAsync_Returns_Failure_When_TokenAlreadyCancelled()
    {
        var handler = new StubHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Canned(HttpStatusCode.OK, OkBody);
        });
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: "k"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), cts.Token);

        Assert.False(result.IsSuccess, "a cancelled request must return Failure without throwing out of CompleteAsync.");
    }

    [Fact] // AC4 / no-key -> Failure (no request issued; the no-key NeedsKey state lives at the engine).
    public async Task CompleteAsync_Returns_Failure_When_NoKeyInStore()
    {
        var handler = new StubHandler((_, __) => Canned(HttpStatusCode.OK, OkBody));
        var client = new AnthropicLlmClient(new HttpClient(handler), new InMemorySecretStore(seed: null));

        LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

        Assert.False(result.IsSuccess, "with no key the client must return Failure (the engine maps the user-facing NeedsKey).");
        Assert.Equal(0, handler.RequestCount); // and must NOT issue a request without a key.
    }

    // ----- AC3 no-leak: the sentinel key never appears in any FailureReason ----------------------

    [Fact] // AC3 no-leak — across every failure path, the sentinel key leaks into NO FailureReason.
    public async Task SentinelKey_NeverLeaks_IntoFailureReason()
    {
        var store = new InMemorySecretStore(seed: TestKeys.SentinelKey);

        StubHandler[] handlers =
        {
            new StubHandler((_, __) => Canned(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}")),
            new StubHandler((_, __) => Canned(HttpStatusCode.InternalServerError, "boom")),
            new StubHandler((_, __) => Canned(HttpStatusCode.OK, "not json {{{")),
            new StubHandler((_, __) => throw new HttpRequestException("refused")),
            new StubHandler((_, __) => Canned(HttpStatusCode.OK, "{\"content\":[]}")),
        };

        foreach (StubHandler handler in handlers)
        {
            var client = new AnthropicLlmClient(new HttpClient(handler), store);
            LlmResult result = await client.CompleteAsync(SystemPrompt, PageMarkdown, Ctx(), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.DoesNotContain(TestKeys.SentinelKey, result.FailureReason ?? string.Empty);
        }
    }
}
