using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC11 — Rendering stays PURE: no networking (System.Net.Http / sockets), no AI, no reference
/// to App or Agent. Mirrors the spirit of App.Tests' boundary check, scoped here to net/AI for
/// the Rendering assembly. Plain [Fact] (no WPF type touched) so it needs no STA thread.
/// </summary>
public class RenderingPurityTests
{
    [Fact]
    public void Rendering_DoesNotReference_SystemNetHttp()
    {
        Assembly rendering = typeof(FlowDocumentRenderer).Assembly;

        string[] referenced = rendering
            .GetReferencedAssemblies()
            .Select(an => an.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("System.Net.Http", referenced, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rendering_DoesNotReference_AppOrAgent()
    {
        Assembly rendering = typeof(FlowDocumentRenderer).Assembly;

        string[] referenced = rendering
            .GetReferencedAssemblies()
            .Select(an => an.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("TheMarkdownWeb.App", referenced, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("TheMarkdownWeb.Agent", referenced, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderingCsproj_AddsNoNetworkingOrAiPackageReference()
    {
        string csproj = LocateRenderingCsproj();
        string xml = File.ReadAllText(csproj);

        var packageRefRegex = new Regex(
            "<PackageReference\\s+[^>]*Include\\s*=\\s*\"(?<id>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        string[] packages = packageRefRegex
            .Matches(xml)
            .Select(m => m.Groups["id"].Value)
            .ToArray();

        // Allowlist: Rendering references ONLY Markdig + ColorCode.Core — no net/AI/webview package is added.
        string[] allowed = { "Markdig", "ColorCode.Core" };
        Assert.All(packages, id => Assert.Contains(id, allowed, StringComparer.OrdinalIgnoreCase));

        // Defensive substring guard: no networking/AI/webview hints anywhere in the csproj.
        string[] forbidden = { "System.Net.Http", "HttpClient", "OpenAI", "Anthropic", "Azure.AI", "WebView", "CefSharp" };
        foreach (string needle in forbidden)
        {
            Assert.DoesNotContain(needle, xml, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string LocateRenderingCsproj()
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

        string[] matches = Directory.GetFiles(
            dir.FullName, "TheMarkdownWeb.Rendering.csproj", SearchOption.AllDirectories);
        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"Could not find TheMarkdownWeb.Rendering.csproj under '{dir.FullName}'.");
        }

        return matches[0];
    }
}
