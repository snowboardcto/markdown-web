# Story 3.1: WPF app shell with toolbar

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want a native Windows app window with a browser-like toolbar,
so that I have a place to read Markdown Web pages.

## Context note (this is the FIRST `clients/windows/` feature story — and the first of Epic 3)

Epics 1–2 built the web (`web/` Astro static site + `api/` content negotiation). **Story 3.1 is the first feature work in the native Windows client** (`clients/windows/`) and the first story of **Epic 3 (Read Markdown in the Native Client)**. It builds the **app shell only**: the WPF window + the browser-like toolbar (back / forward / reload). It does NOT add the address bar (Story 3.2), the Markdig→FlowDocument rendering (Story 3.3), highlighting (3.4), in-client navigation/links/media (3.5), or the basic faithful render (3.6).

**A minimal buildable skeleton already exists** (committed in `Epic 3: Windows build/test CI + minimal buildable WPF solution skeleton`, commit `8a651b1`). It is the foundation this story fleshes out — do NOT recreate it:
- `clients/windows/TheMarkdownWeb.sln` with four projects: `App` (WinExe, WPF), `Rendering` (pure bedrock, Markdig 1.3.1), `Agent` (seam only), `Rendering.Tests` (xUnit).
- `App/App.xaml` (+`.cs`), `App/MainWindow.xaml` (+`.cs`) — the window today is an empty `<Grid/>` with `Title="The Markdown Web"`.
- `App/TheMarkdownWeb.App.csproj` already references `Rendering` and sets `net10.0-windows`, `UseWPF=true`, `OutputType=WinExe`.
- `.github/workflows/build-windows.yml` — windows-latest CI: `dotnet restore` → `build -c Release` → `test -c Release`. **This CI is the ONLY way this story is verified** (see the Environment Constraint below).

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test (drives AC4, AC5, Task 5)

