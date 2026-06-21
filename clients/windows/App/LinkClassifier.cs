using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Deterministic, TOTAL classification of a rendered <c>Hyperlink</c>'s href (Story 3.5 AC2). Resolves
/// the href against the current page base (<see cref="PageUrlResolver"/>), then classifies into one of
/// the four <see cref="LinkKind"/>s:
///   • <see cref="LinkKind.InternalMarkdown"/> — resolved http(s) <c>.md</c> page on the SAME host as
///     the base (navigate in place; the resolved Url carries any query/fragment);
///   • <see cref="LinkKind.Anchor"/> — a pure fragment <c>#heading</c> (scroll; no fetch);
///   • <see cref="LinkKind.External"/> — an absolute http(s) link that is not an internal <c>.md</c> page;
///   • <see cref="LinkKind.Unsupported"/> — <c>mailto:</c>/<c>tel:</c>/<c>javascript:</c>/<c>data:</c>/
///     empty/garbage (no-op).
/// Pure (no I/O), case-insensitive on scheme + <c>.md</c>, never throws for any string.
/// </summary>
public static class LinkClassifier
{
    private const string MarkdownExtension = ".md";

    /// <summary>Classifies <paramref name="href"/> resolved against <paramref name="basePageUrl"/>.</summary>
    public static LinkTarget Classify(string? href, Uri? basePageUrl)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return LinkTarget.Unsupported;
        }

        string trimmed = href.Trim();

        // A pure fragment (#heading) is a same-page anchor — no fetch, no resolution needed.
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return LinkTarget.AnchorTo(trimmed.Substring(1));
        }

        // Resolve relative-or-absolute against the base. A null base + relative ref -> null (Unsupported).
        Uri? resolved = ResolveHref(trimmed, basePageUrl);
        if (resolved is null)
        {
            return LinkTarget.Unsupported;
        }

        // Only http(s) is navigable/external; everything else (mailto:/tel:/javascript:/data:) is Unsupported.
        bool isHttp =
            string.Equals(resolved.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolved.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp)
        {
            return LinkTarget.Unsupported;
        }

        bool sameHost = basePageUrl is not null &&
            string.Equals(resolved.Host, basePageUrl.Host, StringComparison.OrdinalIgnoreCase);

        if (sameHost && IsMarkdownPath(resolved.AbsolutePath))
        {
            return LinkTarget.Internal(resolved);
        }

        return LinkTarget.ExternalTo(resolved);
    }

    private static Uri? ResolveHref(string href, Uri? basePageUrl)
    {
        // An already-absolute href stands on its own (no base needed).
        if (Uri.TryCreate(href, UriKind.Absolute, out Uri? absolute))
        {
            return absolute;
        }

        // Otherwise it must resolve against the base; without a base it is unresolvable (total -> null).
        if (basePageUrl is null)
        {
            return null;
        }

        return PageUrlResolver.ResolveAgainst(basePageUrl, href);
    }

    private static bool IsMarkdownPath(string absolutePath)
    {
        if (absolutePath.Length <= MarkdownExtension.Length ||
            !absolutePath.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject a bare "/.md" (empty document stem).
        char beforeExtension = absolutePath[absolutePath.Length - MarkdownExtension.Length - 1];
        return beforeExtension != '/';
    }
}
