using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Pure, TOTAL <c>.md</c>-only URL acceptance predicate (Story 3-2 AC2). No I/O, no statics-with-state,
/// no socket — a plain function that is <c>[Fact]</c>-testable with no window and no network.
/// </summary>
public static class AddressBarValidation
{
    private const string MarkdownExtension = ".md";

    /// <summary>
    /// Returns <c>true</c> iff <paramref name="input"/>, after trimming surrounding whitespace, is an
    /// absolute <c>http</c>/<c>https</c> URL whose <see cref="Uri.AbsolutePath"/> ends (ordinal,
    /// case-insensitive) in <c>.md</c> with a non-empty document stem (the bare <c>/.md</c> is rejected).
    /// Query and fragment are ignored (<see cref="Uri.AbsolutePath"/> already excludes them).
    /// NEVER throws for any <see cref="string"/> input.
    /// </summary>
    public static bool IsLoadableMarkdownUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out Uri? uri) || uri is null)
        {
            return false;
        }

        bool isHttp =
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp)
        {
            return false;
        }

        string path = uri.AbsolutePath;
        if (path.Length <= MarkdownExtension.Length ||
            !path.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject a bare "/.md" (empty document stem): the character immediately before ".md" must be a
        // real filename character, not the path separator.
        char beforeExtension = path[path.Length - MarkdownExtension.Length - 1];
        return beforeExtension != '/';
    }

    /// <summary>
    /// Returns <c>true</c> iff <paramref name="input"/>, after trimming surrounding whitespace, is an
    /// absolute <c>http</c>/<c>https</c> URL. This is the 6.2 broadened acceptance predicate — it accepts
    /// ANY http(s) URL (including non-<c>.md</c> URLs destined for discovery). Non-<c>http(s)</c> schemes
    /// (<c>ftp:</c>, <c>javascript:</c>, <c>file:</c>, <c>mailto:</c>) and unparseable/relative inputs
    /// return <c>false</c>. The existing <see cref="IsLoadableMarkdownUrl"/> is RETAINED unchanged as
    /// the <c>.md</c> fast-path discriminator. NEVER throws for any <see cref="string"/> input.
    /// </summary>
    public static bool IsAcceptableUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out Uri? uri) || uri is null)
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formats the <c>host + path</c> portion of an absolute http(s) URL for display in the address bar
    /// (Story 3-2 AC1). Returns <c>false</c> (with <paramref name="hostPath"/> = empty) for any input that
    /// is not an absolute http(s) URL. Pure — never throws, no I/O.
    /// </summary>
    public static bool TryGetHostPath(string? input, out string hostPath)
    {
        hostPath = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out Uri? uri) || uri is null)
        {
            return false;
        }

        bool isHttp =
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp)
        {
            return false;
        }

        hostPath = uri.Host + uri.AbsolutePath;
        return true;
    }
}
