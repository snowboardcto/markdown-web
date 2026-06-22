using System;
using System.IO;
using Xunit;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.4 AC8 — the boundary + <c>System.Speech</c> placement (D3 / Q-Speech-Placement). The
/// reading-order extractor + Translate prompt live in <c>Agent</c> (no speech); the speech seam +
/// <c>SapiSpeechSynthesizer</c> + the <c>System.Speech</c> reference live in <c>App</c>. <c>Rendering</c> is
/// untouched and stays pure (its <c>RenderingPurityTests</c> + the inherited <c>NoEmbeddedBrowserTests</c>
/// guard it). Pure <c>[Fact]</c>s (assembly identity + committed csproj text — elision-proof).
/// </summary>
public class SpeechBoundaryTests
{
    [Fact] // AC8 — the pure reading-order extractor lives in Agent (no WPF, no speech).
    public void ReadingOrderExtractor_LivesIn_Agent()
    {
        Assert.Equal("TheMarkdownWeb.Agent", typeof(ReadingOrderExtractor).Assembly.GetName().Name);
    }

    [Fact] // AC8 — the speech seam + the real SAPI synthesizer live in App (Q-Speech-Placement).
    public void SpeechSeam_And_SapiSynthesizer_LiveIn_App()
    {
        Assert.Equal("TheMarkdownWeb.App", typeof(ISpeechSynthesizer).Assembly.GetName().Name);
        Assert.Equal("TheMarkdownWeb.App", typeof(SapiSpeechSynthesizer).Assembly.GetName().Name);
    }

    [Fact] // AC8 — System.Speech is referenced by App only; Agent stays net+AI (no speech reference).
    public void SystemSpeech_Is_InApp_NotInAgent()
    {
        string appCsproj = File.ReadAllText(LocateCsproj("TheMarkdownWeb.App.csproj"));
        string agentCsproj = File.ReadAllText(LocateCsproj("TheMarkdownWeb.Agent.csproj"));

        Assert.Contains("System.Speech", appCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Speech", agentCsproj, StringComparison.OrdinalIgnoreCase);
    }

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
            throw new InvalidOperationException($"Could not find {csprojFileName} under '{dir.FullName}'.");
        }

        return matches[0];
    }
}
