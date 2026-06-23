using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.App;

/// <summary>
/// The Markdown Lens discovery cascade (Story 6.3 AC1–AC8). For any <c>http(s)</c> URL, executes an
/// ordered, first-hit-wins cascade that determines whether a markdown representation exists:
/// <list type="number">
///   <item>GET page with <c>Accept: text/markdown</c> + honest UA → validate; ELSE parse <c>&lt;head&gt;</c>
///         for <c>&lt;link rel="alternate" type="text/markdown"&gt;</c> → resolve + GET → validate.</item>
///   <item><c>&lt;path&gt;.md</c> sibling probe → validate.</item>
///   <item><c>/llms.txt</c> at the site root → validate with structure check → surface as <see cref="DiscoveryResult.LlmsIndex"/>.</item>
/// </list>
/// Every candidate is validated by <see cref="MarkdownCandidateValidator"/> (Content-Type + HTML-doctype
/// byte-sniff — zero false positives). The service is pure/total, never throws, is bounded to ≤
/// <see cref="MaxProbes"/> GETs, and distinguishes a bot-block (403/401) as a <see cref="DiscoveryResult.Blocked"/>
/// outcome (distinct from <see cref="DiscoveryResult.NoMarkdown"/>). Uses an honest, non-spoofed
/// User-Agent on every request.
///
/// Injects its HTTP seam via <see cref="HttpClient"/> (the same pattern as <see cref="MarkdownFetcher"/>),
/// so tests pass a stub <see cref="HttpMessageHandler"/> — no live network in CI.
/// </summary>
public sealed class MarkdownDiscoveryService
{
    /// <summary>Hard cap on the number of HTTP GETs per discovery invocation (AC2).</summary>
    public const int MaxProbes = 4;

    /// <summary>Honest, non-spoofed User-Agent (AC5).</summary>
    public const string UserAgent = "MarkdownLens/0.1 (+https://themarkdownweb.com)";

    /// <summary>
    /// Default per-request timeout in milliseconds (AC2 §6.3 "~10 s total per probe"). Injected via
    /// the overloaded constructor so tests can pass a small value without waiting 10 s per test.
    /// </summary>
    public const int DefaultProbeTimeoutMs = 10_000;

    private const string MarkdownMediaType = "text/markdown";

    // Regex to extract markdown links from an llms.txt body: [text](https://...) or [text](http://...).
    private static readonly Regex MarkdownLinkExtractor = new(
        @"\[(?:[^\]]*)\]\((https?://[^)]+)\)",
        RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly int _probeTimeoutMs;

    /// <summary>
    /// Constructs the service over an injectable <see cref="HttpClient"/>. The real app passes a shared
    /// client; tests inject a stub handler.
    /// </summary>
    public MarkdownDiscoveryService(HttpClient http)
        : this(http, DefaultProbeTimeoutMs) { }

    /// <summary>
    /// Constructs the service over an injectable <see cref="HttpClient"/> with a custom per-probe
    /// timeout in milliseconds. Intended for tests, which pass a small value (e.g. 50 ms) so timeout
    /// assertions complete quickly without waiting for the default 10-second budget.
    /// </summary>
    public MarkdownDiscoveryService(HttpClient http, int probeTimeoutMs)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _probeTimeoutMs = probeTimeoutMs;
    }

