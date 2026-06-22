using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// Story 4.4 AC3 (routing half) + AC7 (Translate-no-language) — the engine's target-language directive
/// (Q-Lang-Plumbing). Pure <c>[Fact]</c>s over the REAL <see cref="PersonalityEngine"/> with the
/// <see cref="CapturingLlmClient"/> (records the EFFECTIVE systemPrompt) + an in-memory key. Proves:
///   • Translate + a non-blank language → the EFFECTIVE systemPrompt STARTS WITH the registry Translate
///     prompt (directive APPENDED, not substituted) AND CONTAINS the chosen language string;
///   • a NON-Translate persona + the same language → the systemPrompt is forwarded BYTE-UNCHANGED (no
///     directive — the 4.3 routing invariant, no regression);
///   • Translate + a blank/whitespace language → ZERO provider calls + the ORIGINAL markdown byte-identical
///     (the safe "choose a language" pass-through; never an under-specified request, never NeedsKey).
/// </summary>
public class TranslateDirectiveTests
{
    private static readonly ReaderContext SpanishContext = new("https://x/y.md", "Spanish");
    private const string Source = "# Title\n\nSome body paragraph.";

    private static Persona ById(string id) => PersonaRegistry.Seed.Single(p => p.Id == id);

    private static PersonalityEngine EngineWithKey(CapturingLlmClient capturing) =>
        new(capturing, new InMemorySecretStore(seed: TestKeys.SentinelKey));

    [Fact] // AC3 — Translate + a language: the EFFECTIVE prompt carries the registry prompt AND the language.
    public async Task Translate_WithLanguage_EffectivePromptStartsWithRegistryPrompt_AndContainsLanguage()
    {
        var capturing = new CapturingLlmClient();
        PersonalityEngine engine = EngineWithKey(capturing);
        Persona translate = ById("translate");

        PersonalizationResult result =
            await engine.PersonalizeAsync(Source, translate, SpanishContext, CancellationToken.None);

        Assert.Equal(1, capturing.Calls);
        Assert.NotNull(capturing.LastSystemPrompt);
        Assert.StartsWith(translate.SystemPrompt, capturing.LastSystemPrompt!, StringComparison.Ordinal);
        Assert.Contains("Spanish", capturing.LastSystemPrompt!, StringComparison.Ordinal);
        Assert.Equal(PersonalizationOutcome.Transformed, result.Outcome);
    }

    [Fact] // AC3 — the chosen language string is forwarded VERBATIM (opaque — never normalized).
    public async Task Translate_WithOpaqueLanguage_ForwardsItVerbatim()
    {
        var capturing = new CapturingLlmClient();
        PersonalityEngine engine = EngineWithKey(capturing);

        await engine.PersonalizeAsync(
            Source, ById("translate"), new ReaderContext("https://x/y.md", "日本語"), CancellationToken.None);

        Assert.Contains("日本語", capturing.LastSystemPrompt!, StringComparison.Ordinal);
    }

    [Fact] // AC3 no-regression — a NON-Translate persona + a language forwards its prompt byte-unchanged.
    public async Task NonTranslate_WithLanguage_ForwardsRegistryPrompt_Verbatim_NoDirective()
    {
        var capturing = new CapturingLlmClient();
        PersonalityEngine engine = EngineWithKey(capturing);
        Persona cozy = ById("cozy");

        await engine.PersonalizeAsync(Source, cozy, SpanishContext, CancellationToken.None);

        Assert.Equal(cozy.SystemPrompt, capturing.LastSystemPrompt); // exact — no appended directive.
        Assert.DoesNotContain("Target language", capturing.LastSystemPrompt!, StringComparison.Ordinal);
    }

    [Fact] // AC7 — Translate + a blank language → no provider call, original byte-identical ("choose a language").
    public async Task Translate_WithBlankLanguage_NoProviderCall_ReturnsOriginal()
    {
        var capturing = new CapturingLlmClient();
        PersonalityEngine engine = EngineWithKey(capturing);

        PersonalizationResult result = await engine.PersonalizeAsync(
            Source, ById("translate"), new ReaderContext("https://x/y.md", null), CancellationToken.None);

        Assert.Equal(0, capturing.Calls);
        Assert.Equal(Source, result.Markdown);
        Assert.Equal(PersonalizationOutcome.PassThrough, result.Outcome);
    }

    [Fact] // AC7 — whitespace-only language is treated as blank (same safe pass-through, zero calls).
    public async Task Translate_WithWhitespaceLanguage_NoProviderCall_ReturnsOriginal()
    {
        var capturing = new CapturingLlmClient();
        PersonalityEngine engine = EngineWithKey(capturing);

        PersonalizationResult result = await engine.PersonalizeAsync(
            Source, ById("translate"), new ReaderContext("https://x/y.md", "   "), CancellationToken.None);

        Assert.Equal(0, capturing.Calls);
        Assert.Equal(Source, result.Markdown);
    }

    [Fact] // AC7 — Translate + a language but NO key → NeedsKey (reuses the 4.1 key flow; not pass-through).
    public async Task Translate_WithLanguage_NoKey_ReturnsNeedsKey()
    {
        var capturing = new CapturingLlmClient();
        var engine = new PersonalityEngine(capturing, new InMemorySecretStore(seed: null));

        PersonalizationResult result =
            await engine.PersonalizeAsync(Source, ById("translate"), SpanishContext, CancellationToken.None);

        Assert.Equal(0, capturing.Calls);
        Assert.Equal(PersonalizationOutcome.NeedsKey, result.Outcome);
        Assert.Equal(Source, result.Markdown);
    }
}