**This repo is developed on Linux with NO .NET SDK installed; WPF builds and runs ONLY on Windows.** The dev agent CANNOT build, run, or visually confirm the WPF window locally. **Verification happens exclusively through the `build-windows.yml` GitHub Actions CI on `windows-latest`** (restore/build/test). Therefore:
- Every acceptance bar that must be *checked* has to be checked **either by the compiler (build succeeds) or by an xUnit test that runs in `dotnet test` on `windows-latest`** — never by "launch the app and look."
- The CI runner is **headless / no interactive desktop session**. Do NOT write tests that require showing a real visible window, message-pumping a live UI, or screen-pixel assertions. Prefer asserting on **constructed WPF objects** (instantiate `MainWindow`/controls in-process and inspect their properties/automation peers) and on **project-reference metadata** (the no-Chromium assertion). WPF UI objects have **thread-affinity (STA)** — any test that constructs a `Window`/`Control` MUST run on an STA thread (use `Xunit.StaFact`'s `[StaFact]`), otherwise it throws `InvalidOperationException: The calling thread must be STA`.
- The "app launches and a window opens" AC is satisfied **structurally**: the build produces the `WinExe`, and a `[StaFact]` test constructs the real `MainWindow` and asserts its title + the presence/automation-identity of the toolbar buttons. We assert the shell is *correctly constructed*, which is the testable proxy for "it opens" on a headless runner.

## Acceptance Criteria

1. **Given** the built .NET 10 WPF client, **When** I launch it, **Then** a single native WPF window opens with the OS titlebar text **"The Markdown Web"**. Concretely: `App.xaml` declares exactly one startup window (`StartupUri="MainWindow.xaml"` or an equivalent single-window `OnStartup`), `MainWindow` is a `System.Windows.Window` whose `Title` is exactly the string `The Markdown Web` (the OS draws this in the titlebar — WPF does not need a custom titlebar control; the standard window chrome shows `Title`), and launching opens **one** window (no second/duplicate window, no console window — `OutputType` stays `WinExe`). *(AC1 — single native WPF window, titlebar text "The Markdown Web"; epics.md Story 3.1 line 296 "a native WPF window opens with a titlebar (\"The Markdown Web\")", UX-DR4, DESIGN.md `client-titlebar` "window title \"The Markdown Web\"")*

2. **Given** the open window, **When** it displays, **Then** the shell shows a **browser-like toolbar** docked at the top of the window containing, in order, three controls: **Back**, **Forward**, and **Reload**. Each is a real, focusable WPF control (e.g. `Button`), visually a toolbar control per DESIGN.md (flat on a faint fill, `client-toolbar`), and laid out left-aligned in the back→forward→reload order shown in the native-client mockup. The toolbar is a structural region of `MainWindow` (e.g. a `ToolBar`/`DockPanel`/`Grid` row) distinct from the (future, Story 3.2) address-bar slot — leave room for the address bar to its right but do NOT build it here. *(AC2 — toolbar with Back/Forward/Reload present, in order, top-docked; epics.md Story 3.1 line 296 "a toolbar (back/forward/reload)", UX-DR4, DESIGN.md `client-toolbar` "toolbar with back/forward/reload", mockup `native-client.png`)*

3. **[DERIVED — NFR-6 / UX-DR9 accessibility floor]** **Given** the toolbar controls, **When** inspected via WPF UI Automation, **Then** each of Back / Forward / Reload exposes a stable, human-readable accessible name (e.g. via `AutomationProperties.Name` = "Back" / "Forward" / "Reload", or equivalent labeled content) and is **keyboard-reachable** (each is a focusable control in the tab order; none has `IsTabStop=false` / `Focusable=false`). A screen reader / UI Automation client can identify each button by name. This is the testable expression of the epic's NFR-6 ("native WPF UI Automation labeled, keyboard-reachable controls") and UX-DR9 ("native labeled, keyboard-reachable WPF controls") at shell level. *(AC3 — toolbar buttons are UI-Automation-named and keyboard-reachable; NFR-6, UX-DR9, EXPERIENCE.md Accessibility Floor "the shell's controls are labeled and keyboard-reachable")*

4. **[DERIVED + HARD — NFR-1 / FR-13, the defining constraint of the native client]** **Given** the built client and its project references, **When** the dependency graph is inspected, **Then** the application has **NO Chromium / WebView2 / CefSharp / embedded-browser / webview dependency of any kind** — and this is **proven by an automated, CI-runnable test** (not by manual inspection). The test MUST FAIL (red) if any such dependency is ever introduced. Concretely the assertion covers: (a) **no PackageReference / transitive NuGet package** whose id matches a forbidden set — at minimum `Microsoft.Web.WebView2*`, `Microsoft.Toolkit.Wpf.UI.Controls.WebView*`, `Microsoft.Toolkit.Forms.UI.Controls.WebView*`, `CefSharp*`, `CefSharp.Wpf*`, `Chromely*`, `WebView*`, `Microsoft.AspNetCore.Components.WebView*`, `Xamarin.*WebView*`; (b) **no loaded assembly** at runtime named for those engines (e.g. `Microsoft.Web.WebView2.*`, `CefSharp.*`); (c) **no XAML/code use of a `WebView`/`WebView2`/`WebBrowser` control** in `App`. The forbidden-substring set (case-insensitive: `webview`, `webview2`, `cefsharp`, `chromium`, `chromely`, `cef.`) is the screen. *(AC4 — no-Chromium/WebView2/embedded-browser dependency, ENFORCED BY A FAILING TEST that inspects references/loaded assemblies; epics.md Story 3.1 line 297 "the application has no Chromium / WebView2 / embedded-browser dependency (verified in project references)", NFR-1 "must not depend on Chromium/WebView2/any embedded browser engine", FR-13, architecture FC-1 "No embedded browser engine … embedding any HTML webview … defeats the client's reason to exist")*

5. **[DERIVED — CI/build gate; the only verification surface per the Environment Constraint]** **Given** this story is verified exclusively on `windows-latest` CI, **When** `build-windows.yml` runs (`dotnet restore` → `dotnet build -c Release` → `dotnet test -c Release`), **Then** the whole solution **builds clean** (App + Rendering + Agent + Rendering.Tests, no new warnings-as-errors regressions) and **all tests pass green**, including: the existing `Rendering.Tests` skeleton probes (must stay green — no regression), the new shell/toolbar `[StaFact]` tests (AC1–AC3), and the new no-Chromium assertion test (AC4). New WPF-shell tests that construct `Window`/`Control` objects run under **STA** (`Xunit.StaFact`); the **strict App↔Rendering dependency direction is preserved** (`App`→`Rendering`; `Rendering` gains NO reference to `App`/`Agent`, no networking, no AI — unchanged by this story). *(AC5 — solution builds + all tests green on windows-latest CI; existing Rendering.Tests stay green; dependency boundary preserved; epics.md/architecture "`Rendering` pure, `App` depends on it never the reverse", build-windows.yml CI, NFR-7)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 3.1: WPF app shell with toolbar] (lines 286–297): **Given** the built .NET 10 WPF client **When** I launch it **Then** a native WPF window opens with a titlebar ("The Markdown Web") and a toolbar (back/forward/reload) **And** the application has no Chromium / WebView2 / embedded-browser dependency (verified in project references). (FR-13, NFR-1, UX-DR4). **AC1** = the titlebar/single-window half of the epic's "Then". **AC2** = the toolbar (back/forward/reload) half. **AC3** = the derived NFR-6/UX-DR9 accessibility-floor bar applied to the shell controls (the epic/PRD require labeled, keyboard-reachable native controls; the shell is where they first appear). **AC4** = the epic's "And … no Chromium/WebView2/embedded-browser dependency (verified in project references)" — promoted from "verified" to "verified by a FAILING automated test", because per the Environment Constraint manual inspection is not a reproducible gate and NFR-1 is the hard, defining constraint. **AC5** = the derived build/CI gate — the only place this story is actually verified (Linux dev box has no .NET SDK; WPF is Windows-only).

