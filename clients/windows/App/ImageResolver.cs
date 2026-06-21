using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// Pure, TOTAL image-source resolution (Story 3.5 AC7). Maps each recorded <c>Image.Tag</c> source
/// string (3.3) + the current page's base URL → an absolute <see cref="Uri"/>, reusing
/// <see cref="PageUrlResolver"/>. Returns <c>null</c> (never throws) for an unresolvable/garbage source
/// (the host then leaves the <c>Image</c> empty with its alt preserved).
/// </summary>
public static class ImageResolver
{
    /// <summary>
    /// Resolves <paramref name="recordedSource"/> against <paramref name="basePageUrl"/>. An
    /// already-absolute source (http(s), data:, etc.) is returned as-is; a relative source resolves
    /// against the page directory. Returns <c>null</c> for empty/garbage or a relative source with no base.
    /// </summary>
    public static Uri? Resolve(string? recordedSource, Uri? basePageUrl)
    {
        if (string.IsNullOrWhiteSpace(recordedSource))
        {
            return null;
        }

        string trimmed = recordedSource.Trim();

        // An already-absolute source (https://, data:, …) stands on its own — no base required.
        // EXCEPT a protocol-relative "//host/path": .NET parses that as an absolute file:// UNC URI,
        // but per web semantics it is scheme-relative and must adopt the base's scheme. Let it fall
        // through to PageUrlResolver (new Uri(base, "//host/path") -> "<base-scheme>://host/path").
        if (!trimmed.StartsWith("//", StringComparison.Ordinal) &&
            Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute))
        {
            return absolute;
        }

        // Otherwise resolve relative-or-protocol-relative against the page base.
        if (basePageUrl is null)
        {
            return null;
        }

        return PageUrlResolver.ResolveAgainst(basePageUrl, trimmed);
    }
}
