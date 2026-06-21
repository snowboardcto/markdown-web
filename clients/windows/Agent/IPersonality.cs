namespace TheMarkdownWeb.Agent;

/// <summary>
/// Placeholder for the AI-personality transform that will sit between the
/// Markdig AST and the FlowDocument render stage (FR-10/11). The internals are
/// deferred pending the agent-integration decision; this interface only marks
/// the seam and the dependency direction (Agent -> Rendering).
/// </summary>
public interface IPersonality
{
    /// <summary>
    /// Human-readable name of the personality (e.g. "Faithful", "Plain Language").
    /// </summary>
    string Name { get; }
}
