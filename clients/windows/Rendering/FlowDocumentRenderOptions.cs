namespace TheMarkdownWeb.Rendering;

/// <summary>
/// Minimal font seams for <see cref="FlowDocumentRenderer"/>. This story (3.3) sets element
/// STRUCTURE plus the markers the acceptance tests assert; exact GitHub theming (px sizes,
/// hairlines, zebra rows, code-block backgrounds, reading measure, light/dark) is Story 3.6.
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
}