    /// <summary>
    /// Runs the ordered, first-hit-wins cascade for <paramref name="url"/>.
    /// Never throws — every failure is surfaced as a <see cref="DiscoveryResult"/>. Total.
    /// </summary>
    public async Task<DiscoveryResult> DiscoverAsync(Uri url, CancellationToken ct = default)
    {
        try
        {
            return await DiscoverCoreAsync(url, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Uri safe = url ?? new Uri("about:blank");
            return new DiscoveryResult.NoMarkdown(safe);
        }
        catch (Exception)
        {
            Uri safe = url ?? new Uri("about:blank");
            return new DiscoveryResult.NoMarkdown(safe);
        }
    }

    private async Task<DiscoveryResult> DiscoverCoreAsync(Uri url, CancellationToken ct)
    {
        // Defensive: handle null/relative/non-http(s) defensively (6.2 should never send these).
        if (url is null || !url.IsAbsoluteUri)
        {
            return new DiscoveryResult.Invalid("URL is null or relative.");
        }

        bool isHttp = string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp)
        {
            return new DiscoveryResult.Invalid($"Non-http(s) scheme: {url.Scheme}");
        }

        int probeCount = 0;

        // ── Step 1a: GET the page with Accept: text/markdown. ──────────────────────────────────
        if (probeCount >= MaxProbes)
        {
            return new DiscoveryResult.NoMarkdown(url);
        }

        var step1Response = await ProbeAsync(url, ct).ConfigureAwait(false);
        probeCount++;

        if (step1Response is null)
        {
            // Network failure — not a block
            return new DiscoveryResult.NoMarkdown(url);
        }

        // 403/401 at ANY step short-circuits immediately to Blocked (AC5/LOW #8).
        if (IsBlocked(step1Response.StatusCode))
        {
            return new DiscoveryResult.Blocked(url, step1Response.StatusCode);
        }

        // Check if step 1 directly returned markdown.
        if (step1Response.IsSuccess &&
            MarkdownCandidateValidator.IsValidMarkdown(
                step1Response.StatusCode, step1Response.ContentType, step1Response.Body,
                CandidateKind.PageOrAlternate))
        {
            return new DiscoveryResult.PageMarkdown(step1Response.Body!, step1Response.FinalUrl ?? url);
        }

        // ── Step 1b: Parse <head> for <link rel="alternate" type="text/markdown">. ───────────
        if (!string.IsNullOrEmpty(step1Response.Body))
        {
            Uri? alternateLinkUrl = AlternateLinkParser.Parse(step1Response.Body, step1Response.FinalUrl ?? url);
            if (alternateLinkUrl is not null && probeCount < MaxProbes)
            {
                var altResponse = await ProbeAsync(alternateLinkUrl, ct).ConfigureAwait(false);
                probeCount++;

                if (altResponse is not null)
                {
                    if (IsBlocked(altResponse.StatusCode))
                    {
                        // 403 at any step short-circuits (LOW #8).
                        return new DiscoveryResult.Blocked(url, altResponse.StatusCode);
                    }

                    if (altResponse.IsSuccess &&
                        MarkdownCandidateValidator.IsValidMarkdown(
                            altResponse.StatusCode, altResponse.ContentType, altResponse.Body,
                            CandidateKind.PageOrAlternate))
                    {
                        return new DiscoveryResult.PageMarkdown(altResponse.Body!, altResponse.FinalUrl ?? alternateLinkUrl);
                    }
                }
            }
        }

        // ── Step 2: .md sibling probe. ──────────────────────────────────────────────────────────
        if (probeCount < MaxProbes)
        {
            Uri? siblingUrl = BuildMdSiblingUrl(url);
            if (siblingUrl is not null)
            {
                var siblingResponse = await ProbeAsync(siblingUrl, ct).ConfigureAwait(false);
                probeCount++;

                if (siblingResponse is not null)
                {
                    if (IsBlocked(siblingResponse.StatusCode))
                    {
                        // 403 at any step short-circuits (LOW #8).
                        return new DiscoveryResult.Blocked(url, siblingResponse.StatusCode);
                    }

                    if (siblingResponse.IsSuccess &&
                        MarkdownCandidateValidator.IsValidMarkdown(
                            siblingResponse.StatusCode, siblingResponse.ContentType, siblingResponse.Body,
                            CandidateKind.MdSibling))
                    {
                        return new DiscoveryResult.PageMarkdown(siblingResponse.Body!, siblingResponse.FinalUrl ?? siblingUrl);
                    }
                }
            }
        }

        // ── Step 3: /llms.txt at the site root. ─────────────────────────────────────────────────
        if (probeCount < MaxProbes)
        {
            Uri llmsUrl = BuildLlmsTextUrl(url);
            var llmsResponse = await ProbeAsync(llmsUrl, ct).ConfigureAwait(false);
            probeCount++;

            if (llmsResponse is not null)
            {
                if (IsBlocked(llmsResponse.StatusCode))
                {
                    // 403 at any step short-circuits (LOW #8).
                    return new DiscoveryResult.Blocked(url, llmsResponse.StatusCode);
                }

                if (llmsResponse.IsSuccess &&
                    MarkdownCandidateValidator.IsValidMarkdown(
                        llmsResponse.StatusCode, llmsResponse.ContentType, llmsResponse.Body,
                        CandidateKind.LlmsText))
                {
                    var links = ExtractMarkdownLinks(llmsResponse.Body!);
                    return new DiscoveryResult.LlmsIndex(llmsResponse.Body!, links, llmsResponse.FinalUrl ?? llmsUrl);
                }
            }
        }

        return new DiscoveryResult.NoMarkdown(url);
    }

    /// <summary>
    /// Issues a single GET with <c>Accept: text/markdown</c> and the honest UA. Returns <c>null</c> on
    /// network error or timeout (never throws). On a transient network exception or 5xx response, retries
    /// ONCE before returning null/the failed result (MEDIUM #4). Enforces a per-request timeout via a
    /// linked <see cref="CancellationTokenSource"/> of <see cref="_probeTimeoutMs"/> milliseconds so no
    /// single probe can block the cascade beyond the research budget (HIGH #1).
    /// </summary>
    private async Task<ProbeResult?> ProbeAsync(Uri url, CancellationToken ct)
    {
        // Single retry on transient 5xx or network exception (MEDIUM #4).
        for (int attempt = 0; attempt <= 1; attempt++)
        {
            ProbeResult? result = await ProbeOnceAsync(url, ct).ConfigureAwait(false);

            if (result is null)
            {
                // Network exception — retry once, then give up.
                if (attempt == 0)
                {
                    continue;
                }
                return null;
            }

            // 5xx: retry once.
            if (result.StatusCode >= 500 && result.StatusCode <= 599 && attempt == 0)
            {
                continue;
            }

            return result;
        }

        return null;
    }

