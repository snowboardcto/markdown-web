using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// AC1 / AC3 / AC4 — the REAL <see cref="PersonalityEngine"/> over a FAKE <see cref="ILlmClient"/> +
/// an in-memory <see cref="ISecretStore"/>. Proves the failure-mode / Outcome totality table (the
/// load-bearing AC4): every path resolves to exactly one of {Transformed, PassThrough, NeedsKey,
/// FellBack}, NEVER throws out of PersonalizeAsync, and ALWAYS yields renderable markdown (the
/// transformed text on success, else the ORIGINAL). The pass-through + no-key short-circuits make
/// ZERO LLM calls (asserted via the counting fake). The sentinel key never leaks into any surfaced
/// string. RED until the Agent module exists.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.Agent):
///
///   public sealed class PersonalityEngine {
///       public PersonalityEngine(ILlmClient llmClient, ISecretStore secretStore);
///       public Task&lt;PersonalizationResult&gt; PersonalizeAsync(
///           string pageMarkdown, Persona persona, ReaderContext readerContext, CancellationToken ct); }
///
/// Logic (total):
///   persona.IsPassThrough            -> (pageMarkdown, PassThrough, null), NO LLM call
///   else !secretStore.HasApiKey      -> (pageMarkdown, NeedsKey, "<add-your-key notice>"), NO LLM call
///   else CompleteAsync (try/catch):
///       Success w/ non-empty text    -> (transformed, Transformed, null)
///       Failure / throw / cancel /
///         null|empty|whitespace text -> (pageMarkdown, FellBack, "<key-free notice>")
/// </summary>
public class PersonalityEngineTests
{
    private const string Original = "# Heading\n\nThe original page body.";

    // A canned non-pass-through persona (the engine must take the LLM path for it). 4.1 ships only
    // Basic as a *built-in*; constructing an ad-hoc persona here exercises the transform branch.
    private static Persona CustomPersona() =>
        new("custom", "Custom", "You are a helpful transform.", IsPassThrough: false);

    private static ReaderContext Ctx() =>
        new(PageUrl: "https://h/x.md", PreferredLanguage: "en");

    // ----- Row 1: pass-through persona -> PassThrough, ZERO LLM calls -----------------------------

    [Fact] // AC1 / table row 1 — Persona.Basic returns the ORIGINAL unchanged + PassThrough, LLM never called.
    public async Task PassThrough_ReturnsOriginal_AndNeverCallsLlm()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# SHOULD NOT BE USED"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "any-key"));

        PersonalizationResult result =
            await engine.PersonalizeAsync(Original, Persona.Basic, Ctx(), CancellationToken.None);

