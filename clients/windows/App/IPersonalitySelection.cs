using System;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App;

/// <summary>
/// The selection state the gateway reads (Story 4.2 AC3) — replaces the 4-1 constant
/// <c>() =&gt; Persona.Basic</c>. The gateway is composed with <c>() =&gt; selection.Current</c>;
/// <see cref="Select"/> raises <see cref="SelectionChanged"/>, the signal the re-render-in-place path
/// listens to.
/// </summary>
public interface IPersonalitySelection
{
    /// <summary>The currently selected persona (default <see cref="Persona.Basic"/>).</summary>
    Persona Current { get; }

    /// <summary>Raised when <see cref="Select"/> changes <see cref="Current"/>.</summary>
    event EventHandler? SelectionChanged;

    /// <summary>Selects <paramref name="persona"/>; a no-op (no event) if it equals <see cref="Current"/>.</summary>
    void Select(Persona persona);
}
