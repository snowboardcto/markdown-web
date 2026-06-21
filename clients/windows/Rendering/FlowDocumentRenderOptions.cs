namespace TheMarkdownWeb.Rendering;

/// <summary>
/// Font + theme seams for <see cref="FlowDocumentRenderer"/>. Story 3.3 set element STRUCTURE plus
/// the markers the acceptance tests assert; Story 3.6 lands the exact GitHub theming (px-ish
/// hairlines, spacing/line-height, code-block backgrounds, link/muted/border colors) ADDITIVELY via
/// the <see cref="Theme"/> seam below — the defaults ARE the faithful basic (GitHub-light) theme.
/// The existing font/highlighting defaults are UNCHANGED.
/// </summary>
public sealed class FlowDocumentRenderOptions
{
    /// <summary>Monospace family for inline code + fenced code blocks (DESIGN.md typography.mono).</summary>
    public string MonospaceFontFamily { get; init; } = "Consolas";

    /// <summary>Body/sans family applied to the document baseline (DESIGN.md typography.sans).</summary>
    public string BodyFontFamily { get; init; } = "Segoe UI";

    /// <summary>
    /// Story 3.4 — syntax-highlight on/off seam. When ON (the default), a fenced code block whose
    /// info-string names a language ColorCode recognizes is tokenized into per-token colored
    /// monospace runs (github-light palette). When OFF, the block falls back to the 3.3 single-color
    /// monospace rendering. Unknown/missing languages always fall back regardless of this flag.
    /// </summary>
    public bool SyntaxHighlighting { get; init; } = true;

    /// <summary>
    /// Story 3.6 — the render-theme / Epic-4 personality-override seam. Defaults to
    /// <see cref="RenderTheme.Basic"/>, so the no-personality DEFAULT render IS the faithful basic
    /// (GitHub-light) theme with no opt-in. Epic 4 supplies a non-Basic theme to override the render
    /// without reworking the Basic path (open/closed — see <see cref="RenderTheme"/>).
    /// </summary>
    public RenderTheme Theme { get; init; } = RenderTheme.Basic;
}
