using System.Linq;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC8 — a blockquote maps to a Section marked with a left-edge rule: BorderThickness.Left > 0
/// AND a non-null BorderBrush. Quoted content is preserved inside it; nested quotes nest as a
/// Section within a Section.
/// </summary>
public class BlockquoteTests
{
    [StaFact]
    public void Blockquote_IsSectionWithLeftRuleAndPreservedContent()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("> hello\n");

        Section section = Assert.IsType<Section>(document.Blocks.Single());

        Assert.True(section.BorderThickness.Left > 0, "Blockquote Section must carry a left-rule (BorderThickness.Left > 0).");
        Assert.NotNull(section.BorderBrush);

        Paragraph inner = section.Blocks.OfType<Paragraph>().Single();
        Assert.Equal("hello", FlowDocumentTestHelpers.ParagraphText(inner));
    }

    [StaFact]
    public void NestedBlockquote_NestsSectionWithinSection()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("> > deep\n");

        Section outer = Assert.IsType<Section>(document.Blocks.Single());
        Section inner = outer.Blocks.OfType<Section>().Single();
        Assert.True(inner.BorderThickness.Left > 0);
        Assert.Contains("deep", FlowDocumentTestHelpers.BlockText(inner));
    }
}
