using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC3 — inline emphasis: bold -> FontWeight.Bold; italic -> FontStyle.Italic;
/// GFM strikethrough (~~x~~, requires the extension) -> TextDecorations.Strikethrough.
/// Nested emphasis composes (bold AND italic on the inner run).
/// </summary>
public class EmphasisTests
{
    [StaFact]
    public void Emphasis_BoldItalicStrikethrough_AreEachDetectable()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("**b** *i* ~~s~~");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.First());
        var inlines = FlowDocumentTestHelpers.FlattenInlines(paragraph);

        // One bold inline carrying the text "b".
        bool hasBold = inlines.Any(i => i.FontWeight == FontWeights.Bold
            && FlowDocumentTestHelpers.InlineText(i).Contains('b'));
        Assert.True(hasBold, "Expected a bold inline for **b**.");

        // One italic inline carrying the text "i".
        bool hasItalic = inlines.Any(i => i.FontStyle == FontStyles.Italic
            && FlowDocumentTestHelpers.InlineText(i).Contains('i'));
        Assert.True(hasItalic, "Expected an italic inline for *i*.");

        // One inline carrying the strikethrough decoration (proves UseEmphasisExtras is on).
        bool hasStrikethrough = inlines.Any(HasStrikethrough);
        Assert.True(hasStrikethrough, "Expected a strikethrough inline for ~~s~~ (GFM extension).");
    }

    [StaFact]
    public void NestedEmphasis_DoubleEmphasis_ComposesBoldAndItalic()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("**_x_**");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.First());

        // The inner Run carrying "x" must report BOTH bold and italic (composition).
        Run inner = FlowDocumentTestHelpers.CollectRuns(paragraph).Single(r => r.Text == "x");
        Assert.Equal(FontWeights.Bold, inner.FontWeight);
        Assert.Equal(FontStyles.Italic, inner.FontStyle);
    }

    [StaFact]
    public void NestedEmphasis_TripleEmphasis_ComposesBoldAndItalic(/* D3 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("***x***");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.First());

        Run inner = FlowDocumentTestHelpers.CollectRuns(paragraph).Single(r => r.Text == "x");
        Assert.Equal(FontWeights.Bold, inner.FontWeight);
        Assert.Equal(FontStyles.Italic, inner.FontStyle);
    }

    private static bool HasStrikethrough(Inline inline)
    {
        TextDecorationCollection? decorations = inline.TextDecorations;
        if (decorations is null)
        {
            return false;
        }

        // A decoration whose Location is Strikethrough is present on this inline.
        return decorations.Any(d => d.Location == TextDecorationLocation.Strikethrough);
    }
}
