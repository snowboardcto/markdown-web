using System;
using System.Linq;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC1 — the Render API + the Markdig GFM pipeline + the FlowDocument root contract.
/// Render is pure + TOTAL: ""/whitespace/malformed -> non-null no-throw; null -> ArgumentNullException.
/// All tests are [StaFact] because they construct/walk a FlowDocument (a DispatcherObject, STA-affine).
/// </summary>
public class FlowDocumentRendererTests
{
    [StaFact]
    public void Render_EmptyString_ReturnsNonNullEmptyDocument()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render(string.Empty);

        Assert.NotNull(document);
        Assert.Empty(document.Blocks);
    }

    [StaFact]
    public void Render_WhitespaceOnly_ReturnsNonNullDocumentWithNoSignificantBlocks()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("   \n\n");

        Assert.NotNull(document);
        // Whitespace-only input produces no significant blocks (Markdig emits no blocks for it).
        Assert.Empty(document.Blocks);
    }

    [StaFact]
    public void Render_Null_ThrowsArgumentNullException()
    {
        var renderer = new FlowDocumentRenderer();

        // Mirrors the existing MarkdownRenderer.CountTopLevelBlocks null-guard.
        Assert.Throws<ArgumentNullException>(() => renderer.Render(null!));
    }

    [StaFact]
    public void Render_MultiBlockDocument_PreservesTopLevelBlockCountAndOrder()
    {
        var renderer = new FlowDocumentRenderer();

        // Three top-level GFM blocks in source order: a heading, a paragraph, a fenced code block.
        const string markdown = "# H\n\npara\n\n```\nx\n```";

        FlowDocument document = renderer.Render(markdown);

        Assert.Equal(3, document.Blocks.Count);
        // First block is the heading paragraph, second the body paragraph (source order preserved).
        Block first = document.Blocks.ElementAt(0);
        Block second = document.Blocks.ElementAt(1);
        Assert.IsType<Paragraph>(first);
        Assert.Equal("h1", (first as Paragraph)?.Tag as string);
        Assert.IsType<Paragraph>(second);
        Assert.Null((second as Paragraph)?.Tag);
    }

    [StaFact]
    public void Render_WithCustomOptions_DoesNotThrowAndProducesDocument()
    {
        var options = new FlowDocumentRenderOptions
        {
            MonospaceFontFamily = "Cascadia Mono",
            BodyFontFamily = "Calibri",
        };
        var renderer = new FlowDocumentRenderer(options);

        FlowDocument document = renderer.Render("hello");

        Assert.NotNull(document);
        Assert.Single(document.Blocks);
    }

    [StaFact]
    public void RenderOptions_DefaultsAreConsolasAndSegoeUi()
    {
        // The options defaults are the contract Story 3.4/3.6 build on; assert them directly.
        var options = new FlowDocumentRenderOptions();

        Assert.Equal("Consolas", options.MonospaceFontFamily);
        Assert.Equal("Segoe UI", options.BodyFontFamily);
    }
}
