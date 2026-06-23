using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 6.3 AC4 — pure <c>[Fact]</c>/<c>[Theory]</c> tests for <see cref="MarkdownCandidateValidator"/>.
/// No window, no network, no socket. Feeds canned <c>(status, contentType, body, kind)</c> tuples.
/// </summary>
public class MarkdownCandidateValidatorTests
{
    // ── text/markdown: accepted ────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidMarkdown_TextMarkdown_2xx_NonEmpty_ReturnsTrueForPage()
    {
        Assert.True(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown", "# Hello", CandidateKind.PageOrAlternate));
    }

    [Fact]
    public void IsValidMarkdown_TextMarkdown_WithCharset_IsNormalized()
    {
        Assert.True(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown; charset=utf-8", "# Hello", CandidateKind.PageOrAlternate));
    }

    [Fact]
    public void IsValidMarkdown_TextMarkdownCaseInsensitive_Accepted()
    {
        Assert.True(MarkdownCandidateValidator.IsValidMarkdown(200, "TEXT/MARKDOWN", "# Hello", CandidateKind.MdSibling));
    }

    // ── Non-2xx: rejected ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(404)]
    [InlineData(403)]
    [InlineData(500)]
    public void IsValidMarkdown_NonSuccessStatus_ReturnsFalse(int status)
    {
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(status, "text/markdown", "# Hello", CandidateKind.PageOrAlternate));
    }

    // ── text/html: rejected even with 2xx ─────────────────────────────────────────────────────────

    [Fact]
    public void IsValidMarkdown_TextHtml_ReturnsFalse_ForAllKinds()
    {
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/html", "# Hello", CandidateKind.PageOrAlternate));
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/html", "# Hello", CandidateKind.MdSibling));
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/html", "# Hello", CandidateKind.LlmsText));
    }

    // ── HTML-doctype byte-sniff: rejects HTML-served-as-markdown ──────────────────────────────────

    [Theory]
    [InlineData("<!doctype html><html><head></head></html>")]
    [InlineData("<!DOCTYPE HTML PUBLIC ...")]
    [InlineData("<html lang=\"en\"><head></head><body></body></html>")]
    [InlineData("<head><title>Test</title></head>")]
    [InlineData("<?xml version=\"1.0\"?><html></html>")]
    [InlineData("<body class=\"page\">")]
    [InlineData("<script>var x=1</script>")]
    [InlineData("<meta charset=\"utf-8\">")]
    public void IsValidMarkdown_HtmlBody_WithTextMarkdown_ReturnsFalse(string htmlBody)
    {
        // Even if Content-Type says text/markdown, an HTML body is rejected (soft-404 defense).
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown", htmlBody, CandidateKind.PageOrAlternate));
    }

    [Fact]
    public void IsValidMarkdown_HtmlBody_WithLeadingWhitespace_IsSniffedCorrectly()
    {
        // Whitespace before the doctype must be skipped.
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown",
            "   \n\n   <!doctype html><html></html>", CandidateKind.MdSibling));
    }

    // ── text/plain fallback: accepted for MdSibling/LlmsText but NOT for PageOrAlternate ──────────

    [Fact]
    public void IsValidMarkdown_TextPlain_Accepted_ForMdSibling_WithMarkdownBody()
    {
        Assert.True(MarkdownCandidateValidator.IsValidMarkdown(200, "text/plain", "# A Heading\n\nSome content.", CandidateKind.MdSibling));
    }

    [Fact]
    public void IsValidMarkdown_TextPlain_Accepted_ForLlmsText_WithMarkdownBody()
    {
        Assert.True(MarkdownCandidateValidator.IsValidMarkdown(200, "text/plain", "# Site Index\n\n[Page](https://example.com/page)", CandidateKind.LlmsText));
    }

    [Fact]
    public void IsValidMarkdown_TextPlain_Rejected_ForPageOrAlternate()
    {
        // text/plain is NOT accepted for the page/alternate probe — must be text/markdown.
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/plain", "# Hello", CandidateKind.PageOrAlternate));
    }

    [Fact]
    public void IsValidMarkdown_TextPlain_HtmlBody_Rejected()
    {
        // text/plain + HTML body → rejected (the HTML sniff fires before the type check).
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/plain",
            "<!doctype html><html></html>", CandidateKind.MdSibling));
    }

    // ── Empty body: rejected ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidMarkdown_EmptyBody_ReturnsFalse()
    {
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown", string.Empty, CandidateKind.PageOrAlternate));
    }

    [Fact]
    public void IsValidMarkdown_NullBody_ReturnsFalse()
    {
        // null body treated as empty (total)
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown", null!, CandidateKind.PageOrAlternate));
    }

    // ── Oversized body: rejected ──────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidMarkdown_OversizedBody_ReturnsFalse()
    {
        string huge = new string('a', MarkdownCandidateValidator.MaxBodyChars + 1);
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown", huge, CandidateKind.PageOrAlternate));
    }

    // ── /llms.txt structure check ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidMarkdown_LlmsText_WithLeadingHeading_Accepted()
    {
        Assert.True(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown",
            "# Site Index\n\nSome content here.", CandidateKind.LlmsText));
    }

    [Fact]
    public void IsValidMarkdown_LlmsText_WithMarkdownLink_Accepted()
    {
        Assert.True(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown",
            "Site index\n\n[Docs](https://example.com/docs)", CandidateKind.LlmsText));
    }

    [Fact]
    public void IsValidMarkdown_LlmsText_WithoutHeadingOrLinks_Rejected()
    {
        // Soft-404: a page served at /llms.txt path with no markdown structure.
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, "text/markdown",
            "Just some plain text without any structure.", CandidateKind.LlmsText));
    }

    // ── Null content-type: rejected ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidMarkdown_NullContentType_ReturnsFalse()
    {
        Assert.False(MarkdownCandidateValidator.IsValidMarkdown(200, null, "# Hello", CandidateKind.PageOrAlternate));
    }

    // ── Total: never throws ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsValidMarkdown_NeverThrows_ForAnyInput()
    {
        var ex = Record.Exception(() =>
        {
            MarkdownCandidateValidator.IsValidMarkdown(-1, null, null!, CandidateKind.PageOrAlternate);
            MarkdownCandidateValidator.IsValidMarkdown(200, "x/x", string.Empty, CandidateKind.LlmsText);
        });
        Assert.Null(ex);
    }
}
