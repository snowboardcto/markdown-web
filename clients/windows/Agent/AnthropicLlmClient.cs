using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// A real <see cref="ILlmClient"/> over the Anthropic Messages API (Story 4.1 AC1 / AC4 / AC5), driven by
/// an injectable <see cref="HttpClient"/> so tests exercise it with a stub <see cref="HttpMessageHandler"/>
/// (no socket). It issues exactly ONE POST to <c>{BaseUrl}/v1/messages</c> carrying the reader's key in
/// <c>x-api-key</c> + <c>anthropic-version</c>, with the page markdown in the body. The call is TOTAL —
/// every failure (no key, non-2xx, malformed JSON, missing/blank text, network error, cancellation, any
/// other exception) becomes a <see cref="LlmResult.Failure"/>. The key NEVER appears in any
/// <see cref="LlmResult.FailureReason"/> and rides ONLY the <c>x-api-key</c> header — never logged.
/// </summary>
public sealed class AnthropicLlmClient : ILlmClient
{
    /// <summary>
    /// Defensive request-size cap (mirrors <c>MarkdownFetcher.MaxBodyBytes = 8 MiB</c> intent). On exceed
    /// the client refuses to transform rather than building an unbounded request.
    /// </summary>
    public const int MaxInputChars = 8 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly ISecretStore _secretStore;
    private readonly AnthropicOptions _options;

    public AnthropicLlmClient(HttpClient http, ISecretStore secretStore, AnthropicOptions? options = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _options = options ?? new AnthropicOptions();
    }

    /// <inheritdoc />
    public async Task<LlmResult> CompleteAsync(
        string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
    {
        // No key -> Failure, NO request issued (the engine maps the user-facing NeedsKey).
        string? key = _secretStore.HasApiKey ? _secretStore.GetApiKey() : null;
        if (string.IsNullOrEmpty(key))
        {
            return LlmResult.Failure("No API key configured.");
        }

        string page = pageMarkdown ?? string.Empty;

        // Oversized page -> refuse-transform (row 14); never build an unbounded request.
        if (page.Length > MaxInputChars)
        {
            return LlmResult.Failure("Input too large.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/messages");
            request.Headers.TryAddWithoutValidation("x-api-key", key);
            request.Headers.TryAddWithoutValidation("anthropic-version", _options.AnthropicVersion);
            request.Content = new StringContent(BuildRequestBody(systemPrompt ?? string.Empty, page), Encoding.UTF8, "application/json");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_options.Timeout);

            using HttpResponseMessage response =
                await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                    .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return LlmResult.Failure($"Provider returned HTTP {(int)response.StatusCode}.");
            }

            string body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            return ParseResponse(body);
        }
        catch (OperationCanceledException)
        {
            return LlmResult.Failure("The request was cancelled or timed out.");
        }
        catch (HttpRequestException ex)
        {
            return LlmResult.Failure($"Network error: {ex.Message}");
        }
        catch (Exception)
        {
            // Defensive catch-all — CompleteAsync is contractually total. Never surface internals/the key.
            return LlmResult.Failure("Unexpected error during the provider request.");
        }
    }

    private string BuildRequestBody(string systemPrompt, string pageMarkdown)
    {
        // Build VALID JSON via System.Text.Json. The real Anthropic API rejects malformed JSON, so the
        // body MUST properly escape newlines / quotes / control characters (an earlier hand-rolled encoder
        // that left newlines raw produced invalid JSON for any multi-line page). The test asserts the
        // SEMANTIC contract — it parses the body and checks messages[0].content == pageMarkdown — not a
        // brittle raw substring. Response parsing also uses System.Text.Json (JsonDocument) below.
        // { "model", "max_tokens", "system", "messages": [ { "role": "user", "content": <pageMarkdown> } ] }
        var payload = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = pageMarkdown } },
        };
        return JsonSerializer.Serialize(payload);
    }

    private static LlmResult ParseResponse(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("content", out JsonElement content)
                || content.ValueKind != JsonValueKind.Array)
            {
                return LlmResult.Failure("Provider response had no content.");
            }

            foreach (JsonElement block in content.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (block.TryGetProperty("type", out JsonElement type)
                    && type.ValueKind == JsonValueKind.String
                    && string.Equals(type.GetString(), "text", StringComparison.Ordinal)
                    && block.TryGetProperty("text", out JsonElement text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    string? value = text.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return LlmResult.Success(value!);
                    }

                    return LlmResult.Failure("Provider returned blank text.");
                }
            }

            return LlmResult.Failure("Provider response had no text block.");
        }
        catch (JsonException)
        {
            return LlmResult.Failure("Provider response was not valid JSON.");
        }
    }
}
