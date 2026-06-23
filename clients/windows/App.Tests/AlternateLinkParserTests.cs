using System;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.3 AC1 (step 1b) — pure <c>[Fact]</c>/<c>[Theory]</c> tests for <see cref="AlternateLinkParser"/>.
/// No window, no network. Feeds canned HTML strings.
/// </summary>
public class AlternateLinkParserTests
{
    private static readonly Uri BaseUri = new("https://example.com/docs/intro");

    // ── Basic extraction ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_BasicAlternateLink_ReturnsAbsoluteUri()
    {
        string html = "<html><head>" +
                      "<link rel=\"alternate\" type=\"text/markdown\" href=\"https://example.com/docs/intro.md\">" +
                      "</head><body></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/docs/intro.md", result!.ToString());
    }

    [Fact]
    public void Parse_RelativeHref_IsResolvedAgainstBaseUri()
    {
        string html = "<html><head>" +
                      "<link rel=\"alternate\" type=\"text/markdown\" href=\"intro.md\">" +
                      "</head><body></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);

        Assert.NotNull(result);
        // Resolved: https://example.com/docs/intro.md
        Assert.True(result!.IsAbsoluteUri);
        Assert.Contains("intro.md", result.ToString());
    }

    // ── Attribute order variation ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_AttributeOrderDoesNotMatter_TypeBeforeRel()
    {
        string html = "<html><head>" +
                      "<link type=\"text/markdown\" rel=\"alternate\" href=\"/page.md\">" +
                      "</head><body></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);
        Assert.NotNull(result);
    }

    [Fact]
    public void Parse_HrefFirstAttributeOrder_Handled()
    {
        string html = "<html><head>" +
                      "<link href=\"/page.md\" rel=\"alternate\" type=\"text/markdown\">" +
                      "</head><body></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);
        Assert.NotNull(result);
    }

    // ── Quote style ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleQuotedAttributes_Extracted()
    {
        string html = "<html><head>" +
                      "<link rel='alternate' type='text/markdown' href='https://example.com/x.md'>" +
                      "</head><body></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);
        Assert.NotNull(result);
        Assert.Equal("https://example.com/x.md", result!.ToString());
    }

    // ── Wrong type: not extracted ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WrongType_ReturnsNull()
    {
        string html = "<html><head>" +
                      "<link rel=\"alternate\" type=\"text/plain\" href=\"/page.txt\">" +
                      "</head><body></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WrongRel_ReturnsNull()
    {
        string html = "<html><head>" +
                      "<link rel=\"stylesheet\" type=\"text/markdown\" href=\"/page.md\">" +
                      "</head><body></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);
        Assert.Null(result);
    }

    // ── No alternate link ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NoAlternateLink_ReturnsNull()
    {
        string html = "<html><head><title>Test</title></head><body><p>Content</p></body></html>";

        Uri? result = AlternateLinkParser.Parse(html, BaseUri);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyHtml_ReturnsNull()
    {
        Uri? result = AlternateLinkParser.Parse(string.Empty, BaseUri);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_NullHtml_ReturnsNull()
    {
        Uri? result = AlternateLinkParser.Parse(null, BaseUri);
        Assert.Null(result);
    }

    // ── Total: never throws ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NeverThrows_ForAnyInput()
    {
        var ex = Record.Exception(() =>
        {
            AlternateLinkParser.Parse(null, null);
            AlternateLinkParser.Parse("garbage <<<>>>", null);
            AlternateLinkParser.Parse("<link rel=\"alternate\" type=\"text/markdown\" href=\"\">", null);
        });
        Assert.Null(ex);
    }

    // ── Body tag boundary: link in body (after </head>) is ignored ────────────────────────────────

    [Fact]
    public void Parse_LinkInBodyNotHead_ReturnsNull()
    {
        // The alternate link is in the body, not the head — must be ignored.
        string html = "<html><head><title>Test</title></head><body>" +
                      "<link rel=\"alternate\" type=\"text/markdown\" href=\"/page.md\">" +
                      "</body></html>";

        // When there is a proper head region (scoped), the body link must not be found.
        // NOTE: if the head is empty, we may fall back to full-doc scan; this is an edge case.
        // The parser scopes to <head>...</head>; a proper head with no link returns null.
        Uri? result = AlternateLinkParser.Parse(html, BaseUri);
        Assert.Null(result);
    }
}
