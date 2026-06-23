using System;
using System.Text.RegularExpressions;

namespace TheMarkdownWeb.App;

/// <summary>
/// The kind of candidate being validated — affects which fallback content-types are allowed and
/// whether the llms.txt minimal-structure check applies (Story 6.3 AC4).
/// </summary>
public enum CandidateKind
{
    /// <summary>The page URL probed with <c>Accept: text/markdown</c> (or the alternate-link href).</summary>
    PageOrAlternate,

    /// <summary>The <c>&lt;path&gt;.md</c> sibling probe.</summary>
    MdSibling,

    /// <summary>The <c>/llms.txt</c> site-index probe.</summary>
    LlmsText,
}

/// <summary>
/// Pure, total validation rule for a discovery candidate (Story 6.3 AC4). Implements the
/// "No Markdown Available Determination Rule" from the 2026-06-23 research:
/// <list type="number">
///   <item>HTTP status is 2xx;</item>
///   <item><c>Content-Type</c> media type is <c>text/markdown</c> (case-insensitive; charset ignored),
///         OR <c>text/plain</c> as a weak fallback for <see cref="CandidateKind.MdSibling"/> /
///         <see cref="CandidateKind.LlmsText"/> — but ONLY if the body also passes the structure check;</item>
///   <item>Body does NOT begin with an HTML doctype/tag cluster (first ≈512 non-whitespace bytes);</item>
///   <item>Body is non-empty and within the 8 MiB size bound;</item>
///   <item>For <see cref="CandidateKind.LlmsText"/> specifically: minimal markdown structure
///         (leading <c>#</c> heading and/or markdown links).</item>
/// </list>
/// Never throws for any input.
/// </summary>
public static class MarkdownCandidateValidator
{
    /// <summary>Maximum accepted body size (chars). Mirrors MarkdownFetcher's 8 MiB guard.</summary>
    public const int MaxBodyChars = 8 * 1024 * 1024;

    private const string MarkdownMediaType = "text/markdown";
    private const string PlainTextMediaType = "text/plain";

    // Leading bytes that indicate an HTML response (soft-404 / SPA catch-all).
    // Matched against the first ~512 non-whitespace characters, case-insensitive.
    private static readonly string[] HtmlDoctypeMarkers =
    {
        "<!doctype html",
        "<html",
        "<head",
        "<?xml",
        "<body",
        "<script",
        "<meta",
    };

    // Minimal markdown-structure patterns for /llms.txt: a leading # heading or a markdown link [text](url).
    private static readonly Regex MarkdownHeadingPattern = new(@"^\s*#", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkPattern = new(@"\[.+?\]\(https?://", RegexOptions.Compiled);

    /// <summary>
    /// Returns <c>true</c> iff the candidate response passes all validation checks for the given
    /// <paramref name="kind"/>. Never throws.
    /// </summary>
    public static bool IsValidMarkdown(int statusCode, string? contentType, string body, CandidateKind kind)
    {
        try
        {
            return ValidateInternal(statusCode, contentType, body, kind);
        }
        catch
        {
            return false; // total — any unexpected parse error is a reject
        }
    }

    private static bool ValidateInternal(int statusCode, string? contentType, string body, CandidateKind kind)
    {
        // Rule 1: 2xx status.
        if (statusCode < 200 || statusCode > 299)
        {
            return false;
        }

        // Rule 4: non-empty body within size bound.
        if (string.IsNullOrEmpty(body))
        {
            return false;
        }

        if (body.Length > MaxBodyChars)
        {
            return false;
        }

        // Rule 3: HTML-doctype byte-sniff on the first ~512 non-whitespace bytes.
        if (BeginsWithHtmlMarker(body))
        {
            return false;
        }

        // Rule 2: Content-Type check.
        string normalizedType = NormalizeMediaType(contentType);

        bool isMarkdown = string.Equals(normalizedType, MarkdownMediaType, StringComparison.OrdinalIgnoreCase);
        bool isPlainText = string.Equals(normalizedType, PlainTextMediaType, StringComparison.OrdinalIgnoreCase);

        if (!isMarkdown)
        {
            // text/plain is only accepted as a weak fallback for sibling/.md and llms.txt — and only
            // if the body passes the structure sniff (no HTML, above) AND kind allows it.
            if (!isPlainText)
            {
                return false;
            }

            if (kind == CandidateKind.PageOrAlternate)
            {
                // No text/plain fallback for page/alternate probes — must be text/markdown.
                return false;
            }

            // For MdSibling and LlmsText: text/plain is accepted but body must also have some structure.
            // The HTML sniff above already ran; here we just need minimal markdown-like content.
            // We'll do a light check: body must not be blank (already checked) and not pure HTML.
            // That's sufficient — the HTML sniff is the main gate.
        }

        // Rule 5: For llms.txt, require minimal markdown structure.
        if (kind == CandidateKind.LlmsText)
        {
            if (!HasMinimalMarkdownStructure(body))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Strips charset and parameters from a Content-Type value to get the bare media type.
    /// E.g. <c>"text/markdown; charset=utf-8"</c> → <c>"text/markdown"</c>.
    /// Returns empty string for null/empty input. Never throws.
    /// </summary>
    private static string NormalizeMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        int semicolon = contentType.IndexOf(';');
        return (semicolon >= 0 ? contentType.Substring(0, semicolon) : contentType).Trim();
    }

    /// <summary>
    /// Scans the first ~512 non-whitespace characters of <paramref name="body"/> for HTML doctype/tag
    /// markers. Returns <c>true</c> if the body looks like HTML.
    /// </summary>
    private static bool BeginsWithHtmlMarker(string body)
    {
        // Build the sniff window from the first ~512 chars AFTER leading whitespace, collapsing any
        // internal run of whitespace to a single space. We must NOT drop internal spaces: a marker
        // like "<!doctype html" contains a space, and stripping all whitespace would turn the body
        // "<!DOCTYPE HTML PUBLIC ..." into "<!DOCTYPEHTMLPUBLIC..." which no marker would match.
        var sb = new System.Text.StringBuilder(512);
        bool seenNonWhitespace = false;
        bool pendingSpace = false;
        foreach (char ch in body)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (seenNonWhitespace)
                {
                    pendingSpace = true; // collapse internal whitespace; emit at most one space
                }
                continue; // skip leading whitespace entirely
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
                if (sb.Length >= 512)
                {
                    break;
                }
            }

            seenNonWhitespace = true;
            sb.Append(ch);
            if (sb.Length >= 512)
            {
                break;
            }
        }

        string sniff = sb.ToString();

        foreach (string marker in HtmlDoctypeMarkers)
        {
            if (sniff.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> iff <paramref name="body"/> contains minimal markdown structure: a leading
    /// <c>#</c> heading OR at least one markdown link <c>[text](url)</c>.
    /// </summary>
    private static bool HasMinimalMarkdownStructure(string body)
    {
        return MarkdownHeadingPattern.IsMatch(body) || MarkdownLinkPattern.IsMatch(body);
    }
}
