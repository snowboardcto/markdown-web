using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// Task 11b — edge-case / totality hardening (decisions D1–D9). Render is TOTAL over any string
/// except null; raw HTML is literal text (no webview); thematic break -> Tag=="hr"; recursive
/// nesting round-trips; "####### x" is a paragraph not a heading; soft/hard line breaks.
/// </summary>
public class EdgeCaseHardeningTests
{
    public static TheoryData<string> MalformedInputs() => new()
    {
        "```\nunterminated fence",       // unterminated fence
        "| a |\n|---|",                  // ragged / malformed table
        "~ stray tilde ~",               // stray strikethrough markers
        "> dangling quote",              // stray blockquote marker
        "| only a pipe",                 // stray pipe
        "<div>raw html</div>",           // raw HTML
        "   \n\n",                       // whitespace-only
        "***",                           // ambiguous emphasis / thematic break
    };

    [Theory]
    [MemberData(nameof(MalformedInputs))]
    public void Render_MalformedInput_ReturnsNonNull_NeverThrows(string markdown /* D9 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument? document = null;
        var ex = Record.Exception(() => document = renderer.Render(markdown));

        Assert.Null(ex);
        Assert.NotNull(document);
    }

    [StaFact]
    public void RawHtmlBlock_RendersAsLiteralText_NoWebview(/* D2 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("<div>x</div>");

        // The literal HTML source appears as plain text somewhere in the document.
        string allText = string.Concat(document.Blocks.Select(FlowDocumentTestHelpers.BlockText));
        Assert.Contains("<div>x</div>", allText);
    }

    [StaFact]
    public void RawInlineHtml_RendersAsLiteralText_NoWebview(/* D2 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("a <b>x</b> b");

        string allText = string.Concat(document.Blocks.Select(FlowDocumentTestHelpers.BlockText));
        Assert.Contains("<b>x</b>", allText);
    }

    [StaFact]
    public void ThematicBreak_IsTaggedHr_BetweenParagraphs(/* D1 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("a\n\n---\n\nb");

        // Some block carries Tag == "hr".
        bool hasHr = document.Blocks.Any(b => (b as Paragraph)?.Tag as string == "hr"
            || (b as Section)?.Tag as string == "hr"
            || (b as BlockUIContainer)?.Tag as string == "hr");
        Assert.True(hasHr, "Thematic break must map to a block tagged \"hr\".");
    }

    [StaFact]
    public void H7_IsParagraphNotHeading(/* D6 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("####### x");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
        Assert.Null(paragraph.Tag); // NOT "h7" or any heading tag.
        Assert.Contains("####### x", FlowDocumentTestHelpers.ParagraphText(paragraph));
    }

    [StaFact]
    public void ListInsideBlockquote_IsReachable(/* D3 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("> - a\n> - b\n");

        Section section = Assert.IsType<Section>(document.Blocks.Single());
        List list = section.Blocks.OfType<List>().Single();
        Assert.Equal(2, list.ListItems.Count);
    }

    [StaFact]
    public void CodeBlockInsideList_IsReachableAndMono(/* D3 */)
    {
        var renderer = new FlowDocumentRenderer();

        // A fenced code block indented inside a list item.
        FlowDocument document = renderer.Render("- item\n\n  ```\n  code\n  ```\n");

        List list = Assert.IsType<List>(document.Blocks.Single());
        ListItem item = list.ListItems.First();

        // The item contains a paragraph with a mono run carrying the code.
        bool hasMonoCode = item.Blocks.OfType<Paragraph>()
            .SelectMany(FlowDocumentTestHelpers.CollectRuns)
            .Any(r => r.FontFamily.Source == "Consolas" && r.Text.Contains("code"));
        Assert.True(hasMonoCode, "A code block inside a list item must render as a mono code run.");
    }

    [StaFact]
    public void TableCell_WithBoldInline_CarriesBoldRun(/* D3 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render(
            "| H |\n| --- |\n| **bold** |\n");

        Table table = Assert.IsType<Table>(document.Blocks.Single());
        var rows = FlowDocumentTestHelpers.AllRows(table);
        TableCell bodyCell = rows[1].Cells[0];

        bool cellHasBold = bodyCell.Blocks.OfType<Paragraph>()
            .SelectMany(FlowDocumentTestHelpers.CollectRuns)
            .Any(r => r.FontWeight == FontWeights.Bold && r.Text.Contains("bold"));
        Assert.True(cellHasBold, "A bold inline inside a table cell must carry FontWeight.Bold.");
    }

    [StaFact]
    public void DeeplyNestedList_ThreeDeep_IsReachable(/* D3 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("- a\n  - b\n    - c\n");

        List level1 = Assert.IsType<List>(document.Blocks.Single());
        List level2 = level1.ListItems.First().Blocks.OfType<List>().Single();
        List level3 = level2.ListItems.First().Blocks.OfType<List>().Single();
        Assert.Single(level3.ListItems);
    }

    [StaFact]
    public void HardLineBreak_EmitsLineBreakInline(/* D4 */)
    {
        var renderer = new FlowDocumentRenderer();

        // Trailing two spaces => hard break.
        FlowDocument document = renderer.Render("a  \nb");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
        bool hasLineBreak = FlowDocumentTestHelpers.FlattenInlines(paragraph).OfType<LineBreak>().Any();
        Assert.True(hasLineBreak, "A hard line break must emit a LineBreak inline.");
    }

    [StaFact]
    public void Autolink_MapsToInertHyperlink(/* D5 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("see https://example.com now");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
        Hyperlink? link = FlowDocumentTestHelpers.FirstHyperlink(paragraph);
        Assert.NotNull(link);
        // Records the href; inert navigation is Story 3.5 (we do not assert a click handler).
        // Use OriginalString (the exact href as authored): System.Uri canonicalization appends a
        // trailing slash to an authority-only absolute URL, so .ToString() would yield
        // "https://example.com/". OriginalString preserves the URL the renderer recorded verbatim.
        Assert.Equal("https://example.com", link!.NavigateUri?.OriginalString);
    }

    [StaFact]
    public void ExplicitLink_MapsToInertHyperlink(/* D5 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("[label](https://example.com/page)");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
        Hyperlink? link = FlowDocumentTestHelpers.FirstHyperlink(paragraph);
        Assert.NotNull(link);
        Assert.Equal("https://example.com/page", link!.NavigateUri?.OriginalString);
    }
}
