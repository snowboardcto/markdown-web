using System;
using System.Linq;
using System.Reflection;
using Xunit;
using TheMarkdownWeb.Rendering;
using AppType = global::TheMarkdownWeb.App.App;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC5 — App -> Rendering one-way boundary. <c>Rendering</c> is the pure bedrock: it must depend on
/// neither <c>App</c> nor <c>Agent</c> (never "up"), and <c>App</c> must reference <c>Rendering</c>.
/// A plain <c>[Fact]</c> (no STA) that makes the architectural boundary a CI gate, not just a convention.
/// Passes today; FAILS if a later story introduces a reverse reference.
/// </summary>
public class DependencyBoundaryTests
{
    private const string AppAssemblyName = "TheMarkdownWeb.App";
    private const string AgentAssemblyName = "TheMarkdownWeb.Agent";
    private const string RenderingAssemblyName = "TheMarkdownWeb.Rendering";

    [Fact]
    public void Rendering_DoesNotReference_AppOrAgent()
    {
        Assembly rendering = typeof(MarkdownRenderer).Assembly;

        string[] referenced = rendering
            .GetReferencedAssemblies()
            .Select(an => an.Name ?? string.Empty)
            .ToArray();

        Assert.False(
            referenced.Contains(AppAssemblyName, StringComparer.OrdinalIgnoreCase),
            $"{RenderingAssemblyName} must NOT reference {AppAssemblyName}: the bedrock never depends 'up'.");
        Assert.False(
            referenced.Contains(AgentAssemblyName, StringComparer.OrdinalIgnoreCase),
            $"{RenderingAssemblyName} must NOT reference {AgentAssemblyName}: the bedrock never depends 'up'.");
    }

    [Fact]
    public void App_References_Rendering()
    {
        Assembly app = typeof(AppType).Assembly;

        bool referencesRendering = app
            .GetReferencedAssemblies()
            .Any(an => string.Equals(an.Name, RenderingAssemblyName, StringComparison.OrdinalIgnoreCase));

        Assert.True(
            referencesRendering,
            $"{AppAssemblyName} must reference {RenderingAssemblyName} (App depends on the pure render bedrock).");
    }
}