## Tasks / Subtasks

- [ ] **Task 1 — Flesh out `MainWindow` shell: titlebar + top toolbar with Back/Forward/Reload (XAML)** (AC: 1, 2, 3)
  - [ ] In `App/MainWindow.xaml`, keep `Title="The Markdown Web"` (already present — AC1) and replace the empty `<Grid/>` with a top-docked toolbar region. Use a `DockPanel` (or a `Grid` with a fixed top row): top region = the toolbar; the remaining area is a **named, empty content host** (e.g. `<Border x:Name="ContentHost"/>` or a `<FlowDocumentScrollViewer x:Name="ContentHost"/>` placeholder) that Story 3.3/3.6 will fill — do NOT render content now. [Source: architecture "App/ — shell, window, navigation"; DESIGN.md `client-titlebar`/`client-toolbar`; mockup native-client.png]
  - [ ] Add the toolbar as a WPF `ToolBar` (or a `StackPanel Orientation="Horizontal"` styled flat) containing three `Button`s in order: **Back**, **Forward**, **Reload**. Left-aligned. Visually per DESIGN.md (flat controls on a faint toolbar fill, `{rounded.button}` feel) — keep it simple; a full DESIGN.md token theme is not required at this story, but the control order and roles must match the mockup. Use glyph/text content (e.g. `‹` / `›` / `⟳`, or text "Back"/"Forward"/"Reload") — but ALSO set accessible names in Task 2 regardless of visible content. [Source: epics.md Story 3.1 "back/forward/reload"; DESIGN.md `client-toolbar`; mockup native-client.png shows back ‹ · forward › · reload ⟳ on the left]
  - [ ] **Leave room for, but do NOT build, the Story 3.2 address bar and the personality-selector (3.x).** Reserve the space to the right of the toolbar buttons (e.g. an empty `Grid` column / a commented placeholder), so 3.2 can slot the address bar in without restructuring the shell. Explicitly out of scope here. [Source: UX-DR5 address-bar = Story 3.2; UX-DR6 personality-selector = Epic 4; task scope note]
  - [ ] Name the three buttons (`x:Name="BackButton"`, `ForwardButton`, `ReloadButton`) so the `[StaFact]` tests (Task 4) can find them by name and assert order/identity. [Source: AC2/AC3 testability]

