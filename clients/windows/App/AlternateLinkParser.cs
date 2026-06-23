using System;
using System.Text.RegularExpressions;

namespace TheMarkdownWeb.App;

/// <summary>
/// Extracts the first <c>&lt;link rel="alternate" type="text/markdown" href="..."&gt;</c> href from an
/// HTML string, scoped to the <c>&lt;head&gt;</c> region (Story 6.3 AC1, Task 3).
///
/// Uses a tolerant regex/streaming scan approach — zero new dependencies, no Chromium, no JS execution
/// (satisfies AC7 / <see cref="NoEmbeddedBrowserTests"/>). Attribute order, single/double quotes, and
/// whitespace variation are all handled. Returns <c>null</c> if no alternate markdown link is present.
/// Pure, total — never throws.
/// </summary>
public static class AlternateLinkParser
{
    // Match the <head>...</head> region (case-insensitive; DOTALL for multiline heads).
    // We use a simple approach: find everything up to </head> or <body (whichever comes first).
    private static readonly Regex HeadRegion = new(
        @"<head\b[^>]*>(.*?)(?:</head>|<body)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Match a <link ...> tag that has rel="alternate" (or rel='alternate') AND type="text/markdown"
    // (or type='text/markdown') in any attribute order. Capture the href value.
    // Strategy: capture the full tag, then extract rel, type, and href attributes individually.
    private static readonly Regex LinkTagPattern = new(
        @"<link\b([^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex RelAlternate = new(
        @"\brel\s*=\s*(?:""alternate""|'alternate')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeTextMarkdown = new(
        @"\btype\s*=\s*(?:""text/markdown""|'text/markdown')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HrefValue = new(
        @"\bhref\s*=\s*(?:""([^""]*)""|'([^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses <paramref name="html"/> for a <c>&lt;link rel="alternate" type="text/markdown"&gt;</c>
    /// tag within the <c>&lt;head&gt;</c> region and returns the resolved absolute href, or <c>null</c>
    /// if none is found or the href cannot be resolved. Never throws.
    /// </summary>
    /// <param name="html">The HTML body to parse.</param>
    /// <param name="finalResponseUri">The final response URI (after redirects), used to resolve a
    /// relative <c>href</c> against the document base.</param>
    /// <returns>An absolute <see cref="Uri"/> for the alternate markdown resource, or <c>null</c>.</returns>
    public static Uri? Parse(string? html, Uri? finalResponseUri)
    {
        try
        {
            return ParseInternal(html, finalResponseUri);
        }
        catch
        {
            return null; // total
        }
    }

    private static Uri? ParseInternal(string? html, Uri? finalResponseUri)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }

        // Scope to <head> region only.
        string searchRegion = html;
        Match headMatch = HeadRegion.Match(html);
        if (headMatch.Success)
        {
            searchRegion = headMatch.Value;
        }

        // Find all <link ...> tags in the scoped region.
        foreach (Match linkMatch in LinkTagPattern.Matches(searchRegion))
        {
            string attrs = linkMatch.Groups[1].Value;

            // Must have rel="alternate" AND type="text/markdown".
            if (!RelAlternate.IsMatch(attrs) || !TypeTextMarkdown.IsMatch(attrs))
            {
                continue;
            }

            // Extract the href value.
            Match hrefMatch = HrefValue.Match(attrs);
            if (!hrefMatch.Success)
            {
                continue;
            }

            // Group 1 = double-quoted href, Group 2 = single-quoted href.
            string href = hrefMatch.Groups[1].Success
                ? hrefMatch.Groups[1].Value
                : hrefMatch.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            // Resolve relative hrefs against the final response URI.
            if (Uri.TryCreate(href, UriKind.Absolute, out Uri? absolute))
            {
                return absolute;
            }

            if (finalResponseUri is not null &&
                Uri.TryCreate(finalResponseUri, href, out Uri? resolved))
            {
                return resolved;
            }
        }

        return null;
    }
}
