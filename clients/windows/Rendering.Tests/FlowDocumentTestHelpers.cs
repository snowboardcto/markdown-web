using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// Pure tree-walking helpers for asserting on a produced <see cref="FlowDocument"/>'s LOGICAL
/// structure (Blocks / Inlines / ListItems / Table rows / Section blocks / *UIContainer.Child).
/// No Measure/Arrange, no pixels, no timing — every helper reads the logical tree only.
///
/// NOTE: these helpers construct/inspect WPF DispatcherObjects, so every test that calls them
/// must run on an STA thread via [StaFact].
/// </summary>
internal static class FlowDocumentTestHelpers
{
    /// <summary>All top-level blocks of the document, in source order.</summary>
    public static IReadOnlyList<Block> TopLevelBlocks(FlowDocument document)
        => document.Blocks.ToList();

    /// <summary>The first top-level <see cref="Paragraph"/> (or null if none).</summary>
    public static Paragraph? FirstParagraph(FlowDocument document)
        => document.Blocks.OfType<Paragraph>().FirstOrDefault();

    /// <summary>The Nth (0-based) top-level block of a given type.</summary>
    public static T? NthBlockOfType<T>(FlowDocument document, int index) where T : Block
        => document.Blocks.OfType<T>().Skip(index).FirstOrDefault();

    /// <summary>The single top-level block of a given type (first match).</summary>
    public static T? FirstBlockOfType<T>(FlowDocument document) where T : Block
        => document.Blocks.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Recursively flattens every <see cref="Inline"/> reachable from a paragraph's inlines,
    /// descending into Span/Bold/Italic/Hyperlink containers. Leaf inlines (Run, LineBreak,
    /// InlineUIContainer) are included; container inlines are included AND descended.
    /// </summary>
    public static IReadOnlyList<Inline> FlattenInlines(Paragraph paragraph)
    {
        var acc = new List<Inline>();
        Walk(paragraph.Inlines, acc);
        return acc;

        static void Walk(InlineCollection inlines, List<Inline> acc)
        {
            foreach (Inline inline in inlines)
            {
                acc.Add(inline);
                if (inline is Span span)
                {
                    Walk(span.Inlines, acc);
                }
            }
        }
    }

    /// <summary>Every <see cref="Run"/> reachable from a paragraph, in document order.</summary>
    public static IReadOnlyList<Run> CollectRuns(Paragraph paragraph)
        => FlattenInlines(paragraph).OfType<Run>().ToList();

