using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
using TheMarkdownWeb.Rendering;
using TheMarkdownWeb.Agent;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// Story 4.3 AC5 — switching persona VISIBLY changes the render in place (the "preference change";
/// Q-Preference: the persona choice IS the preference, NO new knob). Reuses the UNCHANGED 4-2
/// <see cref="PersonalityRerenderCoordinator"/> + <see cref="PersonalitySelectionViewModel"/> over a
/// systemPrompt-KEYED fake <see cref="ILlmClient"/>: the held RAW is re-personalized in place with the
/// now-current persona; cozy and terminal produce DIFFERENT sink outputs; Basic round-trips byte-identical.
/// Zero re-fetch is a by-construction guarantee of the 4-2 coordinator (no fetcher dependency).
///   • [Fact] coordinator: SetCurrentPage(raw); cozy + Rerender → sink gets cozy's output; terminal +
///     Rerender → sink gets terminal's (DIFFERENT) output; Basic → byte-identical held raw.
///   • [StaFact] App-seam: the same switch in a real host → the FlowDocument text changes cozy→terminal.
/// No real key/network/socket/model/pixels.
/// </summary>
public class VisibleChangeOnSwitchTests
{
    private static readonly Uri PageUrl = new("https://themarkdownweb.com/x.md");
    private const string HeldRaw = "# Original\n\nthe source body";

    // ---- local fakes -----------------------------------------------------------------------------

    private sealed class KeyedLlmClient : ILlmClient
    {
        public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)
            => Task.FromResult(LlmResult.Success(Marker(systemPrompt) + "\n\n" + pageMarkdown));

        public static string Marker(string systemPrompt)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt ?? string.Empty));
            return "k" + Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private string? _key;
        public InMemorySecretStore(string? seed = null) => _key = seed;
        public bool HasApiKey => !string.IsNullOrEmpty(_key);
        public string? GetApiKey() => _key;
        public void SetApiKey(string key) => _key = key;
        public void ClearApiKey() => _key = null;
    }

    private sealed class NullImageLoader : IImageLoader
    {
        public System.Windows.Media.ImageSource? Load(Uri absolute) => null;
    }

    private sealed class RecordingSink
    {
        public List<(string Markdown, Uri Url)> Calls { get; } = new();
        public Action<string, Uri> Sink => (md, url) => Calls.Add((md, url));
        public string? Last => Calls.Count == 0 ? null : Calls[^1].Markdown;
    }

    private static ContentHostController NewHost() =>
        new(new FlowDocumentScrollViewer(), new FlowDocumentRenderer(), new NullImageLoader(), _ => Task.CompletedTask);

    private static string DocumentText(FlowDocument doc)
        => new TextRange(doc.ContentStart, doc.ContentEnd).Text;

    private static Persona ById(string id) => PersonaRegistry.Seed.Single(p => p.Id == id);

    [Fact] // AC5 — switching persona re-renders the HELD RAW in place to the OTHER persona's output.
    public async Task SwitchPersona_RerendersHeldRaw_ToDifferentOutput_ZeroRefetch()
    {
        var selection = new PersonalitySelectionViewModel();
        var gateway = new PersonalizationGateway(
            new PersonalityEngine(new KeyedLlmClient(), new InMemorySecretStore(seed: "real-key")),
            () => selection.Current);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);

        coordinator.SetCurrentPage(HeldRaw, PageUrl);

        selection.Select(ById("cozy"));
        await coordinator.RerenderAsync();
        string cozyOut = sink.Last!;

        selection.Select(ById("terminal"));
        await coordinator.RerenderAsync();
        string terminalOut = sink.Last!;

        // The visible render changed on the preference (persona) switch.
        Assert.Equal(KeyedLlmClient.Marker(ById("cozy").SystemPrompt) + "\n\n" + HeldRaw, cozyOut);
        Assert.Equal(KeyedLlmClient.Marker(ById("terminal").SystemPrompt) + "\n\n" + HeldRaw, terminalOut);
        Assert.NotEqual(cozyOut, terminalOut);
    }

    [Fact] // AC5 — switching to Basic re-renders the held RAW byte-identically (pass-through; no regression).
    public async Task SwitchToBasic_RerendersHeldRaw_ByteIdentical()
    {
        var selection = new PersonalitySelectionViewModel();
        var gateway = new PersonalizationGateway(
            new PersonalityEngine(new KeyedLlmClient(), new InMemorySecretStore(seed: "real-key")),
            () => selection.Current);
        var sink = new RecordingSink();
        var coordinator = new PersonalityRerenderCoordinator(gateway, sink.Sink);
        coordinator.SetCurrentPage(HeldRaw, PageUrl);

        // Cozy transforms; Basic round-trips the held RAW unchanged (Basic is the default — re-select it).
        selection.Select(ById("cozy"));
        await coordinator.RerenderAsync();
        Assert.NotEqual(HeldRaw, sink.Last);

        selection.Select(Persona.Basic);
        await coordinator.RerenderAsync();
        Assert.Equal(HeldRaw, sink.Last); // byte-identical pass-through.
    }

    [StaFact] // AC5 (App-seam) — the same switch in a real host changes the FlowDocument text cozy→terminal.
    public async Task SwitchPersona_ChangesHostedFlowDocument_CozyToTerminal()
    {
        var selection = new PersonalitySelectionViewModel();
        var gateway = new PersonalizationGateway(
            new PersonalityEngine(new KeyedLlmClient(), new InMemorySecretStore(seed: "real-key")),
            () => selection.Current);
        ContentHostController host = NewHost();
        var coordinator = new PersonalityRerenderCoordinator(gateway, (md, url) => host.ShowMarkdown(md, url));
        coordinator.SetCurrentPage(HeldRaw, PageUrl);

        Persona cozy = ById("cozy");
        Persona terminal = ById("terminal");
        string cozyMarker = KeyedLlmClient.Marker(cozy.SystemPrompt);
        string terminalMarker = KeyedLlmClient.Marker(terminal.SystemPrompt);

        selection.Select(cozy);
        await coordinator.RerenderAsync();
        string cozyText = DocumentText(host.Host.Document!);
        Assert.Contains(cozyMarker, cozyText);
        Assert.DoesNotContain(terminalMarker, cozyText);

        selection.Select(terminal);
        await coordinator.RerenderAsync();
        string terminalText = DocumentText(host.Host.Document!);
        Assert.Contains(terminalMarker, terminalText);
        Assert.DoesNotContain(cozyMarker, terminalText);

        // The hosted render visibly changed on the preference (persona) switch.
        Assert.NotEqual(cozyText, terminalText);
    }
}
