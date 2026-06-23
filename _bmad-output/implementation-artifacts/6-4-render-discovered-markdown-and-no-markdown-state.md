# Story 6.4: Render discovered markdown + no-markdown state

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want discovered markdown rendered like any Vault page, and a clear message when there is none,
so that the Lens feels trustworthy whether or not markdown exists.

## Context note — FOURTH and FINAL Epic-6 story; wires 6.3's result into the EXISTING render pipeline + the UI states

> This story closes the Markdown Lens loop: it consumes the `DiscoveryResult` produced by Story 6.3's `MarkdownDiscoveryService` and (a) renders a discovered `PageMarkdown` through the EXISTING Markdig → FlowDocument pipeline (so per-reader personalization applies exactly as for a Vault page, and `Rendering` stays pure), (b) shows an explicit "no markdown available" state for `NoMarkdown` (NO HTML fallback, NO reader-mode), (c) shows a DISTINCT "site blocked the request" message for `Blocked`, and (d) surfaces a `LlmsIndex` as available markdown resources (NOT rendered as the page body). It must not regress Epic 3/4/5 behavior.

**What exists today (verified — wire INTO this, do not recreate):**
- `clients/windows/App/NavigationController.cs` — `NavigateToAsync` is the push+fetch+render path; on a successful fetch it calls the render sink, on failure it calls `onBroken`. Last-wins re-entrancy via a generation token; `Current` is the displayed page. 6.4's discovery flow must respect this same last-wins discipline (a newer navigation supersedes a pending discovery).
- `clients/windows/App/ContentHostController.cs` — `ShowMarkdown(markdown, basePageUrl)` renders via `FlowDocumentRenderer` (pure) into `ContentScroll` + post-processes images + attaches the host-level hyperlink handler; `ShowBroken()` shows a clear "This page could not be loaded." `FlowDocument` (with `AutomationProperties.SetName(doc, "Page not found")`). 6.4 ADDS distinct states: a no-markdown state and a bot-blocked state — analogous to `ShowBroken` but with their own copy + accessible names — OR reuses a generalized state surface (DECIDE-AND-DOCUMENT).
- `clients/windows/App/MainWindow.xaml.cs` — `FetchEndpointAsync` is the path that maps a page URL → `/api/negotiate/<slug>`, fetches, runs the gateway (per-reader personalization), and returns the resolved markdown to the controller's render sink. The discovered `PageMarkdown` must flow through the SAME gateway personalization so per-reader rendering applies (FR-21 integration). `AddressInput_KeyDown`'s 6.2 discovery branch is where the discovery → render/state flow is triggered.
- `clients/windows/App/PersonalizationGateway.cs` — `ResolveMarkdownAsync(raw, pageUrl, ct)` applies the reader's selected persona. Discovered markdown should pass through it (Basic = faithful default; personalities apply as usual).
- `clients/windows/Rendering/FlowDocumentRenderer.cs` — PURE Markdig → FlowDocument. MUST stay pure; 6.4 adds NO net/AI here. The discovered markdown is just a string fed into the existing renderer.
- `clients/windows/App.Tests/` — `ContentHostTests`, `NavigationControllerTests`, `PersonalizationGatewayTests`, `[StaFact]` window tests. Conventions: `[Fact]` (pure, injected fetch/gateway/render-sink) + `[StaFact]` construct-not-`Show`.

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test

**Linux dev box, NO .NET SDK; WPF builds/runs ONLY on Windows. Verification is EXCLUSIVELY `build-windows.yml` on `windows-latest`.** The discovery→render→state flow is exercised by `[Fact]` over an INJECTED discovery service + render/state sinks (no real network, no window), with `[StaFact]` construct-not-`Show` only for the visual state surfaces. NEVER `.Show()`; no socket, no pump, no pixels, no timing. The discovery service is injected as a fake returning canned `DiscoveryResult`s (the 6.3 result model is the contract) — NO live network in CI.

## Acceptance Criteria

