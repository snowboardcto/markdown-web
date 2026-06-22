using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// Story 4.3 AC3 — the engine routes the SELECTED persona's EXACT <c>SystemPrompt</c> to the provider.
/// Pure <c>[Fact]</c>s (plain CLR; no WPF, no STA, no socket, no key, no model) over the REAL
/// <see cref="PersonalityEngine"/> + a <see cref="CapturingLlmClient"/> that records the <c>systemPrompt</c>
/// argument it received (and returns a FIXED canned success — it does NOT key its output; that is the
/// keyed-fake's job). The prompts are asserted byte-equal against <see cref="PersonaRegistry.Seed"/>
/// (looked up by id), NEVER a re-declared literal:
///   • personalizing with <c>cozy</c> → the captured prompt == registry cozy's SystemPrompt;
///   • same for <c>terminal</c>; the two captured prompts differ;
///   • the pass-through <c>Basic</c> persona makes ZERO CompleteAsync calls (the engine short-circuits).
///
/// The engine already forwards <c>persona.SystemPrompt</c> verbatim (4-1), so this proof passes on the
/// current pipeline — it pins the AC3 contract for 4.3+. (The RED tests of 4.3 are the AC2 distinctness
/// asserts, which fail until Step 5 writes the real prompts.)
/// </summary>
public class PersonaRoutingTests
{
    private static readonly ReaderContext Ctx = new(PageUrl: "https://themarkdownweb.com/x.md", PreferredLanguage: null);
    private const string Source = "# Source\n\nbody";

    private static Persona ById(string id) => PersonaRegistry.Seed.Single(p => p.Id == id);

    private static PersonalityEngine Engine(CapturingLlmClient capturing) =>
        new(capturing, new InMemorySecretStore(seed: "real-key"));

    [Fact] // AC3 — personalizing with cozy forwards cozy's EXACT registry prompt (byte-equal) to the provider.
    public async Task Personalize_WithCozy_ForwardsCozysExactRegistryPrompt()
    {
        var capturing = new CapturingLlmClient();
        Persona cozy = ById("cozy");

        await Engine(capturing).PersonalizeAsync(Source, cozy, Ctx, CancellationToken.None);

        Assert.Equal(1, capturing.Calls);
        Assert.Equal(cozy.SystemPrompt, capturing.LastSystemPrompt);
    }

    [Fact] // AC3 — personalizing with terminal forwards terminal's EXACT registry prompt (byte-equal).
    public async Task Personalize_WithTerminal_ForwardsTerminalsExactRegistryPrompt()
    {
        var capturing = new CapturingLlmClient();
        Persona terminal = ById("terminal");

        await Engine(capturing).PersonalizeAsync(Source, terminal, Ctx, CancellationToken.None);

        Assert.Equal(1, capturing.Calls);
        Assert.Equal(terminal.SystemPrompt, capturing.LastSystemPrompt);
    }

    [Fact] // AC3 — two different personas yield two DIFFERENT captured prompts at the provider boundary.
    public async Task Personalize_WithDifferentPersonas_CapturesDifferentPrompts()
    {
        var cozyCapture = new CapturingLlmClient();
        var terminalCapture = new CapturingLlmClient();

        await Engine(cozyCapture).PersonalizeAsync(Source, ById("cozy"), Ctx, CancellationToken.None);
        await Engine(terminalCapture).PersonalizeAsync(Source, ById("terminal"), Ctx, CancellationToken.None);

        Assert.NotEqual(cozyCapture.LastSystemPrompt, terminalCapture.LastSystemPrompt);
    }

    [Fact] // AC3 — the pass-through Basic persona makes ZERO provider calls (the engine short-circuits).
    public async Task Personalize_WithBasic_MakesZeroProviderCalls()
    {
        var capturing = new CapturingLlmClient();

        PersonalizationResult result =
            await Engine(capturing).PersonalizeAsync(Source, Persona.Basic, Ctx, CancellationToken.None);

        Assert.Equal(0, capturing.Calls);
        Assert.Null(capturing.LastSystemPrompt);
        Assert.Equal(PersonalizationOutcome.PassThrough, result.Outcome);
        Assert.Equal(Source, result.Markdown); // byte-identical pass-through (no regression).
    }
}
