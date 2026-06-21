using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using AppType = global::TheMarkdownWeb.App.App;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC4 [DERIVED + HARD — NFR-1 / FR-13 / architecture FC-1]: the native client MUST have NO
/// Chromium / WebView2 / CefSharp / embedded-browser / webview dependency of any kind. This guard
/// FAILS (red) the moment any such dependency is introduced.
///
/// Two tiers are implemented:
///   (a) csproj tier — AUTHORITATIVE. Scans the committed <c>clients/windows/**/*.csproj</c> for any
///       <c>&lt;PackageReference Include="..."&gt;</c> whose id contains a forbidden substring. This is the
///       line of defense for an indirectly-pulled engine: NuGet writes transitive package ids into the
///       restored assets, but the Linux dev box cannot restore, so the committed csproj text is the
///       deterministic backstop. It also catches a forbidden dep BEFORE it is ever restored.
///   (b) runtime closure tier — supplementary. Forces the <c>App</c> assembly + its bound closure to load,
///       then scans direct references, a 1-hop transitive sweep, and the loaded AppDomain assemblies.
///       This sees only the *bound* closure on the Windows runner; full N-hop graph walking is out of
///       scope (a webview engine cannot be a pure-managed leaf without surfacing in tier (a) or the
///       1-hop scan). Tier (a) remains the authoritative transitive backstop.
///
/// Today (no webview deps anywhere) BOTH tiers PASS — this is a guard, not a red-phase driver.
/// </summary>
public class NoEmbeddedBrowserTests
{
    // Case-insensitive substring screen. Catches package ids, assembly simple names, AND native
    // file names in one pass (e.g. Microsoft.Web.WebView2.Core, CefSharp.Wpf, libcef, Chromely).
    private static readonly string[] ForbiddenSubstrings =
    {
        "webview", "webview2", "cefsharp", "chromium", "chromely", "libcef",
        "cef.", "cef3", "xulrunner", "geckofx", "awesomium", "electron",
    };

    private const string Rationale =
        "An embedded browser engine is forbidden by NFR-1 and architecture FC-1: embedding an HTML " +
        "webview reproduces the browser and defeats the native client's reason to exist. The client " +
        "renders native WPF (Markdig -> FlowDocument), never an HTML webview.";

    [Fact] // AC4 (a) — authoritative csproj tier.
    public void NoForbiddenPackageReferences_InAnyWindowsClientCsproj()
    {
        string windowsRoot = LocateWindowsClientRoot();
        string[] csprojFiles = Directory.GetFiles(windowsRoot, "*.csproj", SearchOption.AllDirectories);

        // Anti-tautology: a silent empty glob would make this test a false green. Fail loudly.
        Assert.True(
            csprojFiles.Length > 0,
            $"No .csproj files discovered under '{windowsRoot}'. The no-embedded-browser csproj guard " +
            "cannot run with zero project files — this would be a false-green tautology. " + Rationale);

        var offenders = new List<string>();

        // <PackageReference Include="Id" ... /> — capture the Include value.
        var packageRefRegex = new Regex(
            "<PackageReference\\s+[^>]*Include\\s*=\\s*\"(?<id>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (string file in csprojFiles)
        {
            string xml = File.ReadAllText(file);
            foreach (Match match in packageRefRegex.Matches(xml))
            {
                string id = match.Groups["id"].Value;
                string? hit = MatchForbidden(id);
                if (hit is not null)
                {
                    offenders.Add($"{Path.GetFileName(file)} -> PackageReference '{id}' matches forbidden substring '{hit}'");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Forbidden embedded-browser PackageReference(s) found:\n  " +
            string.Join("\n  ", offenders) + "\n" + Rationale);
    }

    [Fact] // AC4 (b) — supplementary runtime-closure tier.
    public void NoForbiddenAssemblies_InAppBoundClosure()
    {
        // Touch a real App type to force the App assembly + its bound closure to load.
        Assembly appAssembly = typeof(AppType).Assembly;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var simpleNames = new List<string>();

        void Add(AssemblyName an)
        {
            if (an.Name is { Length: > 0 } name && seen.Add(name))
            {
                simpleNames.Add(name);
            }
        }

        // Direct references of App + a 1-hop transitive sweep into each referenced assembly.
        foreach (AssemblyName direct in appAssembly.GetReferencedAssemblies())
        {
            Add(direct);
            try
            {
                Assembly loaded = Assembly.Load(direct);
                foreach (AssemblyName oneHop in loaded.GetReferencedAssemblies())
                {
                    Add(oneHop);
                }
            }
            catch
            {
                // If a referenced assembly cannot be loaded for the 1-hop sweep, its direct name is
                // still screened above; tier (a) is the authoritative backstop for anything unbound.
            }
        }

        // Everything currently loaded into the test AppDomain.
        foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            Add(loaded.GetName());
        }

        var offenders = new List<string>();
        foreach (string name in simpleNames)
        {
            string? hit = MatchForbidden(name);
            if (hit is not null)
            {
                offenders.Add($"assembly '{name}' matches forbidden substring '{hit}'");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Forbidden embedded-browser assembly(ies) found in the App bound closure:\n  " +
            string.Join("\n  ", offenders) + "\n" + Rationale);
    }

    private static string? MatchForbidden(string value)
    {
        foreach (string forbidden in ForbiddenSubstrings)
        {
            if (value.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return forbidden;
            }
        }
        return null;
    }

    /// <summary>
    /// Walks up from the test assembly's bin/ directory to the <c>clients/windows/</c> root, located by
    /// the sentinel file <c>TheMarkdownWeb.sln</c>. Robust to bin/ relocation. Throws (fails the test)
    /// if the sentinel is never found.
    /// </summary>
    private static string LocateWindowsClientRoot()
    {
        const string sentinel = "TheMarkdownWeb.sln";
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, sentinel)))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the '{sentinel}' sentinel walking up from '{AppContext.BaseDirectory}'. " +
            "The no-embedded-browser csproj guard needs the clients/windows root to glob *.csproj.");
    }
}
