using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TheMarkdownWeb.Agent.Tests;

/// <summary>
/// Story 4.3 AC2 — the LOAD-BEARING prompt-distinctness contract. Pure <c>[Fact]</c>s (plain CLR; no WPF,
/// no STA, no socket, no key, no model) over the REAL <see cref="PersonaRegistry.Seed"/> (looked up by id,
/// NEVER a re-declared literal). The FOUR structural personas — <c>cozy</c>, <c>terminal</c>, <c>tldr</c>,
/// <c>plain</c> — each carry a real, distinct, intent-encoding markdown→markdown system prompt:
///   • non-empty;
///   • pairwise-distinct (the four structural prompts <c>Distinct().Count() == 4</c>; Basic/Translate excluded);
///   • no placeholder residue (case-insensitive: "refined in story 4.3" / "placeholder");
///   • each mentions "markdown" (the emit-valid-markdown proxy);
///   • each contains its pinned intent keyword(s) (case-insensitive substring, OR-groups tolerated).
///
/// RED until Step 5 replaces the 4-2 PLACEHOLDER prompts in <c>Agent/PersonaRegistry.cs</c> with the real
/// ones — the placeholder-residue + keyword + distinctness asserts fail against the current placeholders.
/// (Basic = pass-through / empty prompt; Translate = a minimal 4.4 placeholder — both EXCLUDED here.)
/// </summary>
public class PersonaPromptDistinctnessTests
{
    // The four STRUCTURAL personas under the AC2 contract (ids are the stable contract; Basic/Translate
    // are deliberately NOT in this set).
    private static readonly string[] StructuralIds = { "cozy", "terminal", "tldr", "plain" };

    private static Persona ById(string id) =>
        PersonaRegistry.Seed.Single(p => p.Id == id);

    private static string PromptOf(string id) => ById(id).SystemPrompt;

    /// <summary>Case-insensitive substring test (the prompts may freely vary wording/casing).</summary>
    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    /// <summary>Tolerant OR-group: the prompt may satisfy the intent via ANY of the alternative tokens.</summary>
    private static bool ContainsAnyOf(string haystack, params string[] needles) =>
        needles.Any(n => Contains(haystack, n));

    [Fact] // AC2 — each of the four structural prompts is non-null/non-empty/non-whitespace.
    public void StructuralPrompts_AreNonEmpty()
    {
        foreach (string id in StructuralIds)
        {
            Assert.False(string.IsNullOrWhiteSpace(PromptOf(id)),
                $"Structural persona '{id}' must carry a non-empty SystemPrompt.");
        }
    }

    [Fact] // AC2 — the four structural prompts are pairwise-distinct (Distinct().Count() == 4).
    public void StructuralPrompts_ArePairwiseDistinct()
    {
        string[] prompts = StructuralIds.Select(PromptOf).ToArray();

        Assert.Equal(4, prompts.Distinct().Count());
    }

    [Fact] // AC2 — no placeholder residue ("refined in story 4.3" / "placeholder"), case-insensitive.
    public void StructuralPrompts_HaveNoPlaceholderResidue()
    {
        foreach (string id in StructuralIds)
        {
            string prompt = PromptOf(id);
            Assert.False(Contains(prompt, "refined in story 4.3"),
                $"Structural persona '{id}' still carries the 4-2 placeholder marker 'refined in Story 4.3'.");
            Assert.False(Contains(prompt, "placeholder"),
                $"Structural persona '{id}' still contains the bare token 'placeholder'.");
        }
    }

    [Fact] // AC2 — each structural prompt mentions "markdown" (the emit-valid-markdown proxy).
    public void StructuralPrompts_MentionMarkdown()
    {
        foreach (string id in StructuralIds)
        {
            Assert.True(Contains(PromptOf(id), "markdown"),
                $"Structural persona '{id}' must instruct a markdown→markdown transform (mention 'markdown').");
        }
    }

    [Fact] // AC2 — Cozy encodes its emphasis+ordering intent: "cozy" AND "tl;dr".
    public void CozyPrompt_EncodesItsIntent()
    {
        string prompt = PromptOf("cozy");

        Assert.True(Contains(prompt, "cozy"), "Cozy's prompt must contain 'cozy'.");
        Assert.True(Contains(prompt, "tl;dr"), "Cozy's prompt must contain 'tl;dr' (the lead-summary intent).");
    }

    [Fact] // AC2 — Terminal encodes its length+terseness intent: "terminal" AND ("terse" OR "concise").
    public void TerminalPrompt_EncodesItsIntent()
    {
        string prompt = PromptOf("terminal");

        Assert.True(Contains(prompt, "terminal"), "Terminal's prompt must contain 'terminal'.");
        Assert.True(ContainsAnyOf(prompt, "terse", "concise"),
            "Terminal's prompt must contain 'terse' or 'concise' (the terseness intent).");
    }

    [Fact] // AC2 — TL;DR encodes its summarization intent: ("tl;dr" OR "tldr") AND "summar".
    public void TldrPrompt_EncodesItsIntent()
    {
        string prompt = PromptOf("tldr");

        Assert.True(ContainsAnyOf(prompt, "tl;dr", "tldr"),
            "TL;DR's prompt must contain 'tl;dr' or 'tldr'.");
        Assert.True(Contains(prompt, "summar"),
            "TL;DR's prompt must contain 'summar' (summary/summarize/summarise — the compression intent).");
    }

    [Fact] // AC2 — Plain encodes its reading-level intent: "plain" AND ("reading level" OR "simple" OR "simpler").
    public void PlainPrompt_EncodesItsIntent()
    {
        string prompt = PromptOf("plain");

        Assert.True(Contains(prompt, "plain"), "Plain's prompt must contain 'plain'.");
        Assert.True(ContainsAnyOf(prompt, "reading level", "simple", "simpler"),
            "Plain's prompt must contain 'reading level' or 'simple'/'simpler' (the reading-level intent).");
    }
}
