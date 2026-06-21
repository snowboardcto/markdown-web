using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Pure, TOTAL fragment→heading matching (Story 3.5 AC5). Locates the target heading for an
/// <c>#anchor</c> click by github-style slugging the heading text and comparing it to the fragment.
/// The heading-anchor slug is the SAME per-segment github-slug used by <see cref="SlugDeriver"/>
/// (lowercase, delete punctuation/symbols, spaces→<c>-</c>, no collapse, no trim), so anchors agree
/// with GitHub's rendered heading ids. Never throws.
/// </summary>
public static class AnchorMatcher
{
    /// <summary>
    /// github-style anchor slug of the visible heading text. Reuses the single-segment slug rule
    /// (<see cref="SlugDeriver.PathToSlug"/> over a name with no <c>/</c> and no <c>.md</c>). Total.
    /// </summary>
    public static string SlugHeading(string headingText)
    {
        if (string.IsNullOrEmpty(headingText))
        {
            return string.Empty;
        }

        // A heading is a single "segment": no path split, no .md drop, no index collapse — just the
        // github-slugger per-segment transform, which PathToSlug applies to a bare (slash-free) name.
        return SlugDeriver.PathToSlug(headingText);
    }

    /// <summary>
    /// <c>true</c> iff the heading text's anchor slug equals the (sans-<c>#</c>) fragment. Total.
    /// </summary>
    public static bool Matches(string headingText, string fragment)
    {
        if (fragment is null)
        {
            return false;
        }

        string frag = fragment.StartsWith("#", StringComparison.Ordinal) ? fragment.Substring(1) : fragment;
        return string.Equals(SlugHeading(headingText ?? string.Empty), frag, StringComparison.Ordinal);
    }
}
