using System;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.App;

/// <summary>
/// The real <see cref="ISpeechSynthesizer"/> over Windows SAPI TTS (Story 4.4 AC5 / D5 — offline, no API
/// key, no cost). Wraps <see cref="SpeechSynthesizer"/>: <see cref="SpeakAsync"/> queues the text on SAPI's
/// own async voice (returns immediately — it does not block the UI thread waiting for speech to finish);
/// <see cref="Stop"/> cancels any queued/playing speech. TOTAL — a SAPI exception (no voice installed, an
/// unavailable device) is swallowed so the read-aloud path never crashes the app; the
/// <see cref="ISpeechSynthesizer"/> seam is the place CI fakes (no real audio device in CI). Lives ONLY in
/// <c>App</c> (Q-Speech-Placement) — <c>Agent</c> and <c>Rendering</c> never reference <c>System.Speech</c>.
/// </summary>
public sealed class SapiSpeechSynthesizer : ISpeechSynthesizer, IDisposable
{
    private readonly SpeechSynthesizer _synth = new();

    /// <inheritdoc />
    public Task SpeakAsync(string text, CancellationToken ct = default)
    {
        try
        {
            // SpeakAsync queues on SAPI's background voice and returns immediately — the UI thread is not
            // blocked for the duration of the utterance.
            _synth.SpeakAsync(text ?? string.Empty);
        }
        catch
        {
            // SAPI unavailable / no installed voice — graceful no-op (the route is total).
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Stop()
    {
        try
        {
            _synth.SpeakAsyncCancelAll();
        }
        catch
        {
            // Cancelling when nothing is queued (or SAPI is unavailable) must never throw.
        }
    }

    public void Dispose()
    {
        try
        {
            _synth.Dispose();
        }
        catch
        {
            // Disposing an already-faulted SAPI handle must never throw.
        }
    }
}
