using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.4 AC4 / AC5 / AC7 (audio) — the <see cref="AudioReadAloudController"/> route. Pure <c>[Fact]</c>s
/// over the testable route (no window): the synthesizer is invoked with the reading-order text, Stop
/// precedes every Speak (no overlap), audio is LLM-free + key-free BY CONSTRUCTION, and the totality edges
/// (empty page → no-op; throwing synthesizer → caught) never throw.
/// </summary>
public class AudioRouteTests
{
    private const string Page = "# HeadingToken\n\nParagraphToken body.\n\n- ItemToken\n";

    [Fact] // AC5 — the synthesizer is invoked with EXACTLY the reading-order extractor output.
    public async Task ReadAsync_SpeaksTheReadingOrderText()
    {
        var synth = new FakeSpeechSynthesizer();
        var route = new AudioReadAloudController(synth);

        await route.ReadAsync(Page);

        Assert.Equal(1, synth.SpeakCount);
        Assert.Equal(ReadingOrderExtractor.Extract(Page), synth.LastSpokenText);
    }

    [Fact] // AC5 — Stop precedes Speak on a non-empty read (no overlap).
    public async Task ReadAsync_StopsBeforeSpeaking()
    {
        var synth = new FakeSpeechSynthesizer();
        var route = new AudioReadAloudController(synth);

        await route.ReadAsync(Page);

        Assert.True(synth.StopCount >= 1, "Stop must be called before Speak on a non-empty read.");
        Assert.Equal(new[] { "stop", "speak" }, synth.Ops);
    }

    [Fact] // AC5 — a second consecutive read stops the first before speaking the second (no bleed).
    public async Task ReadAsync_SecondRead_StopsBeforeSecondSpeak()
    {
        var synth = new FakeSpeechSynthesizer();
        var route = new AudioReadAloudController(synth);

        await route.ReadAsync(Page);
        await route.ReadAsync(Page);

        Assert.Equal(2, synth.SpeakCount);
        Assert.Equal(new[] { "stop", "speak", "stop", "speak" }, synth.Ops);
    }

    [Theory] // AC7 — an empty/missing page is a safe no-op: no Speak, no crash.
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t")]
    public async Task ReadAsync_EmptyOrMissingPage_DoesNotSpeak(string? held)
    {
        var synth = new FakeSpeechSynthesizer();
        var route = new AudioReadAloudController(synth);

        await route.ReadAsync(held);

        Assert.Equal(0, synth.SpeakCount);
    }

    [Fact] // AC7 — a throwing synthesizer (SAPI unavailable) is caught; the route never throws.
    public async Task ReadAsync_ThrowingSynthesizer_IsCaught()
    {
        var synth = new UnavailableSpeechSynthesizer();
        var route = new AudioReadAloudController(synth);

        // Must not throw — the route is total.
        await route.ReadAsync(Page);
    }

    [Fact] // AC4 — audio is LLM-free + key-free BY CONSTRUCTION: the only ctor dependency is ISpeechSynthesizer.
    public void AudioRoute_DependsOnlyOnTheSpeechSeam_NotOnLlmOrSecretStore()
    {
        ParameterInfo[] ctorParams = typeof(AudioReadAloudController)
            .GetConstructors()
            .Single()
            .GetParameters();

        Assert.Single(ctorParams);
        Assert.Equal(typeof(ISpeechSynthesizer), ctorParams[0].ParameterType);

        // Defensively: no constructor takes an ILlmClient or ISecretStore (no provider, no key path).
        Assert.DoesNotContain(ctorParams, p => p.ParameterType == typeof(ILlmClient));
        Assert.DoesNotContain(ctorParams, p => p.ParameterType == typeof(ISecretStore));
    }
}
