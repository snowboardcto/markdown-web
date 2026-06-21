using System;

namespace TheMarkdownWeb.App;

/// <summary>
/// The four ways a rendered <c>Hyperlink</c>'s href is classified by <see cref="LinkClassifier"/>
/// (Story 3.5 AC2). Deterministic + total — every href maps to exactly one of these.
/// </summary>
public enum LinkKind
{
    /// <summary>A relative-or-absolute link whose resolved URL is an http(s) <c>.md</c> page on the
    /// same host as the base — navigate in place (AC4).</summary>
    InternalMarkdown,

    /// <summary>A pure fragment <c>#heading</c> — scroll within the current page (AC5); no fetch.</summary>
    Anchor,

    /// <summary>An absolute http(s) link that is NOT an internal <c>.md</c> page — open in the system
    /// browser (AC6).</summary>
    External,

    /// <summary><c>mailto:</c>/<c>tel:</c>/<c>javascript:</c>/<c>data:</c>/empty/garbage — no-op,
    /// never a crash.</summary>
    Unsupported,
}

/// <summary>
/// The classified result for a clicked/typed link (Story 3.5 AC2). Carries the resolved data the
/// navigator needs: the resolved absolute page <see cref="Url"/> for
/// <see cref="LinkKind.InternalMarkdown"/>, the (sans-<c>#</c>) <see cref="Fragment"/> for
/// <see cref="LinkKind.Anchor"/>, and the absolute <see cref="Url"/> for <see cref="LinkKind.External"/>.
/// </summary>
public readonly record struct LinkTarget(LinkKind Kind, Uri? Url, string? Fragment)
{
    /// <summary>An internal markdown page to navigate to in place.</summary>
    public static LinkTarget Internal(Uri pageUrl) => new(LinkKind.InternalMarkdown, pageUrl, null);

    /// <summary>A same-page anchor to scroll to (fragment without the leading <c>#</c>).</summary>
    public static LinkTarget AnchorTo(string fragment) => new(LinkKind.Anchor, null, fragment);

    /// <summary>An external link to open in the system browser.</summary>
    public static LinkTarget ExternalTo(Uri url) => new(LinkKind.External, url, null);

    /// <summary>An unsupported/garbage link — a total no-op dispatch.</summary>
    public static LinkTarget Unsupported => new(LinkKind.Unsupported, null, null);
}
