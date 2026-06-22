using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// Story 4.2 AC1 / AC6 — the seed persona registry the toolbar selector enumerates. Lives in the
/// <c>Agent</c> module (D3 boundary). A pure <c>[Fact]</c> suite (plain CLR; no WPF, no STA, no socket,
/// no key):
///   • <see cref="PersonaRegistry.Seed"/> is a non-empty ordered list;
///   • <see cref="Persona.Basic"/> is FIRST and is pass-through (so first run = the faithful basic render);
///   • every non-Basic seed persona is a real <see cref="Persona"/> with a non-empty placeholder
///     <c>SystemPrompt</c> + <c>IsPassThrough == false</c> (the PROMPTS are placeholders refined in 4.3 —
///     4.2 only needs the personas to EXIST so the selector -> gateway -> engine wiring is provable);
///   • every persona has a non-empty <c>Id</c> + <c>DisplayName</c>; the <c>Id</c>s are unique;
///   • the set contains the architecture seed ids (incl. Translate — the 4.4 placeholder).
///
/// INTENDED API CONTRACT (Step-5 implementer must match; namespace TheMarkdownWeb.Agent):
///
///   public static class PersonaRegistry { public static IReadOnlyList&lt;Persona&gt; Seed { get; } }
///
/// RED until Step 5 adds <c>Agent/PersonaRegistry.cs</c>.
/// </summary>
public class PersonaRegistryTests
{
    [Fact] // AC1 — the seed list exists and is non-empty.
    public void Seed_IsNonEmpty()
    {
        IReadOnlyList<Persona> seed = PersonaRegistry.Seed;

        Assert.NotNull(seed);
        Assert.NotEmpty(seed);
    }

    [Fact] // AC1 — Basic is first and is pass-through (no regression: first run = faithful basic render).
    public void Seed_FirstIsBasic_AndIsPassThrough()
    {
        IReadOnlyList<Persona> seed = PersonaRegistry.Seed;

        Assert.Same(Persona.Basic, seed[0]);
        Assert.True(seed[0].IsPassThrough, "The first seed persona (Basic) must be pass-through.");
    }

    [Fact] // AC1 — every non-Basic seed persona is a real transform persona (placeholder prompt, IsPassThrough=false).
    public void Seed_NonBasicPersonas_AreTransformPersonas_WithNonEmptyPlaceholderPrompts()
    {
        IReadOnlyList<Persona> seed = PersonaRegistry.Seed;

        foreach (Persona persona in seed.Skip(1))
        {
            Assert.False(persona.IsPassThrough,
                $"Non-Basic seed persona '{persona.Id}' must have IsPassThrough == false so it drives the engine.");
            Assert.False(string.IsNullOrWhiteSpace(persona.SystemPrompt),
                $"Non-Basic seed persona '{persona.Id}' must carry a non-empty (placeholder) SystemPrompt.");
        }
    }

    [Fact] // AC1 — every persona has a non-empty Id + DisplayName (each item's display text is the DisplayName).
    public void Seed_EveryPersona_HasNonEmptyIdAndDisplayName()
    {
        IReadOnlyList<Persona> seed = PersonaRegistry.Seed;

        foreach (Persona persona in seed)
        {
            Assert.False(string.IsNullOrWhiteSpace(persona.Id), "Every seed persona needs a non-empty Id.");
            Assert.False(string.IsNullOrWhiteSpace(persona.DisplayName),
                "Every seed persona needs a non-empty DisplayName (the ComboBox item text).");
        }
    }

    [Fact] // AC1 — persona ids are unique (a selector must not list duplicates).
    public void Seed_Ids_AreUnique()
    {
        IReadOnlyList<Persona> seed = PersonaRegistry.Seed;

        string[] ids = seed.Select(p => p.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact] // AC1 — the set lists the architecture seed personas, including the Translate placeholder (4.4).
    public void Seed_ContainsArchitectureSeedPersonas_IncludingTranslate()
    {
        IReadOnlyList<Persona> seed = PersonaRegistry.Seed;

        // The architecture seed set (architecture-epic4-agent.md D2): Basic, Cozy Reader, Terminal,
        // TL;DR, Plain Language, Translate. Assert by id (the ids are the stable contract).
        string[] ids = seed.Select(p => p.Id).ToArray();

        Assert.Contains("basic", ids);
        Assert.Contains("translate", ids); // the 4.4 placeholder must already be LISTED at 4.2.

        // The Translate persona is a placeholder transform persona at 4.2 (the target-language UX is 4.4).
        Persona translate = seed.Single(p => p.Id == "translate");
        Assert.False(translate.IsPassThrough, "Translate must be a transform persona (IsPassThrough == false) at 4.2.");
    }
}
