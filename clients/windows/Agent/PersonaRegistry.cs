using System.Collections.Generic;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// The ordered seed persona list the toolbar personality-selector enumerates (Story 4.2 AC1). Lives in
/// the <c>Agent</c> module (D3 boundary — persona prompts live in Agent). <see cref="Persona.Basic"/> is
/// FIRST and pass-through (so first run = the faithful basic render, no regression). The non-Basic seed
/// personas are real <see cref="Persona"/> records with non-empty PLACEHOLDER <c>SystemPrompt</c>s +
/// <c>IsPassThrough = false</c>, so the selector → gateway → engine wiring is provable with a fake
/// <see cref="ILlmClient"/>. The ACTUAL structurally-different prompts are refined in Story 4.3; the
/// Translate persona's target-language UX is Story 4.4 — at 4.2 it is a LISTED placeholder.
/// </summary>
public static class PersonaRegistry
{
    /// <summary>
    /// The seed personas in selector order — <see cref="Persona.Basic"/> first (pass-through), then the
    /// placeholder transform personas. The ids are the stable contract: basic, cozy, terminal, tldr,
    /// plain, translate.
    /// </summary>
    public static IReadOnlyList<Persona> Seed { get; } = new[]
    {
        Persona.Basic,
        new Persona("cozy", "Cozy Reader", "You are the Cozy Reader. (Prompt refined in Story 4.3.)", IsPassThrough: false),
        new Persona("terminal", "Terminal", "You are the Terminal persona. (Prompt refined in Story 4.3.)", IsPassThrough: false),
        new Persona("tldr", "TL;DR", "You are the TL;DR persona. (Prompt refined in Story 4.3.)", IsPassThrough: false),
        new Persona("plain", "Plain Language", "You are the Plain Language persona. (Prompt refined in Story 4.3.)", IsPassThrough: false),
        new Persona("translate", "Translate", "You are the Translate persona. (Target-language UX in Story 4.4.)", IsPassThrough: false),
    };
}
