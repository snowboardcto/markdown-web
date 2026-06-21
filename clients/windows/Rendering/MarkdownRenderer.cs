using Markdig;
using Markdig.Syntax;

namespace TheMarkdownWeb.Rendering;

/// <summary>
/// Bedrock entry point for the Markdig AST -> WPF FlowDocument render path.
/// At this stage (Epic 3 skeleton) it only exposes trivial probes used to verify
/// that the .NET 10 + WPF toolchain and the Markdig reference build and run.
/// The full FlowDocument rendering lands in Story 3-3.
/// </summary>
public sealed class MarkdownRenderer
{
    /// <summary>
    /// Constant returned by <see cref="Probe"/>. Used by the skeleton unit test
    /// to confirm the Rendering assembly is referenced and executable.
    /// </summary>
    public const string ProbeValue = "TheMarkdownWeb.Rendering:ok";

    /// <summary>
    /// Trivial toolchain probe: returns a known constant.
    /// </summary>
    public string Probe() => ProbeValue;

    /// <summary>
    /// Parses GFM markdown with Markdig and returns the number of top-level
    /// block nodes in the resulting document AST. Exercises the Markdig
    /// dependency without committing to the full FlowDocument mapping yet.
    /// </summary>
    /// <param name="markdown">Raw GFM markdown.</param>
    /// <returns>Count of top-level blocks in the parsed document.</returns>
    public int CountTopLevelBlocks(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        MarkdownDocument document = Markdown.Parse(markdown, pipeline);
        return document.Count;
    }
}
