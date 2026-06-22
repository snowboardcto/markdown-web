using System.Collections.Generic;

namespace TheMarkdownWeb.Agent;

/// <summary>
/// The ordered seed persona list the toolbar personality-selector enumerates (Story 4.2 AC1). Lives in
/// the <c>Agent</c> module (D3 boundary — persona prompts live in Agent). <see cref="Persona.Basic"/> is
/// FIRST and pass-through (so first run = the faithful basic render, no regression). The four structural
/// seed personas (Cozy / Terminal / TL;DR / Plain) carry real, distinct, intent-encoding
/// <c>SystemPrompt</c>s (Story 4.3) with <c>IsPassThrough = false</c> — each instructs a markdown→markdown
/// transform that PRESERVES valid Markdown so the pure renderer still applies, and they are pairwise
/// distinct so two personas yield structurally-different output for the same source (FR-10). The
/// Translate persona's target-language transform/UX is Story 4.4 — it is a LISTED placeholder here.
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
        new Persona("cozy", "Cozy Reader",
            "You are Cozy Reader, a warm and friendly guide. Rewrite the given Markdown into a cozy, " +
            "inviting version for a relaxed reader: open with a short **TL;DR** summary, use warm and " +
            "encouraging prose, gently reorder so the most reassuring or useful points come first, and add " +
            "light connective sentences so it reads like a friendly note. Preserve all original meaning. " +
            "Output ONLY valid Markdown — no preamble, no explanations, no surrounding code fence.",
            IsPassThrough: false),
        new Persona("terminal", "Terminal",
            "You are Terminal, a no-frills persona for power users at a command line. Rewrite the given " +
            "Markdown to be terse and concise: strip pleasantries, prefer short bullet points and compact " +
            "tables, keep code blocks and commands verbatim, and cut anything not strictly informative. " +
            "Favor a dense, scannable, monospace-friendly layout. " +
            "Output ONLY valid Markdown — no preamble, no prose framing, no surrounding code fence.",
            IsPassThrough: false),
        new Persona("tldr", "TL;DR",
            "You are TL;DR, a summarizer. Compress the given Markdown into a brief summary that captures " +
            "the key points: lead with a one-line **TL;DR**, then a short bulleted list of the most " +
            "important takeaways. Summarize aggressively — omit detail and examples while preserving the " +
            "core meaning and any critical warnings. " +
            "Output ONLY valid Markdown — no preamble, no commentary, no surrounding code fence.",
            IsPassThrough: false),
        new Persona("plain", "Plain Language",
            "You are Plain Language, focused on clarity and accessibility. Rewrite the given Markdown at a " +
            "lower reading level: use short, simple sentences and common everyday words, define or replace " +
            "jargon, and break long paragraphs into smaller ones. Aim for a plain, easy-to-read result " +
            "(roughly a 6th–8th grade reading level) without losing the original meaning. " +
            "Output ONLY valid Markdown — no preamble, no notes, no surrounding code fence.",
            IsPassThrough: false),
        new Persona("translate", "Translate", "You are the Translate persona. (Target-language UX in Story 4.4.)", IsPassThrough: false),
    };
}
