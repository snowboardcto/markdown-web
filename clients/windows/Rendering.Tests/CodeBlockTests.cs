using System.Linq;
using Xunit;
using System.Windows.Documents;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// A fenced code block maps to a top-level block (Paragraph) whose code is verbatim
/// (newlines preserved), rendered in the monospace family, with the language info-string in
/// Tag. A KNOWN-language fence is SYNTAX-HIGHLIGHTED — it carries MULTIPLE distinct foreground
/// brushes across its runs (Story 3.4 inverts 3.3's single-foreground proof); an unknown/missing
/// language degrades to a single-color mono fallback.
/// </summary>
public class CodeBlockTests
{
    [StaFact]
    public void FencedCode_WithLanguage_IsMonoVerbatimTaggedAndSyntaxHighlighted()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```csharp\nvar x = 1;\nif (x) {}\n```");

        // One top-level code block.
        Block block = Assert.Single(document.Blocks);

        // Language preserved in Tag.
        Assert.Equal("csharp", BlockTag(block));

        // Mono font on every run of the block.
        var runs = CollectBlockRuns(block);
        Assert.NotEmpty(runs);
        Assert.All(runs, r => Assert.Equal("Consolas", r.FontFamily.Source));

        // Verbatim text, newline preserved between the two source lines.
        string text = FlowDocumentTestHelpers.BlockText(block);
        Assert.Contains("var x = 1;", text);
        Assert.Contains("if (x) {}", text);
        Assert.Contains('\n', text);

        // MULTIPLE distinct foreground brushes across the block (proves syntax highlighting ran).
        var foregrounds = FlowDocumentTestHelpers.DistinctForegrounds(block);
        Assert.True(foregrounds.Count >= 2,
            $"A known-language fence must be syntax-highlighted (>=2 distinct foregrounds), found {foregrounds.Count}.");
    }

    [StaFact]
    public void FencedCode_NoLanguage_RendersMonoWithNullOrEmptyTag_NoThrow(/* D7 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```\nplain\n```");

        Block block = Assert.Single(document.Blocks);
        string? tag = BlockTag(block);
        Assert.True(string.IsNullOrEmpty(tag), "A no-language fence should have a null/empty language Tag.");

        var runs = CollectBlockRuns(block);
        Assert.All(runs, r => Assert.Equal("Consolas", r.FontFamily.Source));
        Assert.Contains("plain", FlowDocumentTestHelpers.BlockText(block));
    }

    [StaFact]
    public void FencedCode_EmptyFence_DoesNotThrow(/* D7 */)
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("```\n```");

        // Produces a (possibly empty) code block, never throws.
        Assert.NotNull(document);
    }

    [StaFact]
    public void FencedCode_BackticksInContent_PreservedVerbatim(/* D7 */)
    {
        var renderer = new FlowDocumentRenderer();

        // Four-backtick fence so two-backtick content is preserved literally.
        FlowDocument document = renderer.Render("````\na `b` c\n````");

        Block block = Assert.Single(document.Blocks);
        Assert.Contains("a `b` c", FlowDocumentTestHelpers.BlockText(block));
    }

    private static string? BlockTag(Block block) => block switch
    {
        Paragraph p => p.Tag as string,
        Section s => s.Tag as string,
        _ => null,
    };

    private static System.Collections.Generic.IReadOnlyList<Run> CollectBlockRuns(Block block)
    {
        return block switch
        {
            Paragraph p => FlowDocumentTestHelpers.CollectRuns(p),
            Section s => s.Blocks.OfType<Paragraph>()
                .SelectMany(FlowDocumentTestHelpers.CollectRuns).ToList(),
            _ => new System.Collections.Generic.List<Run>(),
        };
    }
}