- [ ] **Task 2 — Accessibility: UI Automation names + keyboard reachability for the toolbar controls** (AC: 3)
  - [ ] On each toolbar `Button` set `AutomationProperties.Name` to "Back" / "Forward" / "Reload" (do this even if the visible content is a glyph, so a glyph-only button still has a screen-reader name). Optionally add a `ToolTip` with the same text. [Source: NFR-6, UX-DR9, EXPERIENCE.md "controls are labeled and keyboard-reachable"]
  - [ ] Ensure each button is keyboard-reachable: it is a focusable control in the tab order (do NOT set `Focusable="False"` or `IsTabStop="False"`). A `ToolBar`'s default behavior keeps buttons tab-reachable; if a custom `StackPanel` is used, confirm tab order. [Source: NFR-6/UX-DR9 keyboard-reachable]
  - [ ] (No new accessibility infra — WPF UI Automation gives baseline peers for free; this task only ensures the names/focus are set so AC3's test can assert them.) [Source: architecture/EXPERIENCE.md "WPF UI Automation gives baseline screen-reader support for free"]

- [ ] **Task 3 — Minimal command wiring for the toolbar buttons (NO real navigation — that is 3.2/3.5)** (AC: 2, 3)
  - [ ] Wire each button to a minimal, inert handler/command so the buttons are functional controls, NOT dead XAML: e.g. `Click` handlers (or `ICommand`s on a small view-model) named `OnBack` / `OnForward` / `OnReload`. At this story they may be no-ops or set a simple observable state (e.g. a `LastAction` string / an enum) — there is NO history stack, NO fetch, NO page to reload yet. Real back/forward/reload navigation lands in Story 3.2 (fetch) and 3.5 (in-client navigation). [Source: scope note — "toolbar back/forward/reload buttons can be present but their navigation wiring is minimal"; epics.md 3.2/3.5]
  - [ ] **If** a small view-model is introduced (recommended — it makes the command logic testable WITHOUT a visible window, per the Environment Constraint), keep it in `App` (e.g. `App/ShellViewModel.cs`), keep `Rendering` untouched, and expose the command-invoked state so a plain `[Fact]` (no STA needed) can assert "invoking Reload set LastAction=Reload" etc. Prefer testing view-model/command logic that does not require a window over STA UI tests where practical. [Source: Environment Constraint "prefer testing view-model/command logic that doesn't require a visible window"; architecture App-vs-Rendering boundary]

- [ ] **Task 4 — Shell/toolbar tests: `[StaFact]` constructing the real `MainWindow` (AC1–AC3) + command-logic `[Fact]`s (AC2/AC3)** (AC: 1, 2, 3, 5)
  - [ ] **Add a test project for `App`** — `clients/windows/App.Tests/TheMarkdownWeb.App.Tests.csproj` (`net10.0-windows`, `UseWPF=true`, `IsTestProject=true`), referencing `App` (NOT `Rendering` — App already transitively brings it) and the test stack: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` (match the versions already in `Rendering.Tests`: `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2) **plus `Xunit.StaFact`** (latest stable, e.g. `Xunit.StaFact` ~1.1.x) for the `[StaFact]` STA attribute. **Add the new project to `TheMarkdownWeb.sln`** (so CI's `dotnet test` on the solution discovers it). [Source: Environment Constraint STA requirement; Rendering.Tests csproj as the version template; build-windows.yml runs `dotnet test` on the .sln]
  - [ ] **`[StaFact]` AC1 — window identity:** construct `new MainWindow()` on the STA test thread and assert `window.Title == "The Markdown Web"`. (Constructing the window — `InitializeComponent()` — exercises that the XAML loads with no parse error; this is the headless-runner proxy for "the window opens".) Do NOT call `.Show()` / `.ShowDialog()` (no visible window needed and the runner is headless). [Source: AC1; Environment Constraint headless/STA]
  - [ ] **`[StaFact]` AC2 — toolbar buttons present + ordered:** from the constructed `MainWindow`, locate `BackButton`/`ForwardButton`/`ReloadButton` (by `x:Name` via `FindName`, or by walking the visual/logical tree) and assert all three exist, are `Button`s, and appear in back→forward→reload order within the toolbar container. [Source: AC2]
  - [ ] **`[StaFact]` AC3 — accessibility:** for each toolbar button assert `AutomationProperties.GetName(button)` equals the expected "Back"/"Forward"/"Reload" (or the button's `AutomationPeer.GetName()` returns it), and assert each is keyboard-reachable (`button.Focusable == true` && `KeyboardNavigation.GetIsTabStop`/`IsTabStop != false`). [Source: AC3; NFR-6/UX-DR9]
  - [ ] **`[Fact]` AC2/AC3 command logic (no window):** if Task 3 added a view-model, assert invoking the Back/Forward/Reload command updates the observable state (e.g. `LastAction`) — a pure `[Fact]`, no STA, no window. (This is the preferred CI-cheap proof that the buttons are wired.) [Source: Environment Constraint "prefer view-model/command logic tests"]

- [ ] **Task 5 — No-Chromium assertion test (AC4 — the HARD constraint; must FAIL if a webview dep is ever added)** (AC: 4, 5)
  - [ ] Add a test (place it in `App.Tests`, or in `Rendering.Tests` if it should guard the whole solution — recommended: `App.Tests`, since `App` is the only project that could host a webview) that **fails when any forbidden embedded-browser dependency is present.** Implement at least the **assembly/reference inspection** tier (deterministic, CI-runnable, no window):
    - Enumerate the dependencies of the `App` assembly and its referenced assemblies (e.g. `typeof(App).Assembly.GetReferencedAssemblies()` and/or load + scan `AppDomain.CurrentDomain.GetAssemblies()` after touching `App` types), and assert **none** has a name matching the forbidden set (case-insensitive substring screen: `webview`, `webview2`, `cefsharp`, `chromium`, `chromely`, `cef.`). [Source: AC4 (b)]
    - **Belt-and-braces — scan the committed project files:** read `App/TheMarkdownWeb.App.csproj` (and the other `clients/windows/**/*.csproj`) and assert **no `<PackageReference>` Include** matches the forbidden package id set (`Microsoft.Web.WebView2*`, `CefSharp*`, `Chromely*`, `*WebView*`, `Microsoft.Toolkit.*.UI.Controls.WebView*`, `Microsoft.AspNetCore.Components.WebView*`). Resolve the csproj paths relative to the test assembly / repo root robustly (the test runs from `bin/` — walk up to `clients/windows/`). This tier catches a forbidden dep added to the csproj even before it's restored. [Source: AC4 (a); epics.md "verified in project references"]
  - [ ] Make the failure message explicit and educational (name the offending dependency and cite NFR-1 / FC-1 "no embedded browser engine — embedding a webview defeats the client's reason to exist"), so a future contributor who trips it understands WHY it's forbidden. [Source: architecture FC-1; NFR-1]
  - [ ] (Optional, only if cheap) Add a code/XAML grep-style assertion that `App`'s XAML/source contains no `WebView`/`WebView2`/`WebBrowser` element/type usage. The package + assembly tiers are the primary gate; this is a nicety. [Source: AC4 (c)]

- [ ] **Task 6 — CI / build hygiene: ensure the new test project runs on windows-latest and nothing regresses** (AC: 5)
  - [ ] Confirm `build-windows.yml` will pick up the new `App.Tests` project: it runs `dotnet restore/build/test` against `TheMarkdownWeb.sln`, so the new project MUST be added to the `.sln` (Task 4). No workflow edit should be needed if the project is in the solution — verify the `paths:` filter (`clients/windows/**`) already covers the new files (it does). If a workflow change IS needed, keep it minimal. [Source: build-windows.yml; AC5]
  - [ ] Confirm the existing `Rendering.Tests` (the `Probe` + `CountTopLevelBlocks` skeleton tests) remain untouched and green — this story adds to `App`/`App.Tests` and MUST NOT modify `Rendering` (preserve the pure, no-net/no-AI bedrock and the App→Rendering one-way dependency). [Source: AC5; architecture Rendering boundary; existing Rendering.Tests]
  - [ ] Keep `nullable`/`ImplicitUsings` settings consistent with the existing projects; do not introduce build warnings that would break a stricter CI later. [Source: existing csproj conventions]

- [ ] **Task 7 — Final verification against ACs (Definition of Done — checked via CI, not locally)** (AC: 1, 2, 3, 4, 5)
  - [ ] **AC1:** `App.xaml` declares one startup window; `MainWindow.Title == "The Markdown Web"`; `OutputType=WinExe` (no console). Proven by the AC1 `[StaFact]` + build.
  - [ ] **AC2:** toolbar shows Back/Forward/Reload in order, top-docked. Proven by the AC2 `[StaFact]`.
  - [ ] **AC3:** each toolbar button has the right `AutomationProperties.Name` and is keyboard-reachable. Proven by the AC3 `[StaFact]`.
  - [ ] **AC4:** the no-Chromium test passes today AND would fail if a webview/Chromium/CefSharp dep were added (sanity-check by mentally/temporarily adding one — do NOT commit it). Proven by the AC4 reference/assembly test.
  - [ ] **AC5:** `dotnet build -c Release` clean + `dotnet test -c Release` all green on `windows-latest` (existing Rendering.Tests + new App.Tests); `Rendering` unchanged; App→Rendering boundary intact. Proven by the green `build-windows.yml` run.
  - [ ] Push and confirm the `Build Windows Client` GitHub Actions run is green (the authoritative verification — there is no local build). Record the run result in the Dev Agent Record.

## Dev Notes

### What already exists (build ON this, do not recreate)

- **Solution & projects** (`clients/windows/`, commit `8a651b1`): `TheMarkdownWeb.sln` → `App` (WinExe/WPF, `net10.0-windows`, refs `Rendering`), `Rendering` (pure; `Markdig` 1.3.1; `MarkdownRenderer.cs` has only toolchain probes), `Agent` (refs `Rendering`; `IPersonality` seam only), `Rendering.Tests` (xUnit 2.9.2 + Test.Sdk 17.12.0 + runner.visualstudio 2.8.2; two green probe tests).
- **Shell today:** `App/App.xaml` has `StartupUri="MainWindow.xaml"`; `MainWindow.xaml` is `Title="The Markdown Web"`, `Height=600`, `Width=800`, body is an empty `<Grid/>`. **AC1's title is already correct** — this story adds the toolbar (AC2/AC3) and the no-Chromium test (AC4).
- **CI:** `.github/workflows/build-windows.yml` on `windows-latest`, `dotnet-version: 10.0.x`, triggers on `clients/windows/**` and the workflow file; steps restore→`build -c Release`→`test -c Release` against the `.sln`. This is the sole verification path.

### Critical constraints (do not violate)

- **NFR-1 / FC-1 — NO embedded browser engine.** No Chromium, WebView2, CefSharp, Chromely, WebBrowser, or any webview. This is the *defining reason the native client exists* — embedding an HTML webview "reproduces the browser … and defeats the client's reason to exist." Enforced by AC4's failing test, not just by discipline. The shell renders native WPF; content rendering (later) is Markdig→FlowDocument, never HTML.
- **App → Rendering, one way.** `App` (and `Agent`) depend on `Rendering`; `Rendering` depends on neither and stays pure (no networking, no AI). This story touches `App`/`App.Tests` only; leave `Rendering` and `Agent` alone.
- **Windows-only verification.** No .NET SDK on the Linux dev box; WPF is Windows-only; the runner is headless. Every check is compiler-or-`dotnet test`-on-windows-latest. No "run the app and look." STA for any test that constructs a `Window`/`Control` (`Xunit.StaFact` `[StaFact]`); prefer view-model/command `[Fact]`s where a window isn't needed.
- **Scope: shell + toolbar ONLY.** No address bar (3.2), no fetch (3.2), no rendering (3.3/3.6), no highlighting (3.4), no link/media/nav (3.5), no personality selector (Epic 4). Toolbar buttons exist and are wired to inert/minimal handlers; real navigation is later.

### Source tree components to touch

- `clients/windows/App/MainWindow.xaml` (+ `MainWindow.xaml.cs`) — add toolbar + named content host (UPDATE).
- `clients/windows/App/ShellViewModel.cs` (optional, recommended) — minimal command logic, testable without a window (NEW).
- `clients/windows/App.Tests/` — new test project: `TheMarkdownWeb.App.Tests.csproj` + test classes for AC1–AC4 (NEW).
- `clients/windows/TheMarkdownWeb.sln` — add `App.Tests` project (UPDATE).
- Do NOT touch: `Rendering/*`, `Agent/*`, `Rendering.Tests/*`, `App.xaml` (title/startup already correct — only edit if you choose a single-window `OnStartup` over `StartupUri`).

### Testing standards summary

- **Framework:** xUnit (match `Rendering.Tests` versions). **STA:** `Xunit.StaFact` `[StaFact]` for any test that news up a `Window`/`Control`. **No headless-incompatible tests** (no `.Show()`, no message-pump-dependent assertions, no pixels).
- **Prefer cheap proofs:** view-model/command `[Fact]`s for wiring (AC2/AC3 logic); the no-Chromium test is a plain `[Fact]` over reference/assembly metadata + csproj text (no STA, no window).
- **No-tautology:** the no-Chromium test asserts against the REAL `App` references / real csproj files, not a re-declared list; the shell tests assert against the REAL constructed `MainWindow`, not a stub.
- **No regression:** existing `Rendering.Tests` must stay green; the App→Rendering boundary must stay intact.

### Project Structure Notes

- Aligns with architecture `clients/windows/{App,Rendering,Agent}` boundary; adding `App.Tests` mirrors the existing `Rendering.Tests` convention (a sibling test project per production project). All four+1 projects live under the one `TheMarkdownWeb.sln` that CI builds/tests.
- No variance from the architecture. The only structural addition is the `App.Tests` test project (consistent with the existing `Rendering.Tests` pattern) and an optional `ShellViewModel` inside `App`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.1: WPF app shell with toolbar] (lines 286–297) — user story + ACs (FR-13, NFR-1, UX-DR4).
- [Source: _bmad-output/planning-artifacts/epics.md#NonFunctional Requirements] — NFR-1 (no-Chromium, hard), NFR-6 (accessibility floor: native WPF UI Automation labeled, keyboard-reachable), NFR-7 (don't reinvent plumbing).
- [Source: _bmad-output/planning-artifacts/epics.md#UX Design Requirements] — UX-DR4 (native shell = titlebar + toolbar back/forward/reload), UX-DR5 (address-bar = Story 3.2, NOT here), UX-DR9 (native labeled, keyboard-reachable WPF controls).
- [Source: _bmad-output/planning-artifacts/architecture.md#Foundational Constraints] — FC-1 (no embedded browser engine; webview defeats the client's reason to exist).
- [Source: _bmad-output/planning-artifacts/architecture.md#Native client bedrock — Windows stack] — .NET 10, WPF (FlowDocument), Markdig 1.3.1.
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure & Boundaries] — `clients/windows/{App,Rendering,Agent}`; `Rendering` pure (no net, no AI); App depends on Rendering never the reverse.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/DESIGN.md] — `client-titlebar` (window title "The Markdown Web"), `client-toolbar` (back/forward/reload), shell-only ownership (content is the personality's, not designed here).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/EXPERIENCE.md#Accessibility Floor] — native shell controls labeled + keyboard-reachable; WPF UI Automation baseline.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/mockups/native-client.png] — shell layout: titlebar "The Markdown Web"; toolbar left = back ‹ · forward › · reload ⟳; then address bar (3.2); then personality chip (Epic 4).
- [Source: clients/windows/TheMarkdownWeb.sln, App/*, Rendering.Tests/*, .github/workflows/build-windows.yml] — existing skeleton this story builds on.

## Dev Agent Record

### Agent Model Used

_(to be filled by the dev agent)_

### Debug Log References

### Completion Notes List

### File List
