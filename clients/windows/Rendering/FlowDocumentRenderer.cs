using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ColorCode;
using TheMarkdownWeb.Rendering.Highlighting;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using Block = System.Windows.Documents.Block;
using Inline = System.Windows.Documents.Inline;

namespace TheMarkdownWeb.Rendering;

/// <summary>
/// Bedrock render path: Markdig GFM AST → WPF <see cref="FlowDocument"/>. Pure (no networking,
/// no AI, no embedded browser): it maps the parsed AST to native FlowDocument elements and records
/// image sources without fetching them. <see cref="Render"/> is TOTAL — it never throws for any
/// non-null string; only a null argument throws <see cref="ArgumentNullException"/> (mirroring
/// <see cref="MarkdownRenderer.CountTopLevelBlocks"/>).
/// </summary>
public sealed class FlowDocumentRenderer
{
    // Baseline body font size. Every heading is strictly larger than this (AC2), so it is the
    // document FontSize the heading tests compare against.
    private const double BodyFontSize = 14.0;

    // Built ONCE (static): UseAdvancedExtensions turns on pipe tables, task lists, strikethrough
    // (EmphasisExtras), autolinks, and the rest of the GFM family.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private readonly FlowDocumentRenderOptions _options;

    public FlowDocumentRenderer()
        : this(new FlowDocumentRenderOptions())
    {
    }

    public FlowDocumentRenderer(FlowDocumentRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Renders GFM markdown into a WPF <see cref="FlowDocument"/>. Pure and total: any non-null
    /// string yields a valid non-null document and never throws; <c>null</c> throws
    /// <see cref="ArgumentNullException"/>.
    /// </summary>
    public FlowDocument Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var document = new FlowDocument
        {
            FontFamily = new FontFamily(_options.BodyFontFamily),
            FontSize = BodyFontSize,
        };

        MarkdownDocument ast = Markdown.Parse(markdown, Pipeline);

        foreach (Block? block in MapBlocks(ast))
        {
            if (block is not null)
            {
                document.Blocks.Add(block);
            }
        }

        return document;
    }

    private FontFamily MonoFamily => new(_options.MonospaceFontFamily);

    // ---- Block mapping -----------------------------------------------------------------------

    private IEnumerable<Block?> MapBlocks(IEnumerable<MarkdigBlock> blocks)
    {
        foreach (MarkdigBlock block in blocks)
        {
            yield return MapBlock(block);
        }
    }

