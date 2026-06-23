using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using TheMarkdownWeb.Rendering;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.4 AC2/AC3/AC4 — <c>[StaFact]</c> tests for the three new discovery-state methods on
/// <see cref="ContentHostController"/>:
///   <list type="bullet">
///     <item><see cref="ContentHostController.ShowNoMarkdown"/> (AC2)</item>
///     <item><see cref="ContentHostController.ShowBlocked"/>    (AC3)</item>
///     <item><see cref="ContentHostController.ShowLlmsIndex"/>  (AC4)</item>
///   </list>
/// Constructs <see cref="ContentHostController"/> directly (no MainWindow, no network, no Show).
/// WPF objects require STA thread — <c>[StaFact]</c> from Xunit.StaFact satisfies this.
/// </summary>
public class DiscoveryStateWindowTests
{
    private static readonly Uri AnyUri = new("https://example.com/page");

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────

    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    private static ContentHostController NewHost()
    {
        var scroll = new FlowDocumentScrollViewer();
        return new ContentHostController(
            scroll,
            new FlowDocumentRenderer(),
            new NullImageLoader(),
            _ => Task.CompletedTask);
    }

    // ── ShowNoMarkdown (AC2) ──────────────────────────────────────────────────────────────────────

    [StaFact]
    public void ShowNoMarkdown_SetsNonNullDocument_WithNonEmptyBlocks()
    {
        ContentHostController host = NewHost();

        host.ShowNoMarkdown(AnyUri);

        FlowDocument? doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.NotEmpty(doc!.Blocks);
    }

    [StaFact]
    public void ShowNoMarkdown_DocumentHasNonEmptyAutomationName()
    {
        ContentHostController host = NewHost();

        host.ShowNoMarkdown(AnyUri);

        FlowDocument doc = host.Host.Document!;
        string name = AutomationProperties.GetName(doc);
        Assert.False(string.IsNullOrWhiteSpace(name),
            "ShowNoMarkdown must set a non-empty AutomationProperties.Name on the document " +
            "so the state is announced to assistive technologies.");
        Assert.Equal("No markdown available", name);
    }

    [StaFact]
    public void ShowNoMarkdown_IsBroken_RemainsOrBecomesNotBroken()
    {
        // ShowNoMarkdown is a genuine state — not the same as ShowBroken.
        ContentHostController host = NewHost();
        host.ShowBroken(); // put it in broken state first
        Assert.True(host.IsBroken);

        host.ShowNoMarkdown(AnyUri);

        Assert.False(host.IsBroken, "ShowNoMarkdown must clear the IsBroken flag.");
    }

    [StaFact]
    public void ShowNoMarkdown_DistinctFromShowBroken_DifferentAutomationName()
    {
        ContentHostController host = NewHost();

        host.ShowBroken();
        string brokenName = AutomationProperties.GetName(host.Host.Document!);

        host.ShowNoMarkdown(AnyUri);
        string noMarkdownName = AutomationProperties.GetName(host.Host.Document!);

        Assert.NotEqual(brokenName, noMarkdownName);
    }

    [StaFact]
    public void ShowNoMarkdown_NullUrl_DoesNotThrow()
    {
        ContentHostController host = NewHost();
        var ex = Record.Exception(() => host.ShowNoMarkdown(null));
        Assert.Null(ex);
        Assert.NotNull(host.Host.Document);
    }

    // ── ShowBlocked (AC3) ─────────────────────────────────────────────────────────────────────────

    [StaFact]
    public void ShowBlocked_SetsNonNullDocument_WithNonEmptyBlocks()
    {
        ContentHostController host = NewHost();

        host.ShowBlocked(AnyUri);

        FlowDocument? doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.NotEmpty(doc!.Blocks);
    }

    [StaFact]
    public void ShowBlocked_DocumentHasNonEmptyAutomationName()
    {
        ContentHostController host = NewHost();

        host.ShowBlocked(AnyUri);

        FlowDocument doc = host.Host.Document!;
        string name = AutomationProperties.GetName(doc);
        Assert.False(string.IsNullOrWhiteSpace(name),
            "ShowBlocked must set a non-empty AutomationProperties.Name on the document.");
        Assert.Equal("Site blocked the request", name);
    }