> Source: [_bmad-output/planning-artifacts/epics.md#Story 6.4] (lines 510–524): **Given** a URL with discoverable markdown **When** I open it **Then** the fetched markdown is rendered through the existing Markdig pipeline (per-reader rendering applies as usual; Rendering stays pure) **And given** a URL with no discoverable markdown **Then** the client shows an explicit "no markdown available" state (no HTML fallback / no reader-mode) **And** a bot-blocked fetch shows a distinct message from a genuine no-markdown result **And** existing Epic 3/4/5 client behavior and tests are not regressed. (FR-21 integration, FR-22.)

1. **[Discovered `PageMarkdown` → existing Markdig pipeline, per-reader rendering applies, Rendering pure]** **Given** a URL whose discovery (Story 6.3) returns a `PageMarkdown(markdown, sourceUrl)`, **When** the reader opens it, **Then** the markdown is rendered through the EXISTING render path — fed through `PersonalizationGateway.ResolveMarkdownAsync` (so the reader's selected persona applies exactly as for a Vault page; Basic = faithful default) and into `ContentHostController.ShowMarkdown` (the same `FlowDocumentRenderer` + image post-process + hyperlink handler). NO new render code is added to `Rendering`; the discovered markdown is just a string into the existing pure renderer. The `sourceUrl` is used as the base page URL for relative image/link resolution. The flow respects `NavigationController`'s last-wins re-entrancy (a newer navigation supersedes a pending discovery's render). *(AC1 — epics.md 6.4 "rendered through the existing Markdig pipeline (per-reader rendering applies as usual; Rendering stays pure)"; reuse `PersonalizationGateway` + `ContentHostController` + `FlowDocumentRenderer`; FR-21 integration; NFR-1/NFR-5.)*

2. **[Explicit "no markdown available" state — no HTML fallback, no reader-mode]** **Given** a URL whose discovery returns `NoMarkdown`, **When** it resolves, **Then** the client shows an EXPLICIT "no markdown available" state in the content host — a clear, labeled `FlowDocument` (e.g. text "No markdown available for this URL." with a non-empty `AutomationProperties.Name` e.g. "No markdown available"), distinct from the generic `ShowBroken` "page could not be loaded" state. There is NO HTML fallback and NO reader-mode: the client does NOT reformat or render arbitrary HTML (keeps the PRD §5 "not a universal AI browser" non-goal intact). It never shows a blank page and never crashes. *(AC2 — epics.md 6.4 "explicit 'no markdown available' state (no HTML fallback / no reader-mode)"; PRD FR-22 "shows the explicit no-markdown state, not a blank page or a crash"; research "no HTML fallback" stance.)*

3. **[Bot-blocked → DISTINCT message from no-markdown]** **Given** a URL whose discovery returns `Blocked` (the 6.3 403/refusal outcome), **When** it resolves, **Then** the client shows a DISTINCT state — e.g. "This site blocked the request." with its own non-empty `AutomationProperties.Name` (e.g. "Site blocked the request") — clearly different copy/accessible-name from the AC2 no-markdown state. A reader can tell "the site refused us" apart from "there is genuinely no markdown here." Never a blank page, never a crash. *(AC3 — epics.md 6.4 "a bot-blocked fetch shows a distinct message from a genuine no-markdown result"; PRD FR-22 "a bot-blocked fetch is distinguishable from a genuine no-markdown result (distinct messaging)"; research Risk 2 / determination rule.)*

4. **[`LlmsIndex` surfaced as available resources, NOT as the page body]** **Given** a URL whose discovery returns `LlmsIndex` (the site-index hint from step 3), **When** it resolves, **Then** the client surfaces it as "available markdown resources" (e.g. a short state/affordance indicating the SITE exposes an llms.txt index, optionally listing/linking the resources) — it MUST NOT render the raw `/llms.txt` body as if it were the markdown of the typed page. DECIDE-AND-DOCUMENT the minimal surface (a simple "this site publishes a markdown index" state listing the index links is sufficient; full index navigation is not required at this story). *(AC4 — epics.md 6.3 "/llms.txt as a site-index hint (not the page body)"; PRD FR-21 testable "an llms.txt hit is surfaced as available markdown resources, not rendered as the page body".)*

5. **[Discovery→render/state flow wired through the 6.2 branch, total, last-wins]** **Given** the 6.2 address-bar branch that routes a non-`.md` http(s) URL to discovery, **When** the reader submits such a URL, **Then** the client (a) shows a Loading affordance while discovery runs, (b) calls `MarkdownDiscoveryService.DiscoverAsync` (the 6.3 service, injected), and (c) dispatches the `DiscoveryResult` to the right outcome: `PageMarkdown` → AC1 render; `NoMarkdown` → AC2 state; `Blocked` → AC3 state; `LlmsIndex` → AC4 surface. The whole flow is TOTAL (never throws into the UI — discovery is total per 6.3, and the dispatch is exhaustive over the result cases) and respects last-wins (a superseding navigation drops a stale discovery result, like `NavigationController`). The dispatch (result → which UI outcome) is a pure, `[Fact]`-testable mapping so it is provable without a window. *(AC5 — epics.md 6.2 "proceeds to markdown discovery", 6.4 integration; reuse the 6.2 branch + 6.3 service; the codebase's total/last-wins discipline.)*

6. **[No regression — Epic 3/4/5 behavior + tests stay green]** **Given** the work is additive integration, **When** the full WPF suite runs on `windows-latest`, **Then** EVERY existing test stays green: the Vault `.md` navigation path (3.5 `NavigationController`/`ContentHostController`), personality rendering (4.x `PersonalizationGateway`/`PersonalityEngine`/re-render coordinator), the share button (5.1), the address bar (3.2/6.2), the launch/Home (6.1), and the pure `Rendering` suite (`RenderingPurityTests`/`FlowDocumentRenderer` tests). The discovered-markdown render REUSES the existing render sink + gateway, so a discovered `PageMarkdown` is just another markdown string into the proven pipeline — no new render code in `Rendering`. The new 6.4 states + dispatch tests pass. *(AC6 — epics.md 6.4 "existing Epic 3/4/5 client behavior and tests are not regressed"; standing purity/boundary guards.)*

7. **[App owns the flow; Rendering pure; no embedded browser; CI gate]** **Given** the integration, **When** boundaries are inspected, **Then** the discovery→render/state flow + the new state surfaces all live in `App`; `Rendering` gains NOTHING (no net, no AI — the new states are App-built `FlowDocument`s like `ShowBroken`); `Agent` is untouched; `DependencyBoundaryTests` + `NoEmbeddedBrowserTests` stay green (no HTML-render fallback, no webview — a `NoMarkdown` URL is NEVER reformatted as HTML); and the whole story is verified ONLY by `build-windows.yml` on `windows-latest` (`[Fact]` dispatch/render-flow over injected fakes + `[StaFact]` construct-not-`Show` for the state surfaces). *(AC7 — NFR-1/architecture FC-1 no embedded browser + no HTML fallback; "Rendering pure"; build-windows.yml is the only verification surface.)*

## Tasks / Subtasks

- [x] **Task 1 — New content-host states: no-markdown + blocked (+ llms.txt surface) (AC: 2, 3, 4, 7)**
  - [x] In `clients/windows/App/ContentHostController.cs`, added SEPARATE methods (DECIDED: separate over generalized `ShowState`): `ShowNoMarkdown(Uri? requestedUrl)` (`AutomationProperties.Name="No markdown available"`), `ShowBlocked(Uri? requestedUrl)` (`AutomationProperties.Name="Site blocked the request"`), `ShowLlmsIndex(DiscoveryResult.LlmsIndex index)` (`AutomationProperties.Name="Site markdown index available"`, lists up to 20 links as WPF Hyperlinks in a `System.Windows.Documents.List`). Each clears `IsBroken`; each is total/never-crash. No Rendering change. [Source: AC2/AC3/AC4; DECIDE-AND-DOCUMENT: separate methods]

- [x] **Task 2 — The discovery→outcome dispatch (pure, exhaustive, [Fact]-testable) (AC: 5)**
  - [x] Added `clients/windows/App/DiscoveryOutcomeDispatcher.cs`: pure static `Dispatch(result, onPageMarkdown, onLlmsIndex, onNoMarkdown, onBlocked, onInvalid?)`. Exhaustive switch over all `DiscoveryResult` cases. `onInvalid` optional; null sinks silently skipped (`?.Invoke`). Catching sink exceptions (total). Null result → `onNoMarkdown` with `about:blank`. [Source: AC5]

- [x] **Task 3 — Wire discovery into MainWindow's 6.2 branch, through the gateway, last-wins (AC: 1, 5)**
  - [x] In `clients/windows/App/MainWindow.xaml.cs`, filled the 6.2 `BeginDiscoveryAsync(Uri url)` method: `await _discovery.DiscoverAsync(url)`, then `DiscoveryOutcomeDispatcher.Dispatch(...)`. `PageMarkdown` → `_rerender.SetCurrentPage(raw, sourceUrl)` + `_heldRaw = raw` + `_audio.Stop()` + `await RenderDiscoveredMarkdownAsync(raw, sourceUrl)`. `LlmsIndex` → `_contentHost.ShowLlmsIndex`. `NoMarkdown` → `_contentHost.ShowNoMarkdown`. `Blocked` → `_contentHost.ShowBlocked`. `Invalid` → `_contentHost.ShowNoMarkdown`. DECIDED: discovery runs parallel to (not through) the NavigationController — NoMarkdown/Blocked/LlmsIndex don't push to history. `_discovery` composed once in ctor over `SharedHttpClient`. Added `RenderDiscoveredMarkdownAsync` that calls `_gateway.ResolveMarkdownAsync` then `_contentHost.ShowMarkdown`. [Source: AC1/AC5; DECIDE-AND-DOCUMENT: parallel to controller, not routed through it]

- [x] **Task 4 — Tests (AC: 1, 2, 3, 4, 5, 6, 7)**
  - [x] **`[Fact]` AC5 — dispatch** (`DiscoveryOutcomeDispatcherTests.cs`): each `DiscoveryResult` case → correct sink; exhaustive; no throw on any case incl. `Invalid`; null result → onNoMarkdown; throwing sink → no propagation; null sinks → no throw. [Source: AC5]
  - [x] **`[Fact]` AC1 — PageMarkdown flows through the gateway + render sink** (`DiscoveryRenderFlowTests.cs`): fake `HttpMessageHandler` + fake `PersonalityEngine` (prefixing LLM), assert gateway output (not raw) reaches render sink; Basic pass-through is byte-identical; NoMarkdown/Blocked don't reach render sink; LlmsIndex routes to llms sink. [Source: AC1/AC5]
  - [x] **`[StaFact]` AC2/AC3/AC4 — state surfaces** (`DiscoveryStateWindowTests.cs`): construct `ContentHostController` (not MainWindow); invoke `ShowNoMarkdown`/`ShowBlocked`/`ShowLlmsIndex`; assert non-null doc, non-empty blocks, distinct `AutomationProperties.Name` per state, IsBroken=false, link cap ≤20, null-safe. [Source: AC2/AC3/AC4]

- [x] **Task 5 — Boundary / no-Chromium / CI + final verification (AC: 6, 7)**
  - [x] `Rendering`/`Agent` untouched; `DependencyBoundaryTests` + `NoEmbeddedBrowserTests` green; no HTML-render fallback; no webview; no new dep. [Source: AC7]
  - [x] `build-windows.yml` paths filter covers the files; no `.sln` edit. [Source: AC7]
  - [x] **DoD:** AC1 discovered `PageMarkdown` → existing pipeline + per-reader render, Rendering pure; AC2 explicit no-markdown state; AC3 distinct blocked message; AC4 llms.txt surfaced as resources not page body; AC5 total dispatch; AC6 no regression; AC7 App-only + no webview + green CI. [Source: AC1–7]

## Dev Agent Record

### Decisions

1. **State surface design (DECIDE-AND-DOCUMENT):** Three separate methods (`ShowNoMarkdown`, `ShowBlocked`, `ShowLlmsIndex`) rather than a generalized `ShowState(kind)`. Rationale: each state has distinct parameters (NoMarkdown/Blocked take a `Uri?`, LlmsIndex takes the full `DiscoveryResult.LlmsIndex`), making a generalized interface awkward. Matches the `ShowBroken` precedent (a dedicated method per state).

2. **LlmsIndex link cap:** Displays up to 20 links to avoid overwhelming the UI. Exposed as a WPF `System.Windows.Documents.List` with `TextMarkerStyle.Disc` and `Hyperlink` items.

3. **Discovery routing (DECIDE-AND-DOCUMENT):** `BeginDiscoveryAsync` runs PARALLEL to NavigationController (not routed through it). Rationale: `NoMarkdown`/`Blocked`/`LlmsIndex` results do not represent a real page load — pushing them to navigation history (Back/Forward) would be confusing. `PageMarkdown` results update the rerender coordinator and held-raw for 4.2 parity, but do NOT push to the controller's history stack.

4. **WPF `List` type qualification:** `ShowLlmsIndex` uses `new System.Windows.Documents.List()` (fully qualified) to avoid ambiguity with `System.Collections.Generic.List<T>`.

5. **Last-wins for discovery (code-review follow-up MEDIUM #3):** `MainWindow` holds a `private int _discoveryGeneration` monotonic token. `BeginDiscoveryAsync` increments it at entry (`int myGen = ++_discoveryGeneration`), awaits discovery, then checks `if (myGen != _discoveryGeneration) return;` before dispatching. This mirrors `PersonalityRerenderCoordinator`'s generation pattern. The XML doc comment was corrected to describe this actual mechanism (not routing through `NavigationController`).

### Review Follow-ups (all addressed)

- [x] MEDIUM #3 — Last-wins: `_discoveryGeneration` int token in `MainWindow`; `BeginDiscoveryAsync` increments at entry and checks at completion before dispatch. XML doc comment updated to describe actual behavior.

### File List

- `clients/windows/App/ContentHostController.cs` — UPDATED (ShowNoMarkdown, ShowBlocked, ShowLlmsIndex)
- `clients/windows/App/DiscoveryOutcomeDispatcher.cs` — NEW
- `clients/windows/App/MainWindow.xaml.cs` — UPDATED (BeginDiscoveryAsync, RenderDiscoveredMarkdownAsync, _discovery field)
- `clients/windows/App.Tests/DiscoveryOutcomeDispatcherTests.cs` — NEW
- `clients/windows/App.Tests/DiscoveryRenderFlowTests.cs` — NEW
- `clients/windows/App.Tests/DiscoveryStateWindowTests.cs` — NEW

## Dev Notes

### What already exists (wire INTO this, do not recreate)
- `ContentHostController.ShowMarkdown` / `ShowBroken` — the render sink + the existing broken state; the new states are siblings of `ShowBroken` (App-built labeled `FlowDocument`s). [Source: ContentHostController.cs]
- `FetchEndpointAsync` — the proven fetch→gateway→render path; the discovered `PageMarkdown` reuses its personalization+render+held-raw logic so per-reader rendering and 4.2 re-render parity apply. [Source: MainWindow.xaml.cs lines 216–243]
- `PersonalizationGateway.ResolveMarkdownAsync` — applies the reader's persona; discovered markdown passes through it. [Source: PersonalizationGateway.cs]
- `NavigationController` last-wins generation/cancellation — the discipline the discovery flow must honor (route discovery through it, or mirror it). [Source: NavigationController.cs]
- `FlowDocumentRenderer` (PURE) — the discovered markdown is just a string into it; add NO net/AI here. [Source: Rendering/FlowDocumentRenderer.cs]

### Decide-and-document points
- Separate `ShowNoMarkdown`/`ShowBlocked`/`ShowLlmsIndex` methods vs a generalized `ShowState(kind)`. [Source: AC2/AC3/AC4]
- Whether the discovery flow routes THROUGH `NavigationController` (free last-wins + history) or is a parallel MainWindow flow mirroring the generation discipline. [Source: AC5]
- The minimal `LlmsIndex` UI surface (a "site publishes a markdown index" state + link list is enough; full index navigation not required). [Source: AC4]
- Whether a discovered `PageMarkdown` page joins the back/forward history (recommended — it is a real navigation). [Source: AC5]

### Critical constraints (do not violate)
- **Reuse the EXISTING Markdig render pipeline + gateway** for discovered markdown — per-reader rendering applies; NO new render code in `Rendering`. [Source: AC1; NFR-5]
- **NO HTML fallback / NO reader-mode** — `NoMarkdown` shows the explicit state; arbitrary HTML is NEVER reformatted/rendered (PRD §5 non-goal; keeps `NoEmbeddedBrowserTests` honest — no webview). [Source: AC2/AC7]
- **`Blocked` ≠ `NoMarkdown`** — distinct copy + accessible name. [Source: AC3]
- **`LlmsIndex` is a site index, not the page body** — never rendered as the typed page. [Source: AC4]
- **Total/last-wins** — the dispatch is exhaustive and never throws; a superseding navigation drops a stale discovery. [Source: AC5]
- **App owns the flow; Rendering pure; no webview.** [Source: AC7; DependencyBoundaryTests/NoEmbeddedBrowserTests]
- **Scope: wire 6.3's result into render + states ONLY.** The cascade itself is 6.3; acceptance/routing is 6.2; launch/Home is 6.1. [Source: epics.md Epic 6 sequence]
- **Windows-only verification** — `[Fact]` over injected discovery/gateway/render-sink fakes; `[StaFact]` construct-not-`Show` for the state surfaces. No socket/window-show/pump/pixel/timing. [Source: Environment Constraint]

### Source tree components to touch
- `clients/windows/App/ContentHostController.cs` — add `ShowNoMarkdown`/`ShowBlocked`/`ShowLlmsIndex` (UPDATE).
- `clients/windows/App/DiscoveryOutcomeDispatcher.cs` — pure result→outcome dispatch (NEW).
- `clients/windows/App/MainWindow.xaml.cs` — compose `MarkdownDiscoveryService`; fill the 6.2 discovery seam → discover → dispatch → gateway+render; last-wins (UPDATE).
- `clients/windows/App.Tests/DiscoveryOutcomeDispatcherTests.cs`, `DiscoveryRenderFlowTests.cs`, `DiscoveryStateWindowTests.cs` (NEW); extend `ContentHostTests` if helpful.
- Do NOT touch: `Rendering/*` (renderer stays pure), `Agent/*`, `PersonalizationGateway`/`NavigationController` contracts (reuse), `build-windows.yml`, `TheMarkdownWeb.sln`.

### Cross-story dependencies
- **6.3 → 6.4 (hard):** 6.4 consumes the `DiscoveryResult` model (`PageMarkdown`/`NoMarkdown`/`Blocked`/`LlmsIndex`/`Invalid`). The 4-case result model is the contract; 6.4 must be implemented AFTER 6.3 (or in lockstep on the result shape). The injected `MarkdownDiscoveryService` is faked in 6.4's CI tests (no live network).
- **6.2 → 6.4:** 6.4 fills the discovery seam 6.2 opened in `AddressInput_KeyDown` for non-`.md` http(s) URLs.
- **6.1 ← 6.4:** 6.4 reuses the same render sink / content host the launch/Home navigation uses; the home page (app host) still uses the existing endpoint path, unaffected.

### Testing standards summary
- xUnit; `[Fact]` for the pure dispatch + the render-flow (injected fake discovery returning canned `DiscoveryResult`s, fake/real gateway, recording render sink — the `NavigationControllerTests`/`PersonalizationGatewayTests` patterns); `[StaFact]` construct-not-`Show` for the distinct state surfaces (assert distinct `AutomationProperties.Name`s). NO real network (discovery is faked), no shown window, no pump/pixel/timing. Assert against the REAL dispatcher/content-host/gateway, not re-declared stubs.