    private Block? MapBlock(MarkdigBlock block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                return MapHeading(heading);
            case ParagraphBlock paragraph:
                return MapParagraph(paragraph);
            case FencedCodeBlock fenced:
                return MapCodeBlock(fenced, fenced.Info);
            case CodeBlock code:
                return MapCodeBlock(code, info: null);
            case ListBlock list:
                return MapList(list);
            case QuoteBlock quote:
                return MapQuote(quote);
            case MarkdigTable table:
                return MapTable(table);
            case ThematicBreakBlock:
                return MapThematicBreak();
            case HtmlBlock html:
                return MapHtmlBlock(html);
            default:
                // Unknown / container block: best-effort flatten of child blocks into a Section so
                // nothing is silently dropped and nothing throws (D9 totality).
                return MapUnknownBlock(block);
        }
    }

    private Paragraph MapHeading(HeadingBlock heading)
    {
        int level = Math.Clamp(heading.Level, 1, 6);
        var paragraph = new Paragraph
        {
            Tag = "h" + level,
            FontWeight = FontWeights.Bold,
            FontSize = HeadingFontSize(level),
        };
        AppendInlines(paragraph.Inlines, heading.Inline);
        return paragraph;
    }

    private static double HeadingFontSize(int level) => level switch
    {
        1 => 30.0,
        2 => 24.0,
        3 => 20.0,
        4 => 18.0,
        5 => 16.0,
        _ => 15.0,
    };

    private Paragraph MapParagraph(ParagraphBlock paragraph)
    {
        var result = new Paragraph();
        AppendInlines(result.Inlines, paragraph.Inline);
        return result;
    }

    private Paragraph MapCodeBlock(LeafBlock code, string? info)
    {
        // First word of the info-string is the language marker for Story 3.4.
        string? language = null;
        if (!string.IsNullOrEmpty(info))
        {
            int space = info.IndexOf(' ');
            language = space >= 0 ? info.Substring(0, space) : info;
        }

        var paragraph = new Paragraph
        {
            Tag = string.IsNullOrEmpty(language) ? null : language,
            FontFamily = MonoFamily,
        };

        IReadOnlyList<string> lines = CodeLines(code);

        // Story 3.4: when highlighting is on AND the language resolves to a ColorCode grammar,
        // tokenize the code into per-token colored mono runs. ANY failure (unknown language,
        // highlighting off, or a tokenizer throw) falls back to the 3.3 single-color mono path.
        if (_options.SyntaxHighlighting)
        {
            ILanguage? lang = ResolveLanguage(language);
            if (lang is not null)
            {
                try
                {
                    string source = string.Join("\n", lines);
                    var colorizer = new FlowDocumentCodeColorizer(_options.MonospaceFontFamily);
                    IReadOnlyList<Run> coloredRuns = colorizer.GetRuns(source, lang);

                    foreach (Run run in coloredRuns)
                    {
                        paragraph.Inlines.Add(run);
                    }

                    return paragraph;
                }
                catch
                {
                    // Totality (AC5): a tokenizer failure degrades to plain mono — never an error.
                    paragraph.Inlines.Clear();
                }
            }
        }

        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            // Single foreground (inherited / unset) on every run — NO per-token colors (3.3 fallback).
            paragraph.Inlines.Add(new Run(lines[i]) { FontFamily = MonoFamily });
        }

        return paragraph;
    }

    /// <summary>
    /// Resolves a GFM fence info-string id to a ColorCode <see cref="ILanguage"/>, or null when the
    /// id is null/empty/unknown (→ plain-mono fallback, AC4). Total and never throws: a small
    /// case-insensitive alias map covers common GFM ids ColorCode doesn't id 1:1, then
    /// <see cref="Languages.FindById"/> (case-insensitive on the canonical id, returns null for
    /// unknowns) handles the rest.
    /// </summary>
    private static ILanguage? ResolveLanguage(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string key = id.Trim().ToLowerInvariant();
        if (LanguageAliases.TryGetValue(key, out string? canonical))
        {
            key = canonical;
        }

        return Languages.FindById(key);
    }

    // Case-insensitive (lowercased keys) GFM fence id → ColorCode canonical id. Ids not listed here
    // are passed to FindById as-is (which is case-insensitive on canonical ids). Unmapped/unknown →
    // FindById returns null → plain-mono fallback (still "not an error", AC4).
    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.Ordinal)
    {
        ["cs"] = "csharp",
        ["c#"] = "csharp",
        ["js"] = "javascript",
        ["jsx"] = "javascript",
        ["ts"] = "typescript",
        ["tsx"] = "typescript",
        ["py"] = "python",
        ["py3"] = "python",
        ["c++"] = "cpp",
        ["fs"] = "fsharp",
        ["f#"] = "fsharp",
        ["vb"] = "vb.net",
        ["ps"] = "powershell",
        ["ps1"] = "powershell",
        ["md"] = "markdown",
    };

    private static IReadOnlyList<string> CodeLines(LeafBlock code)
    {
        var result = new List<string>();
        StringLineGroup group = code.Lines;
        StringLine[]? lines = group.Lines;
        if (lines is null)
        {
            return result;
        }

        for (int i = 0; i < group.Count; i++)
        {
            result.Add(lines[i].Slice.ToString());
        }

        return result;
    }

    private List MapList(ListBlock list)
    {
        var result = new List
        {
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
        };

        if (list.IsOrdered
            && int.TryParse(list.OrderedStart, out int start)
            && start > 0)
        {
            result.StartIndex = start;
        }

        foreach (MarkdigBlock child in list)
        {
            if (child is ListItemBlock itemBlock)
            {
                result.ListItems.Add(MapListItem(itemBlock));
            }
        }

        return result;
    }

    private ListItem MapListItem(ListItemBlock itemBlock)
    {
        var item = new ListItem();
        foreach (MarkdigBlock child in itemBlock)
        {
            Block? mapped = MapBlock(child);
            if (mapped is not null)
            {
                item.Blocks.Add(mapped);
            }
        }

        // A GFM task-list marker lives as a TaskList inline inside the item's first paragraph.
        // Prefix the first paragraph with a read-only CheckBox reflecting the checked state.
        TaskList? task = FindTaskList(itemBlock);
        if (task is not null)
        {
            Paragraph? firstParagraph = item.Blocks.OfType<Paragraph>().FirstOrDefault();
            if (firstParagraph is null)
            {
                firstParagraph = new Paragraph();
                item.Blocks.Add(firstParagraph);
            }

            var checkBox = new CheckBox
            {
                IsChecked = task.Checked,
                IsEnabled = false,
            };
            var container = new InlineUIContainer(checkBox);
            Inline? firstInline = firstParagraph.Inlines.FirstInline;
            if (firstInline is null)
            {
                firstParagraph.Inlines.Add(container);
            }
            else
            {
                firstParagraph.Inlines.InsertBefore(firstInline, container);
            }
        }

        return item;
    }

    private static TaskList? FindTaskList(ListItemBlock itemBlock)
    {
        foreach (MarkdigBlock child in itemBlock)
        {
            if (child is ParagraphBlock { Inline: { } inline })
            {
                foreach (MarkdigInline node in inline)
                {
                    if (node is TaskList task)
                    {
                        return task;
                    }
                }
            }
        }

        return null;
    }

    private Section MapQuote(QuoteBlock quote)
    {
        var section = new Section
        {
            BorderThickness = new Thickness(4, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDF, 0xE2, 0xE5)),
            Padding = new Thickness(12, 0, 0, 0),
        };

        foreach (MarkdigBlock child in quote)
        {
            Block? mapped = MapBlock(child);
            if (mapped is not null)
            {
                section.Blocks.Add(mapped);
            }
        }

        return section;
    }

    private WpfTable MapTable(MarkdigTable table)
    {
        var wpfTable = new WpfTable();

        int columnCount = table
            .OfType<MarkdigTableRow>()
            .Select(r => r.Count)
            .DefaultIfEmpty(0)
            .Max();

        // Column alignments where Markdig provides them.
        IReadOnlyList<TableColumnAlign?> alignments = ColumnAlignments(table, columnCount);

        for (int i = 0; i < columnCount; i++)
        {
            wpfTable.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        wpfTable.RowGroups.Add(group);

        foreach (MarkdigTableRow markdigRow in table.OfType<MarkdigTableRow>())
        {
            var wpfRow = new WpfTableRow();
            if (markdigRow.IsHeader)
            {
                wpfRow.FontWeight = FontWeights.Bold;
            }

            int columnIndex = 0;
            foreach (MarkdigTableCell markdigCell in markdigRow.OfType<MarkdigTableCell>())
            {
                var cellParagraph = new Paragraph();
                if (markdigRow.IsHeader)
                {
                    cellParagraph.FontWeight = FontWeights.Bold;
                }

                foreach (MarkdigBlock cellChild in markdigCell)
                {
                    if (cellChild is ParagraphBlock { Inline: { } cellInline })
                    {
                        AppendInlines(cellParagraph.Inlines, cellInline);
                    }
                }

                var wpfCell = new WpfTableCell(cellParagraph);
                if (markdigRow.IsHeader)
                {
                    wpfCell.FontWeight = FontWeights.Bold;
                }

                if (columnIndex < alignments.Count && alignments[columnIndex] is { } align)
                {
                    cellParagraph.TextAlignment = align switch
                    {
                        TableColumnAlign.Center => TextAlignment.Center,
                        TableColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left,
                    };
                }

                wpfRow.Cells.Add(wpfCell);
                columnIndex++;
            }

            group.Rows.Add(wpfRow);
        }

        return wpfTable;
    }

    private static IReadOnlyList<TableColumnAlign?> ColumnAlignments(MarkdigTable table, int columnCount)
    {
        var alignments = new TableColumnAlign?[columnCount];
        List<TableColumnDefinition>? defs = table.ColumnDefinitions;
        if (defs is not null)
        {
            for (int i = 0; i < defs.Count && i < columnCount; i++)
            {
                alignments[i] = defs[i].Alignment;
            }
        }

        return alignments;
    }

    private static Paragraph MapThematicBreak()
    {
        // A thin separator paragraph tagged "hr" (D1). Exact GitHub hairline is Story 3.6.
        return new Paragraph
        {
            Tag = "hr",
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xEC, 0xEF)),
        };
    }

    private Paragraph MapHtmlBlock(HtmlBlock html)
    {
        // Raw HTML rendered as LITERAL TEXT — never parsed/executed, never a webview (D2).
        var paragraph = new Paragraph();
        string text = CodeLinesJoined(html);
        paragraph.Inlines.Add(new Run(text));
        return paragraph;
    }

    private static string CodeLinesJoined(LeafBlock block)
    {
        IReadOnlyList<string> lines = CodeLines(block);
        return string.Join("\n", lines);
    }

    private Section? MapUnknownBlock(MarkdigBlock block)
    {
        if (block is not ContainerBlock container)
        {
            return null;
        }

        var section = new Section();
        foreach (MarkdigBlock child in container)
        {
            Block? mapped = MapBlock(child);
            if (mapped is not null)
            {
                section.Blocks.Add(mapped);
            }
        }

        return section.Blocks.Count > 0 ? section : null;
    }

    // ---- Inline mapping ----------------------------------------------------------------------

    private void AppendInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (MarkdigInline inline in container)
        {
            foreach (Inline mapped in MapInline(inline))
            {
                target.Add(mapped);
            }
        }
    }

    private IEnumerable<Inline> MapInline(MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                yield return new Run(literal.Content.ToString());
                break;

            case CodeInline code:
                yield return new Run(code.Content) { FontFamily = MonoFamily };
                break;

            case EmphasisInline emphasis:
                yield return MapEmphasis(emphasis);
                break;

            case LinkInline { IsImage: true } image:
                yield return BuildImageContainer(image);
                break;

            case LinkInline link:
                yield return MapLink(link);
                break;

            case AutolinkInline autolink:
                yield return MapAutolink(autolink);
                break;

            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                {
                    yield return new LineBreak();
                }
                else
                {
                    yield return new Run(" ");
                }

                break;

            case TaskList:
                // Rendered as a CheckBox prefix at the ListItem level (MapListItem); skip inline.
                break;

            case HtmlInline html:
                // Literal escaped text (D2) — never parsed, never a webview.
                yield return new Run(html.Tag);
                break;

            case ContainerInline childContainer:
                // Unknown container inline: flatten its children so nothing is dropped.
                var span = new Span();
                AppendInlines(span.Inlines, childContainer);
                yield return span;
                break;

            default:
                yield return new Run(inline.ToString() ?? string.Empty);
                break;
        }
    }

    private Inline MapEmphasis(EmphasisInline emphasis)
    {
        Span span;
        if (emphasis.DelimiterChar == '~')
        {
            // GFM strikethrough (~~). Double tilde => strikethrough; single subscript also maps here.
            span = new Span { TextDecorations = TextDecorations.Strikethrough };
        }
        else if (emphasis.DelimiterCount >= 2)
        {
            span = new Bold();
        }
        else
        {
            span = new Italic();
        }

        AppendInlines(span.Inlines, emphasis);
        return span;
    }

    private Hyperlink MapLink(LinkInline link)
    {
        var hyperlink = new Hyperlink();
        AppendInlines(hyperlink.Inlines, link);
        if (hyperlink.Inlines.Count == 0 && !string.IsNullOrEmpty(link.Url))
        {
            hyperlink.Inlines.Add(new Run(link.Url));
        }

        SetNavigateUri(hyperlink, link.Url);
        return hyperlink;
    }

    private Hyperlink MapAutolink(AutolinkInline autolink)
    {
        var hyperlink = new Hyperlink(new Run(autolink.Url));
        SetNavigateUri(hyperlink, autolink.Url);
        return hyperlink;
    }

    private InlineUIContainer BuildImageContainer(LinkInline link)
    {
        string source = link.Url ?? string.Empty;
        string alt = GetImageAlt(link);

        var image = new Image { Tag = source };
        AutomationProperties.SetName(image, alt);

        return new InlineUIContainer(image);
    }

    private static string GetImageAlt(LinkInline link)
    {
        var sb = new System.Text.StringBuilder();
        CollectLiteral(link, sb);
        return sb.ToString();

        static void CollectLiteral(ContainerInline container, System.Text.StringBuilder sb)
        {
            foreach (MarkdigInline child in container)
            {
                switch (child)
                {
                    case LiteralInline literal:
                        sb.Append(literal.Content.ToString());
                        break;
                    case ContainerInline nested:
                        CollectLiteral(nested, sb);
                        break;
                }
            }
        }
    }

    private static void SetNavigateUri(Hyperlink hyperlink, string? url)
    {
        // Inert: records the href; click navigation is Story 3.5. No click handler is wired.
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            hyperlink.NavigateUri = uri;
        }
    }
}
