using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.2 AC6 — the App -&gt; Agent -&gt; (API) layering. The persona REGISTRY lives in
/// <c>TheMarkdownWeb.Agent</c>; the SELECTOR/SELECTION/KEY-UX types live in <c>TheMarkdownWeb.App</c>.
/// A pure <c>[Fact]</c> that locks the boundary (no STA). The standing purity/boundary/no-webview guards
/// (RenderingPurityTests / DependencyBoundaryTests / NoEmbeddedBrowserTests) stay green unchanged.
/// RED until Step 5 adds the types.
/// </summary>
public class PersonalityLayeringTests
{
    [Fact] // AC6 — the persona registry is in the Agent assembly (persona prompts live in Agent).
    public void PersonaRegistry_LivesInAgentAssembly()
    {
        Assert.Equal("TheMarkdownWeb.Agent", typeof(PersonaRegistry).Assembly.GetName().Name);
    }

    [Fact] // AC6 — the selection state lives in the App assembly (the selector UI lives in App).
    public void PersonalitySelectionViewModel_LivesInAppAssembly()
    {
        Assert.Equal("TheMarkdownWeb.App", typeof(PersonalitySelectionViewModel).Assembly.GetName().Name);
    }

    [Fact] // AC6 — the re-render coordinator lives in the App assembly.
    public void PersonalityRerenderCoordinator_LivesInAppAssembly()
    {
        Assert.Equal("TheMarkdownWeb.App", typeof(PersonalityRerenderCoordinator).Assembly.GetName().Name);
    }

    [Fact] // AC6 — the key-entry VM lives in the App assembly (the key-entry UX lives in App).
    public void ApiKeyPromptViewModel_LivesInAppAssembly()
    {
        Assert.Equal("TheMarkdownWeb.App", typeof(ApiKeyPromptViewModel).Assembly.GetName().Name);
    }
}
