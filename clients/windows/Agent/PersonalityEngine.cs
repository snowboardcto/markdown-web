using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// The total personalization engine (Story 4.1 AC1 / AC3 / AC4). Turns (page markdown + persona +
/// reader context) into an always-renderable <see cref="PersonalizationResult"/>. It NEVER throws out of
/// <see cref="PersonalizeAsync"/> and ALWAYS yields renderable markdown (the transformed text on success,
/// else the ORIGINAL). The reader's key never leaks into <see cref="PersonalizationResult.Markdown"/> or
/// <see cref="PersonalizationResult.Notice"/>.
///
/// Logic (the failure-mode / Outcome totality table):
///   persona.IsPassThrough           -> (pageMarkdown, PassThrough, null), NO LLM call
///   oversized page                  -> (pageMarkdown, FellBack, notice), NO LLM call
///   else !secretStore.HasApiKey     -> (pageMarkdown, NeedsKey, "add your key" notice), NO LLM call
///   else CompleteAsync (try/catch):
///       Success w/ non-blank text   -> (transformed, Transformed, null)
///       Failure / throw / cancel /
///         blank text                -> (pageMarkdown, FellBack, notice)
/// </summary>
public sealed class PersonalityEngine
{
    /// <summary>
    /// The defensive input-size cap (mirrors <c>MarkdownFetcher.MaxBodyBytes = 8 MiB</c>). An oversized
    /// page degrades to a total pass-through fallback rather than building an unbounded provider request.
    /// </summary>
    public const int MaxInputChars = 8 * 1024 * 1024;

    private const string NeedsKeyNotice =
        "Personalization is off: add your AI provider key to enable it. Showing the original page.";

    private const string FellBackNotice =
        "Personalization is unavailable right now. Showing the original page.";

    private readonly ILlmClient _llm;
    private readonly ISecretStore _secretStore;

    public PersonalityEngine(ILlmClient llmClient, ISecretStore secretStore)
    {
        _llm = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <summary>
    /// Personalizes <paramref name="pageMarkdown"/> with <paramref name="persona"/>. Total — never throws,
    /// always returns renderable markdown. Honours cancellation by folding to <c>FellBack</c> with the
    /// original markdown.
    /// </summary>
    public async Task<PersonalizationResult> PersonalizeAsync(
        string pageMarkdown, Persona persona, ReaderContext readerContext, CancellationToken ct)
    {
        string original = pageMarkdown ?? string.Empty;

        try
        {
            // Row 1: pass-through persona — original, no provider call.
            if (persona is null || persona.IsPassThrough)
            {
                return new PersonalizationResult(original, PersonalizationOutcome.PassThrough, Notice: null);
            }

            // Row 14: oversized page — degrade to a total fallback, no unbounded request.
            if (original.Length > MaxInputChars)
            {
                return new PersonalizationResult(original, PersonalizationOutcome.FellBack, FellBackNotice);
            }

            // Row 2: no key — NeedsKey, original, notice, no provider call.
            if (!_secretStore.HasApiKey)
            {
                return new PersonalizationResult(original, PersonalizationOutcome.NeedsKey, NeedsKeyNotice);
            }

            // Row 6: honour cancellation before trusting any provider output — a cancelled run folds to the
            // ORIGINAL (FellBack), never a stale/partial transform. Thrown OCE is caught below.
            ct.ThrowIfCancellationRequested();

            // Provider path. ILlmClient is contractually total; catch defensively (row 13).
            LlmResult result = await _llm
                .CompleteAsync(persona.SystemPrompt, original, readerContext, ct)
                .ConfigureAwait(false);

            // Row 12: success with usable (non-blank) text -> Transformed.
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Markdown))
            {
                return new PersonalizationResult(result.Markdown!, PersonalizationOutcome.Transformed, Notice: null);
            }

            // Rows 3-11: failure / blank text -> FellBack + ORIGINAL (never an empty doc for a non-empty source).
            return new PersonalizationResult(original, PersonalizationOutcome.FellBack, FellBackNotice);
        }
        catch
        {
            // Rows 6 / 13: cancellation or a throwing fake -> total FellBack + ORIGINAL, no exception escapes.
            return new PersonalizationResult(original, PersonalizationOutcome.FellBack, FellBackNotice);
        }
    }
}
