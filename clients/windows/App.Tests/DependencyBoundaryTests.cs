using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using TheMarkdownWeb.Rendering;

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
        // NOTE: we assert the build-time ProjectReference in App's .csproj, NOT App's bound assembly
        // closure. At this story App declares the dependency but does not yet *use* a Rendering type
        // (rendering lands in Story 3.3), so the C# compiler ELIDES the metadata assembly reference and
        // GetReferencedAssemblies() would not list Rendering — an assembly-closure check here gives a
        // false negative. The csproj ProjectReference is the elision-proof, authoritative edge (same
        // philosophy as the no-embedded-browser csproj tier).
        string appCsproj = LocateAppCsproj();
        string xml = File.ReadAllText(appCsproj);

        var projectRefRegex = new Regex(
            "<ProjectReference\\s+[^>]*Include\\s*=\\s*\"(?<path>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        bool referencesRendering = projectRefRegex
            .Matches(xml)
            .Select(m => m.Groups["path"].Value)
            .Any(p => p.IndexOf("TheMarkdownWeb.Rendering.csproj", StringComparison.OrdinalIgnoreCase) >= 0
                   || (p.IndexOf("Rendering", StringComparison.OrdinalIgnoreCase) >= 0
                       && p.IndexOf("Rendering.Tests", StringComparison.OrdinalIgnoreCase) < 0));

        Assert.True(
            referencesRendering,
            $"{AppAssemblyName} must declare a <ProjectReference> to {RenderingAssemblyName} " +
            $"(App depends on the pure render bedrock). Checked: {appCsproj}");
    }

    [Fact]
    public void Agent_DoesNotReference_App()
    {
        // AC6 — Agent must not depend "up" on App. Assert the build-time ProjectReference in Agent's
        // .csproj (the elision-proof, authoritative edge — same philosophy as App_References_Rendering):
        // the C# compiler elides an unused metadata reference, so the csproj edge is the real contract.
        string agentCsproj = LocateCsproj("TheMarkdownWeb.Agent.csproj");
        string xml = File.ReadAllText(agentCsproj);

        bool referencesApp = ProjectReferencePaths(xml)
            .Any(p => p.IndexOf("TheMarkdownWeb.App.csproj", StringComparison.OrdinalIgnoreCase) >= 0);

        Assert.False(
            referencesApp,
            $"{AgentAssemblyName} must NOT declare a <ProjectReference> to {AppAssemblyName}: the Agent " +
            $"owns net+AI but must never depend 'up' on App. Checked: {agentCsproj}");
    }

    [Fact]
    public void App_References_Agent()
    {
        // AC6 — App composes the engine, so it must declare a ProjectReference to Agent. Assert the
        // build-time csproj edge (elision-proof; App may not yet *use* an Agent type at red-phase, so
        // an assembly-closure check would give a false negative — the csproj edge is authoritative).
        string appCsproj = LocateCsproj("TheMarkdownWeb.App.csproj");
        string xml = File.ReadAllText(appCsproj);

        bool referencesAgent = ProjectReferencePaths(xml)
            .Any(p => p.IndexOf("TheMarkdownWeb.Agent.csproj", StringComparison.OrdinalIgnoreCase) >= 0
                   || (p.IndexOf("Agent", StringComparison.OrdinalIgnoreCase) >= 0
                       && p.IndexOf("Agent.Tests", StringComparison.OrdinalIgnoreCase) < 0));

        Assert.True(
            referencesAgent,
            $"{AppAssemblyName} must declare a <ProjectReference> to {AgentAssemblyName} " +
            $"(App composes the PersonalityEngine). Checked: {appCsproj}");
    }

    private static System.Collections.Generic.IEnumerable<string> ProjectReferencePaths(string csprojXml)
    {
        var projectRefRegex = new Regex(
            "<ProjectReference\\s+[^>]*Include\\s*=\\s*\"(?<path>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return projectRefRegex.Matches(csprojXml).Select(m => m.Groups["path"].Value);
    }

    /// <summary>
    /// Locates <c>App/TheMarkdownWeb.App.csproj</c> by walking up from the test bin/ to the
    /// <c>clients/windows/</c> root (sentinel <c>TheMarkdownWeb.sln</c>) then globbing for the App csproj.
    /// Robust to bin/ relocation; throws (fails the test) if not found.
    /// </summary>
    private static string LocateAppCsproj() => LocateCsproj("TheMarkdownWeb.App.csproj");

    /// <summary>
    /// Locates a named <c>.csproj</c> by walking up from the test bin/ to the <c>clients/windows/</c> root
    /// (sentinel <c>TheMarkdownWeb.sln</c>) then globbing for it. Robust to bin/ relocation; throws (fails
    /// the test) if not found.
    /// </summary>
    private static string LocateCsproj(string csprojFileName)
    {
        const string sentinel = "TheMarkdownWeb.sln";
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, sentinel)))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                $"Could not locate the '{sentinel}' sentinel walking up from '{AppContext.BaseDirectory}'.");
        }

        string[] matches = Directory.GetFiles(dir.FullName, csprojFileName, SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"Could not find {csprojFileName} under '{dir.FullName}'.");
        }

        return matches[0];
    }
}
