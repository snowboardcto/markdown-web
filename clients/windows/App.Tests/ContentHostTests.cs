using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using TheMarkdownWeb.Rendering;
using AppMainWindow = global::TheMarkdownWeb.App.MainWindow;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC1 / AC4 / AC8 ([StaFact] surface) — the App content host renders fetched markdown into the
/// <c>ContentHost</c>'s read-only <c>FlowDocumentScrollViewer</c>, attaches ONE host-level hyperlink
/// click handler that classifies + dispatches the click through the <c>NavigationController</c>
/// (<c>e.Handled = true</c> so WPF's default shell-launch does NOT also fire), and shows a clear
/// Broken state distinct from a rendered page. The renderer stays inert/pure — the click behavior is
/// App-attached here.
///
/// Every test constructs WPF objects (FlowDocument / FlowDocumentScrollViewer / Hyperlink) → STA via
/// <c>[StaFact]</c>. No <c>Window.Show</c>, no socket, no real <c>Process.Start</c>, no pixels.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public sealed class ContentHostController
///   {
///       // Wraps the read-only FlowDocumentScrollViewer hosted in ContentHost. The link callback is
///       // raised (already classified) on a host-level Hyperlink.RequestNavigate with e.Handled=true.
///       public ContentHostController(
///           System.Windows.Controls.FlowDocumentScrollViewer host,
///           FlowDocumentRenderer renderer,
///           IImageLoader imageLoader,
///           Func&lt;LinkTarget, Task&gt; onLinkActivated);
///
///       public System.Windows.Controls.FlowDocumentScrollViewer Host { get; }
///       public void ShowMarkdown(string markdown, Uri basePageUrl); // render + image post-process + set Document
///       public void ShowBroken();                                   // clear "page not found" state
///       public bool IsBroken { get; }
///       public void ScrollToAnchor(string fragment);                // best-effort; missing -> no-op
///   }
/// </summary>
public class ContentHostTests
{
    // A no-op image loader for tests that don't exercise images (returns null -> empty Image, never throws).
    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    private sealed class RecordingDispatch
    {
        public System.Collections.Generic.List<LinkTarget> Targets { get; } = new();
        public Task Handle(LinkTarget t) { Targets.Add(t); return Task.CompletedTask; }
    }

    private static ContentHostController NewHost(Func<LinkTarget, Task>? onLink = null)
    {
        var scroll = new FlowDocumentScrollViewer();
        return new ContentHostController(
            scroll,
            new FlowDocumentRenderer(),
            new NullImageLoader(),
            onLink ?? (_ => Task.CompletedTask));
    }

    [StaFact] // AC1 — driving the display seam sets the host's Document to a non-null FlowDocument with content.
    public void ShowMarkdown_SetsHostDocument_ToRenderedFlowDocument()
    {
        ContentHostController host = NewHost();

        host.ShowMarkdown("# Title\n\nHello world.", new Uri("https://themarkdownweb.com/x.md"));

        FlowDocument? doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.NotEmpty(doc!.Blocks); // >= 1 block — content actually rendered
    }

    [StaFact] // AC1 — the constructed MainWindow hosts a named read-only scroll host inside ContentHost.
    public void MainWindow_ContentHost_HostsReadOnlyScrollViewer()
    {
        AppMainWindow window = ShellTestHelpers.CreateWindow();

        // The story pins a FlowDocumentScrollViewer named "ContentScroll" inside <Border x:Name="ContentHost"/>.
        object? scroll = window.FindName("ContentScroll");
        Assert.True(scroll is FlowDocumentScrollViewer,
            "ContentHost must host a FlowDocumentScrollViewer named 'ContentScroll' (read-only by construction).");
    }

