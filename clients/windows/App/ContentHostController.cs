using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using TheMarkdownWeb.Rendering;

namespace TheMarkdownWeb.App;

/// <summary>
/// Hosts the rendered <see cref="FlowDocument"/> in the <c>ContentHost</c>'s read-only
/// <see cref="FlowDocumentScrollViewer"/> (Story 3.5 AC1/AC4/AC5/AC7/AC8). Owns:
///   • render markdown → set <c>Document</c> (AC1);
///   • the image post-process: resolve each recorded <c>Image.Tag</c> source + load via the injected
///     <see cref="IImageLoader"/> seam (AC7; broken → empty + alt preserved, never a crash);
///   • the SINGLE host-level <see cref="Hyperlink.RequestNavigateEvent"/> handler → classify
///     (<see cref="LinkClassifier"/>) → raise <c>onLinkActivated</c>, with <c>e.Handled = true</c> so
///     WPF's default shell-launch does NOT also fire (AC4/AC6);
///   • anchor scroll: locate the matching heading <see cref="Block"/> via <see cref="AnchorMatcher"/>
///     and <c>BringIntoView()</c> (AC5; missing fragment → no-op);
///   • the clear Broken state (AC8).
/// <see cref="Rendering"/> stays pure — the click behavior + image I/O are App-attached here.
/// </summary>
public sealed class ContentHostController
{
    private readonly FlowDocumentScrollViewer _host;
    private readonly FlowDocumentRenderer _renderer;
    private readonly IImageLoader _imageLoader;
    private readonly Func<LinkTarget, Task> _onLinkActivated;

    private Uri? _basePageUrl;

    public ContentHostController(
        FlowDocumentScrollViewer host,
        FlowDocumentRenderer renderer,
        IImageLoader imageLoader,
        Func<LinkTarget, Task> onLinkActivated)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
        _onLinkActivated = onLinkActivated ?? throw new ArgumentNullException(nameof(onLinkActivated));

