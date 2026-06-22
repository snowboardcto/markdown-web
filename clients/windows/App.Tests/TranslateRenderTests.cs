using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using TheMarkdownWeb.Agent;
using TheMarkdownWeb.Rendering;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.4 AC3 (render half) — a valid, language-tagged translated markdown still renders with its
/// STRUCTURE preserved (headings stay headings, links keep their target URL). The deterministic renderer
/// guarantees this for any valid markdown — so CI proves the PIPELINE (translated valid markdown → the pure
/// renderer → a FlowDocument with a heading + a hyperlink whose NavigateUri survived). Real translation
/// QUALITY (is the Spanish correct?) is runtime-only and NOT asserted. <c>[StaFact]</c> — constructs WPF
/// objects (FlowDocument / FlowDocumentScrollViewer); never <c>.Show()</c>'d.
/// </summary>
public class TranslateRenderTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");

    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    private static Persona Translate => System.Linq.Enumerable.Single(
        PersonaRegistry.Seed, p => p.Id == "translate");

    [StaFact] // AC3 — translated valid markdown renders with a heading block + a hyperlink (URL preserved).
    public async Task TranslatedMarkdown_RendersWithHeadingAndLink_StructurePreserved()
    {
        // A lang-aware fake returns VALID translated-looking markdown that keeps a heading + a [text](url) link.
        const string translated = "# Título\n\n[enlace](https://x/y)";
        var llm = new LangAwareLlmClient(translated);
        var engine = new PersonalityEngine(llm, new MemorySecretStore(seed: "real-key"));
        var language = new LanguageSelectionViewModel();
        var gateway = new PersonalizationGateway(engine, () => Translate, () => language.Current);

        language.Select("Spanish");
        string resolved = await gateway.ResolveMarkdownAsync("# Title\n\n[link](https://x/y)", PageUrl, CancellationToken.None);

        // Render the resolved (translated) markdown through the REAL host over the REAL pure renderer.
        var scroll = new FlowDocumentScrollViewer();
        var host = new ContentHostController(scroll, new FlowDocumentRenderer(), new NullImageLoader(), _ => Task.CompletedTask);
        host.ShowMarkdown(resolved, PageUrl);

        FlowDocument doc = host.Host.Document!;
        Assert.True(HasHeading(doc), "the translated render must keep a heading block (structure preserved).");

        Hyperlink? link = FindHyperlink(doc);
        Assert.True(link is not null, "the translated render must keep the hyperlink.");
        Assert.Equal("https://x/y", link!.NavigateUri!.ToString()); // the link URL survived the pipeline.
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static bool HasHeading(FlowDocument doc)
    {
        foreach (Block block in doc.Blocks)
        {
            if (block is Paragraph p && p.Tag is string tag && tag.Length == 2 && tag[0] == 'h' && char.IsDigit(tag[1]))
            {
                return true;
            }
        }
        return false;
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
}