    [StaFact]
    public void ShowBlocked_IsBroken_RemainsOrBecomesNotBroken()
    {
        ContentHostController host = NewHost();
        host.ShowBroken();
        Assert.True(host.IsBroken);

        host.ShowBlocked(AnyUri);

        Assert.False(host.IsBroken, "ShowBlocked must clear the IsBroken flag.");
    }

    [StaFact]
    public void ShowBlocked_DistinctFromShowNoMarkdown_DifferentAutomationName()
    {
        ContentHostController host = NewHost();

        host.ShowNoMarkdown(AnyUri);
        string noMarkdownName = AutomationProperties.GetName(host.Host.Document!);

        host.ShowBlocked(AnyUri);
        string blockedName = AutomationProperties.GetName(host.Host.Document!);

        Assert.NotEqual(noMarkdownName, blockedName);
    }

    [StaFact]
    public void ShowBlocked_DistinctFromShowBroken_DifferentAutomationName()
    {
        ContentHostController host = NewHost();

        host.ShowBroken();
        string brokenName = AutomationProperties.GetName(host.Host.Document!);

        host.ShowBlocked(AnyUri);
        string blockedName = AutomationProperties.GetName(host.Host.Document!);

        Assert.NotEqual(brokenName, blockedName);
    }

    [StaFact]
    public void ShowBlocked_NullUrl_DoesNotThrow()
    {
        ContentHostController host = NewHost();
        var ex = Record.Exception(() => host.ShowBlocked(null));
        Assert.Null(ex);
        Assert.NotNull(host.Host.Document);
    }

    // ── ShowLlmsIndex (AC4) ────────────────────────────────────────────────────────────────────────

    [StaFact]
    public void ShowLlmsIndex_SetsNonNullDocument_WithNonEmptyBlocks()
    {
        ContentHostController host = NewHost();
        var index = new DiscoveryResult.LlmsIndex(
            "# Site Index\n\n[Docs](https://example.com/docs.md)",
            new List<Uri> { new Uri("https://example.com/docs.md") },
            AnyUri);

        host.ShowLlmsIndex(index);

        FlowDocument? doc = host.Host.Document;
        Assert.NotNull(doc);
        Assert.NotEmpty(doc!.Blocks);
    }

    [StaFact]
    public void ShowLlmsIndex_DocumentHasNonEmptyAutomationName()
    {
        ContentHostController host = NewHost();
        var index = new DiscoveryResult.LlmsIndex(
            "# Site Index",
            new List<Uri>(),
            AnyUri);

        host.ShowLlmsIndex(index);

        FlowDocument doc = host.Host.Document!;
        string name = AutomationProperties.GetName(doc);
        Assert.False(string.IsNullOrWhiteSpace(name),
            "ShowLlmsIndex must set a non-empty AutomationProperties.Name on the document.");
        Assert.Equal("Site markdown index available", name);
    }

    [StaFact]
    public void ShowLlmsIndex_IsBroken_RemainsOrBecomesNotBroken()
    {
        ContentHostController host = NewHost();
        host.ShowBroken();
        Assert.True(host.IsBroken);

        var index = new DiscoveryResult.LlmsIndex("# x", new List<Uri>(), AnyUri);
        host.ShowLlmsIndex(index);

        Assert.False(host.IsBroken, "ShowLlmsIndex must clear the IsBroken flag.");
    }

    [StaFact]
    public void ShowLlmsIndex_DistinctFromAllOtherStates_DifferentAutomationName()
    {
        ContentHostController host = NewHost();

        host.ShowBroken();
        string brokenName = AutomationProperties.GetName(host.Host.Document!);

        host.ShowNoMarkdown(AnyUri);
        string noMarkdownName = AutomationProperties.GetName(host.Host.Document!);

        var index = new DiscoveryResult.LlmsIndex("# x", new List<Uri>(), AnyUri);
        host.ShowLlmsIndex(index);
        string llmsName = AutomationProperties.GetName(host.Host.Document!);

        Assert.NotEqual(brokenName, llmsName);
        Assert.NotEqual(noMarkdownName, llmsName);
    }