        // ONE host-level hyperlink handler for every hosted document. The renderer records the
        // NavigateUri inertly (3.3); this is the App↔Rendering seam that makes the link navigate.
        _host.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
    }

    /// <summary>The read-only scroll host wrapped by this controller.</summary>
    public FlowDocumentScrollViewer Host => _host;

    /// <summary><c>true</c> after <see cref="ShowBroken"/> until the next <see cref="ShowMarkdown"/>.</summary>
    public bool IsBroken { get; private set; }

    /// <summary>
    /// Renders <paramref name="markdown"/> into the host and post-processes its images against
    /// <paramref name="basePageUrl"/>. Total — a render/image failure never crashes the host.
    /// </summary>
    public void ShowMarkdown(string markdown, Uri basePageUrl)
    {
        _basePageUrl = basePageUrl;
        IsBroken = false;

        FlowDocument document = _renderer.Render(markdown ?? string.Empty);
        PostProcessImages(document, basePageUrl);
        _host.Document = document;
    }

    /// <summary>
    /// Shows a clear, distinguishable "page not found / not a markdown page" document (AC8). Never an
    /// empty crash — the host always holds a valid <see cref="FlowDocument"/>.
    /// </summary>
    public void ShowBroken()
    {
        IsBroken = true;

        var paragraph = new Paragraph(new Run("This page could not be loaded."))
        {
            Tag = "broken",
            FontWeight = FontWeights.Bold,
        };
        var doc = new FlowDocument(paragraph);
        AutomationProperties.SetName(doc, "Page not found");
        _host.Document = doc;
    }

    /// <summary>
    /// Locates the heading <see cref="Block"/> whose github-style anchor slug matches
    /// <paramref name="fragment"/> (sans <c>#</c>), or <c>null</c> if none matches (AC5). Total.
    /// </summary>
    public Block? FindAnchorTarget(string fragment)
    {
        FlowDocument? doc = _host.Document;
        if (doc is null || string.IsNullOrEmpty(fragment))
        {
            return null;
        }

        string frag = fragment.StartsWith("#", StringComparison.Ordinal) ? fragment.Substring(1) : fragment;
        return FindHeadingBlock(doc.Blocks, frag);
    }

    /// <summary>
    /// Brings the heading matching <paramref name="fragment"/> into view; a missing fragment is a safe
    /// no-op (no scroll, no re-fetch, no throw) (AC5).
    /// </summary>
    public void ScrollToAnchor(string fragment)
    {
        Block? target = FindAnchorTarget(fragment);
        if (target is null)
        {
            return;
        }

        try
        {
            target.BringIntoView();
        }
        catch
        {
            // BringIntoView before a layout pass (headless runner) must never crash.
        }
    }

    // ---- internals ----------------------------------------------------------------------------

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true; // suppress WPF's default shell-launch so navigation is App-controlled.

        string? href = e.Uri?.ToString();
        LinkTarget target = LinkClassifier.Classify(href, _basePageUrl);

        // Fire-and-forget the activation (the controller is total/non-throwing); ignore the task.
        _ = _onLinkActivated(target);
    }

    private void PostProcessImages(FlowDocument document, Uri basePageUrl)
    {
        foreach (Block block in document.Blocks)
        {
            PostProcessBlock(block, basePageUrl);
        }
    }

    private void PostProcessBlock(Block block, Uri basePageUrl)
    {
        switch (block)
        {
            case Paragraph paragraph:
                foreach (Inline inline in paragraph.Inlines)
                {
                    PostProcessInline(inline, basePageUrl);
                }
                break;
            case Section section:
                foreach (Block child in section.Blocks)
                {
                    PostProcessBlock(child, basePageUrl);
                }
                break;
            case List list:
                foreach (ListItem item in list.ListItems)
                {
                    foreach (Block child in item.Blocks)
                    {
                        PostProcessBlock(child, basePageUrl);
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
                                PostProcessBlock(child, basePageUrl);
                            }
                        }
                    }
                }
                break;
        }
    }

    private void PostProcessInline(Inline inline, Uri basePageUrl)
    {
        switch (inline)
        {
            case InlineUIContainer container when container.Child is Image image:
                LoadImage(image, basePageUrl);
                break;
            case Span span:
                foreach (Inline child in span.Inlines)
                {
                    PostProcessInline(child, basePageUrl);
                }
                break;
        }
    }

    private void LoadImage(Image image, Uri basePageUrl)
    {
        // The renderer records the source string on Image.Tag (3.3); resolve + load it App-side.
        string? recordedSource = image.Tag as string;
        Uri? resolved = ImageResolver.Resolve(recordedSource, basePageUrl);
        if (resolved is null)
        {
            return; // unresolvable -> leave Image empty, alt preserved, never load.
        }

        ImageSource? loaded = _imageLoader.Load(resolved);
        if (loaded is not null)
        {
            image.Source = loaded;
        }
        // null (broken) -> leave Source unset, alt (AutomationProperties.Name) preserved.
    }

    private static Block? FindHeadingBlock(BlockCollection blocks, string fragment)
    {
        foreach (Block block in blocks)
        {
            Block? found = FindHeadingInBlock(block, fragment);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static Block? FindHeadingInBlock(Block block, string fragment)
    {
        if (block is Paragraph paragraph && IsHeading(paragraph))
        {
            string headingText = ExtractText(paragraph.Inlines);
            if (AnchorMatcher.Matches(headingText, fragment))
            {
                return paragraph;
            }
        }
        else if (block is Section section)
        {
            return FindHeadingBlock(section.Blocks, fragment);
        }

        return null;
    }

    private static bool IsHeading(Paragraph paragraph)
    {
        return paragraph.Tag is string tag && tag.Length == 2 && tag[0] == 'h' && char.IsDigit(tag[1]);
    }

    private static string ExtractText(InlineCollection inlines)
    {
        var sb = new System.Text.StringBuilder();
        AppendText(inlines, sb);
        return sb.ToString();
    }

    private static void AppendText(InlineCollection inlines, System.Text.StringBuilder sb)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    sb.Append(run.Text);
                    break;
                case Span span:
                    AppendText(span.Inlines, sb);
                    break;
            }
        }
    }
}
