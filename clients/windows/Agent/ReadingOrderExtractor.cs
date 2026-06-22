using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// The PURE, deterministic reading-order extractor (Story 4.4 AC6 — the load-bearing audio AC). Turns a
/// page's RAW Markdown into the plain text content the audio persona speaks, covering the FULL body in
/// document order: headings, paragraphs, list items, table cells, blockquotes, and code blocks (the code
/// TEXT is emitted — the actual lines are spoken — not a placeholder marker).
///
/// It parses the MARKDOWN with Markdig (the SAME <c>UseAdvancedExtensions</c> pipeline the renderer uses,
/// so pipe tables / GFM blocks parse identically) and walks the resulting AST in document order. It does
/// NOT read a <see cref="System.Windows.Documents.FlowDocument"/> — it lives in <c>Agent</c> (D3: no WPF,
/// no net, no speech) and is testable with a plain <c>[Fact]</c> (no STA). Total — null/empty/whitespace
/// markdown → an empty string; malformed markdown never throws (Markdig is total over arbitrary input).
/// </summary>
public static class ReadingOrderExtractor
{
    // Same pipeline as FlowDocumentRenderer (UseAdvancedExtensions => pipe tables, task lists, etc.) so the
    // spoken reading order covers exactly the blocks the renderer shows. Built ONCE, shared, pure.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>
    /// Extracts the page's readable text in document order. Total: a null/empty/whitespace input yields
    /// <see cref="string.Empty"/>; any other input yields the joined block texts (one block per line).
    /// </summary>
    public static string Extract(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        MarkdownDocument document = Markdown.Parse(markdown, Pipeline);

        var pieces = new List<string>();
        foreach (Block block in document)
        {
            WalkBlock(block, pieces);
        }

        return string.Join("\n", pieces.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static void WalkBlock(Block block, List<string> sink)
    {
        switch (block)
        {
            case HeadingBlock heading:
                Emit(sink, ExtractInline(heading.Inline));
                break;

            case ParagraphBlock paragraph:
                Emit(sink, ExtractInline(paragraph.Inline));
                break;

            // FencedCodeBlock derives from CodeBlock — both land here; emit the code TEXT (the lines),
            // never the fence info-string / language tag.
            case CodeBlock code:
                Emit(sink, ExtractCode(code));
                break;

            // ListBlock / QuoteBlock / Table / TableRow / TableCell / ListItemBlock are all ContainerBlocks
            // — recurse so each contained leaf (paragraph/heading/code) is emitted in document order.
            case ContainerBlock container:
                foreach (Block child in container)
                {
                    WalkBlock(child, sink);
                }
                break;

            // Any other leaf that still carries inline content (e.g. an unusual block) — emit its text so
            // nothing in the body is silently dropped.
            case LeafBlock leaf when leaf.Inline is not null:
                Emit(sink, ExtractInline(leaf.Inline));
                break;
        }
    }

    private static string ExtractCode(CodeBlock code)
    {
        var lines = code.Lines.Lines;
        if (lines is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < code.Lines.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }
            sb.Append(lines[i].Slice.ToString());
        }
        return sb.ToString();
    }

    private static string ExtractInline(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        AppendInline(container, sb);
        return sb.ToString().Trim();
    }

    private static void AppendInline(Inline inline, StringBuilder sb)
    {
        switch (inline)
        {
            case LiteralInline literal:
                sb.Append(literal.Content.ToString());
                break;

            case CodeInline code:
                sb.Append(code.Content);
                break;

            case AutolinkInline autolink:
                // An autolink's visible text IS its URL — spoken as-is.
                sb.Append(autolink.Url);
                break;

            case LineBreakInline:
                sb.Append(' ');
                break;

            // EmphasisInline + LinkInline are ContainerInlines — recurse to emit their TEXT (a link's
            // visible words are spoken; its target URL, which is on LinkInline.Url, is NOT appended).
            case ContainerInline childContainer:
                foreach (Inline child in childContainer)
                {
                    AppendInline(child, sb);
                }
                break;
        }
    }

    private static void Emit(List<string> sink, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            sink.Add(text);
        }
    }
}
