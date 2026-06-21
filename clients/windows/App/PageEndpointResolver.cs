using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Maps a typed/clicked absolute app-host <c>.md</c> page URL to the FETCHABLE content-negotiation
/// Function endpoint <c>&lt;scheme&gt;://&lt;host&gt;/api/negotiate/&lt;slug&gt;</c> (Story 3.5 AC9), where
/// <c>&lt;slug&gt;</c> is derived by the SAME normalization as the server (<see cref="SlugDeriver"/>). The
/// fetcher then GETs the endpoint (not the raw page URL) with <c>Accept: text/markdown</c>, so a live
/// <c>.md</c> request returns real <c>text/markdown</c> instead of HTML → Broken.
///
/// Host policy (<see cref="IsAppHost"/>): the mapping applies ONLY for the canonical app host
/// (<c>themarkdownweb.com</c> / <c>www.themarkdownweb.com</c>, case-insensitive). Any other host is
/// returned AS-IS. The endpoint is host-preserving (same origin), drops query/fragment, and decodes
/// <c>%XX</c> ONCE before slugging (mirroring the server's single <c>decodeURIComponent</c>). Pure, total.
/// </summary>
public static class PageEndpointResolver
{
    private const string CanonicalHost = "themarkdownweb.com";
    private const string WwwHost = "www.themarkdownweb.com";

    /// <summary>
    /// The canonical app-host predicate: <c>themarkdownweb.com</c> or <c>www.themarkdownweb.com</c>,
    /// case-insensitive. An EXACT host match (no suffix/prefix attack). Total — never throws.
    /// </summary>
    public static bool IsAppHost(Uri url)
    {
        if (url is null || !url.IsAbsoluteUri)
        {
            return false;
        }

        string host = url.Host;
        return host.Equals(CanonicalHost, StringComparison.OrdinalIgnoreCase) ||
               host.Equals(WwwHost, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps an app-host <c>.md</c> page URL to its <c>/api/negotiate/&lt;slug&gt;</c> endpoint on the same
    /// origin; any non-app-host URL is returned unchanged. Pure, total — never throws.
    /// </summary>
    public static Uri ToFetchEndpoint(Uri pageUrl)
    {
        if (pageUrl is null || !pageUrl.IsAbsoluteUri || !IsAppHost(pageUrl))
        {
            return pageUrl!;
        }

        try
        {
            // Decode %XX ONCE so e.g. "%20" -> space BEFORE slugging (mirrors decodeURIComponent).
            string relPath = pageUrl.AbsolutePath.TrimStart('/');
            string decoded = Uri.UnescapeDataString(relPath);
            string slug = SlugDeriver.PathToSlug(decoded);

            // Rebuild on the SAME origin; query/fragment dropped (the slug uses the path only).
            var builder = new UriBuilder
            {
                Scheme = pageUrl.Scheme,
                Host = pageUrl.Host,
                Path = slug.Length == 0 ? "/api/negotiate" : "/api/negotiate/" + slug,
            };
            if (!pageUrl.IsDefaultPort)
            {
                builder.Port = pageUrl.Port;
            }
            else
            {
                builder.Port = -1; // omit the port for the default scheme port
            }

            return builder.Uri;
        }
        catch
        {
            // Total — on any unexpected malformation fall back to the page URL as-is.
            return pageUrl;
        }
    }
}