    /// <summary>
    /// One raw HTTP GET with the honest UA, per-request timeout, and no retry. Returns <c>null</c> on
    /// any exception or timeout.
    /// </summary>
    private async Task<ProbeResult?> ProbeOnceAsync(Uri url, CancellationToken ct)
    {
        // Enforce the per-request timeout by linking a timeout token to the caller's token (HIGH #1).
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(_probeTimeoutMs);
        CancellationToken probeCt = probeCts.Token;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MarkdownMediaType));
            request.Headers.UserAgent.ParseAdd(UserAgent);

            using HttpResponseMessage response =
                await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, probeCt)
                    .ConfigureAwait(false);

            string body = string.Empty;
            try
            {
                body = await response.Content.ReadAsStringAsync(probeCt).ConfigureAwait(false);
            }
            catch
            {
                // If body read fails, treat as empty (not markdown)
            }

            // Guard against oversized bodies.
            if (body.Length > MarkdownCandidateValidator.MaxBodyChars)
            {
                body = string.Empty;
            }

            string? contentType = response.Content.Headers.ContentType?.MediaType;

            // The HttpClient may have followed redirects; the final URL is the request URI.
            Uri? finalUrl = response.RequestMessage?.RequestUri;

            return new ProbeResult(
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                contentType,
                body,
                finalUrl);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsBlocked(int statusCode)
        => statusCode == 403 || statusCode == 401;

    /// <summary>
    /// Constructs the <c>&lt;path&gt;.md</c> sibling URL preserving the original percent-encoding of the
    /// path so that non-ASCII characters and literal <c>%2F</c> sequences are not double-encoded (MEDIUM
    /// #6). Operates on the already-escaped absolute URI string rather than <c>UriBuilder.Path</c> (which
    /// re-encodes). Drops query and fragment. Returns <c>null</c> if the path already ends in <c>.md</c>
    /// or on any construction error.
    /// </summary>
    private static Uri? BuildMdSiblingUrl(Uri url)
    {
        try
        {
            // Work from the escaped absolute URI to avoid double-encoding.
            // UriComponents.Path returns the path portion with its original percent-encoding intact.
            string escapedPath = url.GetComponents(UriComponents.Path, UriFormat.UriEscaped);

            // If the path already ends in .md, there's no sibling to probe.
            if (escapedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Strip trailing slash before appending .md (e.g. /docs/intro/ → /docs/intro.md).
            string newEscapedPath = escapedPath.TrimEnd('/') + ".md";
            if (string.IsNullOrEmpty(newEscapedPath) || newEscapedPath == ".md")
            {
                return null;
            }

            // Build the sibling URL by splicing on SchemeAndServer + the new path, dropping query/fragment.
            // This is safe because SchemeAndServer is ASCII-only and the path is already escaped.
            string schemeAndServer = url.GetComponents(
                UriComponents.SchemeAndServer, UriFormat.UriEscaped);

            string siblingUriString = schemeAndServer + "/" + newEscapedPath.TrimStart('/');

            if (Uri.TryCreate(siblingUriString, UriKind.Absolute, out Uri? siblingUri))
            {
                return siblingUri;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the <c>/llms.txt</c> URL at the site root (scheme + host only; no path from original).
    /// </summary>
    private static Uri BuildLlmsTextUrl(Uri url)
    {
        var builder = new UriBuilder(url)
        {
            Path = "/llms.txt",
            Query = string.Empty,
            Fragment = string.Empty,
        };

        if (url.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return builder.Uri;
    }

    /// <summary>
    /// Extracts absolute http(s) URIs from markdown link syntax <c>[text](url)</c> in the llms.txt body.
    /// Returns an empty list on any error.
    /// </summary>
    private static IReadOnlyList<Uri> ExtractMarkdownLinks(string body)
    {
        var links = new List<Uri>();
        try
        {
            foreach (Match match in MarkdownLinkExtractor.Matches(body))
            {
                string href = match.Groups[1].Value;
                if (Uri.TryCreate(href, UriKind.Absolute, out Uri? uri))
                {
                    links.Add(uri);
                }
            }
        }
        catch
        {
            // total
        }

        return links;
    }

    /// <summary>Internal result of a single probe — holds everything the cascade needs.</summary>
    private sealed class ProbeResult
    {
        public ProbeResult(int statusCode, bool isSuccess, string? contentType, string body, Uri? finalUrl)
        {
            StatusCode = statusCode;
            IsSuccess = isSuccess;
            ContentType = contentType;
            Body = body;
            FinalUrl = finalUrl;
        }

        public int StatusCode { get; }
        public bool IsSuccess { get; }
        public string? ContentType { get; }
        public string Body { get; }
        public Uri? FinalUrl { get; }
    }
}
