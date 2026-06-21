using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.App;

/// <summary>
/// The outcome of a markdown fetch (Story 3-2 AC4). On success carries the raw markdown body string;
/// on failure carries a non-empty human-readable reason. Held as a string only — NOT rendered at this
/// story (Markdig render is Story 3-3).
/// </summary>
public readonly record struct FetchResult
{
    private FetchResult(bool isSuccess, string? markdown, string? failureReason)
    {
        IsSuccess = isSuccess;
        Markdown = markdown;
        FailureReason = failureReason;
    }

    /// <summary>Whether the fetch produced a usable markdown body.</summary>
    public bool IsSuccess { get; }

    /// <summary>The raw markdown body on success; <c>null</c> on failure.</summary>
    public string? Markdown { get; }

    /// <summary>A non-empty reason on failure; <c>null</c> on success.</summary>
    public string? FailureReason { get; }

    /// <summary>Creates a successful result carrying the fetched markdown body.</summary>
    public static FetchResult Success(string markdown) => new(true, markdown, null);

    /// <summary>Creates a failed result carrying a non-empty reason.</summary>
    public static FetchResult Failure(string reason) => new(false, null, reason);
}

/// <summary>
/// App-side fetcher for raw markdown (Story 3-2 AC4 / AC6). Issues a GET carrying
/// <c>Accept: text/markdown</c> (the Story 2.7 content-negotiation contract) and returns the body string
/// on a 2xx <c>text/markdown</c> response. NEVER throws out of <see cref="FetchAsync"/> — every failure
/// (non-2xx, wrong content-type, empty/oversized body, network exception, cancellation) is surfaced as a
/// <see cref="FetchResult.Failure"/>. Networking lives in <c>App</c>; <c>Rendering</c> stays pure.
/// </summary>
public sealed class MarkdownFetcher
{
    /// <summary>Maximum accepted markdown body size (bytes). Larger responses are rejected, not loaded.</summary>
    private const long MaxBodyBytes = 8L * 1024 * 1024; // 8 MiB — generous for markdown, guards against OOM.

    private const string MarkdownMediaType = "text/markdown";

    private readonly HttpClient _http;

    /// <summary>
    /// Constructs the fetcher over an injectable <see cref="HttpClient"/> so tests can pass a stub
    /// <see cref="HttpMessageHandler"/> (no socket) and the app passes a shared real client.
    /// </summary>
    public MarkdownFetcher(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>
    /// GETs <paramref name="url"/> AS-IS with <c>Accept: text/markdown</c>. Returns
    /// <see cref="FetchResult.Success"/> only on a 2xx whose media type is <c>text/markdown</c> with a
    /// non-empty, size-bounded body; otherwise <see cref="FetchResult.Failure"/>. Never throws.
    /// </summary>
    public async Task<FetchResult> FetchAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd(MarkdownMediaType);

            using HttpResponseMessage response =
                await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return FetchResult.Failure($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim());
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(mediaType, MarkdownMediaType, StringComparison.OrdinalIgnoreCase))
            {
                return FetchResult.Failure(
                    $"Response is not markdown (Content-Type '{mediaType ?? "<none>"}').");
            }

            long? declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength.HasValue && declaredLength.Value > MaxBodyBytes)
            {
                return FetchResult.Failure("Markdown body is too large.");
            }

            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (body.Length == 0)
            {
                return FetchResult.Failure("Markdown body was empty.");
            }

            // Guard against an undeclared (chunked) oversized body once it is materialized.
            if (body.Length > MaxBodyBytes)
            {
                return FetchResult.Failure("Markdown body is too large.");
            }

            return FetchResult.Success(body);
        }
        catch (OperationCanceledException)
        {
            return FetchResult.Failure("The fetch was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return FetchResult.Failure($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Defensive catch-all — AC6 mandates SubmitAsync/FetchAsync never let an exception escape.
            return FetchResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
}
