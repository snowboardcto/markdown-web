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

    /// <summary>
    /// Locates <c>App/TheMarkdownWeb.App.csproj</c> by walking up from the test bin/ to the
    /// <c>clients/windows/</c> root (sentinel <c>TheMarkdownWeb.sln</c>) then globbing for the App csproj.
    /// Robust to bin/ relocation; throws (fails the test) if not found.
    /// </summary>
    private static string LocateAppCsproj()
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

        string[] matches = Directory.GetFiles(dir.FullName, "TheMarkdownWeb.App.csproj", SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"Could not find TheMarkdownWeb.App.csproj under '{dir.FullName}'.");
        }

        return matches[0];
    }
}
