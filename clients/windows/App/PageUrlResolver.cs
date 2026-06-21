using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Pure, TOTAL relative→absolute URL resolver (Story 3.5 AC3). Resolves a relative href/src against
/// the current page's absolute base URL using standard <c>new Uri(base, rel)</c> semantics:
/// <c>./</c> and <c>..</c> resolve against the base's DIRECTORY, a leading <c>/</c> against the host
/// root, an already-absolute ref is returned unchanged. Returns <c>null</c> (NEVER throws) for an
/// unresolvable/garbage ref. Shared by link navigation (AC4) and image resolution (AC7).
/// </summary>
public static class PageUrlResolver
{
    /// <summary>
    /// Resolves <paramref name="relativeRef"/> against <paramref name="basePageUrl"/>. Returns the
    /// absolute URL, or <c>null</c> if the ref is empty/whitespace/unresolvable. Never throws.
    /// </summary>
    public static Uri? ResolveAgainst(Uri basePageUrl, string relativeRef)
    {
        if (basePageUrl is null || string.IsNullOrWhiteSpace(relativeRef))
        {
            return null;
        }

        string trimmed = relativeRef.Trim();

        // An interior space (e.g. "ht tp://x") is never a valid URL component; treat as garbage. The
        // legitimate relative refs (./x.md, x.md?v=1, /logo.png, //cdn/p.png, …) carry no interior space.
        if (trimmed.IndexOf(' ') >= 0)
        {
            return null;
        }

        // A ref that starts with ':' (e.g. ":://garbage") is malformed — a leading empty scheme. .NET's
        // lenient parser may otherwise mis-resolve it; reject explicitly for determinism.
        if (trimmed.StartsWith(":", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            if (Uri.TryCreate(basePageUrl, trimmed, out Uri? resolved) &&
                resolved is { IsAbsoluteUri: true })
            {
                return resolved;
            }
        }
        catch
        {
            // Total — any malformed ref yields null, never an exception.
        }

        return null;
    }
}
