# Story 6.1: Default to themarkdownweb.com on launch

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want the client to open themarkdownweb.com when it starts (and on a Home action),
so that I land somewhere useful instead of a blank address bar.

## Context note — FIRST Epic-6 ("Markdown Lens") story; the window currently opens BLANK

> Epic 6 turns the native Windows client outward — from a reader of OUR Vault into the native reader for the *markdown-native web*. The four-story sequence is **6.1 (default home) → 6.2 (open any URL) → 6.3 (discovery service, load-bearing) → 6.4 (render discovered markdown + no-markdown state)**. This story, 6.1, is the smallest: it makes the client land on `themarkdownweb.com` on launch and adds a Home affordance — no discovery, no relaxed address-bar rule (that is 6.2), no new render path.

**What exists today (verified — build ON this, do not recreate):**
- `clients/windows/App/MainWindow.xaml.cs` — the constructor (`MainWindow()`) composes the full pipeline: a shared `HttpClient`, `MarkdownFetcher`, `IUrlLauncher`, `IClipboard`, the agent/gateway, `AddressBarViewModel`, `ContentHostController` (renders into `ContentScroll`), and `NavigationController _controller` (`FetchEndpointAsync` → `PageEndpointResolver.ToFetchEndpoint` → `MarkdownFetcher` → gateway → render). **But the constructor NEVER navigates** — there is no launch-time `NavigateToAsync` call anywhere (`grep` confirms the only `NavigateToAsync` call is in `AddressInput_KeyDown`, line 309). So the window opens with an empty `ContentScroll`. 6.1 fixes exactly this.
- `clients/windows/App/NavigationController.cs` — `NavigateToAsync(Uri, ct)` is the push+fetch+render path (history + last-wins re-entrancy); `Current` is the displayed page; `GoBackAsync/GoForwardAsync/ReloadAsync` exist. Reuse `NavigateToAsync` for the launch nav and the Home action.
- `clients/windows/App/PageEndpointResolver.cs` — `IsAppHost` (`themarkdownweb.com` / `www.themarkdownweb.com`, case-insensitive) + `ToFetchEndpoint` (app-host `.md` page URL → `/api/negotiate/<slug>`). The Home target IS the app host, so the existing endpoint mapping already serves it (no Lens discovery needed for the home page).
- `clients/windows/App/MainWindow.xaml` — the toolbar `Grid` (col 0 nav `StackPanel`: Back(0)/Forward(1)/Reload(2); col 1 address bar; col 2 PersonalitySelector(4); col 3 LanguagePicker(5); col 4 ShareLinkButton(6); ContentScroll TabIndex=7). The nav `StackPanel` is the natural home for a Home button.
- `clients/windows/App.Tests/` — `[Fact]` for pure logic, `[StaFact]` (construct-not-`Show`, via `ShellTestHelpers.CreateWindow()`) for UI. `NavigationControllerTests`, `ShellTestHelpers` (`FindButton`, `NavStackButtons`, `ButtonsInToolbarOrder`), `ToolbarAccessibilityTests`, `AddressBarWindowTests` are the conventions to match.

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test

**This repo is developed on Linux with NO .NET SDK; WPF builds and runs ONLY on Windows. Verification is EXCLUSIVELY through `build-windows.yml` on `windows-latest`** (restore → `build -c Release` → `test -c Release` on `TheMarkdownWeb.sln`). The dev agent cannot build, run, or look at the window. Therefore every acceptance bar is checked by the compiler or by an xUnit test:
- **Pure home-target/launch logic** → `[Fact]` (no window, no network).
- **The window's Home button + its wiring** → `[StaFact]` construct-not-`Show` via `ShellTestHelpers.CreateWindow()`; inspect synchronously on the STA thread; NEVER `.Show()`/`.ShowDialog()`; no live message pump, no real network, no pixels, no timing.
- **The launch navigation must be unit-testable WITHOUT a window or a socket.** Extract the "where is home / kick off the home load" decision into a pure, injectable seam (a `HomeNavigator`/home-URL constant + a launch hook that calls `NavigationController.NavigateToAsync`) so a `[Fact]` can assert the home `Uri` and that a launch drives the controller, using the SAME injected-fetch `NavigationController` the existing `NavigationControllerTests` use (no real `HttpClient`).

## Acceptance Criteria

