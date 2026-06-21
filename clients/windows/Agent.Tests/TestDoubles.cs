using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// Shared test doubles for the Agent.Tests suite. NONE of these touches a real socket, a real key,
/// or the real DPAPI app-data path. They mirror the proven 3.x discipline:
///   - <see cref="StubHandler"/> is the exact <c>HttpMessageHandler</c> pattern from
///     <c>App.Tests/MarkdownFetcherTests.cs</c> (records the last request, returns a canned response,
///     no transport).
///   - <see cref="InMemorySecretStore"/> is the in-memory <c>ISecretStore</c> fake (D4).
///   - the fake <c>ILlmClient</c>s are total (the contract) OR deliberately throwing (the
///     defense-in-depth row 13), and each COUNTS its calls so a test can assert "never called"
///     (the zero-call-on-pass-through / zero-call-on-no-key invariant).
///
/// SENTINEL KEY: the no-leak tests pin <see cref="SentinelKey"/> in an <see cref="InMemorySecretStore"/>
/// and assert it appears in NO surfaced string. It is NOT a real key.
/// </summary>
internal static class TestKeys
{
    /// <summary>The pinned sentinel key (AC3 / AC5 no-leak list). Never a real Anthropic key.</summary>
    public const string SentinelKey = "sk-ant-SENTINEL";
}

/// <summary>
/// Stub <see cref="HttpMessageHandler"/> — records the last request and returns a caller-supplied
/// canned response (or throws what the factory throws). No socket is opened; this replaces the entire
/// transport. Copied from <c>App.Tests/MarkdownFetcherTests.StubHandler</c> so AnthropicLlmClient is
/// exercised with NO real network.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

    public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        => _responder = responder;

    /// <summary>The single outgoing request the client issued (AC5: assert it is the ONLY one).</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>How many requests the client issued (AC5: exactly ONE POST per CompleteAsync).</summary>
    public int RequestCount { get; private set; }

    /// <summary>The request body text, captured eagerly (the message is disposed after SendAsync).</summary>
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        RequestCount++;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }
        return _responder(request, cancellationToken);
    }
}

/// <summary>
/// In-memory <see cref="ISecretStore"/> fake (D4). Holds the key in a private field; never persists,
/// never logs. Construct with a seed key (e.g. the sentinel) or empty (the NeedsKey path).
/// </summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private string? _key;

    public InMemorySecretStore(string? seed = null) => _key = seed;

    public bool HasApiKey => !string.IsNullOrEmpty(_key);
    public string? GetApiKey() => _key;
    public void SetApiKey(string key) => _key = key;
    public void ClearApiKey() => _key = null;
}

/// <summary>
/// Counting fake <see cref="ILlmClient"/> that returns a canned <see cref="LlmResult"/> and records
/// how many times it was called. Tests assert <see cref="Calls"/> == 0 for the pass-through / no-key
/// short-circuits, and inspect the returned markdown for the transform path.
/// </summary>
internal sealed class CountingLlmClient : ILlmClient
{
    private readonly LlmResult _result;

    public CountingLlmClient(LlmResult result) => _result = result;

    /// <summary>Number of times <see cref="CompleteAsync"/> was invoked (assert 0 on short-circuit).</summary>
    public int Calls { get; private set; }

    public Task<LlmResult> CompleteAsync(
        string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(_result);
    }
}

/// <summary>
/// A deliberately MISBEHAVING fake <see cref="ILlmClient"/> that THROWS out of <see cref="CompleteAsync"/>
/// despite the total contract (table row 13). The engine must CATCH it (defense-in-depth) and fall back.
/// </summary>
internal sealed class ThrowingLlmClient : ILlmClient
{
    /// <summary>Number of times <see cref="CompleteAsync"/> was invoked.</summary>
    public int Calls { get; private set; }

    public Task<LlmResult> CompleteAsync(
        string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
    {
        Calls++;
        throw new InvalidOperationException("fake LLM client blew up despite its total contract");
    }
}
