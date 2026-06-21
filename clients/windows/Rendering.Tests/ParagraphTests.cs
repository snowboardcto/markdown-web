using System.Linq;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC4 — a plain paragraph maps to a top-level Paragraph (NOT heading-tagged, body font),
/// and an inline `code span` inside it maps to a Run whose FontFamily is the configured
/// monospace family, distinct from the body font.
/// </summary>
public class ParagraphTests
{
    [StaFact]
    public void Paragraph_WithInlineCode_HasMonoRunAndBodyRuns()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("Use the `dotnet` CLI.");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());

        // Not a heading.
        Assert.Null(paragraph.Tag);

        var runs = FlowDocumentTestHelpers.CollectRuns(paragraph);

        // The inline-code run is the configured monospace family, text == "dotnet".
        Run codeRun = runs.Single(r => r.Text == "dotnet");
        Assert.Equal("Consolas", codeRun.FontFamily.Source);

        // The surrounding text uses the body font (distinct from mono).
        bool hasBodyRun = runs.Any(r => r.Text.Contains("Use") && r.FontFamily.Source == "Segoe UI");
        Assert.True(hasBodyRun, "Surrounding text should render in the body font.");

        // Full text round-trips.
        Assert.Equal("Use the dotnet CLI.", FlowDocumentTestHelpers.ParagraphText(paragraph));
    }

    [StaFact]
    public void Paragraph_BareText_RoundTrips()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("just some words");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
        Assert.Null(paragraph.Tag);
        Assert.Equal("just some words", FlowDocumentTestHelpers.ParagraphText(paragraph));
    }

    [StaFact]
    public void InlineCode_UsesCustomMonospaceFamilyFromOptions()
    {
        var options = new FlowDocumentRenderOptions { MonospaceFontFamily = "Cascadia Mono" };
        var renderer = new FlowDocumentRenderer(options);

        FlowDocument document = renderer.Render("a `b` c");

        Paragraph paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
        Run codeRun = FlowDocumentTestHelpers.CollectRuns(paragraph).Single(r => r.Text == "b");
        Assert.Equal("Cascadia Mono", codeRun.FontFamily.Source);
    }
}
