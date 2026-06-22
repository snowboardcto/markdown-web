using System;
using System.Threading;
using System.Threading.Tasks;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App;

/// <summary>
/// The audio read-aloud route (Story 4.4 AC4 / AC5 / AC7 — Q-Audio-Trigger), factored OUT of the window so
/// it is testable without a WPF window (a pure-CLR helper over the injected <see cref="ISpeechSynthesizer"/>
/// seam). When the reader selects the Audio persona, the App calls <see cref="ReadAsync"/> with the page's
/// HELD RAW markdown — NOT a gateway/coordinator re-render: this controller touches ONLY the synthesizer
/// (zero render-sink / gateway / LLM calls), so the visible page is left exactly as-is and speech plays
/// over it. Audio is LLM-free and KEY-free (D5).
///
/// Order (PINNED): extract reading-order text → empty-guard (return before any synth call) →
/// <see cref="ISpeechSynthesizer.Stop"/> (Stop-before-speak: no overlap) → <see cref="ISpeechSynthesizer.SpeakAsync"/>,
/// all inside ONE try/catch so a throwing synthesizer (SAPI unavailable) is swallowed and never reaches
/// the UI. TOTAL — an empty/missing page is a safe no-op; a throwing synthesizer never escapes.
/// </summary>
public sealed class AudioReadAloudController
{
    private readonly ISpeechSynthesizer _synth;

    public AudioReadAloudController(ISpeechSynthesizer synthesizer)
        => _synth = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));

    /// <summary>
    /// Speaks the full body of <paramref name="heldRawMarkdown"/> in reading order. No-op (no Stop, no
    /// Speak) when there is no held page or the extracted text is empty/whitespace. Never throws.
    /// </summary>
    public async Task ReadAsync(string? heldRawMarkdown, CancellationToken ct = default)
    {
        string text = ReadingOrderExtractor.Extract(heldRawMarkdown ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
        {
            return; // empty/missing page -> safe no-op (no Stop, no Speak).
        }

        try
        {
            _synth.Stop();                                  // Stop-before-speak: no overlap (Q-Audio-Trigger).
            await _synth.SpeakAsync(text, ct).ConfigureAwait(true);
        }
        catch
        {
            // SAPI unavailable / a throwing synthesizer -> graceful, total. Never reaches the UI.
        }
    }

    /// <summary>Stops any in-progress speech (called on navigation away so audio does not bleed across pages).</summary>
    public void Stop()
    {
        try
        {
            _synth.Stop();
        }
        catch
        {
            // Total — stopping must never throw into the UI.
        }
    }
}
