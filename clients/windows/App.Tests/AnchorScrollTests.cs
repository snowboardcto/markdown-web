using System;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using TheMarkdownWeb.Rendering;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC5 ([StaFact] surface) — the content host locates the heading <c>Block</c> matching an
/// <c>#anchor</c> fragment within the hosted <c>FlowDocument</c> (github-style slug of the heading
/// text via <c>AnchorMatcher</c>) and brings it into view. A fragment with no matching heading is a
/// no-op (no scroll, no re-fetch, no throw).
///
/// These tests host a real <c>FlowDocument</c> (STA affinity) → <c>[StaFact]</c>. They assert the
/// matcher LOCATES the heading block (not real pixels / scroll offsets — the runner is headless).
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists):
///
///   namespace TheMarkdownWeb.App;
///   public sealed class ContentHostController { ... public Block? FindAnchorTarget(string fragment); ... }
///   // FindAnchorTarget returns the matching heading Block, or null for a missing fragment (total).
///   // ScrollToAnchor(fragment) calls BringIntoView() on that block (no-op when null).
/// </summary>
public class AnchorScrollTests
{
    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    private static ContentHostController HostWith(string markdown)
    {
        var scroll = new FlowDocumentScrollViewer();
        var host = new ContentHostController(
            scroll, new FlowDocumentRenderer(), new NullImageLoader(), _ => System.Threading.Tasks.Task.CompletedTask);
        host.ShowMarkdown(markdown, new Uri("https://themarkdownweb.com/x.md"));
        return host;
    }

    [StaFact] // AC5 — "#install" resolves to the "## Install" heading block.
    public void FindAnchorTarget_ResolvesMatchingHeadingBlock()
    {
        ContentHostController host = HostWith("# Doc\n\n## Install\n\nsteps");

        Block? target = host.FindAnchorTarget("install");

        Assert.NotNull(target);
        // The located block is the Install heading (renderer tags headings Tag="h2").
        Assert.Equal("h2", (target as Paragraph)?.Tag as string);
    }

    [StaFact] // AC5 — a missing fragment yields no target (host no-ops), never throws.
    public void FindAnchorTarget_MissingFragment_ReturnsNull_NoThrow()
    {
        ContentHostController host = HostWith("# Doc\n\n## Install\n\nsteps");

        Block? target = host.FindAnchorTarget("does-not-exist");

        Assert.Null(target);
    }

    [StaFact] // AC5 — ScrollToAnchor for a missing fragment is a safe no-op (no throw, no re-fetch).
    public void ScrollToAnchor_MissingFragment_IsNoOp()
    {
        ContentHostController host = HostWith("# Doc\n\n## Install");

        host.ScrollToAnchor("nope"); // must not throw

        Assert.True(true);
    }

    [StaFact] // AC5 — ScrollToAnchor for an existing heading does not throw (brings the block into view).
    public void ScrollToAnchor_ExistingFragment_DoesNotThrow()
    {
        ContentHostController host = HostWith("# Doc\n\n## Getting Started\n\ntext");

        host.ScrollToAnchor("getting-started"); // must not throw

        Assert.NotNull(host.FindAnchorTarget("getting-started"));
    }
}
