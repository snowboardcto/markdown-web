using System.Threading;
using System.Threading.Tasks;

namespace TheMarkdownWeb.App;

/// <summary>
/// The audio output seam (Story 4.4 AC5 / D4 / D5). The audio persona's read-aloud path speaks through
/// this abstraction so CI can verify it with a fake (records the spoken text; never opens an audio
/// device), while at runtime the real <see cref="SapiSpeechSynthesizer"/> drives Windows SAPI TTS
/// (offline, no API key). The audio route calls <see cref="Stop"/> immediately before every
/// <see cref="SpeakAsync"/> so a new utterance never overlaps a still-playing one (Q-Audio-Trigger).
/// </summary>
public interface ISpeechSynthesizer
{
    /// <summary>Speaks <paramref name="text"/>. Total at the call site — the audio route wraps it so a
    /// synthesizer failure (SAPI unavailable) never crashes the UI.</summary>
    Task SpeakAsync(string text, CancellationToken ct = default);

    /// <summary>Stops any in-progress / queued speech. Called before each speak (no overlap) and on
    /// navigation away (no bleed across pages). Total — never throws.</summary>
    void Stop();
}
