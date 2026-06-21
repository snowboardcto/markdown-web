using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC2 — ATX headings H1–H6 map to top-level Paragraphs that are bold, level-tagged
/// (Paragraph.Tag == "h1".."h6"), and monotonic by FontSize (h1 largest, every heading
/// larger than the body default).
/// </summary>
public class HeadingTests
{
    [StaFact]
    public void Headings_AreBold_LevelTagged_AndMonotonicallySized()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("# A\n\n## B\n\n###### F");

        var paragraphs = document.Blocks.OfType<Paragraph>().ToList();
        Assert.Equal(3, paragraphs.Count);

        Paragraph h1 = paragraphs[0];
        Paragraph h2 = paragraphs[1];
        Paragraph h6 = paragraphs[2];

        Assert.Equal("h1", h1.Tag as string);
        Assert.Equal("h2", h2.Tag as string);
        Assert.Equal("h6", h6.Tag as string);

        Assert.Equal(FontWeights.Bold, h1.FontWeight);
        Assert.Equal(FontWeights.Bold, h2.FontWeight);
        Assert.Equal(FontWeights.Bold, h6.FontWeight);

        // Strictly monotonic by level: h1 > h2 > h6.
        Assert.True(h1.FontSize > h2.FontSize, "H1 must be larger than H2.");
        Assert.True(h2.FontSize > h6.FontSize, "H2 must be larger than H6.");

        // Every heading is larger than the document body default font size.
        double bodySize = document.FontSize;
        Assert.True(h1.FontSize > bodySize, "H1 must be larger than the body font size.");
        Assert.True(h2.FontSize > bodySize, "H2 must be larger than the body font size.");
        Assert.True(h6.FontSize > bodySize, "H6 must be larger than the body font size.");

        // Text round-trips into the heading's inlines.
        Assert.Equal("A", FlowDocumentTestHelpers.ParagraphText(h1));
        Assert.Equal("B", FlowDocumentTestHelpers.ParagraphText(h2));
        Assert.Equal("F", FlowDocumentTestHelpers.ParagraphText(h6));
    }

    [StaFact]
    public void Heading_WithInlineCode_KeepsHeadingTagAndCarriesMonoRun(/* D7 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("# Use `dotnet`");

        Paragraph heading = Assert.IsType<Paragraph>(document.Blocks.First());
        Assert.Equal("h1", heading.Tag as string);

        // Inline code inside a heading is still a mono Run (mapper composition, D7).
        bool hasMonoRun = FlowDocumentTestHelpers.CollectRuns(heading)
            .Any(r => r.FontFamily.Source == "Consolas" && r.Text == "dotnet");
        Assert.True(hasMonoRun, "Inline code inside a heading must render as a monospace Run.");
    }
}