    [StaFact]
    public void ShowLlmsIndex_WithLinks_DocumentContainsHyperlinks()
    {
        ContentHostController host = NewHost();
        var links = new List<Uri>
        {
            new Uri("https://example.com/page1.md"),
            new Uri("https://example.com/page2.md"),
        };
        var index = new DiscoveryResult.LlmsIndex(
            "# Site Index\n\n[Page1](https://example.com/page1.md)\n[Page2](https://example.com/page2.md)",
            links,
            AnyUri);

        host.ShowLlmsIndex(index);

        FlowDocument doc = host.Host.Document!;
        // The doc must contain at least a heading block and a list block.
        Assert.True(doc.Blocks.Count >= 2,
            $"ShowLlmsIndex with 2 links must produce at least 2 blocks (heading + list); got {doc.Blocks.Count}.");
    }

    [StaFact]
    public void ShowLlmsIndex_WithMoreThan20Links_CapsAt20()
    {
        ContentHostController host = NewHost();
        var links = new List<Uri>();
        for (int i = 0; i < 25; i++)
        {
            links.Add(new Uri($"https://example.com/page{i}.md"));
        }
        var index = new DiscoveryResult.LlmsIndex("# Site Index", links, AnyUri);

        host.ShowLlmsIndex(index);

        // Count hyperlinks in the document — must not exceed 20.
        int hyperlinkCount = CountHyperlinks(host.Host.Document!);
        Assert.True(hyperlinkCount <= 20,
            $"ShowLlmsIndex must cap the displayed links at 20; found {hyperlinkCount}.");
        Assert.True(hyperlinkCount > 0, "At least one hyperlink must be rendered.");
    }

    [StaFact]
    public void ShowLlmsIndex_WithEmptyLinks_DoesNotThrow_DocumentIsValid()
    {
        ContentHostController host = NewHost();
        var index = new DiscoveryResult.LlmsIndex("# Site Index", new List<Uri>(), AnyUri);

        var ex = Record.Exception(() => host.ShowLlmsIndex(index));

        Assert.Null(ex);
        Assert.NotNull(host.Host.Document);
    }

    [StaFact]
    public void ShowLlmsIndex_NullIndex_DoesNotThrow()
    {
        ContentHostController host = NewHost();
        var ex = Record.Exception(() => host.ShowLlmsIndex(null!));
        Assert.Null(ex);
    }

    // ── All three states: never throw ────────────────────────────────────────────────────────────

    [StaFact]
    public void AllDiscoveryStates_NeverThrow_ForAnyInput()
    {
        ContentHostController host = NewHost();

        var ex = Record.Exception(() =>
        {
            host.ShowNoMarkdown(null);
            host.ShowNoMarkdown(AnyUri);
            host.ShowBlocked(null);
            host.ShowBlocked(AnyUri);
            host.ShowLlmsIndex(null!);
            host.ShowLlmsIndex(new DiscoveryResult.LlmsIndex("# x", new List<Uri>(), AnyUri));
        });

        Assert.Null(ex);
    }

    // ── Helper: count all Hyperlinks in a FlowDocument ────────────────────────────────────────────

    private static int CountHyperlinks(FlowDocument doc)
    {
        int count = 0;
        foreach (Block block in doc.Blocks)
        {
            count += CountHyperlinksInBlock(block);
        }
        return count;
    }

    private static int CountHyperlinksInBlock(Block block)
    {
        int count = 0;
        switch (block)
        {
            case Paragraph p:
                foreach (Inline inline in p.Inlines)
                    count += CountHyperlinksInInline(inline);
                break;
            case System.Windows.Documents.List list:
                foreach (ListItem item in list.ListItems)
                    foreach (Block child in item.Blocks)
                        count += CountHyperlinksInBlock(child);
                break;
            case Section section:
                foreach (Block child in section.Blocks)
                    count += CountHyperlinksInBlock(child);
                break;
        }
        return count;
    }

    private static int CountHyperlinksInInline(Inline inline)
    {
        return inline switch
        {
            Hyperlink => 1,
            Span span => CountHyperlinksInSpan(span),
            _ => 0,
        };
    }

    private static int CountHyperlinksInSpan(Span span)
    {
        int count = 0;
        foreach (Inline child in span.Inlines)
            count += CountHyperlinksInInline(child);
        return count;
    }
}