    [StaFact] // AC4 — a hosted internal .md Hyperlink click is classified + dispatched (e.Handled=true).
    public void HyperlinkClick_OnInternalMarkdown_DispatchesClassifiedTarget()
    {
        var dispatch = new RecordingDispatch();
        ContentHostController host = NewHost(dispatch.Handle);
        var basePage = new Uri("https://themarkdownweb.com/guides/gear.md");

        // Render a doc containing an internal relative .md link (Markdig emits an inert Hyperlink w/ NavigateUri).
        host.ShowMarkdown("[Powder](./powder.md)", basePage);

        Hyperlink link = FindFirstHyperlink(host.Host.Document!);
        RaiseRequestNavigate(link);

        Assert.Single(dispatch.Targets);
        LinkTarget t = dispatch.Targets[0];
        Assert.Equal(LinkKind.InternalMarkdown, t.Kind);
        Assert.Equal("https://themarkdownweb.com/guides/powder.md", t.Url!.ToString());
    }

    [StaFact] // AC6 — a hosted external link click dispatches an External target (no in-client fetch).
    public void HyperlinkClick_OnExternalLink_DispatchesExternalTarget()
    {
        var dispatch = new RecordingDispatch();
        ContentHostController host = NewHost(dispatch.Handle);
        var basePage = new Uri("https://themarkdownweb.com/guides/gear.md");

        host.ShowMarkdown("[Example](https://example.com/x)", basePage);

        Hyperlink link = FindFirstHyperlink(host.Host.Document!);
        RaiseRequestNavigate(link);

        Assert.Single(dispatch.Targets);
        Assert.Equal(LinkKind.External, dispatch.Targets[0].Kind);
    }

    [StaFact] // AC8 — Broken state is shown and is distinguishable from a rendered page; no throw.
    public void ShowBroken_ShowsDistinguishableBrokenState()
    {
        ContentHostController host = NewHost();
        host.ShowMarkdown("# A real page", new Uri("https://themarkdownweb.com/x.md"));

        host.ShowBroken();

        Assert.True(host.IsBroken, "after ShowBroken the host must report the Broken state (not a stale page).");
        // The Broken document is still a valid, non-null FlowDocument (a clear message), never an empty crash.
        Assert.NotNull(host.Host.Document);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static Hyperlink FindFirstHyperlink(FlowDocument doc)
    {
        Hyperlink? found = FindHyperlink(doc);
        Assert.True(found is not null, "rendered document must contain a Hyperlink.");
        return found!;
    }

    private static Hyperlink? FindHyperlink(FlowDocument doc)
    {
        foreach (Block block in doc.Blocks)
        {
            Hyperlink? h = FindInBlock(block);
            if (h is not null) return h;
        }
        return null;
    }

    private static Hyperlink? FindInBlock(Block block)
    {
        if (block is Paragraph p)
        {
            foreach (Inline inline in p.Inlines)
            {
                Hyperlink? h = FindInInline(inline);
                if (h is not null) return h;
            }
        }
        else if (block is Section section)
        {
            foreach (Block child in section.Blocks)
            {
                Hyperlink? h = FindInBlock(child);
                if (h is not null) return h;
            }
        }
        return null;
    }

    private static Hyperlink? FindInInline(Inline inline)
    {
        if (inline is Hyperlink hyperlink) return hyperlink;
        if (inline is Span span)
        {
            foreach (Inline child in span.Inlines)
            {
                Hyperlink? h = FindInInline(child);
                if (h is not null) return h;
            }
        }
        return null;
    }

    /// <summary>
    /// Raises the bubbling <see cref="Hyperlink.RequestNavigateEvent"/> the App's host-level handler
    /// listens for, carrying the hyperlink's recorded <see cref="Hyperlink.NavigateUri"/> — the exact
    /// event WPF raises on a real click, without a visible window or hit-test.
    /// </summary>
    private static void RaiseRequestNavigate(Hyperlink link)
    {
        var args = new System.Windows.Navigation.RequestNavigateEventArgs(link.NavigateUri, target: null)
        {
            RoutedEvent = Hyperlink.RequestNavigateEvent,
            Source = link,
        };
        link.RaiseEvent(args);
    }
}