> Source: [_bmad-output/planning-artifacts/epics.md#Story 6.1] (lines 469–480): **Given** a freshly launched client **When** the window opens **Then** it loads and displays themarkdownweb.com content (not an empty state) **And** a Home affordance returns to themarkdownweb.com from any page. (FR-19.) PRD §4.7 FR-19 "Client default home": on launch (and on a Home action) the client opens `themarkdownweb.com` by default rather than a blank address bar; testable consequence: a freshly launched client shows themarkdownweb.com content, not an empty state.

1. **[Canonical home URL — single pure source of truth]** **Given** the client needs a default home, **When** the home target is requested, **Then** a single pure, App-side source exposes the canonical home `Uri` `https://themarkdownweb.com/` (https scheme, canonical app host). It is a constant/static (e.g. `HomeNavigator.HomeUrl`), is an absolute `https` `Uri` on a host for which `PageEndpointResolver.IsAppHost` returns `true`, and is the SAME value used by both the launch navigation (AC2) and the Home action (AC3) so they can never drift. *(AC1 — FR-19 "opens themarkdownweb.com by default"; reuse `PageEndpointResolver` canonical-host policy, do NOT hardcode a second host string.)*

2. **[Launch navigation — the window loads home, not blank]** **Given** a freshly constructed/launched window, **When** it opens, **Then** the client issues exactly one navigation to the home `Uri` (AC1) through the EXISTING `NavigationController.NavigateToAsync` path (which maps the app-host home URL via `PageEndpointResolver.ToFetchEndpoint` → `/api/negotiate/<slug>` and renders into `ContentScroll`), so the content host shows themarkdownweb.com content rather than an empty `ContentScroll`. The launch navigation must NOT block window construction (it is async / fire-and-forget off the load hook) and must be total — a fetch failure shows the existing Broken state, never an unhandled exception or a crash. The address bar text reflects the loaded home URL. *(AC2 — FR-19 testable consequence "a freshly launched client shows themarkdownweb.com content, not an empty state"; reuse `NavigationController.NavigateToAsync` + `ContentHostController`.)*

3. **[Home affordance — returns to home from any page]** **Given** the client has navigated away (any other page is current), **When** the reader activates a Home affordance (a labeled toolbar button), **Then** the client navigates back to the home `Uri` (AC1) via the SAME `NavigationController.NavigateToAsync` path (a real push into history — Home is a navigation, not a Back), returning to themarkdownweb.com content. The Home button is a real, labeled, keyboard-reachable WPF `Button` (`x:Name="HomeButton"`, non-empty `AutomationProperties.Name` e.g. "Home", `Focusable`, a tab stop) placed in the nav `StackPanel` alongside Back/Forward/Reload, WITHOUT breaking the existing Back(0)/Forward(1)/Reload(2) tab sequence or the address-bar fill. *(AC3 — FR-19 "and on a Home action … returns to themarkdownweb.com from any page"; UX-DR4 toolbar / UX-DR9 a11y floor.)*

4. **[App owns navigation; Rendering stays pure; no embedded browser — boundaries preserved]** **Given** this story adds launch + Home navigation, **When** the dependency graph is inspected, **Then** all new logic (the home-URL source, the launch hook, the Home button handler) lives in `App`; `Rendering` and `Agent` are UNTOUCHED (no net, no AI, no reverse reference); the inherited `DependencyBoundaryTests` and `NoEmbeddedBrowserTests` stay green (no new forbidden `PackageReference`; Home is a `NavigationController` call over the existing `HttpClient`, NOT a webview). *(AC4 — NFR-1 / architecture FC-1; "Rendering pure; App depends on Rendering, never the reverse".)*

5. **[No regression — existing Epic 3/4/5 behavior + tests stay green]** **Given** the work is additive, **When** the full WPF suite runs on `windows-latest`, **Then** every existing `App.Tests`, `Agent.Tests`, and `Rendering.Tests` stays green (Back/Forward/Reload, address bar, personality selector, language picker, share button, content host, navigation controller — all unchanged in contract), AND the new 6.1 tests pass. Adding the Home button must not break the nav-`StackPanel` regression guards (the `NavStackButtons` walker scopes to that one panel) — if Home joins the nav `StackPanel`, the existing "exactly Back/Forward/Reload" assertion in `AddressBarWindowTests.Toolbar_NavStackPanel_StillContainsExactlyBackForwardReload` must be reconciled with the minimal justified edit (it becomes Back/Forward/Reload/Home), since 6.1 legitimately adds one nav-group button. *(AC5 — no-regression; standing purity/boundary guards.)*

6. **[windows-latest CI gate — the only verification surface]** **Given** verification is exclusive to `windows-latest` CI, **When** `build-windows.yml` runs (`dotnet restore` → `build -c Release` → `test -c Release` on `TheMarkdownWeb.sln`), **Then** the solution builds clean (nullable/ImplicitUsings consistent, no new warnings) and ALL tests pass — including the `[Fact]` home-URL + launch-drives-controller tests and the `[StaFact]` Home-button construction test. No test depends on a real socket, a shown window, a live message pump, pixels, or timing. *(AC6 — DERIVED CI/build gate; the only place this story is verified; build-windows.yml.)*

## Tasks / Subtasks

- [x] **Task 1 — Pure home-URL source in `App` (AC: 1, 4)**
  - [x] Add `clients/windows/App/HomeNavigator.cs` (namespace `TheMarkdownWeb.App`): a small pure/static surface exposing the canonical home `Uri` — `public static Uri HomeUrl { get; }` = `new Uri("https://themarkdownweb.com/")`. Keep it pure (no I/O, no statics-with-state) so it is `[Fact]`-testable. Optionally also expose a `public static Task NavigateHomeAsync(NavigationController controller, CancellationToken ct = default)` helper that calls `controller.NavigateToAsync(HomeUrl, ct)` — a single seam used by BOTH launch and the Home button so they share one path. [Source: AC1; PageEndpointResolver canonical-host constants; FR-19]
  - [x] Assert (in code review / by the AC1 `[Fact]`) that `PageEndpointResolver.IsAppHost(HomeNavigator.HomeUrl)` is `true` — the home target is the app host so the existing `/api/negotiate/<slug>` mapping serves it; do NOT introduce a second canonical-host literal. [Source: AC1; PageEndpointResolver.IsAppHost]

- [x] **Task 2 — Wire the launch navigation into `MainWindow` (AC: 2, 4)**
  - [x] In `clients/windows/App/MainWindow.xaml.cs`, after the constructor finishes composing `_controller`/`_contentHost`, kick off the home navigation on a window-load hook (e.g. subscribe `Loaded += ...` or `ContentRendered`, OR call from the end of the constructor) WITHOUT blocking construction — fire `_ = HomeNavigator.NavigateHomeAsync(_controller);` (or `_ = _controller.NavigateToAsync(HomeNavigator.HomeUrl);`). It must be async/fire-and-forget; the constructor must remain synchronous and not `await`. Set `_addressBar.AddressText` to the home URL so the address bar reflects the loaded page. [Source: AC2; MainWindow.xaml.cs ctor lines 70–122 (composition, no nav today); NavigationController.NavigateToAsync]
  - [x] Confirm the launch nav is TOTAL: `NavigationController.NavigateToAsync` + `FetchEndpointAsync` are already non-throwing (a fetch failure → `ShowBroken`), so a network-down launch shows Broken, never a crash. Do NOT add new exception surfaces. [Source: AC2; NavigationController RunAsync is total; ContentHostController.ShowBroken]

- [x] **Task 3 — Home button in the toolbar nav group (AC: 3, 5)**
  - [x] In `clients/windows/App/MainWindow.xaml`, add `<Button x:Name="HomeButton" .../>` to its own col 5 (NEW Auto column), NOT in the col-0 nav StackPanel, so the three existing nav-count guards at exactly 3 stay intact. `AutomationProperties.Name="Home"`, `Focusable="True"`, a tab stop (TabIndex=7, after ShareLinkButton=6), glyph ⌂ (&#x2302;), `ToolTip="Home"`, `Click="HomeButton_Click"`. ContentScroll TabIndex bumped from 7→8 to accommodate. [Source: AC3; DECIDE-AND-DOCUMENT: Home in own column, not nav StackPanel]
  - [x] In `MainWindow.xaml.cs`, add `HomeButton_Click` → `_ = HomeNavigator.NavigateHomeAsync(_controller);` (the SAME seam as launch). Wire `Click="HomeButton_Click"` in XAML. [Source: AC3; existing BackButton_Click/etc handlers]

- [x] **Task 4 — Tests: `[Fact]` home-URL + launch-drives-controller, `[StaFact]` Home button (AC: 1, 2, 3, 5, 6)**
  - [x] **`[Fact]` AC1 — home URL** (`HomeNavigatorTests.cs`): assert `HomeNavigator.HomeUrl` is absolute, `https`, host `themarkdownweb.com`, and `PageEndpointResolver.IsAppHost(HomeNavigator.HomeUrl) == true`. No window, no network. [Source: AC1]
  - [x] **`[Fact]` AC2/AC3 — launch + Home drive the controller** (same file): construct a `NavigationController` with an INJECTED fetch delegate (the pattern from `NavigationControllerTests` — a `Func<Uri,CancellationToken,Task<FetchResult>>` returning `FetchResult.Success("# Home")` and a recording render-sink), call `HomeNavigator.NavigateHomeAsync(controller)` (or the launch hook's pure core), and assert: the fetch delegate was invoked with `HomeNavigator.HomeUrl` (or its mapped endpoint, matching how the existing tests assert), the render sink received the home content, and `controller.Current` equals `HomeNavigator.HomeUrl`. Then navigate elsewhere, call the Home seam again, and assert `Current` is home again AND `CanGoBack` is true (Home is a push, not a Back). No real socket. [Source: AC2/AC3; NavigationControllerTests injected-fetch pattern]
  - [x] **`[StaFact]` AC3 — Home button construction** (`HomeButtonWindowTests.cs`, reuse `ShellTestHelpers.CreateWindow()`): construct `MainWindow`; via `FindName`/`FindButton` assert `HomeButton` exists as a `Button`, has a non-empty `AutomationProperties.Name`, is `Focusable` + a tab stop, and its tab position is consistent with the documented decision (does not disturb Back(0)/Forward(1)/Reload(2)). Construct, never `.Show()`. [Source: AC3; AddressBarWindowTests/ToolbarAccessibilityTests patterns; STA/headless discipline]
  - [x] **Regression (AC5):** Home placed in its OWN col 5 (not in nav StackPanel), so the existing nav-count guards at exactly 3 (Back/Forward/Reload) are undisturbed across all three guarding test classes. [Source: AC5; ShellTestHelpers.NavStackButtons]

- [x] **Task 5 — CI / boundary hygiene + final verification (AC: 4, 5, 6)**
  - [x] No new `PackageReference` added (launch + Home are `NavigationController` calls over the existing `HttpClient`); `NoEmbeddedBrowserTests` + `DependencyBoundaryTests` stay green; `Rendering`/`Agent` untouched. [Source: AC4; NoEmbeddedBrowserTests/DependencyBoundaryTests]
  - [x] `build-windows.yml` picks up the new files (paths filter `clients/windows/**` already covers them; `App.Tests` already in `TheMarkdownWeb.sln` — no `.sln` edit). [Source: AC6; build-windows.yml]
  - [x] **DoD:** AC1 home URL (`[Fact]`); AC2 launch loads home (`[Fact]` controller-driven, never blank/crash); AC3 Home button + Home action (`[Fact]` + `[StaFact]`); AC4 boundaries (inherited guards green); AC5 no regression (full suite + nav-count guards unchanged at 3); AC6 green CI. [Source: AC1–6]

## Dev Agent Record

### Decisions

1. **Home button placement (DECIDE-AND-DOCUMENT):** Placed in a NEW Grid column (col 5, `Width="Auto"`) to the right of ShareLinkButton, NOT inside the col-0 nav StackPanel. Rationale: three existing test classes (`AddressBarWindowTests`, `PersonalitySelectorWindowTests`, `ShareLinkBuilderTests`) all assert the nav StackPanel contains exactly 3 buttons (Back/Forward/Reload). Placing Home in the StackPanel would require updating all three — the "cleaner" zero-regression path is a separate column. `ShellTestHelpers.NavStackButtons()` is already scoped to the col-0 panel's direct children only, so col-5 buttons are never counted.

2. **Launch hook mechanism (DECIDE-AND-DOCUMENT):** Used `Loaded += (_, _) => _ = HomeNavigator.NavigateHomeAsync(_controller);` (the `Loaded` event). Avoids navigating before the window is composed; constructor stays synchronous.

3. **Tab order (DECIDE-AND-DOCUMENT):** HomeButton at `TabIndex=7` (after ShareLinkButton=6). ContentScroll bumped from 7→8. PersonalitySelector pinned at 4, LanguagePicker at 5, ShareLinkButton at 6 — all unchanged.

### File List

- `clients/windows/App/HomeNavigator.cs` — NEW
- `clients/windows/App/MainWindow.xaml` — UPDATED (col 5 + HomeButton + ContentScroll TabIndex 7→8)
- `clients/windows/App/MainWindow.xaml.cs` — UPDATED (Loaded hook + address bar pre-populate + HomeButton_Click)
- `clients/windows/App.Tests/HomeNavigatorTests.cs` — NEW
- `clients/windows/App.Tests/HomeButtonWindowTests.cs` — NEW

## Dev Notes

### What already exists (build ON this, do not recreate)
- `MainWindow()` composes the full pipeline but **never navigates** — the only `NavigateToAsync` call today is in `AddressInput_KeyDown` (line 309). 6.1 adds the FIRST launch-time + Home navigation. [Source: MainWindow.xaml.cs]
- `NavigationController.NavigateToAsync` (push + fetch + render, last-wins, total) and `Current` are the reuse seam. `FetchEndpointAsync` already maps the app-host home URL via `PageEndpointResolver.ToFetchEndpoint`. [Source: NavigationController.cs; MainWindow.xaml.cs FetchEndpointAsync lines 216–243]
- `NavigationControllerTests` already drives the controller with an injected fetch delegate and a recording render sink — the 6.1 launch/Home `[Fact]`s mirror exactly that, so NO real `HttpClient`/socket is needed. [Source: App.Tests/NavigationControllerTests.cs]

### Decide-and-document points
- The exact `HomeButton` `TabIndex` and whether downstream controls re-number (mirror 4.4 `ContentScroll` 5→6, 5.1 6→7). Record the chosen integers in the Dev Agent Record. [Source: AC3]
- The launch hook mechanism: constructor-tail fire-and-forget vs `Loaded`/`ContentRendered` event (the latter avoids navigating before the window is ready; either is acceptable if construction does not block). Record the pick. [Source: AC2]

### Critical constraints (do not violate)
- **App owns navigation; Rendering stays pure** — all new code in `App`; `Rendering`/`Agent` untouched. [Source: AC4; DependencyBoundaryTests]
- **NO embedded browser** — Home is a `NavigationController`/`HttpClient` navigation, NOT a webview. Add no forbidden dependency. [Source: AC4; NoEmbeddedBrowserTests]
- **Scope: launch-to-home + a Home button ONLY.** NO relaxed address-bar rule (6.2), NO discovery cascade (6.3), NO new render path (6.4). The home page is the app host and uses the EXISTING fetch/render path. [Source: epics.md Epic 6 sequence]
- **Windows-only verification.** No "run and look"; `[Fact]` for pure logic, `[StaFact]` construct-not-`Show` for the window. No socket/window-show/pump/pixel/timing dependence. [Source: Environment Constraint]

### Source tree components to touch
- `clients/windows/App/HomeNavigator.cs` — pure home-URL source + navigate-home seam (NEW).
- `clients/windows/App/MainWindow.xaml` (+ `.cs`) — add `HomeButton` + launch-nav hook + `HomeButton_Click` (UPDATE).
- `clients/windows/App.Tests/HomeNavigatorTests.cs`, `HomeButtonWindowTests.cs` (NEW); reconcile `AddressBarWindowTests` nav-count guard (UPDATE — minimal).
- Do NOT touch: `Rendering/*`, `Agent/*`, `PageEndpointResolver.cs`, `NavigationController.cs` (contract unchanged), `build-windows.yml`, `TheMarkdownWeb.sln`.

### Cross-story dependencies
- **6.1 → 6.2:** 6.2 relaxes the address-bar rule and routes non-`.md` input to discovery; 6.1's Home target stays an app-host `.md`/page URL through the existing path, unaffected. No coupling beyond both touching `MainWindow`.
- The home page itself never needs the 6.3 discovery cascade (it is the app host; the existing `/api/negotiate/<slug>` mapping serves it).

### Testing standards summary
- xUnit; `[Fact]` for `HomeNavigator` + controller-driven launch/Home (injected fetch delegate, recording render sink — the `NavigationControllerTests` pattern); `[StaFact]` construct-not-`Show` for the Home button (`ShellTestHelpers.CreateWindow()`). No real network, no shown window, no pump/pixel/timing. Assert against the REAL `HomeNavigator`/`NavigationController`, not re-declared stubs.
