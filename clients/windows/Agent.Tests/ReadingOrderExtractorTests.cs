using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// Story 4.4 AC6 (the load-bearing audio AC) — <see cref="ReadingOrderExtractor.Extract"/> is a PURE,
/// deterministic function (Markdig AST → ordered plain text) covering the FULL body in document order:
/// headings, paragraphs, list items, table cells, and code blocks. Plain <c>[Fact]</c>s (markdown → string;
/// no STA, no WPF, no speech, no socket).
/// </summary>
public class ReadingOrderExtractorTests
{
    // A multi-block fixture: heading, paragraph, a 2-item bulleted list, a GFM table, and a code block.
    // Each block carries a unique token so coverage + document order are unambiguous.
    private const string MultiBlock =
        "# HeadingToken\n" +
        "\n" +
        "ParagraphToken in the body.\n" +
        "\n" +
        "- FirstItemToken\n" +
        "- SecondItemToken\n" +
        "\n" +
        "| HeadAToken | HeadBToken |\n" +
        "| ---------- | ---------- |\n" +
        "| CellOneToken | CellTwoToken |\n" +
        "\n" +
        "```\n" +
        "CodeLineToken();\n" +
        "```\n";

    [Fact] // AC6 — every block's text is present (full-body coverage — no block silently dropped).
    public void Extract_CoversEveryBlocksText()
    {
        string text = ReadingOrderExtractor.Extract(MultiBlock);

        foreach (string token in new[]
                 {
                     "HeadingToken", "ParagraphToken", "FirstItemToken", "SecondItemToken",
                     "HeadAToken", "HeadBToken", "CellOneToken", "CellTwoToken", "CodeLineToken",
                 })
        {
            Assert.Contains(token, text);
        }
    }

    [Fact] // AC6 — the text appears in DOCUMENT ORDER (monotonic, strictly-increasing IndexOf).
    public void Extract_EmitsBlocksInDocumentOrder()
    {
        string text = ReadingOrderExtractor.Extract(MultiBlock);

        int heading = text.IndexOf("HeadingToken", System.StringComparison.Ordinal);
        int paragraph = text.IndexOf("ParagraphToken", System.StringComparison.Ordinal);
        int firstItem = text.IndexOf("FirstItemToken", System.StringComparison.Ordinal);
        int secondItem = text.IndexOf("SecondItemToken", System.StringComparison.Ordinal);
        int cell = text.IndexOf("CellOneToken", System.StringComparison.Ordinal);
        int code = text.IndexOf("CodeLineToken", System.StringComparison.Ordinal);

        Assert.True(heading < paragraph, "heading precedes the paragraph");
        Assert.True(paragraph < firstItem, "paragraph precedes the list");
        Assert.True(firstItem < secondItem, "list items are in order");
        Assert.True(secondItem < cell, "the list precedes the table");
        Assert.True(cell < code, "the table precedes the code block");
    }

    [Fact] // AC6 — the code block's TEXT is spoken (the actual lines), not a placeholder marker.
    public void Extract_EmitsCodeBlockText()
    {
        string text = ReadingOrderExtractor.Extract("```\nCodeLineToken();\n```\n");

        Assert.Contains("CodeLineToken", text);
    }

    [Fact] // AC6 — link TEXT is spoken; the URL is not.
    public void Extract_EmitsLinkText_NotUrl()
    {
        string text = ReadingOrderExtractor.Extract("See [VisibleLinkText](https://example.com/hidden-url).");

        Assert.Contains("VisibleLinkText", text);
        Assert.DoesNotContain("example.com", text);
    }

    [Theory] // AC6 — total: null/empty/whitespace → empty string (the no-op-on-empty source for AC7).
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n\t")]
    public void Extract_Emptyish_ReturnsEmptyString(string? markdown)
    {
        Assert.Equal(string.Empty, ReadingOrderExtractor.Extract(markdown!));
    }

    [Fact] // AC6 — deterministic: same input → byte-identical output across runs.
    public void Extract_IsDeterministic()
    {
        Assert.Equal(
            ReadingOrderExtractor.Extract(MultiBlock),
            ReadingOrderExtractor.Extract(MultiBlock));
    }

    [Fact] // AC6 — malformed / partial markdown never throws (Markdig is total over arbitrary input).
    public void Extract_MalformedMarkdown_DoesNotThrow()
    {
        string text = ReadingOrderExtractor.Extract("# unclosed [link](http:// **bold without close\n| a | b");

        Assert.NotNull(text); // no throw; some best-effort text comes back.
    }
}