        Assert.Equal(Original, result.Markdown);
        Assert.Equal(PersonalizationOutcome.PassThrough, result.Outcome);
        Assert.Null(result.Notice);
        Assert.Equal(0, llm.Calls); // the WHOLE POINT: pass-through short-circuits before the provider.
    }

    // ----- Row 2: no key -> NeedsKey, ZERO LLM calls ---------------------------------------------

    [Fact] // AC3 / table row 2 — non-pass-through persona + empty store -> NeedsKey + ORIGINAL + notice, no LLM call.
    public async Task NoKey_ReturnsNeedsKey_WithOriginalAndNotice_AndNeverCallsLlm()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# SHOULD NOT BE USED"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: null)); // no key

        PersonalizationResult result =
            await engine.PersonalizeAsync(Original, CustomPersona(), Ctx(), CancellationToken.None);

        Assert.Equal(Original, result.Markdown);
        Assert.Equal(PersonalizationOutcome.NeedsKey, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Notice), "NeedsKey must surface a non-blocking 'add your key' notice.");
        Assert.Equal(0, llm.Calls); // short-circuits before the provider.
    }

    // ----- Row 12: success with usable text -> Transformed ---------------------------------------

    [Fact] // AC1 / table row 12 — success w/ non-empty text -> Transformed + the canned transformed markdown.
    public async Task Success_WithUsableText_ReturnsTransformed()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# Transformed"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));

        PersonalizationResult result =
            await engine.PersonalizeAsync(Original, CustomPersona(), Ctx(), CancellationToken.None);

        Assert.Equal("# Transformed", result.Markdown);
        Assert.Equal(PersonalizationOutcome.Transformed, result.Outcome);
        Assert.Null(result.Notice);
        Assert.Equal(1, llm.Calls);
    }

    // ----- Rows 3-10: ILlmClient returns Failure -> FellBack + ORIGINAL --------------------------

    [Fact] // AC4 / table rows 3-10 — any LlmResult.Failure -> FellBack + ORIGINAL + notice, no throw.
    public async Task LlmFailure_FoldsTo_FellBack_WithOriginal()
    {
        var llm = new CountingLlmClient(LlmResult.Failure("HTTP 500"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));

        PersonalizationResult result =
            await engine.PersonalizeAsync(Original, CustomPersona(), Ctx(), CancellationToken.None);

        Assert.Equal(Original, result.Markdown);
        Assert.Equal(PersonalizationOutcome.FellBack, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Notice), "FellBack must surface a non-blocking notice.");
    }

    // ----- Rows 10-11: success but null/empty/whitespace text -> FellBack + ORIGINAL -------------

    [Theory] // AC4 / table rows 10-11 — usable-text-absent (null/empty/whitespace) success -> FellBack + ORIGINAL.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t  \r\n")]
    public async Task SuccessWithBlankText_FoldsTo_FellBack_WithOriginal(string blank)
    {
        var llm = new CountingLlmClient(LlmResult.Success(blank));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));

        PersonalizationResult result =
            await engine.PersonalizeAsync(Original, CustomPersona(), Ctx(), CancellationToken.None);

        Assert.Equal(Original, result.Markdown); // never hand an empty doc to the renderer for a non-empty source.
        Assert.Equal(PersonalizationOutcome.FellBack, result.Outcome);
    }

    // ----- Row 13: ILlmClient THROWS despite the total contract -> caught -> FellBack ------------

    [Fact] // AC4 / table row 13 — a throwing fake is CAUGHT (defense-in-depth) -> FellBack + ORIGINAL, no throw escapes.
    public async Task ThrowingLlm_IsCaught_AndFoldsTo_FellBack()
    {
        var llm = new ThrowingLlmClient();
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));

        PersonalizationResult result =
            await engine.PersonalizeAsync(Original, CustomPersona(), Ctx(), CancellationToken.None);

        Assert.Equal(Original, result.Markdown);
        Assert.Equal(PersonalizationOutcome.FellBack, result.Outcome);
        Assert.Equal(1, llm.Calls); // it was attempted, then caught.
    }

    // ----- Row 6: cancellation honored -> total, no OperationCanceledException escapes ------------

    [Fact] // AC4 / table row 6 — a pre-cancelled token -> engine stays total, original markdown, no OCE escapes.
    public async Task PreCancelledToken_StaysTotal_WithOriginal()
    {
        var llm = new CountingLlmClient(LlmResult.Success("# SHOULD NOT MATTER"));
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Must NOT throw OperationCanceledException out of PersonalizeAsync.
        PersonalizationResult result =
            await engine.PersonalizeAsync(Original, CustomPersona(), Ctx(), cts.Token);

        Assert.Equal(Original, result.Markdown);
        Assert.True(
            result.Outcome is PersonalizationOutcome.FellBack or PersonalizationOutcome.Transformed,
            "A cancelled run must be total: original-markdown fallback (FellBack), never a thrown OCE.");
    }

    // ----- Row 14: oversized page -> total, no OOM/throw, ORIGINAL -------------------------------

    [Fact] // AC4 / table row 14 — a very large page stays total: no OOM/throw, Markdown == ORIGINAL, FellBack-or-PassThrough.
    public async Task OversizedPage_StaysTotal_WithOriginal()
    {
        string huge = new string('a', 32 * 1024 * 1024); // 32 MiB, larger than any sane MaxInputChars cap.
        var llm = new CountingLlmClient(LlmResult.Failure("input too large")); // client may refuse-transform.
        var engine = new PersonalityEngine(llm, new InMemorySecretStore(seed: "real-key"));

        PersonalizationResult result =
            await engine.PersonalizeAsync(huge, CustomPersona(), Ctx(), CancellationToken.None);

        Assert.Equal(huge, result.Markdown); // the original (oversized) markdown is returned unchanged.
        Assert.True(
            result.Outcome is PersonalizationOutcome.FellBack or PersonalizationOutcome.PassThrough,
            "An oversized page degrades to a total fallback, never an unbounded request or a throw.");
    }

    // ----- AC3 no-leak: the sentinel key never appears in any surfaced engine string --------------

    [Fact] // AC3 no-leak — across success AND every failure path, the sentinel key leaks into NO surfaced string.
    public async Task SentinelKey_NeverLeaks_IntoAnySurfacedString()
    {
        var store = new InMemorySecretStore(seed: TestKeys.SentinelKey);

        // Drive the engine through the success path and each failure path; collect every surfaced string.
        ILlmClient[] clients =
        {
            new CountingLlmClient(LlmResult.Success("# Transformed")),       // Transformed
            new CountingLlmClient(LlmResult.Success("")),                    // FellBack (blank)
            new CountingLlmClient(LlmResult.Failure("provider error 401")),  // FellBack (failure)
            new ThrowingLlmClient(),                                          // FellBack (caught throw)
        };

        foreach (ILlmClient client in clients)
        {
            var engine = new PersonalityEngine(client, store);
            PersonalizationResult result =
                await engine.PersonalizeAsync(Original, CustomPersona(), Ctx(), CancellationToken.None);

            Assert.DoesNotContain(TestKeys.SentinelKey, result.Markdown);
            Assert.DoesNotContain(TestKeys.SentinelKey, result.Notice ?? string.Empty);
        }

        // Also the NeedsKey path (store empty so the sentinel is never read, but assert the notice is clean).
        var emptyStore = new InMemorySecretStore(seed: null);
        var needsKeyEngine = new PersonalityEngine(new CountingLlmClient(LlmResult.Success("x")), emptyStore);
        PersonalizationResult needsKey =
            await needsKeyEngine.PersonalizeAsync(Original, CustomPersona(), Ctx(), CancellationToken.None);
        Assert.DoesNotContain(TestKeys.SentinelKey, needsKey.Notice ?? string.Empty);
    }
}
