namespace TheMarkdownWeb.Agent;

/// <summary>
/// A named personality (Story 4.1 AC1). A persona is a markdown→markdown transform described by its
/// <see cref="SystemPrompt"/>. <see cref="IsPassThrough"/> personas make NO provider call — the engine
/// returns the original markdown unchanged. 4.1 ships exactly ONE built-in: <see cref="Basic"/>
/// (pass-through). 4.2+ add the selector and more personas.
/// </summary>
public sealed record Persona(string Id, string DisplayName, string SystemPrompt, bool IsPassThrough)
{
    /// <summary>
    /// The single seed persona for 4.1: identity/pass-through. Empty system prompt; the engine
    /// short-circuits before the provider, so the render is byte-identical to the Epic-3 render.
    /// </summary>
    public static readonly Persona Basic = new("basic", "Basic", string.Empty, IsPassThrough: true);
}
