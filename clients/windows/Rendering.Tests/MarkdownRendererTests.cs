using TheMarkdownWeb.Rendering;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Probe_ReturnsKnownConstant()
    {
        var renderer = new MarkdownRenderer();

        Assert.Equal(MarkdownRenderer.ProbeValue, renderer.Probe());
    }

    [Fact]
    public void CountTopLevelBlocks_ParsesGfm_ReturnsExpectedCount()
    {
        var renderer = new MarkdownRenderer();

        // Three top-level blocks: a heading, a paragraph, and a fenced code block.
        const string markdown =
            "# Title\n\nA paragraph of text.\n\n```csharp\nvar x = 1;\n```\n";

        int blocks = renderer.CountTopLevelBlocks(markdown);

        Assert.Equal(3, blocks);
    }
}
