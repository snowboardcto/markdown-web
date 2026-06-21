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
}
