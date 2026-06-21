using System;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// AC1 — the pinned Agent module surface contracts (no WPF type → pure <c>[Fact]</c>): the
/// <see cref="LlmResult"/> / <see cref="PersonalizationResult"/> factory + default contracts, the
/// <see cref="Persona.Basic"/> constants, and the <see cref="PersonalizationOutcome"/> enum closure.
/// These assert the REAL types' shape so Step-5 implements them verbatim. RED until the Agent module exists.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.Agent):
///
///   public readonly record struct LlmResult {
///       bool IsSuccess; string? Markdown; string? FailureReason;
///       static LlmResult Success(string markdown); static LlmResult Failure(string reason); }
///   public sealed record Persona(string Id, string DisplayName, string SystemPrompt, bool IsPassThrough) {
///       static readonly Persona Basic; }
///   public readonly record struct ReaderContext(string? PageUrl, string? PreferredLanguage);
///   public enum PersonalizationOutcome { Transformed, PassThrough, NeedsKey, FellBack }
///   public readonly record struct PersonalizationResult(string Markdown, PersonalizationOutcome Outcome, string? Notice);
/// </summary>
public class AgentSurfaceTests
{
    [Fact] // AC1 — LlmResult.Success carries the markdown, IsSuccess true, no failure reason.
    public void LlmResult_Success_CarriesMarkdown_AndIsSuccess()
    {
        LlmResult result = LlmResult.Success("# Transformed");

        Assert.True(result.IsSuccess, "Success() must yield IsSuccess == true.");
        Assert.Equal("# Transformed", result.Markdown);
        Assert.True(string.IsNullOrEmpty(result.FailureReason), "Success() must carry no failure reason.");
    }

    [Fact] // AC1 — LlmResult.Failure carries the reason, IsSuccess false, no markdown.
    public void LlmResult_Failure_CarriesReason_AndIsNotSuccess()
    {
        LlmResult result = LlmResult.Failure("boom");

        Assert.False(result.IsSuccess, "Failure() must yield IsSuccess == false.");
        Assert.Equal("boom", result.FailureReason);
        Assert.True(string.IsNullOrEmpty(result.Markdown), "Failure() must carry no markdown.");
    }

    [Fact] // AC1 — Persona.Basic is the single seed persona: id "basic", pass-through, empty/identity prompt.
    public void PersonaBasic_HasPinnedConstants()
    {
        Persona basic = Persona.Basic;

        Assert.Equal("basic", basic.Id);
        Assert.Equal("Basic", basic.DisplayName);
        Assert.True(basic.IsPassThrough, "Basic must be the pass-through (no-transform) persona.");
        Assert.True(string.IsNullOrEmpty(basic.SystemPrompt), "Basic carries an empty/identity system prompt.");
    }

    [Fact] // AC1 — ReaderContext is a minimal-but-real record carrying page identity + preferred language.
    public void ReaderContext_CarriesPageUrl_AndPreferredLanguage()
    {
        var ctx = new ReaderContext(PageUrl: "https://h/x.md", PreferredLanguage: "en");

        Assert.Equal("https://h/x.md", ctx.PageUrl);
        Assert.Equal("en", ctx.PreferredLanguage);
    }

    [Fact] // AC1 — PersonalizationResult carries an always-renderable Markdown + outcome + optional notice.
    public void PersonalizationResult_CarriesMarkdownOutcomeNotice()
    {
        var result = new PersonalizationResult("# md", PersonalizationOutcome.Transformed, Notice: null);

        Assert.Equal("# md", result.Markdown);
        Assert.Equal(PersonalizationOutcome.Transformed, result.Outcome);
        Assert.Null(result.Notice);
    }

    [Fact] // AC4 — the outcome enum is the pinned total, closed set {Transformed, PassThrough, NeedsKey, FellBack}.
    public void PersonalizationOutcome_IsTheClosedTotalSet()
    {
        // Reference every member so the test fails to COMPILE if a name changes or a member is dropped,
        // and assert the enum has EXACTLY these four members (the totality closure AC4 depends on).
        var named = new[]
        {
            PersonalizationOutcome.Transformed,
            PersonalizationOutcome.PassThrough,
            PersonalizationOutcome.NeedsKey,
            PersonalizationOutcome.FellBack,
        };

        Assert.Equal(4, Enum.GetValues(typeof(PersonalizationOutcome)).Length);
        Assert.Equal(4, named.Length);
    }
}
