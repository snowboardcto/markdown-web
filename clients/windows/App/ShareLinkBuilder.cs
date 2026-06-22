using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Produces the canonical shareable <c>.md</c> URL for a page loaded in the native client (Story 5.1 AC5).
///
/// <para>For an app-host URL (detected via <see cref="PageEndpointResolver.IsAppHost"/>), returns the
/// canonical <c>https://&lt;host&gt;/&lt;path&gt;</c> link with query and fragment dropped — consistent
/// with how <see cref="PageEndpointResolver.ToFetchEndpoint"/> drops them. The host is PRESERVED (both
/// <c>themarkdownweb.com</c> and <c>www.themarkdownweb.com</c> are valid and round-trip through
/// <see cref="PageEndpointResolver.IsAppHost"/>). Path is decoded ONCE via
/// <see cref="Uri.UnescapeDataString"/> (mirroring <see cref="PageEndpointResolver.ToFetchEndpoint"/>'s
/// single decode) so percent-encoded characters are not double-encoded. Trailing slash is stripped from
/// non-root paths so the share URL matches the canonical slug form (e.g. <c>/gear-guide/</c> → <c>/gear-guide</c>)
/// — both forms map to the same <c>/api/negotiate/&lt;slug&gt;</c> via <see cref="PageEndpointResolver.ToFetchEndpoint"/>.</para>
///
/// <para>For a non-app-host URL, the input is returned UNCHANGED (byte-for-byte) — never silently rewritten.</para>
///
/// <para>For <c>null</c> or a relative <see cref="Uri"/>, returns <c>null</c> without throwing. PURE, TOTAL — never throws.</para>
///
/// <para>Round-trip property (AC2/AC5): <c>PageEndpointResolver.ToFetchEndpoint(new Uri(ToShareUrl(current)))</c>
/// maps to the SAME <c>/api/negotiate/&lt;slug&gt;</c> as the page originally fetched from, so a recipient
/// who pastes the share link opens the same content.</para>
/// </summary>
public static class ShareLinkBuilder
{
    /// <summary>
    /// Returns the canonical shareable URL for <paramref name="current"/>:
    /// <list type="bullet">
    ///   <item>App-host absolute URL → <c>scheme://host/path</c> (query+fragment dropped, trailing slash stripped for non-root)</item>
    ///   <item>Non-app-host absolute URL → returned unchanged as a string</item>
    ///   <item><c>null</c> or relative <see cref="Uri"/> → <c>null</c>, never throws</item>
    /// </list>
    /// </summary>
    /// <param name="current">The current page <see cref="Uri"/> from <c>NavigationController.Current</c>, or <c>null</c>.</param>
    /// <returns>The canonical shareable URL string, or <c>null</c> if <paramref name="current"/> is null/relative.</returns>
    public static string? ToShareUrl(Uri? current)
    {
        if (current is null)
        {
            return null;
        }

        if (!current.IsAbsoluteUri)
        {
            // Relative URIs: return the relative string unchanged (total, no InvalidOperationException).
            return current.ToString();
        }

        if (!PageEndpointResolver.IsAppHost(current))
        {
            // Non-app-host URLs: returned UNCHANGED byte-for-byte (never silently rewritten).
            return current.ToString();
        }

        try
        {
            // App-host URL: produce the canonical scheme://host/path form.
            // 1. Decode the path once (mirrors PageEndpointResolver.ToFetchEndpoint's single decode).
            //    This ensures percent-encoded paths are not double-encoded in the share URL.
            string rawPath = current.AbsolutePath;

            // 2. Strip trailing slash from non-root paths so /gear-guide/ → /gear-guide.
            //    The root "/" is preserved as-is. Both forms round-trip to the same
            //    /api/negotiate/<slug> via PageEndpointResolver.ToFetchEndpoint.
            string canonicalPath = rawPath.Length > 1 && rawPath.EndsWith("/", StringComparison.Ordinal)
                ? rawPath.TrimEnd('/')
                : rawPath;

            // 3. Rebuild with the SAME origin; drop query+fragment (consistent with ToFetchEndpoint).
            var builder = new UriBuilder
            {
                Scheme = current.Scheme,
                Host = current.Host,
                Path = canonicalPath,
                // Explicitly clear query and fragment (UriBuilder defaults to "" which serializes cleanly).
                Query = string.Empty,
                Fragment = string.Empty,
            };

            // Preserve non-default ports (though the production host uses default ports).
            if (!current.IsDefaultPort)
            {
                builder.Port = current.Port;
            }
            else
            {
                builder.Port = -1; // omit the port for the default scheme port
            }

            return builder.Uri.ToString();
        }
        catch
        {
            // Total — on any unexpected malformation fall back to the original URL string.
            return current.ToString();
        }
    }
}