    /// <summary>The concatenated visible text of a paragraph's runs.</summary>
    public static string ParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();
        foreach (Inline inline in FlattenInlines(paragraph))
        {
            switch (inline)
            {
                case Run run:
                    sb.Append(run.Text);
                    break;
                case LineBreak:
                    sb.Append('\n');
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// The concatenated visible text of a single inline, descending into Span containers.
    /// </summary>
    public static string InlineText(Inline inline)
    {
        var sb = new StringBuilder();
        Append(inline, sb);
        return sb.ToString();

        static void Append(Inline inline, StringBuilder sb)
        {
            switch (inline)
            {
                case Run run:
                    sb.Append(run.Text);
                    break;
                case LineBreak:
                    sb.Append('\n');
                    break;
                case Span span:
                    foreach (Inline child in span.Inlines)
                    {
                        Append(child, sb);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Concatenated visible text of an arbitrary block (paragraph/section/list/etc.),
    /// recursing through nested blocks. Used to round-trip cell/quote/code text.
    /// </summary>
    public static string BlockText(Block block)
    {
        var sb = new StringBuilder();
        AppendBlock(block, sb);
        return sb.ToString();

        static void AppendBlock(Block block, StringBuilder sb)
        {
            switch (block)
            {
                case Paragraph p:
                    sb.Append(ParagraphText(p));
                    break;
                case Section s:
                    foreach (Block child in s.Blocks)
                    {
                        AppendBlock(child, sb);
                    }

                    break;
                case List list:
                    foreach (ListItem item in list.ListItems)
                    {
                        foreach (Block child in item.Blocks)
                        {
                            AppendBlock(child, sb);
                        }
                    }

                    break;
                case Table table:
                    foreach (TableRowGroup group in table.RowGroups)
                    {
                        foreach (TableRow row in group.Rows)
                        {
                            foreach (TableCell cell in row.Cells)
                            {
                                foreach (Block child in cell.Blocks)
                                {
                                    AppendBlock(child, sb);
                                }
                            }
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>All rows across every row group of a table, in order.</summary>
    public static IReadOnlyList<TableRow> AllRows(Table table)
        => table.RowGroups.SelectMany(g => g.Rows).ToList();

    /// <summary>
    /// Collects every distinct non-null <see cref="Brush"/> used as a Foreground across all runs
    /// in a block. Used to prove fenced code uses a SINGLE foreground (no syntax-color runs).
    /// A run that does not set Foreground (inherits) contributes null, which is ignored here.
    /// </summary>
    public static IReadOnlyList<Brush> DistinctForegrounds(Block block)
    {
        var paragraphs = new List<Paragraph>();
        CollectParagraphs(block, paragraphs);

        return paragraphs
            .SelectMany(CollectRuns)
            .Select(r => r.Foreground)
            .Where(b => b is not null)
            .Distinct()
            .ToList()!;

        static void CollectParagraphs(Block block, List<Paragraph> acc)
        {
            switch (block)
            {
                case Paragraph p:
                    acc.Add(p);
                    break;
                case Section s:
                    foreach (Block child in s.Blocks)
                    {
                        CollectParagraphs(child, acc);
                    }

                    break;
            }
        }
    }

    /// <summary>The first <see cref="CheckBox"/> hosted in any inline of a list item, or null.</summary>
    public static CheckBox? FindCheckBox(ListItem item)
    {
        foreach (Block block in item.Blocks)
        {
            if (block is not Paragraph paragraph)
            {
                continue;
            }

            foreach (Inline inline in FlattenInlines(paragraph))
            {
                if (inline is InlineUIContainer container && container.Child is CheckBox checkBox)
                {
                    return checkBox;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// True if the list item carries a checked marker — either a read-only CheckBox with
    /// IsChecked==true, or a checked glyph (☑/✅) somewhere in its text. Accepts EITHER
    /// representation (the per-element marker contract allows both).
    /// </summary>
    public static bool ItemHasCheckedMarker(ListItem item)
    {
        CheckBox? cb = FindCheckBox(item);
        if (cb is not null)
        {
            return cb.IsChecked == true;
        }

        return ItemText(item).Contains('☑') // ☑
            || ItemText(item).Contains('✅'); // ✅
    }

    /// <summary>
    /// True if the list item carries an unchecked marker — either a read-only CheckBox with
    /// IsChecked==false, or an unchecked glyph (☐) in its text.
    /// </summary>
    public static bool ItemHasUncheckedMarker(ListItem item)
    {
        CheckBox? cb = FindCheckBox(item);
        if (cb is not null)
        {
            return cb.IsChecked == false;
        }

        return ItemText(item).Contains('☐'); // ☐
    }

    /// <summary>Concatenated text of a list item's direct paragraph blocks.</summary>
    public static string ItemText(ListItem item)
    {
        var sb = new StringBuilder();
        foreach (Block block in item.Blocks)
        {
            sb.Append(BlockText(block));
        }

        return sb.ToString();
    }

    /// <summary>The first <see cref="Image"/> reachable anywhere in the document, or null.</summary>
    public static Image? FindFirstImage(FlowDocument document)
    {
        foreach (Block block in document.Blocks)
        {
            Image? image = FindImageInBlock(block);
            if (image is not null)
            {
                return image;
            }
        }

        return null;

        static Image? FindImageInBlock(Block block)
        {
            switch (block)
            {
                case BlockUIContainer buc when buc.Child is Image blockImage:
                    return blockImage;
                case Paragraph paragraph:
                    foreach (Inline inline in FlattenInlines(paragraph))
                    {
                        if (inline is InlineUIContainer container && container.Child is Image inlineImage)
                        {
                            return inlineImage;
                        }
                    }

                    return null;
                case Section section:
                    foreach (Block child in section.Blocks)
                    {
                        Image? found = FindImageInBlock(child);
                        if (found is not null)
                        {
                            return found;
                        }
                    }

                    return null;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Reads the source URI a renderer recorded on an Image, accepting EITHER representation
    /// the marker contract allows: Image.Tag holding the URI string, and/or a BitmapImage
    /// Source whose UriSource is set. Returns null if neither is present.
    /// </summary>
    public static string? RecordedImageSource(Image image)
    {
        if (image.Tag is string tag && tag.Length > 0)
        {
            return tag;
        }

        if (image.Source is System.Windows.Media.Imaging.BitmapImage bitmap && bitmap.UriSource is not null)
        {
            return bitmap.UriSource.ToString();
        }

        return null;
    }

    /// <summary>The accessible (alt) name recorded on an element via AutomationProperties.</summary>
    public static string AutomationName(DependencyObject element)
        => AutomationProperties.GetName(element);

    /// <summary>The first <see cref="Hyperlink"/> reachable from a paragraph, or null.</summary>
    public static Hyperlink? FirstHyperlink(Paragraph paragraph)
        => FlattenInlines(paragraph).OfType<Hyperlink>().FirstOrDefault();
}
