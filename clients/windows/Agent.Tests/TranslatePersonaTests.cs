using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// Story 4.4 AC1 — the Translate persona gets a REAL structure-preserving prompt (replacing the 4.3
/// placeholder) and the Audio persona is APPENDED, with the rest of the registry unchanged. Pure
/// <c>[Fact]</c>s over the REAL <see cref="PersonaRegistry.Seed"/> (looked up by id — never re-declared
/// literals). No STA, no socket, no key.
/// </summary>
public class TranslatePersonaTests
{
    private static Persona ById(string id) => PersonaRegistry.Seed.Single(p => p.Id == id);

    private static readonly string[] StructuralIds = { "cozy", "terminal", "tldr", "plain" };

    [Fact] // AC1 — the translate prompt is real, non-empty, and still a transform persona.
    public void Translate_HasRealNonEmptyPrompt_AndIsTransformPersona()
    {
        Persona translate = ById("translate");

        Assert.False(string.IsNullOrWhiteSpace(translate.SystemPrompt),
            "The Translate persona must carry a real, non-empty SystemPrompt.");
        Assert.False(translate.IsPassThrough, "Translate must stay a transform persona (IsPassThrough == false).");
    }

    [Fact] // AC1 — no 4.3 placeholder residue survives into the real prompt.
    public void Translate_Prompt_HasNoPlaceholderResidue()
    {
        string prompt = ById("translate").SystemPrompt;

        Assert.DoesNotContain("target-language ux in story 4.4", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("placeholder", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // AC1 — the prompt encodes the structure-preserving translation intent (CI keyword contract).
    public void Translate_Prompt_ContainsStructurePreservingTranslationKeywords()
    {
        string prompt = ById("translate").SystemPrompt;

        Assert.Contains("markdown", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("translat", prompt, StringComparison.OrdinalIgnoreCase); // translate / translation
        Assert.Contains("structure", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("heading", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("link", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // AC1 — the registry prompt is language-AGNOSTIC (the specific language is engine-appended).
    public void Translate_Prompt_NamesNoSpecificLanguage()
    {
        string prompt = ById("translate").SystemPrompt;

        foreach (string language in new[] { "Spanish", "French", "German", "Japanese", "Chinese", "English" })
        {
            Assert.DoesNotContain(language, prompt, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact] // AC1 — the translate prompt is distinct from each of the four structural prompts.
    public void Translate_Prompt_IsDistinctFromTheFourStructuralPrompts()
    {
        string translate = ById("translate").SystemPrompt;

        foreach (string id in StructuralIds)
        {
            Assert.NotEqual(ById(id).SystemPrompt, translate);
        }
    }

    [Fact] // AC1 — the Audio persona is present, labeled, and pass-through (LLM-free, key-free by construction).
    public void Audio_Persona_IsPresent_Labeled_AndPassThrough()
    {
        Persona audio = ById("audio");

        Assert.Equal("audio", audio.Id);
        Assert.False(string.IsNullOrWhiteSpace(audio.DisplayName),
            "The Audio persona needs a non-empty DisplayName (the ComboBox item text).");
        Assert.True(audio.IsPassThrough,
            "The Audio persona must be IsPassThrough == true so it is never an LLM call / never needs a key.");
    }

    [Fact] // AC1 — the ordered id list is exactly the closed Epic-4 seed set.
    public void Seed_OrderedIds_AreExactly_TheClosedEpic4Set()
    {
        string[] ids = PersonaRegistry.Seed.Select(p => p.Id).ToArray();

        Assert.Equal(
            new[] { "basic", "cozy", "terminal", "tldr", "plain", "translate", "audio" },
            ids);
    }

    [Fact] // AC1 no-regression — Basic is still first and pass-through; ids are unique.
    public void Seed_BasicFirst_AndIdsUnique()
    {
        IReadOnlyList<Persona> seed = PersonaRegistry.Seed;

        Assert.Same(Persona.Basic, seed[0]);
        Assert.True(seed[0].IsPassThrough);

        string[] ids = seed.Select(p => p.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
