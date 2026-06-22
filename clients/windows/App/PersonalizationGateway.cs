using System;
using System.Threading;
using System.Threading.Tasks;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App;

/// <summary>
/// The App-side render-time seam (Story 4.1 AC2). Sits between the fetched markdown and
/// <c>ContentHostController.ShowMarkdown</c>: builds a <see cref="ReaderContext"/> from the current page,
/// runs the fetched markdown through the reader's local <see cref="PersonalityEngine"/> with the selected
/// persona, and returns the engine's always-renderable <see cref="PersonalizationResult.Markdown"/>. The
/// gateway is TOTAL — it returns renderable markdown for every outcome (transformed on success, the
/// original on pass-through/fallback) and never throws. At 4.1 the selected persona is the constant
/// <see cref="Persona.Basic"/> (pass-through), so the rendered output is byte-identical to the Epic-3
/// render. The non-blocking <see cref="LastNotice"/> is surfaced for the UI/4.2 selector to show.
/// </summary>
public sealed class PersonalizationGateway
{
    private readonly PersonalityEngine _engine;
    private readonly Func<Persona> _selectedPersona;
    private readonly Func<string?> _preferredLanguage;

    /// <summary>
    /// Composes the render-time seam. <paramref name="preferredLanguage"/> (Story 4.4 Q-Lang-Source) is an
    /// OPTIONAL third parameter sourcing the reader's chosen target language into
    /// <see cref="ReaderContext.PreferredLanguage"/> per resolve; a null arg coalesces to a null-language
    /// source, so every existing two-arg call site compiles and behaves identically (the language stays
    /// <c>null</c>, the engine ignores it for non-Translate personas). The App composes it with
    /// <c>() =&gt; _languageSelection.Current</c>.
    /// </summary>
    public PersonalizationGateway(
        PersonalityEngine engine,
        Func<Persona> selectedPersona,
        Func<string?>? preferredLanguage = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _selectedPersona = selectedPersona ?? throw new ArgumentNullException(nameof(selectedPersona));
        _preferredLanguage = preferredLanguage ?? (() => null);
    }

    /// <summary>The last non-blocking notice the engine surfaced (NeedsKey/FellBack), or <c>null</c>.</summary>
    public string? LastNotice { get; private set; }

    /// <summary>The last outcome the engine produced.</summary>
    public PersonalizationOutcome LastOutcome { get; private set; } = PersonalizationOutcome.PassThrough;

    /// <summary>
    /// Resolves the markdown to render for <paramref name="pageUrl"/>. Total — returns the engine's
    /// always-renderable markdown; never throws.
    /// </summary>
    public async Task<string> ResolveMarkdownAsync(string fetchedMarkdown, Uri pageUrl, CancellationToken ct)
    {
        var context = new ReaderContext(PageUrl: pageUrl?.ToString(), PreferredLanguage: _preferredLanguage());

        PersonalizationResult result = await _engine
            .PersonalizeAsync(fetchedMarkdown, _selectedPersona(), context, ct)
            .ConfigureAwait(true);

        LastOutcome = result.Outcome;
        LastNotice = result.Notice;
        return result.Markdown;
    }
}
