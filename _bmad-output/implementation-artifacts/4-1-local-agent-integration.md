# Story 4.1: Local agent integration

Status: ready-for-dev

<!-- VALIDATION (Step 3, vs epics.md Story 4.1, lines 373–384; FR-12, NFR-5; architecture-epic4-agent.md D1–D5 + the 4.1 binding) — RESULT = PASS.
  Run AFTER Step-2 advanced-elicitation hardening (this revision). Re-validates AC↔epic alignment, addendum conformance, scope drift, missing ACs, and CI-runnable task completeness against the tightened ACs + the new failure-mode/Outcome table, no-leak assertion list, and pinned Anthropic contract.

  1. AC↔EPIC ALIGNMENT (epics.md Story 4.1 = one Given/When/Then/And):
     • "Given the resolved agent-integration model and the `Agent` module" → AC1 (the concrete BYO-key surface: ILlmClient/AnthropicLlmClient, ISecretStore/DpapiSecretStore, PersonalityEngine, Persona.Basic, ReaderContext, PersonalizationResult/Outcome). D1 made concrete.
     • "When the client renders a page, Then it can invoke the reader's local agent with the page markdown/AST plus reader context" → AC2 (the App render-time seam: fetch → PersonalizationGateway → PersonalityEngine.PersonalizeAsync(markdown, persona, ReaderContext) → markdown → FlowDocumentRenderer) + AC3 (BYO-key plumbing). The reader's agent is invoked, on the reader's machine, with the reader's key.
     • "And no human-facing content is rewritten server-side (rendering is local)" → AC5 (NFR-5 proof: AnthropicLlmClient.CompleteAsync issues exactly ONE POST to {AnthropicOptions.BaseUrl}/v1/messages carrying x-api-key + anthropic-version, asserted via stub handler; the request host is the configured PROVIDER host, never a Markdown-Web host; the GET fetcher (read) and POST transform (personalize) are distinct clients with distinct hosts).
     Every epic clause maps to ≥1 AC; no clause unmapped.
  2. ADDENDUM CONFORMANCE (D1–D5): D1 BYO-key — AC1/AC3 (reader's key via ISecretStore/DpapiSecretStore, DPAPI CurrentUser, never logged, only in x-api-key). D2 markdown→markdown — AC1/AC2 (engine returns a markdown STRING; pure Epic-3 FlowDocumentRenderer renders it UNCHANGED). D3 boundary — AC6 (Agent owns net+AI, Rendering pure, Agent⊀App, App→Agent). D4 CI-fakes/totality — AC4 (Outcome-total failure table, every path a CI [Fact] with a fake ILlmClient) + AC7 (windows-latest, ILlmClient/ISecretStore FAKED, stub HttpMessageHandler, no real network/key/socket). D5 audio — correctly DEFERRED (4.4). CONFORMS on all five.
  3. SCOPE DRIFT: NONE beyond 4.1. Selector UI=4.2, persona variety/structural renders=4.3, translation+audio/SAPI=4.4, second provider/Ollama/streaming/caching/model-selection=Deferred-Work-Log. The hardening added NO new scope — it tightened AC4 totality, AC3 no-leak, AC5 provider exactness, AC6 boundary [Fact] names; all within the 4.1 foundation.
  4. MISSING ACs: none. The 4.1 binding's four pillars (the module surface, the App seam + BYO-key, graceful totality, no-server-rewrite) + the standing boundary/CI invariants are all covered (AC1–AC7).
  5. TASK COMPLETENESS (each AC ≥1 CI-runnable proof on windows-latest; NO launch-and-look, NO real network/key/socket): AC1→Agent.Tests contract/pass-through [Fact]s (fake ILlmClient asserted not-called for Basic). AC2→gateway pass-through/transform [Fact]s + App-seam [StaFact] (gateway→ShowMarkdown→FlowDocument). AC3→DpapiSecretStore temp-path round-trip [Fact] (capability-guarded, never asserts ciphertext) + NeedsKey short-circuit [Fact] + sentinel-key no-leak [Fact]. **AC4 totality**→[Theory] over the REAL engine with failing/throwing/null-or-empty/cancelled/oversized fakes + AnthropicLlmClient stub-handler 500/wrong-content/HttpRequestException/cancelled [Fact]s — every Outcome row a deterministic [Fact]. **AC5 no-server-rewrite**→stub-handler request-shape [Fact] (URI={BaseUrl}/v1/messages, POST, x-api-key==key, anthropic-version present, host≠Markdown-Web). **AC3 no-leak**→sentinel scan across Notice/Markdown/FailureReason. AC6→inherited purity/boundary/no-webview green + new Agent_DoesNotReference_App/App_References_Agent [Fact]s. AC7→.sln add + STA/no-parallel discipline.
  NON-BLOCKING ACTION ITEMS (advisory, not gating): (a) Dev confirms the pinned default model id `claude-sonnet-4-6` is current at impl time (it is a current capable Claude id as of this revision; AnthropicOptions.Model is an overridable init default and CI asserts request SHAPE, not the model string, so the dev retains latitude). (b) Dev decides & documents the gateway insertion placement (async render-sink vs FetchEndpointAsync adapter) — both are listed; pick one and preserve NavigationController last-wins (generation re-check after the awaited gateway call). (c) Dev decides & documents whether the legacy `IPersonality` stub is removed or kept (superseded by Persona). None blocks dev.
  RESULT = PASS (no blocking gap; scope unchanged; AC4 totality + AC5 no-server-rewrite + AC3 no-leak each have CI-runnable proofs).
-->

<!-- VALIDATION (Step 1, vs epics.md Story 4.1, lines 373–384; FR-12, NFR-5; architecture-epic4-agent.md D1/D3/D4 + the 4.1 binding): RESULT = PASS.
  - AC↔epic alignment: the epic is one Given/When/Then/And. Mapped exhaustively:
      • "Given the resolved agent-integration model and the `Agent` module" → AC1 (the Agent module surface: ILlmClient/AnthropicLlmClient, ISecretStore/DpapiSecretStore, PersonalityEngine, Persona, Basic pass-through) — the BYO-key model (D1) made concrete.
      • "When the client renders a page, Then it can invoke the reader's local agent with the page markdown/AST plus reader context" → AC2 (the App render-time seam: fetch → PersonalityEngine.PersonalizeAsync(markdown, persona, ReaderContext) → markdown → FlowDocumentRenderer) + AC3 (BYO-key plumbing: DpapiSecretStore get/set/clear, never logged, missing-key outcome).
      • "And no human-facing content is rewritten server-side (rendering is local)" → AC5 (NFR-5 proof: the LLM call originates in Agent on the reader's machine with the reader's key, against the provider endpoint; nothing on a Markdown-Web server rewrites content — testable via the AnthropicLlmClient targeting the configured provider base URL + carrying the key header, asserted with a stub handler).
    Every epic clause maps to ≥1 AC; no epic clause is unmapped.
  - DERIVED ACs (labeled, all justified): AC4 = graceful totality/fallback-to-Basic on EVERY failure path (no key / bad key / network error / timeout / cancel / non-markdown response / huge page) — mandated by the 4.1 binding "graceful failure/totality … never throws to the UI" + D4 totality. AC6 = Rendering-stays-PURE + the App→Agent→(API) module boundary (Agent owns net+AI; Rendering may not; Agent must not depend "up" on App) — D3 + the standing Epic-3 boundary guards. AC7 = the Agent.Tests project + windows-latest CI gate (STA + DisableTestParallelization; ILlmClient/ISecretStore FAKED, no real network/key/socket) — D4 + the proven Epic-2/3 Windows-CI-only discipline. These are DERIVED but mandated by the addendum + the constant Epic-3 constraints.
  - Scope drift: NONE. EXPLICITLY OUT (each named in-spec): the personality-SELECTOR UI (4.2), multiple distinct personas producing structurally-different renders (4.3), translation + audio/SAPI (4.4), OpenAI/second provider, Ollama/offline, streaming, caching (Deferred-Work-Log). 4.1 builds the Agent module + the App seam + BYO-key plumbing + a Basic/pass-through persona proven end-to-end with a FAKE ILlmClient.
  - Task completeness: every AC has ≥1 CI-runnable proof on windows-latest (AC1→Agent.Tests contract/round-trip [Fact]s; AC2→App seam [Fact]/[StaFact]; AC3→DpapiSecretStore round-trip + missing-key [Fact]; AC4→totality theory [Fact]s with a throwing/timeout/non-markdown fake; AC5→AnthropicLlmClient stub-handler request-shape [Fact]; AC6→purity/boundary guards; AC7→STA/no-parallel + .sln/workflow coverage). No "launch-and-look", no real network/key/socket in CI.
  - SIZE / SPLIT: MEDIUM and coherent (one new Agent module surface + one App seam + one Agent.Tests project). Kept WHOLE — the module, the seam, and the BYO-key plumbing are interdependent and must land together to satisfy the epic. No split.
  RESULT = PASS (no blocking gap).
-->

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want my own local agent wired into the client,
so that personalization runs on my behalf, locally.

## Context note (this is the FIRST Epic-4 story — the AGENT FOUNDATION)

Epic 3 shipped a usable basic Markdown Web reader: a native WPF window + labeled/keyboard-reachable toolbar (3.1), a `.md`-only address bar + fetch (3.2), a Markdig→FlowDocument GFM render (3.3), syntax highlighting (3.4), in-client links/anchors/external/images/history (3.5), and the faithful GitHub-light default theme + locked shell a11y (3.6). The Epic-4 personality seam was pinned with no rework debt: `RenderTheme.Basic` is the default, and the renderer renders whatever markdown it is given.

**Story 4.1 grows the existing `Agent` project stub into the real agent module and wires it between fetch and render.** It is the foundation the rest of Epic 4 builds on:
- **4.2** adds the toolbar personality-SELECTOR (the slot reserved since 3.2) and re-renders the current page in place without re-fetching.
- **4.3** adds multiple distinct personas that produce structurally-different markdown → structurally-different renders of the same source (FR-10).
- **4.4** adds the translation persona + the audio/SAPI personality (FR-11).

**The binding architecture is DECIDED** (`_bmad-output/planning-artifacts/architecture-epic4-agent.md`, status: decided). 4.1 implements **D1 (BYO-key)**, **D3 (App→Agent→Rendering boundary)**, **D4 (Windows-CI-only verification via `ILlmClient`/`ISecretStore` fakes)**, and the 4.1 binding. The reader supplies their **own LLM API key**; the client acts as the reader's agent and calls the LLM API **from the reader's machine, with the reader's key**. A personality is a **markdown→markdown transform** done by the LLM, after which the **existing pure `FlowDocumentRenderer` (Epic 3) renders the transformed markdown**. "Local" means *the reader's agent, locally controlled* — not offline inference (offline = the deferred Ollama path).

**What 4.1 builds vs what later stories add (hold the line):**
- **IN (4.1):** the `Agent` module surface — `ILlmClient` (the provider-call seam) + a real `AnthropicLlmClient` (Anthropic Messages API, reader's key, TLS); `ISecretStore` (get/set/clear the key) + a real Windows `DpapiSecretStore` (`ProtectedData`, CurrentUser, key never logged); a `PersonalityEngine` that turns (page markdown + selected `Persona` + `ReaderContext`) into transformed markdown with **graceful totality**; a built-in **Basic** persona that passes the markdown through unchanged (no transform); the **App render-time seam** that invokes the agent between fetch and `FlowDocumentRenderer`; and the **no-server-side-rewrite (NFR-5)** proof. The whole pass-through path is proven end-to-end with a **FAKE `ILlmClient`** in tests.
- **OUT (later Epic-4 stories / Deferred-Work-Log):** the personality-selector UI (4.2); multiple distinct personas producing structurally-different renders (4.3); translation + audio/SAPI (4.4); OpenAI/second provider, Ollama/offline path, streaming token-by-token render, caching transformed renders per (page, persona, model).

**The hard rules (do not violate):**
- **`Rendering` stays PURE** — no networking, no AI, no LLM SDK, no webview. It renders markdown only. **`Agent` is the ONLY module allowed networking + AI.** App → Agent → (provider API); Agent → produces a markdown string → App → Rendering. **Agent must NOT depend "up" on App, and must NOT force Rendering to gain net/AI.** The standing `RenderingPurityTests` / `NoEmbeddedBrowserTests` / `DependencyBoundaryTests` MUST stay green.
- **Windows-CI-only verification, NO real network/key/socket.** `ILlmClient` + `ISecretStore` are injected; tests use FAKES (canned transformed markdown; in-memory key store) exactly as 3-2 stubbed the fetcher/launcher and 3-5 stubbed the image loader. The real `AnthropicLlmClient`/`DpapiSecretStore` are Windows/runtime-only; smoke-test what is safe without secrets.
- **Security/privacy:** the key is stored encrypted (DPAPI, per-user), **never logged**, and only sent to the chosen provider over TLS. The design must make "the key is sent only to the provider" testable/inspectable (a stub handler asserts the request goes to the configured provider base URL + carries the key header — no real call).
- **Totality/determinism:** every failure path (no key, bad key, network error, timeout, cancel, non-markdown response, huge page) is total — falls back to the original markdown render (Basic), surfaces a non-blocking notice, and **never throws to the UI**.

**What this story builds ON (do NOT recreate):**
- `clients/windows/Agent/IPersonality.cs` + `TheMarkdownWeb.Agent.csproj` — the EXISTING `Agent` project stub (a marker `IPersonality { string Name { get; } }` + a `ProjectReference` to `Rendering`). 4.1 grows this into the real module. **Per D3, the Agent owns net+AI and produces a markdown STRING; it does NOT force `Rendering` to gain net/AI** — the existing `ProjectReference` to `Rendering` is allowed (Agent may use `Rendering` to render its own output if useful, or simply return a string for App to render) but is NOT required by 4.1 and may be removed if unused. The Agent must NOT reference `App`.
- `clients/windows/App/MainWindow.xaml.cs` — constructs `MarkdownFetcher`, `NavigationController`, `ContentHostController(ContentScroll, new FlowDocumentRenderer(), new SystemImageLoader(), DispatchLinkAsync)`. The render sink is `(markdown, pageUrl) => _contentHost.ShowMarkdown(markdown, pageUrl)` (line 49). **4.1 inserts the agent between the fetched markdown and `ShowMarkdown`.**
- `clients/windows/App/ContentHostController.cs` — `ShowMarkdown(string markdown, Uri basePageUrl)` renders via `_renderer.Render(markdown ?? string.Empty)` then post-processes images (line 62–70). 4.1 feeds it the (possibly transformed) markdown; `ContentHostController` is **unchanged** (it hosts whatever markdown string it is given).
- `clients/windows/App/NavigationController.cs` — the history state machine; its `_renderSink(result.Markdown, target)` (line 185) is the call site 4.1's seam wraps. The controller stays total and last-wins; 4.1 must not break its re-entrancy.
- `clients/windows/App/MarkdownFetcher.cs` — the proven stub-`HttpMessageHandler` pattern (`MarkdownFetcher(HttpClient http)` over an injectable client; never throws). **`AnthropicLlmClient` mirrors this exactly** (injectable `HttpClient`, total, no real socket in CI).
- `clients/windows/Rendering/FlowDocumentRenderer.cs` — the pure renderer that renders the transformed markdown. UNCHANGED by 4.1.
- `clients/windows/App.Tests/MarkdownFetcherTests.cs` — the `StubHandler : HttpMessageHandler` that records the last request + returns a canned response (no socket). **`AnthropicLlmClientTests` reuse this exact pattern.**
- `clients/windows/App.Tests/DependencyBoundaryTests.cs` + `clients/windows/Rendering.Tests/RenderingPurityTests.cs` + `clients/windows/App.Tests/NoEmbeddedBrowserTests.cs` — the purity/boundary/no-webview guards. **All stay green.** 4.1 EXTENDS the boundary guard with Agent-direction assertions (see AC6).
- `clients/windows/App.Tests/AssemblyInfo.cs` + `clients/windows/Rendering.Tests/` — the `[assembly: CollectionBehavior(DisableTestParallelization = true)]` + `Xunit.StaFact` discipline the new `Agent.Tests` project mirrors.
- `clients/windows/TheMarkdownWeb.sln` — the solution that 4.1 adds `Agent.Tests` to (5 projects today: Rendering, App, Agent, Rendering.Tests, App.Tests).
- `.github/workflows/build-windows.yml` — windows-latest CI (restore → build -c Release → test -c Release on `TheMarkdownWeb.sln`), `paths: clients/windows/**`. **The ONLY verification surface.** The `paths` filter already covers the new `Agent.Tests` project; the `.sln` add is what brings it into the build.

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test (drives AC7, Tasks for CI)

**This repo is developed on Linux with NO .NET SDK installed; WPF builds and runs ONLY on Windows.** The dev agent CANNOT build, run, or visually confirm anything locally. **Verification happens exclusively through `build-windows.yml` on `windows-latest`** (restore → build -c Release → test -c Release on `TheMarkdownWeb.sln`). Therefore:

- Every acceptance bar that must be *checked* is checked **either by the compiler (build succeeds) or by an xUnit test that runs in `dotnet test` on `windows-latest`** — never "make a real API call and look at the output."
- **No real network, no real key, no real socket in CI.** `ILlmClient` and `ISecretStore` are injected and FAKED in tests (canned transformed markdown; in-memory key store). The real `AnthropicLlmClient` is exercised only via a **stub `HttpMessageHandler`** (no socket, mirroring `MarkdownFetcherTests`); the real `DpapiSecretStore` is Windows-runtime-only and lightly smoke-tested (round-trip protect/unprotect) without asserting on ciphertext.
- **Prefer pure `[Fact]` for the engine / persona / keys / LLM-client logic** (these are plain CLR + `System.Net.Http`, no `DispatcherObject`). Use `[StaFact]` ONLY when a WPF object is constructed (e.g. an App-seam test that constructs `MainWindow` or reads a hosted `FlowDocument`). `Xunit.StaFact` + `[assembly: CollectionBehavior(DisableTestParallelization = true)]` are added to the new `Agent.Tests` project (mirroring the existing test projects). No shown `Window`, no `Dispatcher` pump, no socket, no real `Process.Start`, no real secret in logs, no pixels/timing.
- **No-tautology / no-secret-leak:** assert against the REAL `PersonalityEngine` / `AnthropicLlmClient` / `DpapiSecretStore` behavior (the actual returned markdown, outcome enum, and request shape), never a re-declared stub. NEVER write a real API key into a test, a log, or an exception message; assert that the key never appears in any logged/serialized string the engine or client produces.

## Acceptance Criteria

1. **[The Agent module surface — `ILlmClient` + `AnthropicLlmClient`, `ISecretStore` + `DpapiSecretStore`, `PersonalityEngine`, `Persona`, the Basic pass-through (D1)]** **Given** the resolved BYO-key agent-integration model, **When** the `Agent` module is inspected, **Then** it exposes a concrete, pinned surface (namespace `TheMarkdownWeb.Agent`) that lets the App invoke the reader's local agent with page markdown + reader context and get transformed markdown back, with the provider call and key storage behind injectable seams. The EXACT contract (the Step-4 TDD + Step-5 impl agents implement this verbatim — see "Pinned Agent API surface" in Dev Notes for full signatures/XML intent):
   - **`ILlmClient`** — the provider-call seam. `Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)`. Returns an `LlmResult` (a `readonly record struct` with `bool IsSuccess`, `string? Markdown`, `string? FailureReason` + `Success(string)` / `Failure(string)` factories — mirroring `App.FetchResult`). **Total contract: never throws out of `CompleteAsync`** (every failure → `LlmResult.Failure(reason)`).
   - **`AnthropicLlmClient : ILlmClient`** — a real impl over an injectable `HttpClient` (`AnthropicLlmClient(HttpClient http, ISecretStore secretStore, AnthropicOptions? options = null)`). Posts to the Anthropic Messages API (`{BaseUrl}/v1/messages`, default `BaseUrl = https://api.anthropic.com`) with headers `x-api-key: <reader's key>`, `anthropic-version: 2023-06-01`, `content-type: application/json`, and a body `{ model, max_tokens, system: <systemPrompt>, messages: [{ role: "user", content: <pageMarkdown + reader-context framing> }] }`; default `model` = a current capable Claude model pinned in `AnthropicOptions` (see Dev Notes "Default model"); parses the first `content[].text` block out as the transformed markdown. Never throws (HTTP error / timeout / cancel / malformed JSON / missing key → `LlmResult.Failure`). The reader's key is read from `ISecretStore` per call; **the key is never logged and never placed anywhere except the `x-api-key` header on the request to the configured `BaseUrl`** (AC5).
   - **`ISecretStore`** — `string? GetApiKey()`, `void SetApiKey(string key)`, `void ClearApiKey()`, `bool HasApiKey { get; }`. The key is opaque; the store never logs it.
   - **`DpapiSecretStore : ISecretStore`** — a real Windows impl using `System.Security.Cryptography.ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser`, persisting the encrypted bytes to a per-user app-data file (e.g. `%LOCALAPPDATA%\TheMarkdownWeb\agent.key`). Round-trips set→get; clear removes it; `HasApiKey` reflects presence. **Never logs the plaintext or the ciphertext.** (Windows/runtime-only — smoke-tested per AC7, not asserted on ciphertext bytes.)
   - **`Persona`** — the named persona (a `sealed class` or `record`): `string Id`, `string DisplayName`, `string SystemPrompt`, `bool IsPassThrough`. The seed set for 4.1 is exactly ONE built-in persona: **`Persona.Basic`** (`Id = "basic"`, `DisplayName = "Basic"`, `IsPassThrough = true`, an empty/identity `SystemPrompt`). 4.2/4.3/4.4 add more personas; 4.1 ships only Basic so the integration is provable without persona variety.
   - **`ReaderContext`** — the reader context passed at render time (a `readonly record struct` / `record`): at 4.1 minimal but real (e.g. `string? PreferredLanguage`, `string? PageUrl` so the agent knows the page identity; extensible without breaking the signature). Used by the engine/persona to frame the transform; for Basic it is carried but unused.
   - **`PersonalityEngine`** — `PersonalityEngine(ILlmClient llmClient, ISecretStore secretStore)`; `Task<PersonalizationResult> PersonalizeAsync(string pageMarkdown, Persona persona, ReaderContext readerContext, CancellationToken ct)`. **`PersonalizationResult`** is a `readonly record struct` with `string Markdown` (ALWAYS a non-null renderable markdown string — the transformed markdown on success, else the original), a `PersonalizationOutcome Outcome` enum (`Transformed`, `PassThrough`, `NeedsKey`, `FellBack`), and `string? Notice` (a non-blocking, key-free human notice on `NeedsKey`/`FellBack`, else null). The engine is **total** (AC4). For `Persona.Basic` (`IsPassThrough`) the engine returns the ORIGINAL markdown unchanged with `Outcome = PassThrough` and makes **no** LLM call. For a non-pass-through persona with no key it returns the original markdown + `Outcome = NeedsKey` + a "needs key" notice (no LLM call, no crash). Otherwise it calls `ILlmClient.CompleteAsync` and returns the transformed markdown on success (`Transformed`), or the original markdown + `FellBack` + a notice on any failure.

   Proven by pure `[Fact]`s in `Agent.Tests` over the REAL types: the `Persona.Basic` constants; the `LlmResult`/`PersonalizationResult` factory/default contracts; `PersonalizeAsync(md, Persona.Basic, ctx, ct)` returns `Markdown == md` + `Outcome == PassThrough` with a fake `ILlmClient` that is asserted **never called**; and a non-pass-through persona over a fake `ILlmClient` returning canned transformed markdown yields `Markdown == <canned>` + `Outcome == Transformed`. No real network, no real key. *(AC1 — epics.md Story 4.1 line 381 "the resolved agent-integration model and the `Agent` module"; FR-12; architecture-epic4-agent.md D1 BYO-key + the 4.1 binding.)*

2. **[The App render-time seam — fetch → agent → render (FR-12)]** **Given** the client has fetched a page's raw markdown, **When** it renders the page, **Then** it invokes the reader's local agent with the page markdown + reader context via `PersonalityEngine.PersonalizeAsync(...)` BEFORE handing markdown to `FlowDocumentRenderer`, so the rendered FlowDocument is built from the agent's output (the transformed markdown on success, the original markdown on pass-through/fallback). Concretely:
   - The App composes the agent once (a `PersonalityEngine` over a real `AnthropicLlmClient` + `DpapiSecretStore`, wired in `MainWindow` alongside the existing `MarkdownFetcher`/`SystemBrowserLauncher`) and exposes the selected persona (at 4.1 always `Persona.Basic` — the SELECTOR is 4.2) + a `ReaderContext` built from the current page (`PageUrl`, default `PreferredLanguage`).
   - The render path is wrapped so the markdown handed to `ContentHostController.ShowMarkdown` is the engine's `PersonalizationResult.Markdown`. The cleanest seam is a thin **App-side `PersonalizationGateway`** (or equivalent) `Task<string> ResolveMarkdownAsync(string fetchedMarkdown, Uri pageUrl, CancellationToken ct)` that calls `PersonalizeAsync(fetchedMarkdown, _selectedPersona, ctx, ct)` and returns `result.Markdown` (surfacing `result.Notice` non-blockingly when present). `NavigationController`'s render sink (`MainWindow.xaml.cs` line 49) is updated to run the fetched markdown through this gateway before `ShowMarkdown`. **`ContentHostController` and `FlowDocumentRenderer` are UNCHANGED** — they still render whatever markdown string they are given. The gateway is total (it returns the engine's always-renderable `Markdown`).
   - At 4.1 the default/Basic persona passes through unchanged, so the rendered page is byte-identical to the Epic-3 render — the POINT of 4.1 is the *integration + boundary + BYO-key plumbing*, proven end-to-end with a **FAKE `ILlmClient`** at the engine layer.

   Proven by a pure `[Fact]` over the `PersonalizationGateway` with a fake `ILlmClient`: with `Persona.Basic`, `ResolveMarkdownAsync("# H\n\npara", pageUrl, ct)` returns `"# H\n\npara"` unchanged (pass-through end-to-end, no LLM call); with a non-pass-through persona + a fake returning canned markdown, it returns the canned markdown. Plus a `[StaFact]` proving the App wiring renders the gateway's output: construct the App seam (the gateway + `ContentHostController` over a real `FlowDocumentRenderer` into a `FlowDocumentScrollViewer`), drive a fetched markdown through the gateway → `ShowMarkdown`, and assert the hosted `FlowDocument` is non-empty and reflects the resolved markdown (Basic → reflects the original; a fake-transform persona → reflects the transformed markdown). Construct-not-Show; no pixels; no real network. *(AC2 — epics.md Story 4.1 lines 382–383 "When the client renders a page, Then it can invoke the reader's local agent with the page markdown/AST plus reader context"; FR-12; architecture-epic4-agent.md D3 App→Agent→Rendering wiring + D2 markdown→markdown.)*

3. **[BYO-key storage + missing-key handling (D1 key storage)]** **Given** the reader supplies (or has not yet supplied) their own API key, **When** the key is stored and the engine runs, **Then** the key is stored **encrypted, per-user, locally** via `DpapiSecretStore` (DPAPI/`ProtectedData`, CurrentUser), is **never logged**, and a missing key produces a clear **"needs key"** outcome with no crash (no LLM call). Concretely:
   - `DpapiSecretStore.SetApiKey(k)` then `GetApiKey()` round-trips `k`; `ClearApiKey()` removes it; `HasApiKey` reflects presence — the persisted bytes are DPAPI-encrypted (`DataProtectionScope.CurrentUser`), never the plaintext. (Windows-runtime smoke test per AC7; the round-trip is asserted, the ciphertext is NOT.)
   - With an **in-memory fake `ISecretStore` holding no key**, a non-pass-through persona run yields `Outcome == NeedsKey`, the ORIGINAL markdown (no transform), a non-blocking `Notice` that asks for a key, and **the fake `ILlmClient` is never called** (the engine short-circuits before the provider).
   - **The key never appears in any string the engine/client surfaces — the full pinned set is the "No-leak assertion list" above (items 1–9).** With an in-memory fake holding the sentinel key `"sk-ant-SENTINEL"`, assert that string appears in NO `PersonalizationResult.Notice`, NO `PersonalizationResult.Markdown`, and NO `LlmResult.FailureReason` produced by the engine/client across the success AND every failure path; and that no plaintext key is persisted beyond the DPAPI ciphertext. (The key is only ever placed in the request `x-api-key` header to the configured `BaseUrl` — AC5.)

   Proven by a `[Fact]` round-trip over `DpapiSecretStore` (set→get→clear→HasApiKey) under `[StaFact]`/`[Fact]` as feasible without asserting on ciphertext (skipped/guarded if DPAPI is unavailable on the runner — see Dev Notes "DpapiSecretStore CI smoke test"), a `[Fact]` for the `NeedsKey` short-circuit over the in-memory fake (asserting the fake LLM client is never called), and a `[Fact]` asserting the sentinel key never leaks into any surfaced string. *(AC3 — DERIVED from epics.md Story 4.1 (the reader's "own local agent" = BYO-key) + architecture-epic4-agent.md D1 "Key storage: … DPAPI (`ProtectedData`, CurrentUser scope) via an `ISecretStore` abstraction. The key is never logged" + the 4.1 binding "missing key → a clear 'needs key' outcome (no crash)".)*

4. **[Graceful totality / fallback to Basic render on EVERY failure path (D4 totality)]** **Given** the agent transform can fail in many ways, **When** any failure path occurs, **Then** the engine is **total** — it returns an always-renderable `PersonalizationResult` whose `Markdown` is the ORIGINAL fetched markdown (the Basic render), an `Outcome` of `NeedsKey` (no key) or `FellBack` (any other failure), and a non-blocking key-free `Notice` — and **never throws to the UI**. **The full enumeration is the "Failure-mode / Outcome totality table" above** (14 rows; `PassThrough`/`Transformed`/`NeedsKey`/`FellBack` is a total, closed enum; each row → a deterministic outcome that never throws and always yields renderable markdown; each row a CI-runnable `[Fact]`). Highlights:
   - **No key (non-pass-through persona):** `NeedsKey`, original markdown, "add your key" notice, no LLM call (AC3).
   - **`ILlmClient` returns `LlmResult.Failure(...)`** (the fake simulating HTTP error / wrong-content / empty body): `FellBack`, original markdown, a notice, no throw.
   - **`ILlmClient` THROWS** (a defensive fake that throws despite the total contract): the engine **catches** it → `FellBack`, original markdown, a notice, no throw (defense-in-depth, mirroring `NavigationController`'s defensive catch).
   - **Cancellation:** a pre-cancelled token → the engine returns total (`FellBack` or a cancelled outcome folded into `FellBack`) with the original markdown, no unhandled `OperationCanceledException` escaping `PersonalizeAsync`.
   - **Non-markdown / empty transformed result:** because the renderer is total, "non-markdown" is not the guard — the guard is usable-text-present (`Transformed`) vs absent/empty/whitespace (`FellBack`). If the LLM returns success with a null/empty/whitespace `Markdown`, the engine treats it as a fallback (`FellBack`, original markdown) — it never hands an empty document to the renderer when the source was non-empty (table rows 10–11).
   - **Oversized page:** a very large `pageMarkdown` is handled without OOM/crash — the real `AnthropicLlmClient` bounds the request defensively via a documented `MaxInputChars` cap (mirroring `MarkdownFetcher.MaxBodyBytes`) and may refuse-transform → pass-through; the engine path stays total (table row 14). It never builds an unbounded request and never throws.

   Proven by a `[Theory]`/`[Fact]` suite over the REAL engine with FAKE `ILlmClient`s (a failing fake, a throwing fake, a null/empty-returning fake) + an in-memory `ISecretStore`, plus a pre-cancelled-token `[Fact]` and an oversized-input `[Fact]`, each asserting: no exception escapes `PersonalizeAsync`, `Markdown == <original>`, and `Outcome ∈ {NeedsKey, FellBack}`. Also a `[Fact]` that `AnthropicLlmClient.CompleteAsync` never throws for a stub-handler 500 / wrong-content-type / `HttpRequestException` / pre-cancelled token (mirroring `MarkdownFetcherTests`). *(AC4 — DERIVED from architecture-epic4-agent.md D4 "Totality / failure modes: no key → … never crash; API error/timeout/rate-limit → fall back to Basic render + a non-blocking notice; … Nothing throws to the UI" + the 4.1 binding "every failure path … is total — falls back to the original markdown render, never throws to the UI".)*

5. **[No server-side rewrite — NFR-5 proof: the transform originates in Agent, on the reader's machine, with the reader's key, to the provider]** **Given** the trust model requires that no human-facing content is rewritten server-side, **When** the transform path is inspected, **Then** the LLM call **originates in `Agent` on the reader's machine with the reader's key**, targets the **configured provider endpoint** (not a Markdown-Web server), and nothing on a Markdown-Web server rewrites content — rendering is local. This is made **testable/inspectable**:
   - `AnthropicLlmClient.CompleteAsync` issues exactly one outgoing request whose URI is `{AnthropicOptions.BaseUrl}/v1/messages` (the configured provider base URL, default `https://api.anthropic.com`) — asserted via a stub `HttpMessageHandler` that records `LastRequest` (no real socket), proving the request goes to the provider and NOT to any Markdown-Web host.
   - The recorded request carries the reader's key in the `x-api-key` header and `anthropic-version: 2023-06-01` — proving the key is sent only to the provider over the configured (TLS, in prod) endpoint, and the page markdown is the request body (the reader's machine sends it directly to the reader's chosen provider).
   - The App fetch path (`MarkdownFetcher`, Story 3.2) is **read-only** against the Markdown-Web server (`GET … Accept: text/markdown`); the transform is a SEPARATE client→provider call. There is no code path in `App`/`Agent` that POSTs page content to a Markdown-Web server for rewriting — assertable by inspection + the boundary/purity guards (the Markdown-Web fetch and the provider transform are distinct clients with distinct hosts).

   Proven by a `[Fact]` over `AnthropicLlmClient` with a stub handler: assert `handler.LastRequest.RequestUri` is `{BaseUrl}/v1/messages`, the method is `POST`, the `x-api-key` header equals the secret-store key, `anthropic-version` is `2023-06-01`, and the page markdown is present in the body — all without a real call (the pinned contract is in "Pinned Anthropic Messages API request/response contract" above; the positive-direction assertions are items 7–8 of the "No-leak assertion list"). Plus a `[Fact]` asserting the request host is the configured provider host, never a `themarkdownweb.com`/Markdown-Web host (the transform is client→provider, not client→server→rewrite; the GET fetcher and POST transform are distinct clients with distinct hosts). *(AC5 — epics.md Story 4.1 line 384 "no human-facing content is rewritten server-side (rendering is local)"; NFR-5; architecture-epic4-agent.md D1 trust model + the 4.1 binding "No server-side rewrite (NFR-5): assert the transform path is client-side".)*

6. **[`Rendering` stays PURE + the App→Agent→(API) module boundary (D3)]** **Given** 4.1 adds networking + AI to the client, **When** the dependency graph + csprojs are inspected, **Then** the boundary holds: **`Agent` is the ONLY module with networking + AI** (it may reference `System.Net.Http`; it must NOT embed a browser; it must NOT depend "up" on `App`); **`Rendering` stays PURE** (no `System.Net.Http`, no AI/LLM SDK, no webview, no `App`/`Agent` reference — it renders markdown only); and `App → Agent → Rendering` is the only direction. Concretely:
   - The inherited **`RenderingPurityTests`** (allowlist `{Markdig, ColorCode.Core}` + the forbidden-substring guard that already screens `System.Net.Http`/`HttpClient`/`Anthropic`/`OpenAI`/`WebView`) stay green — **4.1 adds NO package and NO net/AI substring to the `Rendering` csproj.** (The forbidden list in `RenderingPurityTests` already includes `Anthropic`/`OpenAI` — Rendering must keep them out.)
   - The inherited **`DependencyBoundaryTests.Rendering_DoesNotReference_AppOrAgent`** + **`App_References_Rendering`** stay green. 4.1 ADDS boundary assertions for the Agent direction: **`Agent_DoesNotReference_App`** (Agent must not depend "up"; assert via the `Agent` csproj's `ProjectReference`s — it references `Rendering` only, if anything, never `App`) and **`App_References_Agent`** (App declares a `ProjectReference` to Agent so it can compose the engine). Put these in `App.Tests/DependencyBoundaryTests.cs` (extend it) or a new `Agent.Tests` boundary test — decide & document.
   - The inherited **`NoEmbeddedBrowserTests`** (csproj glob across `clients/windows/**`) stays green — an LLM HTTP client is not a browser engine; **no `webview`/`cef`/`chromium`/… substring is added to ANY csproj** (including the new `Agent` net additions and `Agent.Tests`).
   - The `Agent` csproj may add `System.Net.Http` usage (it is in-box for `net10.0-windows`, so likely NO new `PackageReference` is required — `HttpClient`/`ProtectedData` are in the framework). If a package IS added, it is net/AI-appropriate and lives ONLY in `Agent` — never in `Rendering`.

   Proven by the inherited purity/boundary/no-webview guards staying green + the new `Agent_DoesNotReference_App` / `App_References_Agent` `[Fact]`s (plain reflection/csproj checks, no STA). *(AC6 — DERIVED from architecture-epic4-agent.md D3 "Rendering — stays PURE: no networking, no AI, no LLM SDK. … (Agent is the ONLY module allowed networking+AI.) … Agent … must not depend 'up' on App" + the standing Epic-3 boundary invariant; FC-1 no embedded browser.)*

7. **[`Agent.Tests` project + windows-latest CI gate — STA + no-parallel; ILlmClient/ISecretStore FAKED (D4)]** **Given** verification is exclusively `windows-latest` CI with no real network/key, **When** `build-windows.yml` runs (`restore` → `build -c Release` → `test -c Release` on `TheMarkdownWeb.sln`), **Then** the whole solution **builds clean** and **all tests pass green**, including a NEW **`Agent.Tests`** project (mirroring the established one-test-project-per-production-project pattern), with all Agent verification using FAKES (no real Anthropic call, no real key, no socket in CI). Specifically:
   - A new **`clients/windows/Agent.Tests/TheMarkdownWeb.Agent.Tests.csproj`** is created mirroring `App.Tests`/`Rendering.Tests` (`net10.0-windows`, `IsTestProject`, the same `Microsoft.NET.Test.Sdk` 17.12.0 / `xunit` 2.9.2 / `xunit.runner.visualstudio` 2.8.2 / `Xunit.StaFact` 1.1.11 versions, a `ProjectReference` to `Agent`), **added to `TheMarkdownWeb.sln`** (so CI builds + tests it). It carries its own `AssemblyInfo.cs` with `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. App-seam tests that construct WPF objects (AC2's `[StaFact]`) live in `App.Tests` (which already references App + Rendering); pure Agent tests (AC1/AC3/AC4/AC5) live in `Agent.Tests`. **Decide & document** which AC's tests land where (recommend: AC1/AC3/AC4/AC5 + the AnthropicLlmClient/DpapiSecretStore tests in `Agent.Tests`; AC2's App-seam `[StaFact]` in `App.Tests`; AC6 boundary `[Fact]`s in `App.Tests` where the existing boundary tests live).
   - **STA + no-parallel discipline.** Prefer pure `[Fact]` for the engine/persona/keys/LLM-client logic (no `DispatcherObject`). Use `[StaFact]` ONLY for AC2's App-seam test that constructs `MainWindow`/reads a hosted `FlowDocument`. `Xunit.StaFact` + `DisableTestParallelization` are present in every test project (added new to `Agent.Tests`). No shown `Window`, no `Dispatcher` pump, no socket, no real `Process.Start`, no real secret in logs, no pixels/timing.
   - **No regression / boundary intact.** Every existing 3.x `Rendering.Tests`/`App.Tests` stays green UNCHANGED; `Rendering` stays pure (AC6). The `.sln` gains `Agent.Tests` and a `ProjectReference` from `App` to `Agent`; the `Agent` csproj grows to the real module; **`build-windows.yml` is otherwise UNCHANGED** (the `paths: clients/windows/**` filter already covers every new file). No workflow edit beyond what the `.sln`/csproj adds require (none).
   *(AC7 — DERIVED CI/build gate + architecture-epic4-agent.md D4 "Same discipline as Epics 2–3 … `ILlmClient` … tests use a fake … no real API call, no key, no socket in CI. `ISecretStore` … in-memory fake … `DpapiSecretStore` is Windows-only and lightly smoke-tested"; the sole verification per the Environment Constraint; build-windows.yml; the one-test-project-per-production-project pattern.)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 4.1: Local agent integration] (lines 373–384): **Given** the resolved agent-integration model and the `Agent` module **When** the client renders a page **Then** it can invoke the reader's local agent with the page markdown/AST plus reader context **And** no human-facing content is rewritten server-side (rendering is local). (FR-12, NFR-5). **AC1** = the Agent module surface (`ILlmClient`/`AnthropicLlmClient`, `ISecretStore`/`DpapiSecretStore`, `PersonalityEngine`, `Persona`, Basic pass-through). **AC2** = the App render-time seam (fetch → agent → render). **AC3** = BYO-key storage (DPAPI, never logged) + missing-key handling. **AC4** = graceful totality / fallback to Basic on every failure. **AC5** = no-server-side-rewrite (NFR-5) proof. **AC6** = Rendering-stays-pure + the App→Agent→(API) boundary (DERIVED). **AC7** = the Agent.Tests project + windows-latest CI gate (STA + no-parallel; ILlmClient/ISecretStore faked) (DERIVED).

## Advanced-elicitation hardening applied (this revision)

> Step-2 refinement. Methods auto-applied: **(1) Edge-case / failure-mode totality** (exhaustive enumeration of every failure path through `PersonalityEngine.PersonalizeAsync` + `AnthropicLlmClient.CompleteAsync` → a total `PersonalizationOutcome`, each a CI [Fact]); **(2) Security / no-leak rigor** (a pinned sentinel-key assertion list across every surfaced string + the x-api-key-only invariant); **(3) Provider/API exactness** (the precise Anthropic Messages API request/response contract + pinned `AnthropicOptions` defaults). Scope UNCHANGED — the 4.1 foundation; nothing from 4.2 (selector) / 4.3 (multi-persona) / 4.4 (translation+audio) is pulled in. The tightenings below are BINDING on the Step-4 TDD + Step-5 impl agents.

### Failure-mode / Outcome totality table (the load-bearing AC4) — every path → a deterministic Outcome, NEVER throws to the UI, ALWAYS yields renderable markdown

The renderer (`FlowDocumentRenderer.Render`) is **total**: any string — including empty — renders without throwing (Epic-3 invariant). So "renderable" is NOT the guard; the guard is **success-with-usable-text vs fallback-to-original**. The `PersonalizationOutcome` enum is total and closed: `{ Transformed, PassThrough, NeedsKey, FellBack }`. Every path below resolves to exactly one row; each row is a CI-runnable [Fact] over the REAL `PersonalityEngine` (and, where noted, the REAL `AnthropicLlmClient`) with a FAKE `ILlmClient` / in-memory `ISecretStore` / stub `HttpMessageHandler`. `Markdown` is ALWAYS the row's stated value (never null); `Notice` is key-free.

| # | Trigger (path through PersonalizeAsync → CompleteAsync) | Layer that detects | `Outcome` | `Markdown` returned | `Notice` | LLM call? | CI proof |
|---|---|---|---|---|---|---|---|
| 1 | **Pass-through persona** (`Persona.Basic`, `IsPassThrough`) | engine short-circuit | `PassThrough` | ORIGINAL (byte-identical) | `null` | **NO — assert zero requests** | `[Fact]` engine: fake `ILlmClient` asserted **never called**; `Markdown == md` |
| 2 | **No key** (non-pass-through, `HasApiKey == false`) | engine short-circuit (before provider) | `NeedsKey` | ORIGINAL | "add your key" (key-free) | **NO** | `[Fact]` engine: in-memory store empty; fake `ILlmClient` asserted **never called** |
| 3 | **Bad / expired key (401/403)** | client → `Failure` | `FellBack` | ORIGINAL | non-blocking (key-free) | yes (1 POST) | `[Fact]` client: stub 401 → `LlmResult.Failure`; engine folds → `FellBack` |
| 4 | **Network error / DNS / TLS** (`HttpRequestException`) | client catch → `Failure` | `FellBack` | ORIGINAL | non-blocking | attempted | `[Fact]` client: stub throws `HttpRequestException` → `Failure` (no throw) |
| 5 | **Timeout** (request exceeds `AnthropicOptions` timeout → `TaskCanceledException`/`OperationCanceledException`) | client catch → `Failure` | `FellBack` | ORIGINAL | non-blocking | attempted | `[Fact]` client: stub delays/throws `OperationCanceledException` → `Failure` |
| 6 | **Cancellation honored** (pre-cancelled `CancellationToken`) | engine/client total | `FellBack` | ORIGINAL | non-blocking | NO partial render | `[Fact]` engine + `[Fact]` client: pre-cancelled token; **no `OperationCanceledException` escapes** `PersonalizeAsync`/`CompleteAsync`; original rendered (no stale/partial) |
| 7 | **Non-2xx rate-limit (429)** | client → `Failure` | `FellBack` | ORIGINAL | non-blocking | yes | `[Fact]` client: stub 429 → `Failure` |
| 8 | **Non-2xx server error (500)** | client → `Failure` | `FellBack` | ORIGINAL | non-blocking | yes | `[Fact]` client: stub 500 → `Failure` |
| 9 | **2xx but unparseable / malformed JSON** | client parse → `Failure` | `FellBack` | ORIGINAL | non-blocking | yes | `[Fact]` client: stub 200 with garbage body → `Failure` |
| 10 | **2xx but no `text` content block / empty `content[]`** | client parse → `Failure` | `FellBack` | ORIGINAL | non-blocking | yes | `[Fact]` client: stub 200 `{"content":[]}` / non-text block → `Failure` |
| 11 | **2xx success but transformed text null/empty/whitespace** | engine post-check | `FellBack` | ORIGINAL | non-blocking | yes | `[Fact]` engine: fake returns `Success("")`/`Success("   ")` → `FellBack` + ORIGINAL (never an empty doc for a non-empty source) |
| 12 | **2xx success with usable non-empty text** | engine | `Transformed` | the **canned transformed** markdown | `null` | yes | `[Fact]` engine: fake returns `Success("# Transformed")` → `Markdown == "# Transformed"` + `Transformed` |
| 13 | **`ILlmClient` THROWS despite its total contract** (defensive fake) | engine try/catch (defense-in-depth, mirrors `NavigationController` line 146) | `FellBack` | ORIGINAL | non-blocking | n/a | `[Fact]` engine: throwing fake → caught → `FellBack`, **no throw escapes** |
| 14 | **Oversized page** (very large `pageMarkdown`, e.g. > a documented `MaxInputChars` cap, mirroring `MarkdownFetcher.MaxBodyBytes = 8 MiB`) | engine/client total | `FellBack` (degrade/refuse-transform → pass-through original) | ORIGINAL | non-blocking | optional (refuse before send, or send via total client) | `[Fact]` engine: oversized input → total, no OOM/throw, `Markdown == ORIGINAL`, `Outcome ∈ {FellBack}` |

**Note on "non-markdown response":** because the renderer is total, ANY 2xx text is "renderable". So row 12 (`Transformed`) fires on any non-empty usable text; the engine does NOT attempt to "validate markdown" (that would be a fragile gate). The only success-vs-fallback distinction is **usable text present (row 12)** vs **absent/empty/whitespace (rows 10–11)**. This keeps the engine total and deterministic.

**Oversized-page cap (defined):** `AnthropicLlmClient` bounds its request defensively with a documented `MaxInputChars` (the dev picks a value consistent with `AnthropicOptions.MaxTokens` headroom; a sensible default mirrors the fetcher's 8 MiB intent). On exceed it MAY refuse-transform (→ `Failure("input too large")`) so the engine falls back to the original — it NEVER builds an unbounded request or OOMs. The engine path stays total regardless of the client's choice.

### No-leak assertion list (AC3 / AC5 security rigor) — the key NEVER appears anywhere in plaintext except the `x-api-key` header

The API key is read from `ISecretStore` per call and placed **only** in the outgoing request's `x-api-key` header to the configured `BaseUrl`. Pin a **sentinel key** `"sk-ant-SENTINEL"` in an in-memory `ISecretStore` and assert it appears in **NONE** of the following across BOTH the success and EVERY failure path:

1. `PersonalizationResult.Notice` — every Outcome (table rows 1–14).
2. `PersonalizationResult.Markdown` — every Outcome.
3. `LlmResult.FailureReason` — every failure row (3–11, 14).
4. Any exception message thrown anywhere (there are none that escape — but the catch-all `Failure(reason)` reasons are sentinel-scanned too).
5. Any logged / traced / `Console.Write` string the engine or client produces (the design emits none; the [Fact] still scans the surfaced outputs).
6. Any persisted file in plaintext beyond the DPAPI-encrypted blob — `DpapiSecretStore` writes ONLY `ProtectedData.Protect(...)` ciphertext to `%LOCALAPPDATA%\TheMarkdownWeb\agent.key`; the plaintext is never written in the clear.

Positive-direction assertions (the key IS sent to the provider, and ONLY there):
7. `AnthropicLlmClient` sets `x-api-key: <sentinel>` AND `anthropic-version: 2023-06-01` on the request, asserted via the stub `HttpMessageHandler`'s `LastRequest`.
8. The request `RequestUri` targets the configured provider `BaseUrl` **host** (`api.anthropic.com` by default) — asserted to be the provider host and **never** a Markdown-Web host (`themarkdownweb.com` / any `/api` server). The **GET fetcher (read, Story 3.2)** and the **POST transform (personalize)** are **distinct clients with distinct hosts** — there is no code path that POSTs page content to a Markdown-Web server for rewriting (NFR-5).
9. `DpapiSecretStore` round-trip [Fact] is **capability-guarded** (CI is `windows-latest` so DPAPI is present; the guard is defensive — if `ProtectedData` threw `PlatformNotSupportedException` the [Fact] **skips cleanly**, never fails) and **NEVER asserts on the ciphertext bytes** — it asserts only the set→get→clear→`HasApiKey` round-trip over a temp-path store, with a non-secret literal key (`"round-trip-test-key"`), never the real sentinel/Anthropic key.

### Pinned Anthropic Messages API request/response contract (provider exactness — AC1 / AC5)

`AnthropicLlmClient.CompleteAsync` issues exactly ONE outgoing request:

- **Method / URI:** `POST {AnthropicOptions.BaseUrl}/v1/messages` (default host `api.anthropic.com`).
- **Headers:** `x-api-key: <reader's key from ISecretStore>`, `anthropic-version: 2023-06-01`, `content-type: application/json`.
- **Body (JSON):** `{ "model": <AnthropicOptions.Model>, "max_tokens": <AnthropicOptions.MaxTokens>, "system": <persona.SystemPrompt as systemPrompt>, "messages": [ { "role": "user", "content": <pageMarkdown, optionally framed with ReaderContext, e.g. "Transform the following page for the reader. Return only valid GFM markdown.\n\n" + pageMarkdown> } ] }`. The page markdown MUST be present in the request body (asserted).
- **Response parse (2xx):** take the **first `content[]` block whose `type == "text"`**, return its `.text` as the transformed markdown. Empty/whitespace/missing → `Failure` (table rows 10–11).
- **Totality:** non-2xx, wrong/missing content-type or shape, empty/whitespace text, malformed JSON, `HttpRequestException`, `OperationCanceledException`, or ANY other exception → `LlmResult.Failure(reason)`. **Mirror `MarkdownFetcher`'s total try/catch shape exactly** (see `App/MarkdownFetcher.cs` lines 112–124). NEVER throws; NEVER logs the key.

**Pinned `AnthropicOptions` defaults (the working config):** `BaseUrl = "https://api.anthropic.com"`, `Model = "claude-sonnet-4-6"` (current capable Claude id; overridable `init`; SELECTION/streaming/second-provider DEFERRED), `MaxTokens = 8192`, `AnthropicVersion = "2023-06-01"`, plus a request **timeout** (the dev sets a sensible default, e.g. via the injected `HttpClient.Timeout` or a `CancellationTokenSource` linked to `ct`). The model id is verified current/correct as of this revision; CI asserts request SHAPE + success-parse, NOT the model string.

### Boundary [Fact] plan (AC6 — Agent owns net+AI; Rendering pure; App→Agent→Rendering; Agent⊀App)

Extend `clients/windows/App.Tests/DependencyBoundaryTests.cs` (where the existing `Rendering_DoesNotReference_AppOrAgent` + `App_References_Rendering` live) with TWO new csproj-`ProjectReference`-regex [Fact]s (plain reflection/csproj checks, no STA), mirroring the existing `App_References_Rendering` elision-proof csproj pattern (the C# compiler elides an unused metadata reference, so assert the build-time `ProjectReference`, not the bound assembly closure):

- **`Agent_DoesNotReference_App`** — the `Agent` csproj declares NO `<ProjectReference>` to `TheMarkdownWeb.App.csproj` (Agent must not depend "up"; it references `Rendering` only, if anything).
- **`App_References_Agent`** — the `App` csproj declares a `<ProjectReference>` to `TheMarkdownWeb.Agent.csproj` (App composes the engine).

Inherited guards STAY GREEN (no edit, re-confirm only):
- **`RenderingPurityTests`** — `Rendering` adds NO package/net/AI/webview substring; its forbidden set `{System.Net.Http, HttpClient, OpenAI, Anthropic, Azure.AI, WebView, CefSharp}` is scoped to the **Rendering csproj XML only** — Agent's `System.Net.Http`/`Anthropic` usage does NOT false-positive it (different assembly/csproj). `Rendering_DoesNotReference_SystemNetHttp` asserts `System.Net.Http` is absent from `Rendering`'s referenced assemblies — Agent gaining `System.Net.Http` is in a different assembly and does not trip it.
- **`NoEmbeddedBrowserTests`** — its forbidden substrings are `{webview, webview2, cefsharp, chromium, chromely, libcef, cef., cef3, xulrunner, geckofx, awesomium, electron}`. Confirmed: **no false positive** on `System.Net.Http`, `AnthropicLlmClient`, `anthropic-version`, or `Anthropic` — none of those contain a forbidden substring. An LLM HTTP client is not a browser engine. Agent's net additions + `Agent.Tests` add no forbidden substring to ANY csproj.

### Seam testability + re-entrancy (AC2 — last-wins preserved across a slow personalize)

The `PersonalizationGateway` insertion at the render-sink MUST preserve `NavigationController`'s last-wins re-entrancy: a slow `PersonalizeAsync` superseded by a newer navigation must **never** render stale. Pin the mechanism: the gateway call is **awaited INSIDE the existing guarded `RunAsync` path** and the generation token (`_generation`/`myGen`) is **re-checked AFTER the awaited gateway resolves**, before `_renderSink(...)/ShowMarkdown(...)` — exactly as `RunAsync` already drops a stale fetch completion (`if (myGen != _generation) return;`, `NavigationController.cs` lines 152–156). Two equivalent placements (decide & document): (a) make the render sink async-aware so it resolves through the gateway inside the same generation, with the post-await generation re-check; or (b) resolve inside `FetchEndpointAsync`/an adapter so the agent output already rides on `result.Markdown` when it reaches the existing guarded sink (the existing line-153 guard then covers it). Either way the gateway shares the navigation generation and a superseded result is dropped. At 4.1 the selector is the **constant `Persona.Basic`** (pass-through) so `ResolveMarkdownAsync` returns the fetched markdown unchanged → the render is **byte-identical to the Epic-3 render** — assert this (no regression; the seam is additive).

## Tasks / Subtasks

- [ ] **Task 1 — Grow the `Agent` module: `ILlmClient` + `LlmResult`, `ISecretStore`, `Persona`, `ReaderContext`, `PersonalizationResult`/`Outcome` (AC: 1, 3, 4)**
  - [ ] Add `clients/windows/Agent/LlmResult.cs` — `public readonly record struct LlmResult { bool IsSuccess; string? Markdown; string? FailureReason; static Success(string); static Failure(string); }` (mirror `App.FetchResult`). [Source: AC1; App/MarkdownFetcher.cs FetchResult]
  - [ ] Add `clients/windows/Agent/ILlmClient.cs` — `Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct)`; document the TOTAL contract (never throws). [Source: AC1, AC4]
  - [ ] Add `clients/windows/Agent/ISecretStore.cs` — `string? GetApiKey(); void SetApiKey(string key); void ClearApiKey(); bool HasApiKey { get; }`; document "never logs the key". [Source: AC1, AC3]
  - [ ] Add `clients/windows/Agent/Persona.cs` — the persona type + a static `Persona.Basic` (`Id="basic"`, `DisplayName="Basic"`, `IsPassThrough=true`, empty `SystemPrompt`). The ONLY seed persona at 4.1. [Source: AC1; architecture-epic4-agent.md D2 seed set — only Basic in 4.1]
  - [ ] Add `clients/windows/Agent/ReaderContext.cs` — minimal-but-real reader context (`string? PreferredLanguage`, `string? PageUrl`); extensible. [Source: AC1, AC2]
  - [ ] Add `clients/windows/Agent/PersonalizationResult.cs` — `public readonly record struct PersonalizationResult { string Markdown; PersonalizationOutcome Outcome; string? Notice; }` + `public enum PersonalizationOutcome { Transformed, PassThrough, NeedsKey, FellBack }`. `Markdown` is ALWAYS a renderable string. [Source: AC1, AC4]
  - [ ] **`[Fact]` AC1:** the `Persona.Basic` constants; `LlmResult`/`PersonalizationResult` factory + default contracts (no WPF type → `[Fact]`, in `Agent.Tests`). [Source: AC1]

- [ ] **Task 2 — `PersonalityEngine` (total) (AC: 1, 3, 4)**
  - [ ] Add `clients/windows/Agent/PersonalityEngine.cs` — `PersonalityEngine(ILlmClient, ISecretStore)`; `Task<PersonalizationResult> PersonalizeAsync(string pageMarkdown, Persona persona, ReaderContext readerContext, CancellationToken ct)`. Logic: if `persona.IsPassThrough` → return `(pageMarkdown, PassThrough, null)` with NO LLM call; else if `!secretStore.HasApiKey` → return `(pageMarkdown, NeedsKey, "<add-your-key notice, no key in it>")` with NO LLM call; else call `llmClient.CompleteAsync(persona.SystemPrompt, pageMarkdown, readerContext, ct)` inside try/catch → on `IsSuccess` with non-empty markdown return `(transformed, Transformed, null)`, else (`Failure`, throw, cancel, null/empty transformed) return `(pageMarkdown, FellBack, "<non-blocking notice, key-free>")`. **NEVER throws.** [Source: AC1, AC3, AC4]
  - [ ] **`[Fact]` AC1:** pass-through (`Persona.Basic`) returns original markdown + `PassThrough`, fake `ILlmClient` asserted never called. [Source: AC1]
  - [ ] **`[Fact]` AC1:** non-pass-through persona + fake returning canned transformed markdown → `Markdown == <canned>` + `Transformed`. [Source: AC1]
  - [ ] **`[Fact]` AC3:** non-pass-through persona + in-memory fake store with NO key → `NeedsKey` + original markdown + notice; fake `ILlmClient` asserted never called. [Source: AC3]
  - [ ] **`[Theory]`/`[Fact]` AC4:** failing fake / throwing fake / null-or-empty-returning fake / pre-cancelled token / oversized input → each returns total: no exception escapes, `Markdown == <original>`, `Outcome ∈ {NeedsKey, FellBack}`. [Source: AC4]
  - [ ] **`[Fact]` AC3 (no leak):** with an in-memory store holding sentinel key `"sk-ant-SENTINEL"`, assert that string appears in NO surfaced `Notice`/`Markdown`/`FailureReason` across success + failure paths. [Source: AC3]

- [ ] **Task 3 — `AnthropicLlmClient` (real provider call, total, injectable HttpClient) (AC: 1, 4, 5)**
  - [ ] Add `clients/windows/Agent/AnthropicOptions.cs` — `string BaseUrl = "https://api.anthropic.com"`, `string Model = "claude-sonnet-4-6"` (current capable Claude id; overridable `init`), `int MaxTokens = 8192`, `string AnthropicVersion = "2023-06-01"` (additive `init` props; defaults ARE the working config). [Source: AC1, AC5; Dev Notes "Default model"]
  - [ ] Add `clients/windows/Agent/AnthropicLlmClient.cs` — `AnthropicLlmClient(HttpClient http, ISecretStore secretStore, AnthropicOptions? options = null)` implementing `ILlmClient`. `CompleteAsync`: if no key → `LlmResult.Failure("no key")`; **else if `pageMarkdown.Length` > the documented `MaxInputChars` cap → `Failure("input too large")` (oversized degrade — table row 14; mirror `MarkdownFetcher.MaxBodyBytes` intent);** else build `HttpRequestMessage(POST, {BaseUrl}/v1/messages)` with `x-api-key`=key, `anthropic-version: 2023-06-01`, `content-type: application/json`, JSON body `{ model, max_tokens, system:<systemPrompt>, messages:[{role:"user", content:<pageMarkdown framed with ReaderContext>}] }`; send over the injected `HttpClient`; on 2xx parse the **first `content[].type=="text"` `.text`** as markdown (empty/whitespace/missing → `Failure`) → `Success`; everything else (non-2xx incl. 401/403/429/500, wrong/empty content-type, malformed JSON, `HttpRequestException`, `OperationCanceledException`, any exception) → `Failure(reason)`. **NEVER throws; NEVER logs the key; the key rides ONLY the `x-api-key` header.** Mirror `MarkdownFetcher`'s total try/catch shape (App/MarkdownFetcher.cs lines 112–124). [Source: AC1, AC4, AC5; the pinned Anthropic contract; App/MarkdownFetcher.cs]
  - [ ] **`[Fact]` AC5 (request shape):** stub `HttpMessageHandler` (reuse the `MarkdownFetcherTests.StubHandler` pattern; copy into `Agent.Tests`) — with a fake store holding a key, `CompleteAsync` issues ONE POST to `{BaseUrl}/v1/messages`, carrying `x-api-key`==key + `anthropic-version`; the body contains the page markdown. Assert `LastRequest.RequestUri.Host` is the provider host, never a Markdown-Web host. No real socket. [Source: AC5]
  - [ ] **`[Fact]` AC5 (success parse):** stub returns a canned Anthropic 200 (`{"content":[{"type":"text","text":"# Transformed"}]}`) → `LlmResult.Success` with `Markdown == "# Transformed"`. [Source: AC1, AC5]
  - [ ] **`[Theory]`/`[Fact]` AC4 (totality):** stub 500 / wrong content-type / `throw HttpRequestException` / pre-cancelled token / no-key-in-store → each `LlmResult.Failure` (no throw). [Source: AC4]
  - [ ] **`[Fact]` AC3 (no leak):** assert the sentinel key appears in NO `FailureReason` produced across the failure paths. [Source: AC3, AC5]

- [ ] **Task 4 — `DpapiSecretStore` (real Windows DPAPI key store) (AC: 1, 3)**
  - [ ] Add `clients/windows/Agent/DpapiSecretStore.cs` — `ISecretStore` over `System.Security.Cryptography.ProtectedData.Protect/Unprotect` (`DataProtectionScope.CurrentUser`), persisting encrypted bytes to a per-user file (e.g. `%LOCALAPPDATA%\TheMarkdownWeb\agent.key`; ctor accepts an optional path override for the CI smoke test → a temp dir). `GetApiKey` returns null when absent/undecryptable (total); `SetApiKey` writes encrypted; `ClearApiKey` deletes; `HasApiKey` reflects file presence. **Never logs plaintext or ciphertext.** [Source: AC1, AC3; architecture-epic4-agent.md D1 key storage]
  - [ ] **`[Fact]`/`[StaFact]` AC3 (round-trip smoke):** over a temp-path `DpapiSecretStore`: `SetApiKey("k")` → `GetApiKey()=="k"` → `HasApiKey` true → `ClearApiKey()` → `GetApiKey()==null` + `HasApiKey` false. Do NOT assert on ciphertext bytes. **Guard for runner DPAPI availability** (see Dev Notes "DpapiSecretStore CI smoke test"): if `ProtectedData` is unavailable, the round-trip test is skipped via a capability check, never failed. No real key persisted to the real app-data path. [Source: AC3]

- [ ] **Task 5 — The App render-time seam: `PersonalizationGateway` + wire it into the render path (AC: 2)**
  - [ ] Add `clients/windows/App/PersonalizationGateway.cs` — `PersonalizationGateway(PersonalityEngine engine, Func<Persona> selectedPersona)` (or hold the selected persona directly; at 4.1 always `Persona.Basic`); `Task<string> ResolveMarkdownAsync(string fetchedMarkdown, Uri pageUrl, CancellationToken ct)` builds a `ReaderContext` (`PageUrl = pageUrl.ToString()`, default `PreferredLanguage`), calls `engine.PersonalizeAsync(fetchedMarkdown, selectedPersona, ctx, ct)`, surfaces `result.Notice` non-blockingly (a hook the UI/4.2 can show; at 4.1 may be a no-op/stored property — do NOT block), returns `result.Markdown`. Total (returns the engine's always-renderable markdown). [Source: AC2]
  - [ ] Wire it in `clients/windows/App/MainWindow.xaml.cs`: compose `new AnthropicLlmClient(SharedHttpClient-or-its-own-client, new DpapiSecretStore())`, `new PersonalityEngine(llmClient, secretStore)`, `new PersonalizationGateway(engine, () => Persona.Basic)`. Update the render sink so the fetched markdown is resolved through the gateway before `ShowMarkdown` — e.g. the `NavigationController` render sink (line 49) becomes async-aware via the gateway, OR `FetchEndpointAsync`/the sink is adapted so `result.Markdown` flows through `ResolveMarkdownAsync` → `_contentHost.ShowMarkdown(resolved, pageUrl)`. **Keep `NavigationController`'s last-wins re-entrancy intact** (the gateway call is part of the same generation; a stale result is still dropped). `ContentHostController`/`FlowDocumentRenderer` UNCHANGED. [Source: AC2; MainWindow.xaml.cs lines 40–53; NavigationController.cs line 185]
  - [ ] **`[Fact]` AC2 (gateway pass-through):** over `PersonalizationGateway` with `Persona.Basic` + a fake `ILlmClient` → `ResolveMarkdownAsync("# H\n\npara", uri, ct) == "# H\n\npara"`, fake LLM never called. [Source: AC2]
  - [ ] **`[Fact]` AC2 (gateway transform):** with a non-pass-through persona + a fake returning canned markdown → returns the canned markdown. [Source: AC2]
  - [ ] **`[StaFact]` AC2 (App renders the gateway output):** in `App.Tests`, construct a `ContentHostController` over a real `new FlowDocumentRenderer()` into a `FlowDocumentScrollViewer`; drive a fetched markdown through a `PersonalizationGateway` (Basic) → `ShowMarkdown(resolved, uri)`; assert `ContentScroll.Document` is a non-null `FlowDocument` with ≥1 block whose text is non-empty and reflects the original markdown (Basic pass-through). Construct-not-Show; no pixels. [Source: AC2; ContentHostTests patterns]

- [ ] **Task 6 — `Agent.Tests` project + `.sln`/`ProjectReference` wiring (AC: 7)**
  - [ ] Create `clients/windows/Agent.Tests/TheMarkdownWeb.Agent.Tests.csproj` mirroring `App.Tests`/`Rendering.Tests` (`net10.0-windows`, `UseWPF` not required unless a WPF type is touched — Agent.Tests touch none, so omit `UseWPF` or keep parity; `IsTestProject`; the same test-SDK/xunit/StaFact versions; a `ProjectReference` to `..\Agent\TheMarkdownWeb.Agent.csproj`). [Source: AC7]
  - [ ] Add `clients/windows/Agent.Tests/AssemblyInfo.cs` with `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (mirror App.Tests/AssemblyInfo.cs). [Source: AC7]
  - [ ] Add `clients/windows/Agent/TheMarkdownWeb.Agent.csproj` net usage: `System.Net.Http`/`ProtectedData` are in-box for `net10.0-windows` → likely NO new `PackageReference`. If `ProtectedData` needs `System.Security.Cryptography.ProtectedData` as a package on net10, add it ONLY here. Decide & document. Confirm the existing `ProjectReference` to `Rendering` stays (or is removed if unused — Agent returns a markdown string; Rendering ref is optional). NEVER reference `App`. [Source: AC1, AC6]
  - [ ] Add a `ProjectReference` from `clients/windows/App/TheMarkdownWeb.App.csproj` to `..\Agent\TheMarkdownWeb.Agent.csproj` (App composes the engine). [Source: AC2, AC6]
  - [ ] Add `Agent.Tests` to `clients/windows/TheMarkdownWeb.sln` (a new `Project(...)` entry + the four `GlobalSection(ProjectConfigurationPlatforms)` lines, mirroring the existing test-project entries; a fresh GUID). [Source: AC7]
  - [ ] Confirm `build-windows.yml` is UNCHANGED — the `paths: clients/windows/**` filter already covers `Agent.Tests`; the `.sln` add brings it into restore/build/test. [Source: AC7]

- [ ] **Task 7 — Purity / boundary / no-webview re-confirm + extend (AC: 6)**
  - [ ] Confirm `Rendering` gains NO new `PackageReference` (still `{Markdig, ColorCode.Core}`), no `System.Net.*`/AI/webview, no App/Agent ref — `RenderingPurityTests` (allowlist + forbidden-substring incl. `Anthropic`/`OpenAI`/`WebView`) stays green. [Source: AC6]
  - [ ] Extend `clients/windows/App.Tests/DependencyBoundaryTests.cs` with **`Agent_DoesNotReference_App`** (the `Agent` csproj has NO `ProjectReference` to `App`) and **`App_References_Agent`** (the `App` csproj declares a `ProjectReference` to `Agent`), mirroring the existing csproj-`ProjectReference` regex checks. Keep the existing `Rendering_DoesNotReference_AppOrAgent` + `App_References_Rendering` green. [Source: AC6; DependencyBoundaryTests.cs]
  - [ ] Confirm `NoEmbeddedBrowserTests` (csproj glob across `clients/windows/**`) stays green — no `webview`/`cef`/`chromium`/… substring added to ANY csproj (Agent net additions, Agent.Tests). An LLM HTTP client is not a browser. [Source: AC6]

- [ ] **Task 8 — STA / no-parallel discipline + final verification against ACs (Definition of Done — checked via CI, not locally) (AC: 1–7)**
  - [ ] Confirm: engine/persona/keys/LLM-client tests are pure `[Fact]` (no `DispatcherObject`); ONLY AC2's App-seam `MainWindow`/`FlowDocument` test is `[StaFact]`; `Xunit.StaFact` + `DisableTestParallelization` present in every test project (added new to `Agent.Tests`). No shown `Window`/`Dispatcher` pump/socket/real `Process.Start`/real secret in logs/pixels/timing. [Source: AC7]
  - [ ] **AC1:** the Agent module surface (`ILlmClient`/`AnthropicLlmClient`, `ISecretStore`/`DpapiSecretStore`, `PersonalityEngine`, `Persona.Basic`, pass-through) — proven by the `Agent.Tests` `[Fact]`s.
  - [ ] **AC2:** the App render-time seam (fetch → `PersonalityEngine.PersonalizeAsync` → `FlowDocumentRenderer`) — proven by the gateway `[Fact]`s + the App-seam `[StaFact]`.
  - [ ] **AC3:** BYO-key DPAPI storage (round-trip, never logged) + missing-key `NeedsKey` outcome — proven by the `DpapiSecretStore` smoke `[Fact]` + the `NeedsKey`/no-leak `[Fact]`s.
  - [ ] **AC4:** graceful totality / fallback-to-original on every failure (no key, fail, throw, cancel, null/empty, oversized) — proven by the totality theory + the `AnthropicLlmClient` stub-handler totality `[Fact]`s.
  - [ ] **AC5:** no server-side rewrite — the transform is a client→provider POST to `{BaseUrl}/v1/messages` carrying the reader's key; never a Markdown-Web host — proven by the stub-handler request-shape `[Fact]`s.
  - [ ] **AC6:** Rendering pure + App→Agent→(API) boundary (Agent owns net+AI, no up-ref) — inherited purity/boundary/no-webview guards green + the new `Agent_DoesNotReference_App`/`App_References_Agent` `[Fact]`s.
  - [ ] **AC7:** `dotnet build -c Release` clean + `dotnet test -c Release` all green on `windows-latest` — incl. EVERY existing 3.x test UNCHANGED + the new `Agent.Tests` + the new App-seam/boundary tests. PENDING CI run (the sole verification surface; not pushed by this agent).
  - [ ] Push and confirm the `Build Windows Client` GitHub Actions run is green (the authoritative verification — there is no local build). Record the run id in the Dev Agent Record. **Watch specifically that NO existing 3.x test regressed** (the seam is additive — Basic pass-through is byte-identical to the Epic-3 render).

## Dev Notes

### This is the Epic-4 FOUNDATION — what 4.1 builds vs defers

**IN (the agent foundation + the App seam + BYO-key + pass-through):** the `Agent` module surface (`ILlmClient` + real `AnthropicLlmClient`; `ISecretStore` + real `DpapiSecretStore`; `PersonalityEngine`; `Persona` with ONLY `Persona.Basic`; `ReaderContext`; `PersonalizationResult`/`Outcome`); the App render-time seam (`PersonalizationGateway` wired between fetch and `FlowDocumentRenderer`); BYO-key DPAPI storage (never logged) + missing-key handling; graceful totality/fallback on every failure; the no-server-side-rewrite (NFR-5) proof; the boundary (Agent owns net+AI, Rendering pure, App→Agent→Rendering); the `Agent.Tests` project + CI gate. Proven end-to-end with a FAKE `ILlmClient`.

**OUT (Epic-4 4.2/4.3/4.4 / Deferred-Work-Log):** the personality-SELECTOR UI (4.2 — the toolbar chip + re-render-in-place without re-fetch); multiple distinct personas producing structurally-different renders (4.3 — Cozy Reader / Terminal / TL;DR / Plain Language etc.); translation + audio/SAPI (4.4); a second provider (OpenAI) + provider abstraction beyond Anthropic; the local/offline model path (Ollama); streaming token-by-token render; caching transformed renders per (page, persona, model); a key-entry UX surface beyond the `ISecretStore` plumbing (4.2 owns the key-entry chip; 4.1 ships the store + a clear `NeedsKey` outcome). 4.1 ships exactly ONE persona (Basic, pass-through) so the integration is provable without persona variety.

### Pinned Agent API surface (the contract for Step-4 TDD + Step-5 impl)

All in namespace `TheMarkdownWeb.Agent`. The Step-4 oracle tests and the Step-5 impl implement these verbatim. (`App.FetchResult` is the shape model for the result records.)

```csharp
// LlmResult.cs — mirrors App.FetchResult
public readonly record struct LlmResult
{
    public bool IsSuccess { get; }
    public string? Markdown { get; }       // transformed markdown on success; null on failure
    public string? FailureReason { get; }  // non-empty on failure; null on success (NEVER contains the key)
    public static LlmResult Success(string markdown);
    public static LlmResult Failure(string reason);
}

// ILlmClient.cs — the provider-call seam. TOTAL: never throws.
public interface ILlmClient
{
    Task<LlmResult> CompleteAsync(
        string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct);
}

// ISecretStore.cs — BYO-key storage. Never logs the key.
public interface ISecretStore
{
    bool HasApiKey { get; }
    string? GetApiKey();
    void SetApiKey(string key);
    void ClearApiKey();
}

// Persona.cs — a named persona; 4.1 ships ONLY Persona.Basic (pass-through).
public sealed record Persona(string Id, string DisplayName, string SystemPrompt, bool IsPassThrough)
{
    public static readonly Persona Basic = new("basic", "Basic", "", IsPassThrough: true);
}

// ReaderContext.cs — minimal-but-real, extensible.
public readonly record struct ReaderContext(string? PageUrl, string? PreferredLanguage);

// PersonalizationResult.cs — Markdown is ALWAYS renderable.
public enum PersonalizationOutcome { Transformed, PassThrough, NeedsKey, FellBack }
public readonly record struct PersonalizationResult(string Markdown, PersonalizationOutcome Outcome, string? Notice);

// PersonalityEngine.cs — TOTAL: never throws.
public sealed class PersonalityEngine
{
    public PersonalityEngine(ILlmClient llmClient, ISecretStore secretStore);
    public Task<PersonalizationResult> PersonalizeAsync(
        string pageMarkdown, Persona persona, ReaderContext readerContext, CancellationToken ct);
}

// AnthropicOptions.cs — defaults ARE the working config.
public sealed record AnthropicOptions
{
    public string BaseUrl { get; init; } = "https://api.anthropic.com";
    public string Model { get; init; } = "claude-sonnet-4-6"; // current capable Claude id; overridable; CI asserts request SHAPE not the model string. Model SELECTION deferred (4.x / Deferred-Work-Log).
    public int MaxTokens { get; init; } = 8192;
    public string AnthropicVersion { get; init; } = "2023-06-01";
}

// AnthropicLlmClient.cs — real provider call over an injectable HttpClient. TOTAL: never throws; never logs the key.
public sealed class AnthropicLlmClient : ILlmClient
{
    public AnthropicLlmClient(HttpClient http, ISecretStore secretStore, AnthropicOptions? options = null);
    public Task<LlmResult> CompleteAsync(string systemPrompt, string pageMarkdown, ReaderContext readerContext, CancellationToken ct);
}

// DpapiSecretStore.cs — real Windows DPAPI store. Never logs plaintext/ciphertext.
public sealed class DpapiSecretStore : ISecretStore
{
    public DpapiSecretStore(string? keyFilePath = null); // default: %LOCALAPPDATA%\TheMarkdownWeb\agent.key
    // ISecretStore members; Protect/Unprotect with DataProtectionScope.CurrentUser.
}
```

```csharp
// App/PersonalizationGateway.cs — the App render-time seam.
namespace TheMarkdownWeb.App;
public sealed class PersonalizationGateway
{
    public PersonalizationGateway(PersonalityEngine engine, Func<Persona> selectedPersona);
    // Total: returns the engine's always-renderable Markdown; surfaces Notice non-blockingly.
    public Task<string> ResolveMarkdownAsync(string fetchedMarkdown, Uri pageUrl, CancellationToken ct);
}
```

### Anthropic Messages API call shape (the AnthropicLlmClient contract)

Verified against the Anthropic Messages API. `AnthropicLlmClient.CompleteAsync` issues:

- **Method/URI:** `POST {AnthropicOptions.BaseUrl}/v1/messages` (default host `api.anthropic.com`).
- **Headers:** `x-api-key: <reader's key from ISecretStore>`, `anthropic-version: 2023-06-01`, `content-type: application/json`.
- **Body:** `{ "model": <Model>, "max_tokens": <MaxTokens>, "system": <persona.SystemPrompt as systemPrompt>, "messages": [ { "role": "user", "content": <pageMarkdown, optionally framed with ReaderContext (e.g. "Transform the following page for the reader. Return only valid GFM markdown.\n\n" + pageMarkdown)> } ] }`.
- **Response (2xx):** `{ "content": [ { "type": "text", "text": "<transformed markdown>" }, ... ], ... }` → take the first `text` block as the transformed markdown.
- **Totality:** non-2xx, wrong/missing content-type or shape, empty/whitespace text, malformed JSON, `HttpRequestException`, `OperationCanceledException`, or any other exception → `LlmResult.Failure(reason)`. **Mirror `MarkdownFetcher`'s try/catch totality shape exactly.**

### Default model (the pinned `AnthropicOptions.Model`)

Per architecture-epic4-agent.md D1, the default provider/model is Anthropic **Claude** (latest capable model). The reader pays, so default to a strong general Claude model and allow model selection later (deferred).

**PINNED default: `AnthropicOptions.Model = "claude-sonnet-4-6"`.** Rationale: a current capable Claude model chosen for the cost/latency balance D1 calls for (the reader pays per call; Sonnet-tier is the quality/cost sweet spot vs Opus-tier for a per-page markdown→markdown transform). Verified current/correct as of this revision (a real, active Claude model id). It is an **overridable `init` default** — a reader/host may set `AnthropicOptions.Model` to any other current Claude id (e.g. `claude-opus-4-8` for max quality) without code change. Model **SELECTION** (a settings UI / selector), **streaming**, and a **second provider** are explicitly **DEFERRED** (later Epic-4 story / Deferred-Work-Log); 4.1 pins exactly ONE default. The CI tests assert the request **SHAPE** (URI `{BaseUrl}/v1/messages`, POST, headers `x-api-key`/`anthropic-version`, body-contains-the-page-markdown) and the success-parse (first `content[].type=="text"` `.text`), **NOT** a specific model string — so the dev retains latitude on the exact id as long as it is a real current Claude id at impl time.

### App seam — how fetch→agent→render wires (the precise insertion point)

`MainWindow.xaml.cs` (3.5) builds: `MarkdownFetcher` → `NavigationController(FetchEndpointAsync, renderSink, onBroken, launcher)` where `renderSink = (markdown, pageUrl) => _contentHost.ShowMarkdown(markdown, pageUrl)` (line 49). 4.1 inserts the agent **between the fetched markdown and `ShowMarkdown`**:

- **Compose once** in the `MainWindow` ctor: `var secretStore = new DpapiSecretStore(); var llmClient = new AnthropicLlmClient(SharedHttpClient, secretStore); var engine = new PersonalityEngine(llmClient, secretStore); _gateway = new PersonalizationGateway(engine, () => Persona.Basic);` (the `() => Persona.Basic` selector becomes the 4.2 chip later — at 4.1 it is constant Basic).
- **Wrap the render path** so the markdown reaching `ShowMarkdown` is `await _gateway.ResolveMarkdownAsync(fetchedMarkdown, pageUrl, ct)`. Two equivalent placements (decide & document): (a) make the `NavigationController` render sink async-aware (the sink resolves through the gateway before `ShowMarkdown`), keeping last-wins (the gateway call shares the navigation generation, so a stale result is still dropped); or (b) resolve inside `FetchEndpointAsync`/an adapter so `result.Markdown` already carries the agent output when it reaches the existing sink. **Preserve `NavigationController`'s last-wins re-entrancy** (a superseded navigation's gateway result must not render). **`ContentHostController.ShowMarkdown` and `FlowDocumentRenderer.Render` are UNCHANGED** — they host/render whatever markdown string they get.
- At 4.1, Basic pass-through means `ResolveMarkdownAsync` returns the fetched markdown unchanged → the render is byte-identical to Epic-3. The seam exists and is tested; persona variety (4.3) and the selector (4.2) layer on top with no rework.

### BYO-key security/privacy (the testable invariants)

- **Stored encrypted, per-user, locally:** `DpapiSecretStore` uses `ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser`, persisting ciphertext to `%LOCALAPPDATA%\TheMarkdownWeb\agent.key`. The plaintext key is never written to disk in the clear; the ciphertext is opaque (CurrentUser-scoped — only the same Windows user can decrypt).
- **Never logged:** no `Console.Write`/trace/exception carries the key. The no-leak `[Fact]` asserts a sentinel key appears in NO `Notice`/`Markdown`/`FailureReason`. The key is placed ONLY in the request `x-api-key` header (AC5).
- **Sent only to the provider over TLS:** `AnthropicLlmClient` targets `{AnthropicOptions.BaseUrl}/v1/messages` (https in prod). The stub-handler `[Fact]` asserts the request URI host is the provider host (never a Markdown-Web host) and the key rides only on that request — making "the key is sent only to the provider" inspectable without a real call.
- **Missing key → clear outcome:** `PersonalityEngine` returns `Outcome == NeedsKey` + the original markdown + a non-blocking notice, with NO LLM call — never a crash. (The key-entry UX is 4.2; 4.1 ships the store + the `NeedsKey` outcome the UX will surface.)

### DpapiSecretStore CI smoke test (Windows-runtime-only, no asserting on ciphertext)

`ProtectedData` (`System.Security.Cryptography.ProtectedData`) is Windows-only and present on `windows-latest`. The smoke `[Fact]` round-trips set→get→clear over a **temp-path** `DpapiSecretStore` (ctor path override → a `Path.GetTempPath()` subdir), so the real `%LOCALAPPDATA%` app-data path is never touched and no real key persists. It asserts the round-trip (`GetApiKey()` returns what `SetApiKey` stored) and `HasApiKey`/`ClearApiKey` semantics — it does NOT assert on the encrypted bytes. **Guard for capability:** wrap the `Protect` call's availability in a try/catch or a platform check; if DPAPI throws `PlatformNotSupportedException` (it won't on windows-latest, but be defensive), the test is skipped (e.g. `Assert.True(dpapiAvailable)` short-circuit / `Skip`), never a hard failure. No real Anthropic key is ever used; the round-trip key is a non-secret literal like `"round-trip-test-key"`.

### Totality / determinism (the engine + client never throw to the UI)

Every failure path is total (D4). The engine wraps the `ILlmClient` call in try/catch (defense-in-depth even though `ILlmClient` is contractually total — mirroring `NavigationController`'s defensive `catch` at line 146). The client wraps its HTTP in the same total try/catch as `MarkdownFetcher` (OperationCanceledException / HttpRequestException / catch-all → `Failure`). On ANY failure the engine returns the ORIGINAL markdown (the Basic render the reader already had), so the UI always gets a renderable document — the worst case is "you see the faithful Epic-3 render plus a non-blocking notice", never a crash or an empty page. A success that returns null/empty/whitespace transformed markdown is treated as a fallback (don't render an empty doc for a non-empty source).

### Critical constraints (do not violate)

- **`Rendering` stays PURE.** No new package; no `System.Net.*`/socket/AI/LLM SDK/webview/Chromium; no App/Agent ref. `RenderingPurityTests` (allowlist `{Markdig, ColorCode.Core}` + forbidden-substring incl. `Anthropic`/`OpenAI`/`WebView`) + `DependencyBoundaryTests` + `NoEmbeddedBrowserTests` stay green. **The Agent is the ONLY module with networking + AI.**
- **`Agent` must not depend "up" on `App`.** Agent references `Rendering` (optional, existing) at most; NEVER `App`. App references Agent (new `ProjectReference`). New `Agent_DoesNotReference_App` + `App_References_Agent` boundary `[Fact]`s lock this.
- **No real network/key/socket in CI.** `ILlmClient`/`ISecretStore` injected + FAKED; the real `AnthropicLlmClient` exercised only via a stub `HttpMessageHandler` (no socket); the real `DpapiSecretStore` smoke-tested over a temp path. NEVER a real Anthropic key in a test/log.
- **Totality.** The engine + client never throw; every failure → original-markdown fallback + a non-blocking, KEY-FREE notice. The UI never crashes and never sees the key.
- **Windows-only verification, STA + no-parallel.** No .NET SDK on Linux; WPF Windows-only; headless runner. Engine/persona/keys/LLM-client tests = pure `[Fact]`; ONLY the App-seam `MainWindow`/`FlowDocument` test = `[StaFact]`. STA package + `DisableTestParallelization` added new to `Agent.Tests`; already present in `App.Tests`/`Rendering.Tests` — do NOT re-add there. No shown `Window`/`Dispatcher` pump/socket/real `Process.Start`/real secret in logs/pixels/timing.
- **Scope: the agent foundation + the App seam + BYO-key + Basic pass-through ONLY.** NO selector UI (4.2), NO persona variety / structural renders (4.3), NO translation/audio (4.4), NO second provider / Ollama / streaming / caching. ONE persona (Basic).

### Source tree components to touch

- `clients/windows/Agent/LlmResult.cs` — NEW: the LLM result record (mirror `App.FetchResult`).
- `clients/windows/Agent/ILlmClient.cs` — NEW: the provider-call seam (total).
- `clients/windows/Agent/ISecretStore.cs` — NEW: BYO-key store interface (never logs the key).
- `clients/windows/Agent/Persona.cs` — NEW: the persona type + `Persona.Basic` (the only 4.1 persona).
- `clients/windows/Agent/ReaderContext.cs` — NEW: minimal-but-real reader context.
- `clients/windows/Agent/PersonalizationResult.cs` — NEW: the result record + `PersonalizationOutcome` enum.
- `clients/windows/Agent/PersonalityEngine.cs` — NEW: the total engine.
- `clients/windows/Agent/AnthropicOptions.cs` — NEW: provider/model/version/max-tokens config (defaults ARE the working config).
- `clients/windows/Agent/AnthropicLlmClient.cs` — NEW: the real provider call (injectable HttpClient, total, never logs the key).
- `clients/windows/Agent/DpapiSecretStore.cs` — NEW: the real Windows DPAPI key store.
- `clients/windows/Agent/IPersonality.cs` — EXISTING stub: leave or fold into `Persona` (decide & document — `IPersonality` may be superseded by `Persona`; if removed, ensure nothing references it).
- `clients/windows/Agent/TheMarkdownWeb.Agent.csproj` — UPDATED: confirm net usage (in-box `System.Net.Http`/`ProtectedData` → likely no new package); keep the `Rendering` ProjectReference (optional) or remove if unused; NEVER reference App.
- `clients/windows/App/PersonalizationGateway.cs` — NEW: the App render-time seam.
- `clients/windows/App/MainWindow.xaml.cs` — UPDATED: compose engine + gateway; resolve fetched markdown through the gateway before `ShowMarkdown` (preserve last-wins).
- `clients/windows/App/TheMarkdownWeb.App.csproj` — UPDATED: add a `ProjectReference` to `Agent`.
- `clients/windows/Agent.Tests/TheMarkdownWeb.Agent.Tests.csproj` — NEW: the Agent test project (mirror App.Tests).
- `clients/windows/Agent.Tests/AssemblyInfo.cs` — NEW: `DisableTestParallelization`.
- `clients/windows/Agent.Tests/*.cs` — NEW: the engine/persona/keys/LLM-client `[Fact]`s + the `StubHandler` (copy from `MarkdownFetcherTests`) + the `DpapiSecretStore` smoke test.
- `clients/windows/App.Tests/DependencyBoundaryTests.cs` — UPDATED: add `Agent_DoesNotReference_App` + `App_References_Agent`.
- `clients/windows/App.Tests/*.cs` — NEW: the AC2 App-seam `[StaFact]` (gateway → ShowMarkdown → FlowDocument) + the gateway pass-through/transform `[Fact]`s (gateway is in App).
- `clients/windows/TheMarkdownWeb.sln` — UPDATED: add the `Agent.Tests` project entry + config lines (new GUID).
- Do NOT touch: any EXISTING 3.x `Rendering.Tests`/`App.Tests` assertion (the seam is additive — Basic is byte-identical), `Rendering/FlowDocumentRenderer.cs` / `FlowDocumentRenderOptions.cs` / `RenderTheme.cs`, `App/ContentHostController.cs` / `NavigationController.cs` internals (only the render-sink wiring in `MainWindow.xaml.cs` changes), `build-windows.yml`, `api/*`, the STA packages in existing test projects.

### Testing standards summary

- **Framework:** xUnit (existing versions: `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2, `Xunit.StaFact` 1.1.11) — `Agent.Tests` mirrors these exactly. **STA:** `[StaFact]` ONLY for the AC2 App-seam test that constructs `MainWindow`/reads a hosted `FlowDocument`. **`[Fact]`** for everything else (engine/persona/keys/LLM-client — plain CLR + `System.Net.Http`, no `DispatcherObject`). `DisableTestParallelization` added new to `Agent.Tests`; already present in `App.Tests`/`Rendering.Tests`.
- **No real network/key/socket:** `ILlmClient` + `ISecretStore` FAKED; `AnthropicLlmClient` exercised only via a stub `HttpMessageHandler` (the `MarkdownFetcherTests.StubHandler` pattern); `DpapiSecretStore` smoke-tested over a temp path (round-trip only, never ciphertext, capability-guarded). NEVER a real Anthropic key in a test/log.
- **No-tautology / no-secret-leak:** assert against the REAL `PersonalityEngine`/`AnthropicLlmClient`/`DpapiSecretStore` produced values (the actual returned markdown, outcome enum, request URI/headers/body), not a re-declared stub. Assert the sentinel key NEVER leaks into any surfaced string.
- **No regression:** EVERY existing 3.x `Rendering.Tests` + `App.Tests` stay green UNCHANGED; the seam is additive; `Rendering` stays pure; Basic pass-through is byte-identical to the Epic-3 render.

### Deferred-Work-Log (genuinely out-of-scope for 4.1 — recorded, not built)

- **Personality-selector UI** (the toolbar chip + re-render-in-place without re-fetch) — Story 4.2.
- **Persona variety / per-reader structural rendering** (Cozy Reader, Terminal, TL;DR, Plain Language; two personas → structurally-different renders) — Story 4.3.
- **Translation persona + audio/SAPI personality** (FR-11) — Story 4.4.
- **Key-entry UX** (the chip/dialog that calls `ISecretStore.SetApiKey`) — Story 4.2 (4.1 ships the store + the `NeedsKey` outcome).
- **Second provider (OpenAI) + provider abstraction beyond Anthropic** — Deferred-Work-Log.
- **Local/offline model path (Ollama)** — Deferred-Work-Log.
- **Streaming token-by-token render** — Deferred-Work-Log (4.1 is non-streaming with a fallback affordance).
- **Caching transformed renders per (page, persona, model)** — Deferred-Work-Log.
- **Model selection (a settings/selector for the Anthropic model)** — Deferred (4.1 pins one `AnthropicOptions.Model` default).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 4.1: Local agent integration] (lines 373–384) — user story + ACs (FR-12, NFR-5): resolved agent-integration model + the `Agent` module → invoke the reader's local agent with page markdown/AST + reader context; no human-facing content rewritten server-side (rendering is local).
- [Source: _bmad-output/planning-artifacts/architecture-epic4-agent.md] (status: decided) — D1 (BYO-key: reader's own key, called from the reader's machine; default Anthropic Claude; key stored encrypted per-user via DPAPI/`ISecretStore`, never logged, sent only to the provider over TLS), D2 (markdown→markdown transform → the pure Epic-3 `FlowDocumentRenderer` renders the transformed markdown), D3 (App→Agent→(API) boundary: Agent owns net+AI, Rendering stays pure, Agent must not depend up on App), D4 (Windows-CI-only: `ILlmClient`/`ISecretStore` faked, no real call/key/socket; persona/transform logic = pure `[Fact]`; totality/failure modes fall back to Basic + a non-blocking notice, never throw to the UI), and the 4.1 story binding (the Agent module + ILlmClient/ISecretStore/persona scaffolding + BYO-key wiring + the render-time invoke + no server-side rewrite + a real provider call behind ILlmClient).
- [Source: clients/windows/Agent/IPersonality.cs, TheMarkdownWeb.Agent.csproj] — the existing Agent stub (marker interface + Rendering ProjectReference) 4.1 grows into the real module.
- [Source: clients/windows/App/MainWindow.xaml.cs] (lines 19–56, esp. 40–53) — the 3.5 wiring (`MarkdownFetcher`, `NavigationController`, `ContentHostController`, render sink line 49) 4.1 inserts the agent into.
- [Source: clients/windows/App/ContentHostController.cs] (ShowMarkdown lines 62–70) — the host that renders whatever markdown string it is given (UNCHANGED by 4.1).
- [Source: clients/windows/App/NavigationController.cs] (renderSink line 185; defensive catch line 146; last-wins generation 128–159) — the render call site the seam wraps; the totality/last-wins patterns 4.1 mirrors and preserves.
- [Source: clients/windows/App/MarkdownFetcher.cs] (FetchResult record + the total try/catch FetchAsync 68–125; injectable HttpClient 58–61) — the shape model for `LlmResult` + `AnthropicLlmClient`'s total HTTP.
- [Source: clients/windows/App.Tests/MarkdownFetcherTests.cs] (StubHandler : HttpMessageHandler 161–176; canned-response pattern) — the stub-handler pattern `AnthropicLlmClientTests` reuse (no socket).
- [Source: clients/windows/App.Tests/DependencyBoundaryTests.cs] (Rendering_DoesNotReference_AppOrAgent; App_References_Rendering via csproj ProjectReference regex) — the boundary guard 4.1 extends with Agent-direction assertions.
- [Source: clients/windows/Rendering.Tests/RenderingPurityTests.cs] (allowlist {Markdig, ColorCode.Core}; forbidden-substring incl. Anthropic/OpenAI/WebView) — the purity guard that keeps `Rendering` net/AI-free; 4.1 adds net/AI only to `Agent`.
- [Source: clients/windows/App.Tests/NoEmbeddedBrowserTests.cs] (csproj glob across clients/windows/**) — the no-webview guard; an LLM HTTP client is not a browser, so it stays green.
- [Source: clients/windows/App.Tests/AssemblyInfo.cs; clients/windows/App.Tests/TheMarkdownWeb.App.Tests.csproj] — the `DisableTestParallelization` + test-SDK/xunit/StaFact versions `Agent.Tests` mirrors.
- [Source: clients/windows/TheMarkdownWeb.sln] — the solution 4.1 adds `Agent.Tests` to (the existing 5-project entry/config pattern to mirror).
- [Source: .github/workflows/build-windows.yml] — windows-latest CI (restore → build -c Release → test -c Release on the .sln; `paths: clients/windows/**`); the sole verification surface, UNCHANGED by 4.1.
- [Source: _bmad-output/implementation-artifacts/3-2-md-only-address-bar-and-fetch.md, 3-5-in-client-links-media-navigation.md, 3-6-basic-faithful-default-render.md] — the proven Windows-CI-only + STA + DisableTestParallelization + construct-not-Show + stub-seam (no real network/socket/process) discipline this story mirrors.

## Dev Agent Record

### Agent Model Used

_(to be filled by the dev agent)_

### Debug Log References

_(none — no local build possible; verification is windows-latest CI only)_

### Completion Notes List

_(to be filled by the dev agent)_

### File List

_(to be filled by the dev agent)_

### CI Verification

_(PENDING — the authoritative verification is the `Build Windows Client` (`build-windows.yml`) run on `windows-latest`. This agent does NOT commit/push. Record the green run id after CI is green.)_
