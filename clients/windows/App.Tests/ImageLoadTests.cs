using System;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Xunit;
using TheMarkdownWeb.Rendering;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC7 ([StaFact] surface) — when the content host hosts a rendered <c>FlowDocument</c>, it walks the
/// <c>Image</c> elements (each carrying the recorded source on <c>Image.Tag</c>, 3.3), resolves each
/// source to an absolute <c>Uri</c> against the page base (AC3), and loads it via the injected
/// <c>IImageLoader</c> seam — setting <c>Image.Source</c> to the loader's result. A loader that
/// returns <c>null</c> (broken/404/decode-fail) leaves <c>Image.Source</c> unset and PRESERVES the
/// recorded alt (<c>AutomationProperties.Name</c>) — never a crash. A STUB loader records the
/// requested Uri and returns a sentinel — so NO socket is opened and NO bytes are decoded.
///
/// Hosting WPF <c>Image</c> objects → STA via <c>[StaFact]</c>.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public interface IImageLoader { System.Windows.Media.ImageSource? Load(Uri absolute); }
///   // ContentHostController.ShowMarkdown(markdown, basePageUrl) post-processes Image elements via the loader.
/// </summary>
public class ImageLoadTests
{
    /// <summary>Stub loader: records each requested Uri, returns a per-Uri canned ImageSource (default sentinel).</summary>
    private sealed class StubImageLoader : IImageLoader
    {
        private readonly ImageSource? _default;
        private readonly Dictionary<Uri, ImageSource?> _canned = new();

        public StubImageLoader(ImageSource? defaultResult) => _default = defaultResult;

        public List<Uri> Requested { get; } = new();

        public StubImageLoader Returns(Uri url, ImageSource? result) { _canned[url] = result; return this; }

        public ImageSource? Load(Uri absolute)
        {
            Requested.Add(absolute);
            return _canned.TryGetValue(absolute, out ImageSource? r) ? r : _default;
        }
    }

    // A trivial, decode-free sentinel ImageSource (an empty DrawingImage — no socket, no file, no decode).
    private static ImageSource Sentinel()
    {
        var img = new DrawingImage(new DrawingGroup());
        img.Freeze();
        return img;
    }

    private static ContentHostController Host(IImageLoader loader)
    {
        var scroll = new FlowDocumentScrollViewer();
        return new ContentHostController(
            scroll, new FlowDocumentRenderer(), loader, _ => System.Threading.Tasks.Task.CompletedTask);
    }

    [StaFact] // AC7 — relative image source resolves against the page base and loads the sentinel.
    public void ImagePostProcess_ResolvesRelativeSource_AndSetsLoadedSource()
    {
        ImageSource sentinel = Sentinel();
        var expected = new Uri("https://themarkdownweb.com/guides/media/pic.png");
        var loader = new StubImageLoader(defaultResult: null).Returns(expected, sentinel);
        ContentHostController host = Host(loader);

        host.ShowMarkdown("![alt text](media/pic.png)", new Uri("https://themarkdownweb.com/guides/x.md"));

        Image img = FindFirstImage(host.Host.Document!);
        Assert.Contains(expected, loader.Requested);     // loader asked for the RESOLVED absolute Uri
        Assert.Same(sentinel, img.Source);               // Image.Source set to the loader's result
        Assert.Equal("alt text", AutomationProperties.GetName(img)); // alt preserved
    }

    [StaFact] // AC7 — a broken image (loader returns null) leaves Source unset, alt preserved, no throw.
    public void ImagePostProcess_BrokenLoad_LeavesSourceNull_PreservesAlt()
    {
        var loader = new StubImageLoader(defaultResult: null); // every load "fails" -> null
        ContentHostController host = Host(loader);

        host.ShowMarkdown("![logo](broken.png)", new Uri("https://themarkdownweb.com/guides/x.md"));

        Image img = FindFirstImage(host.Host.Document!);
        Assert.Null(img.Source);                         // empty image, never a crash
        Assert.Equal("logo", AutomationProperties.GetName(img)); // alt preserved
    }

    [StaFact] // AC7 — an unresolvable source is never loaded; Source stays null, alt preserved, no throw.
    public void ImagePostProcess_UnresolvableSource_NotLoaded_NoThrow()
    {
        var loader = new StubImageLoader(defaultResult: Sentinel());
        ContentHostController host = Host(loader);

        // An empty src is unresolvable (ImageResolver -> null); the loader must NOT be asked.
        host.ShowMarkdown("![alt]()", new Uri("https://themarkdownweb.com/guides/x.md"));

        Image img = FindFirstImage(host.Host.Document!);
        Assert.Null(img.Source);
        Assert.Empty(loader.Requested); // unresolvable -> loader never invoked
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static Image FindFirstImage(FlowDocument doc)
    {
        Image? img = FindImage(doc);
        Assert.True(img is not null, "rendered document must contain an Image element.");
        return img!;
    }

    private static Image? FindImage(FlowDocument doc)
    {
        foreach (Block block in doc.Blocks)
        {
            Image? i = FindInBlock(block);
            if (i is not null) return i;
        }
        return null;
    }

    private static Image? FindInBlock(Block block)
    {
        switch (block)
        {
            case Paragraph p:
                foreach (Inline inline in p.Inlines)
                {
                    Image? i = FindInInline(inline);
                    if (i is not null) return i;
                }
                break;
            case Section section:
                foreach (Block child in section.Blocks)
                {
                    Image? i = FindInBlock(child);
                    if (i is not null) return i;
                }
                break;
        }
        return null;
    }

    private static Image? FindInInline(Inline inline)
    {
        switch (inline)
        {
            case InlineUIContainer container:
                return container.Child as Image;
            case Span span:
                foreach (Inline child in span.Inlines)
                {
                    Image? i = FindInInline(child);
                    if (i is not null) return i;
                }
                break;
        }
        return null;
    }
}
