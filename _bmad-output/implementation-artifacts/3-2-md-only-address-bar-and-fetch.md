# Story 3.2: `.md`-only address bar and fetch

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want to type a `.md` URL and have it loaded,
so that I can open any Markdown Web page in the client.

## Context note (this is the SECOND `clients/windows/` feature story — Epic 3, builds on 3.1)

Story 3.1 shipped the **app shell only**: the WPF window (`MainWindow`, titlebar "The Markdown Web") and the browser-like toolbar (Back / Forward / Reload) wired to an inert `ShellViewModel`. It deliberately **reserved an empty `Grid` column (column 1, `Width="*"`)** in the toolbar `Border` for this story's address bar, and left a named, empty `<Border x:Name="ContentHost"/>` for the (future, Story 3.3/3.6) render. Story 3.1 is `done` and green on `windows-latest` CI (14/14 tests, run #6).

**Story 3.2 fills that reserved address-bar slot and adds the first networking the client does**: an address bar (lock icon + host/path display + `.md only` tag), `.md`-only URL acceptance, a non-`.md` decline with an "open in system browser" option, and the actual **fetch of the raw markdown over HTTP with `Accept: text/markdown`** — plus the Loading / Broken-or-missing-`.md` / Non-`.md` states from EXPERIENCE.md State Patterns.

**Story 3.2 does NOT render the markdown.** The fetched markdown string is held / shown raw or in a placeholder; Markdig→FlowDocument rendering is **Story 3.3**, highlighting **3.4**, in-client links/media/nav **3.5**, the faithful basic render **3.6**, and the personality selector **Epic 4**. Real back/forward **history** wiring begins here **only as far as fetch needs it** (e.g. a "current address / last-fetched" notion) — keep it minimal; the deep history stack is 3.5.

**What this story builds ON (do NOT recreate):**
- `clients/windows/App/MainWindow.xaml` — DockPanel with the top toolbar `Border`; toolbar is a 2-column `Grid`: column 0 = `StackPanel` of `BackButton`/`ForwardButton`/`ReloadButton`; **column 1 (`Width="*"`) is the reserved-but-empty address-bar slot** — slot the address bar there. `<Border x:Name="ContentHost"/>` stays empty (3.3/3.6 fills it).
- `clients/windows/App/MainWindow.xaml.cs` — holds `ShellViewModel _viewModel`, sets `DataContext`, three `Click` handlers.
- `clients/windows/App/ShellViewModel.cs` — `enum ShellAction { None, Back, Forward, Reload }` + inert `ShellViewModel` (`LastAction`, `OnBack/OnForward/OnReload`). Extend or compose, do not break its existing contract (it has passing tests).
- `clients/windows/App.Tests/` — the existing test project (`net10.0-windows`, `UseWPF`, `IsTestProject`; `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2, `Xunit.StaFact` 1.1.11). **It already exists — add new test files here; do NOT recreate the project.** Existing tests (`ShellWindowTests`, `ToolbarAccessibilityTests`, `ShellViewModelTests`, `NoEmbeddedBrowserTests`, `DependencyBoundaryTests`, `ShellTestHelpers`) MUST stay green.
- `.github/workflows/build-windows.yml` — windows-latest CI: `dotnet restore` → `build -c Release` → `test -c Release` on `TheMarkdownWeb.sln`. **This CI is the ONLY way this story is verified** (see Environment Constraint).
- Epic 2 (Story 2.7, shipped) — content negotiation: raw markdown is served at the **Function endpoint** `/api/negotiate/<slug>` (200, `Content-Type: text/markdown; charset=utf-8`, `Vary: Accept`; 404 for a missing slug). True same-URL negotiation at the page URL is **deferred** (Azure SWA route rules cannot branch on `Accept`). **The native client fetches markdown by sending `Accept: text/markdown`.**

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test (drives AC3, AC6, AC7, Tasks 5–7)

**This repo is developed on Linux with NO .NET SDK installed; WPF builds and runs ONLY on Windows.** The dev agent CANNOT build, run, or visually confirm the WPF window locally. **Verification happens exclusively through the `build-windows.yml` GitHub Actions CI on `windows-latest`** (restore → build -c Release → test -c Release). Therefore:

- Every acceptance bar that must be *checked* has to be checked **either by the compiler (build succeeds) or by an xUnit test that runs in `dotnet test` on `windows-latest`** — never by "launch the app and look."
- The CI runner is **headless / no interactive desktop session**. Do NOT write tests that show a real visible window, message-pump a live UI, hit the real network, launch a real browser process, or assert screen pixels. Prefer asserting on **pure logic** (URL validation, fetch behavior with a **stubbed `HttpMessageHandler`**) via `[Fact]`, and on **constructed WPF objects** (`new MainWindow()` / address-bar control inspected synchronously) via `[StaFact]`.
- WPF UI objects have **thread-affinity (STA)** — any test that constructs a `Window`/`Control` MUST run on an STA thread (`Xunit.StaFact`'s `[StaFact]`), else it throws `InvalidOperationException: The calling thread must be STA`. Construct the window — **never `.Show()`/`.ShowDialog()`** it (headless). Inspect synchronously on the same STA thread; no cross-thread access, no `Dispatcher.Run`/live message pump, no `Measure`/`Arrange` unless a layout pass is genuinely required (prefer logical-tree / `FindName`), no timing/pixel dependence. (Same discipline as 3.1; reuse `ShellTestHelpers`.)
- **The network MUST NOT be touched by any test.** The fetcher takes an injectable `HttpClient`/`HttpMessageHandler`; fetch tests use a **stub handler** that records the outgoing request (asserting `Accept: text/markdown`) and returns a canned response — no socket is opened. **"Open in the system browser" is behind an injectable seam** (e.g. an `IUrlLauncher` / `Action<Uri>`) so the decline test asserts *which* URL would be launched **without spawning a process**.
- **`.md`-only enforcement and fetch are App-side, pure-testable.** The validator (`IsLoadableMarkdownUrl`) is a plain static/pure method tested by `[Fact]` (no window, no net). The fetcher is `[Fact]`-tested with the stub handler. Only the address-bar UI state (lock / host-path / `.md only` tag, and the visual decline affordance) needs `[StaFact]` construction tests.

## Acceptance Criteria

1. **[address-bar visuals — lock + host/path + `.md only` tag]** **Given** the open shell window, **When** it displays, **Then** the toolbar's reserved slot (the existing `Grid` column 1, to the right of Back/Forward/Reload) hosts an **address-bar control** that shows, structurally: **(a)** a **lock indicator** (a glyph/icon element, e.g. `🔒`/`⛊` or a vector, with `AutomationProperties.Name` = `"Secure"` or `"Lock"` — the glyph alone is NOT an acceptable accessible name); **(b)** a **host/path display** — a `TextBox`/`TextBlock` (`x:Name="AddressInput"`) that takes/shows the typed URL's host + path; and **(c)** a **`.md only` tag** — a visible text element whose content is **exactly** the string `.md only` (ordinal). The address bar is a structural region of `MainWindow` reachable by `x:Name` (e.g. `AddressBar`, `LockIndicator`, `AddressInput`, `MdOnlyTag`). It is keyboard-reachable (the input is `Focusable`, in the tab order **after** the three nav buttons — i.e. its effective tab position is > Reload's). Building the address bar MUST NOT disturb the Back→Forward→Reload tab order or AC2/AC3 of Story 3.1 (those tests stay green). *(AC1 — EXPERIENCE.md Component Patterns "address-bar … Shows lock + host/path + `.md only` tag"; UX-DR5; DESIGN.md address-bar; mockup native-client.png. Lock/tag accessible-name bar is the NFR-6/UX-DR9 floor applied to the new control.)*

2. **[`.md`-only acceptance — pure validator]** **Given** a string entered in the address bar, **When** it is validated, **Then** a pure App-side predicate **`AddressBarValidation.IsLoadableMarkdownUrl(string?)`** returns **true** iff the input is an absolute `http`/`https` URL whose **path ends in `.md`** (case-insensitive on the extension, ignoring any query/fragment), and **false** otherwise. Concretely it returns `true` for `https://themarkdownweb.com/guides/powder-day.md`, `http://host/x.md`, `https://h/a/b/c.md?ref=1`, `https://h/page.MD`; and `false` for `null`/empty/whitespace, `https://themarkdownweb.com/about` (no `.md`), `https://h/page.html`, `https://h/notmd` , `mailto:x@y.z`, `ftp://h/x.md` (non-http scheme), `not a url`, a bare `host/x.md` with no scheme, and `https://h/x.md.html` (does not end in `.md`). The check is **scheme- and extension-strict** and **does not** open a socket. *(AC2 — FR-9 "type a `.md` URL and have it loaded"; EXPERIENCE.md "accepts and loads `.md` URLs only"; address-bar tag `.md only`.)*

3. **[non-`.md` decline + open-in-system-browser]** **Given** a non-`.md` URL entered in the address bar, **When** the reader submits it, **Then** the client **declines to load it** (no fetch is issued), surfaces a clear, testable **"loads `.md` only"** decline state (per AC6 state model), **and offers to open it in the system browser** instead. The open-in-browser action goes through an injectable seam **`IUrlLauncher.Open(Uri)`** (default impl wraps `Process.Start(new ProcessStartInfo(url){ UseShellExecute = true })`), so a `[Fact]` can assert that declining `https://example.com/about` (1) issues **zero** HTTP requests through the (stub) fetcher and (2) would launch **exactly** `https://example.com/about` via the (fake) launcher when the reader accepts the offer. A non-URL / un-launchable string is declined without offering a launch (no crash). *(AC3 — EXPERIENCE.md State Patterns "Non-`.md` address (native) — the client declines to load it and explains it loads `.md` only; offers to open it in the system browser instead"; Interaction Primitives "External `http(s)` link → open in the system browser".)*

4. **[fetch raw markdown via `Accept: text/markdown`]** **Given** a valid `.md` URL, **When** the client loads it, **Then** an App-side **`MarkdownFetcher`** (constructed with an injectable `HttpClient`/`HttpMessageHandler`) issues an HTTP **GET** for that URL with the request header **`Accept: text/markdown`** and returns the response body as the raw markdown **string** on `200`. **Proven testably with a stub `HttpMessageHandler`** (no real network): the stub asserts the outgoing request's `Accept` header contains `text/markdown` and the method is `GET`, then returns `200` with body `# Hello` and `Content-Type: text/markdown; charset=utf-8`; the test asserts the returned string equals `# Hello`. The fetch is **async** (`Task<FetchResult>`), does not block the UI thread, and is cancelable (accepts a `CancellationToken`). The returned markdown is **held as a string** (exposed on the address-bar/shell VM, e.g. `LastFetchedMarkdown`) and **not rendered** at this story — rendering is Story 3.3 (the `ContentHost` stays empty or shows the raw string in a placeholder only). *(AC4 — FR-14 consume side; architecture "App/ — shell, window, navigation, fetch raw .md"; Story 2.7 endpoint contract `text/markdown; charset=utf-8`, `Vary: Accept`.)*

5. **[App owns networking; Rendering stays pure — boundary preserved]** **Given** this story adds the first client networking, **When** the dependency graph is inspected, **Then** **all fetch + URL-validation + URL-launch logic lives in the `App` project** (`AddressBarValidation`, `MarkdownFetcher`, `IUrlLauncher`, the address-bar VM) and **`Rendering` gains NO networking, NO AI, and NO reference to `App`/`Agent`** — it is untouched by this story. The existing `DependencyBoundaryTests` (Rendering references neither App nor Agent; App references Rendering) MUST stay green, and the **no-Chromium/WebView2/embedded-browser guard (`NoEmbeddedBrowserTests`) MUST stay green** — adding an `HttpClient`-based fetcher introduces **no** browser engine (it is `System.Net.Http`, already in the framework; no new forbidden `PackageReference`). No webview, no `WebBrowser`, no embedded browser is added to satisfy "open in system browser" — that uses `Process.Start` shell-execute, NOT an in-app browser. *(AC5 — architecture FC-1 / NFR-1 no embedded browser; "Rendering pure (no net, no AI); App depends on Rendering, never the reverse".)*

6. **[Loading / Broken-or-missing-`.md` / Non-`.md` states]** **Given** a load attempt, **When** it proceeds, **Then** the address-bar/shell VM exposes an observable **`AddressBarState`** enum / status surface covering at least: **`Idle`**, **`Loading`** (a fetch is in flight — EXPERIENCE.md "show lightweight progress, not a blank flash"), **`Loaded`** (markdown fetched and held), **`NotMarkdown`** (input declined because it is not a `.md` URL — the decline+open-in-browser offer of AC3), and **`Broken`** (the fetch failed: a non-`2xx` such as the endpoint's `404` for a missing slug, a network/`HttpRequestException`, a non-`text/markdown`/unexpected content, or a cancellation) — a clear "page not found / not a markdown page" state that **never crashes** (PRD FR-2). Each transition is `[Fact]`-testable on the VM/fetcher without a window: a `.md` URL whose stub returns `404` ends in `Broken`; a stub that throws ends in `Broken` (no unhandled exception); a valid fetch goes `Loading`→`Loaded`; a non-`.md` input goes straight to `NotMarkdown` (no fetch). The `Loading`/`Broken` visual treatment in the address bar is asserted structurally (a `[StaFact]` may check the control reflects state) but the **state logic itself is window-free `[Fact]`s**. *(AC6 — EXPERIENCE.md State Patterns "Loading", "Broken / missing `.md` … never a crash", "Non-`.md` address"; PRD FR-2.)*

7. **[windows-latest CI gate — the only verification surface]** **Given** this story is verified exclusively on `windows-latest` CI, **When** `build-windows.yml` runs (`dotnet restore` → `build -c Release` → `test -c Release` on `TheMarkdownWeb.sln`), **Then** the whole solution **builds clean** (no new warnings-as-errors regressions; `nullable`/`ImplicitUsings` consistent with existing projects) and **all tests pass green**, including: all **existing** Story 3.1 `App.Tests` (shell/toolbar/accessibility/no-Chromium/boundary) and `Rendering.Tests` (no regression), **plus** the new Story 3.2 tests — the `[Fact]` validator tests (AC2), the `[Fact]` fetcher tests with the stub handler (AC4), the `[Fact]` decline/launcher tests (AC3), the `[Fact]` state-machine tests (AC6), and the `[StaFact]` address-bar construction tests (AC1). No test depends on real network, a real browser process, a shown window, a live message pump, pixels, or timing. *(AC7 — DERIVED CI/build gate; the only place this story is actually verified, per the Environment Constraint; build-windows.yml; NFR-7 "don't reinvent plumbing" — use `System.Net.Http`.)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 3.2: `.md`-only address bar and fetch] (lines 299–310): **Given** the client toolbar **When** I enter a `.md` URL **Then** the address bar shows a lock, host/path, and a `.md only` tag, and the client fetches the raw markdown via `Accept: text/markdown` **And** entering a non-`.md` URL is declined with an option to open it in the system browser instead. (FR-9, FR-14 consume, UX-DR5, UX-DR7). **AC1** = the address-bar visuals half of the "Then" (lock + host/path + `.md only` tag). **AC2** = the `.md`-only acceptance predicate that "I enter a `.md` URL" implies. **AC4** = the "fetches the raw markdown via `Accept: text/markdown`" half. **AC3** = the epic's "And … non-`.md` URL is declined with an option to open it in the system browser." **AC6** = the EXPERIENCE.md State Patterns (Loading / Broken-or-missing / Non-`.md`) the load flow must cover, derived but mandated by FR-2 "never a crash." **AC5** = the architecture boundary (App owns net; Rendering pure; no embedded browser) preserved — derived/HARD, guarded by the inherited tests. **AC7** = the derived build/CI gate — the only place this story is verified (Linux dev box has no .NET SDK; WPF is Windows-only).

## Tasks / Subtasks

- [ ] **Task 1 — Pure `.md`-only URL validator in `App` (AC: 2)**
  - [ ] Add `clients/windows/App/AddressBarValidation.cs` (namespace `TheMarkdownWeb.App`): a `public static class AddressBarValidation` with `public static bool IsLoadableMarkdownUrl(string? input)`. Implement with `Uri.TryCreate(input, UriKind.Absolute, out var uri)` requiring `uri.Scheme` ∈ {`http`,`https`} (ordinal/insensitive) AND `uri.AbsolutePath` ends with `.md` (case-insensitive, after stripping query/fragment — `AbsolutePath` already excludes those). Return `false` for null/empty/whitespace, relative URLs, non-http schemes, and paths not ending `.md`. Keep it **pure** (no I/O, no statics-with-state) so it is `[Fact]`-testable with no window/network. [Source: AC2; FR-9; EXPERIENCE.md "accepts and loads `.md` URLs only"]
  - [ ] (Optional helper, only if the address-bar display needs it) `public static bool TryGetHostPath(string input, out string hostPath)` to format the `host + path` shown in the bar (AC1) — keep pure. [Source: AC1 host/path display]

- [ ] **Task 2 — `MarkdownFetcher` in `App` (App owns networking; injectable HttpClient) (AC: 4, 5, 6)**
  - [ ] Add `clients/windows/App/MarkdownFetcher.cs` (namespace `TheMarkdownWeb.App`). Constructor takes an injectable `HttpClient` (or `HttpMessageHandler`) so tests can pass a stub and the real app passes a shared `HttpClient`. Expose `public async Task<FetchResult> FetchAsync(string url, CancellationToken ct = default)`. [Source: AC4; architecture "App/ … fetch raw .md"; NFR-7 use System.Net.Http]
  - [ ] In `FetchAsync`: build an `HttpRequestMessage(HttpMethod.Get, url)`, set `request.Headers.Accept.ParseAdd("text/markdown")` (the content-negotiation contract from Story 2.7), `SendAsync`, and on **`200`** return the body string as `FetchResult.Success(markdown)`. On a non-`2xx` (incl. the endpoint's `404`), on `HttpRequestException`/`TaskCanceledException`, or on unexpected/empty content, return `FetchResult.Failure(reason)` — **never throw out of `FetchAsync`** (AC6 "never a crash"). [Source: AC4, AC6; Story 2.7 endpoint `200 text/markdown; charset=utf-8`, `404` missing slug; EXPERIENCE.md "Broken / missing `.md` … never a crash"]
  - [ ] Define `FetchResult` (a small `readonly record struct`/sealed class in `App`): `bool IsSuccess`, `string? Markdown`, `string? FailureReason`. No rendering, no Markdig — the markdown is a raw string handed back (Story 3.3 renders it). [Source: AC4 "held as a string … not rendered at this story"]
  - [ ] Do NOT add any `PackageReference` for networking — `System.Net.Http` ships with the framework. Add NO webview/browser dependency. [Source: AC5; NFR-1/FC-1; AC7 no new forbidden dep]

- [ ] **Task 3 — Open-in-system-browser seam (AC: 3, 5)**
  - [ ] Add `clients/windows/App/IUrlLauncher.cs` (namespace `TheMarkdownWeb.App`): `public interface IUrlLauncher { void Open(Uri url); }` plus a default `public sealed class SystemBrowserLauncher : IUrlLauncher` that calls `Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true })`. This opens the OS default browser — **NOT** an in-app/embedded browser (guards AC5/NFR-1). Wrap the `Process.Start` in try/catch so an un-launchable URL does not crash. [Source: AC3 "open it in the system browser"; EXPERIENCE.md Interaction Primitives "External http(s) link → open in the system browser (the client is not a general web browser)"; AC5 no embedded browser]

- [ ] **Task 4 — Address-bar view-model: validate → decline-or-fetch → state (AC: 2, 3, 4, 6)**
  - [ ] Add `clients/windows/App/AddressBarViewModel.cs` (namespace `TheMarkdownWeb.App`). Construct it with the injectable `MarkdownFetcher` and `IUrlLauncher` (DI-by-constructor so tests pass fakes). Expose: `string AddressText { get; set; }` (the typed URL); an observable `AddressBarState State { get; }` (`enum AddressBarState { Idle, Loading, Loaded, NotMarkdown, Broken }`); `string? LastFetchedMarkdown { get; }`; `string? DeclinedUrl { get; }` (the non-`.md` url offered for browser-open). Implement `INotifyPropertyChanged` so the XAML can bind state (consistent with WPF VM conventions). [Source: AC1, AC6; EXPERIENCE.md State Patterns]
  - [ ] `public async Task SubmitAsync(CancellationToken ct = default)`: if `AddressBarValidation.IsLoadableMarkdownUrl(AddressText)` is **false** → set `State = NotMarkdown`, record `DeclinedUrl` (if it parses as an absolute http(s) Uri, else null), issue **NO** fetch. If **true** → set `State = Loading`, `await _fetcher.FetchAsync(AddressText, ct)`; on success set `LastFetchedMarkdown` + `State = Loaded`; on failure set `State = Broken`. Catch defensively so `SubmitAsync` never throws (AC6). [Source: AC2, AC3, AC4, AC6]
  - [ ] `public void OpenDeclinedInBrowser()`: if `DeclinedUrl` is a launchable absolute http(s) Uri, call `_launcher.Open(uri)`; else no-op (no crash). This is the action behind the "open in system browser" offer. [Source: AC3]
  - [ ] Keep history/back-forward **minimal**: at most track the current/last-fetched address so a future `OnReload` can re-fetch it — do NOT build a history stack (that is Story 3.5). Optionally let `ShellViewModel.OnReload` re-trigger `SubmitAsync` on the current address; otherwise leave the 3.1 inert handlers intact. Do not break `ShellViewModelTests`. [Source: scope note "history minimal"; epics.md 3.5]

- [ ] **Task 5 — Wire the address bar into `MainWindow.xaml` (AC: 1)**
  - [ ] In `MainWindow.xaml`, fill the reserved toolbar **`Grid` column 1** (the empty `Width="*"` slot from Story 3.1) with an `x:Name="AddressBar"` container holding, left→right: `x:Name="LockIndicator"` (glyph/icon, `AutomationProperties.Name="Secure"`), `x:Name="AddressInput"` (a `TextBox` bound to `AddressText`, `Focusable`, stretches to fill), and `x:Name="MdOnlyTag"` (a `TextBlock`/`Border` whose text is **exactly** `.md only`). Do NOT alter the Back/Forward/Reload `StackPanel` in column 0 or their `TabIndex` 0/1/2. Give the address input a `TabIndex` ≥ 3 so it follows Reload in tab order (AC1). [Source: AC1; EXPERIENCE.md "lock + host/path + `.md only` tag"; DESIGN.md address-bar; mockup native-client.png; reserved column from 3.1 MainWindow.xaml]
  - [ ] Bind/wire submission: pressing Enter in `AddressInput` (or a Go affordance) invokes `AddressBarViewModel.SubmitAsync`. Bind the lock/`.md only`/loading affordance to `State` where practical. Keep the markdown UNRENDERED — `ContentHost` stays empty (or shows `LastFetchedMarkdown` as raw text only, clearly a placeholder, NOT a Markdig render). [Source: AC4 "not rendered at this story"; AC6 Loading affordance]
  - [ ] In `MainWindow.xaml.cs`, construct the `AddressBarViewModel` (with a real `MarkdownFetcher(new HttpClient())` + `SystemBrowserLauncher`), expose it for binding (e.g. nested on the existing `DataContext`/`ShellViewModel`, or as a second bound property). Keep `ShellViewModel` and its tests intact. [Source: AC1; existing MainWindow.xaml.cs DataContext]

- [ ] **Task 6 — Tests: pure `[Fact]`s (validator, fetcher, launcher/decline, state machine) + `[StaFact]` address-bar construction (AC: 1, 2, 3, 4, 6, 7)**
  - [ ] **`[Fact]` AC2 — validator** (`AddressBarValidationTests.cs`): a theory over the true-cases (`https://themarkdownweb.com/guides/powder-day.md`, `http://host/x.md`, `https://h/a/b/c.md?ref=1`, `https://h/page.MD`) and false-cases (`null`, `""`, `"   "`, `https://h/about`, `https://h/page.html`, `https://h/notmd`, `mailto:x@y.z`, `ftp://h/x.md`, `"not a url"`, `host/x.md` (no scheme), `https://h/x.md.html`). No window, no network. [Source: AC2]
  - [ ] **`[Fact]` AC4 — fetcher sends `Accept: text/markdown`** (`MarkdownFetcherTests.cs`): build `MarkdownFetcher` with a **stub `HttpMessageHandler`** (a `DelegatingHandler`/`HttpMessageHandler` subclass whose `SendAsync` captures the request and returns a canned `HttpResponseMessage`). Assert the captured request: `Method == GET`, and `request.Headers.Accept` contains a `text/markdown` media type. Stub returns `200` + body `# Hello` (`Content-Type: text/markdown; charset=utf-8`); assert `result.IsSuccess` and `result.Markdown == "# Hello"`. **No socket opened.** [Source: AC4; Story 2.7 contract]
  - [ ] **`[Fact]` AC6 — fetcher failure paths** (same file): stub returns `404` → `result.IsSuccess == false` (maps to `Broken`); stub `SendAsync` throws `HttpRequestException` → fetcher returns failure, does NOT propagate (no unhandled throw); a canceled token → failure, no crash. [Source: AC6; EXPERIENCE.md "never a crash"; Story 2.7 `404` missing slug]
  - [ ] **`[Fact]` AC3 — decline + launcher** (`AddressBarViewModelTests.cs`): a **fake `IUrlLauncher`** recording the last `Open(Uri)`, and a **fake/stub fetcher** counting `FetchAsync` calls. `AddressText = "https://example.com/about"`; `await SubmitAsync()` → `State == NotMarkdown`, fetcher call count `== 0` (no fetch), `DeclinedUrl == "https://example.com/about"`. Then `OpenDeclinedInBrowser()` → fake launcher's last URL `== https://example.com/about`. A non-URL declined input → `NotMarkdown`, `OpenDeclinedInBrowser()` no-ops (no throw). **No real browser process spawned.** [Source: AC3]
  - [ ] **`[Fact]` AC6 — VM state machine** (same file): valid `.md` URL + success stub → states reach `Loaded`, `LastFetchedMarkdown` set; `404`/throwing stub → `Broken` (no crash); non-`.md` → `NotMarkdown` with zero fetches. All on the VM with fakes — no window. [Source: AC6]
  - [ ] **`[StaFact]` AC1 — address-bar construction** (`AddressBarWindowTests.cs`, reuse `ShellTestHelpers.CreateWindow()`): construct `MainWindow`; via `FindName` assert `AddressBar`, `LockIndicator`, `AddressInput`, `MdOnlyTag` exist; assert `MdOnlyTag`'s text is exactly `.md only` (ordinal); assert `LockIndicator`'s `AutomationProperties.GetName` is non-empty (glyph not accepted as the name); assert `AddressInput.Focusable == true` and its effective tab position is **after** `ReloadButton`. Construct, never `.Show()`. [Source: AC1; ShellTestHelpers; Environment Constraint STA/headless]
  - [ ] **Regression guard:** do NOT modify the existing 3.1 tests; confirm they still pass with the new column-1 content (the `ShellTestHelpers` button-order walker is column-0-scoped and a new column-1 address bar must not introduce stray `Button`s into the nav `StackPanel`). If a Go button is added in the address bar, ensure the `Toolbar_Buttons_AreInBackForwardReloadOrder` walker still collects only Back/Forward/Reload (the walker climbs from a nav button to its `StackPanel`; keep the Go button OUT of that `StackPanel`). [Source: AC7; 3.1 ShellWindowTests/ShellTestHelpers]

- [ ] **Task 7 — CI / boundary / no-Chromium hygiene (AC: 5, 7)**
  - [ ] Confirm `build-windows.yml` picks up the new files: it runs `dotnet restore/build/test` on `TheMarkdownWeb.sln` and the `App.Tests` project already exists in the solution — **no `.sln` edit and no new project needed**; the `paths:` filter (`clients/windows/**`) already covers the new files. Verify, do not over-edit. [Source: AC7; build-windows.yml; App.Tests already in .sln]
  - [ ] Confirm the inherited `NoEmbeddedBrowserTests` stays green: the new `MarkdownFetcher` is `System.Net.Http` (no `PackageReference`, no forbidden substring); `SystemBrowserLauncher` is `Process.Start` shell-execute (NOT a `WebBrowser`/webview). Add NO forbidden dependency. [Source: AC5; NFR-1/FC-1; NoEmbeddedBrowserTests]
  - [ ] Confirm `DependencyBoundaryTests` stays green: this story edits `App`/`App.Tests` ONLY; `Rendering`/`Agent` untouched; `Rendering` gains no net/no AI/no reverse reference. [Source: AC5; DependencyBoundaryTests; architecture Rendering boundary]
  - [ ] Keep `nullable`/`ImplicitUsings` consistent; introduce no new build warnings. [Source: AC7; existing csproj conventions]

- [ ] **Task 8 — Final verification against ACs (Definition of Done — checked via CI, not locally) (AC: 1–7)**
  - [ ] **AC1:** address bar in the reserved slot with lock + host/path input + exact `.md only` tag, keyboard-reachable after Reload. Proven by the AC1 `[StaFact]`.
  - [ ] **AC2:** `IsLoadableMarkdownUrl` true/false matrix exact. Proven by the AC2 `[Fact]` theory.
  - [ ] **AC3:** non-`.md` declined (zero fetches) + open-in-system-browser launches the exact URL via the seam. Proven by the AC3 `[Fact]`.
  - [ ] **AC4:** fetch issues `GET` with `Accept: text/markdown` and returns the body string on `200`. Proven by the AC4 stub-handler `[Fact]`.
  - [ ] **AC5:** networking/validation/launch all in `App`; `Rendering` pure/untouched; no embedded browser. Proven by inherited `DependencyBoundaryTests` + `NoEmbeddedBrowserTests` staying green.
  - [ ] **AC6:** Idle/Loading/Loaded/NotMarkdown/Broken transitions; `404`/throw → `Broken` with no crash. Proven by the AC6 VM/fetcher `[Fact]`s.
  - [ ] **AC7:** `dotnet build -c Release` clean + `dotnet test -c Release` all green on `windows-latest` (existing 3.1 `App.Tests` + `Rendering.Tests` + new 3.2 tests); no real net/browser/window/pixel/timing. Proven by the green `build-windows.yml` run.
  - [ ] Push and confirm the `Build Windows Client` GitHub Actions run is green (the authoritative verification — there is no local build). Record the run result in the Dev Agent Record.

## Dev Notes

### What already exists (build ON this, do not recreate)

- **Shell (Story 3.1, done):** `MainWindow.xaml` = `DockPanel`; top toolbar `Border` (faint `#F3F3F3` fill + bottom border) holds a 2-column `Grid` — column 0 `StackPanel` with `BackButton`/`ForwardButton`/`ReloadButton` (`TabIndex` 0/1/2, `AutomationProperties.Name` set, glyph content), **column 1 (`Width="*"`) reserved EMPTY for this story's address bar**; `<Border x:Name="ContentHost"/>` empty (3.3/3.6). `MainWindow.xaml.cs` holds `ShellViewModel _viewModel` as `DataContext` + three `Click` handlers. `ShellViewModel.cs` = `enum ShellAction { None, Back, Forward, Reload }` + inert VM (`LastAction`, `OnBack/OnForward/OnReload`).
- **Tests (Story 3.1, green on windows-latest run #6, 14/14):** `App.Tests/` has `ShellWindowTests` (`[StaFact]` title + button presence/order), `ToolbarAccessibilityTests` (names/reachable/tab-order), `ShellViewModelTests` (4 `[Fact]`), `NoEmbeddedBrowserTests` (csproj tier + bound-closure tier), `DependencyBoundaryTests` (App→Rendering one-way), and `ShellTestHelpers` (`CreateWindow()`, `FindButton`, `ButtonsInToolbarOrder` — the order walker is scoped to the nav `StackPanel` and guards against non-Visual logical children). **Reuse `ShellTestHelpers`; do NOT modify the existing tests.**
- **Test project:** `App.Tests/TheMarkdownWeb.App.Tests.csproj` already references `App` and the test stack incl. `Xunit.StaFact` 1.1.11 — **add new `.cs` test files here; no new project, no `.sln` edit.**
- **Content negotiation (Story 2.7, shipped):** raw markdown at the Function endpoint `/api/negotiate/<slug>` — `GET` with `Accept: text/markdown` → `200`, `Content-Type: text/markdown; charset=utf-8`, `Vary: Accept`; missing slug → `404`. **The client fetches markdown by sending `Accept: text/markdown`.** (True same-URL negotiation at the page URL is deferred — SWA cannot branch on `Accept`. This story's fetcher just sends the header against the given `.md` URL; URL→endpoint routing nuance is not in scope here — fetch the URL the reader typed with the negotiation header.)
- **CI:** `.github/workflows/build-windows.yml` on `windows-latest`, `dotnet-version: 10.0.x`, triggers on `clients/windows/**`; restore → `build -c Release` → `test -c Release` on the `.sln`. **Sole verification path.**

### Critical constraints (do not violate)

- **App owns networking; Rendering stays pure.** Put `AddressBarValidation`, `MarkdownFetcher`, `IUrlLauncher`, `AddressBarViewModel` in `App`. `Rendering` gets NO net, NO AI, NO reverse reference — leave it untouched (and `Agent` too). Inherited `DependencyBoundaryTests` enforces this.
- **NFR-1 / FC-1 — NO embedded browser engine.** Fetch is `System.Net.Http` (framework, no `PackageReference`). "Open in system browser" is `Process.Start` shell-execute → the OS default browser, NOT an in-app `WebBrowser`/webview. Inherited `NoEmbeddedBrowserTests` (csproj + bound-closure tiers) must stay green — add no forbidden dependency.
- **`.md`-only + fetch are pure-testable.** Validator = pure `[Fact]`. Fetcher = `[Fact]` with a **stub `HttpMessageHandler`** asserting `Accept: text/markdown` (no socket). Launcher/decline = `[Fact]` with a **fake `IUrlLauncher`** (no process spawned). Only the address-bar visuals get `[StaFact]` construction tests.
- **Windows-only verification.** No .NET SDK on the Linux dev box; WPF is Windows-only; headless runner. Every check is compiler-or-`dotnet test`-on-windows-latest. No "run and look." `[StaFact]` for any test that news up a `Window`/`Control` — construct, never `.Show()`; inspect synchronously on the STA thread; no cross-thread/message-pump/pixel/timing dependence.
- **Scope: address bar + `.md`-only accept/decline + fetch + states ONLY.** NO Markdig→FlowDocument render (3.3), NO highlighting (3.4), NO in-client link/media/nav (3.5), NO basic render (3.6), NO personality selector (Epic 4). The fetched markdown is a held string / raw placeholder — not rendered. Back/forward history is minimal (only what fetch needs); the real stack is 3.5.

### Source tree components to touch

- `clients/windows/App/AddressBarValidation.cs` — pure `.md`-only predicate (NEW).
- `clients/windows/App/MarkdownFetcher.cs` — `HttpClient`-injectable fetcher + `FetchResult` (NEW).
- `clients/windows/App/IUrlLauncher.cs` — launcher seam + `SystemBrowserLauncher` (NEW).
- `clients/windows/App/AddressBarViewModel.cs` — validate→decline-or-fetch→state VM (NEW).
- `clients/windows/App/MainWindow.xaml` (+ `.cs`) — fill the reserved column-1 address-bar slot; wire the VM (UPDATE).
- `clients/windows/App.Tests/AddressBarValidationTests.cs`, `MarkdownFetcherTests.cs`, `AddressBarViewModelTests.cs`, `AddressBarWindowTests.cs` — new tests (NEW, in the EXISTING `App.Tests` project).
- Do NOT touch: `Rendering/*`, `Agent/*`, `Rendering.Tests/*`, `TheMarkdownWeb.sln` (App.Tests already in it), `build-windows.yml` (paths filter already covers it), and the existing 3.1 test files. `ShellViewModel.cs` may be extended only without breaking `ShellViewModelTests`.

### Intended App-side API surface (the exact contract for Step 4 TDD + Step 5 impl)

```csharp
namespace TheMarkdownWeb.App;

public static class AddressBarValidation
{
    // true iff absolute http/https URL whose path ends ".md" (case-insensitive), else false.
    public static bool IsLoadableMarkdownUrl(string? input);
    // optional: format the host+path shown in the bar (AC1).
    public static bool TryGetHostPath(string input, out string hostPath);
}

public readonly record struct FetchResult
{
    public bool IsSuccess { get; }
    public string? Markdown { get; }
    public string? FailureReason { get; }
    public static FetchResult Success(string markdown);
    public static FetchResult Failure(string reason);
}

public sealed class MarkdownFetcher
{
    public MarkdownFetcher(HttpClient http);          // injectable for stub-handler tests
    // GET url with Accept: text/markdown; 200 -> Success(body); non-2xx/exception -> Failure (never throws).
    public Task<FetchResult> FetchAsync(string url, CancellationToken ct = default);
}

public interface IUrlLauncher { void Open(Uri url); }
public sealed class SystemBrowserLauncher : IUrlLauncher { /* Process.Start(UseShellExecute=true) */ }

public enum AddressBarState { Idle, Loading, Loaded, NotMarkdown, Broken }

public sealed class AddressBarViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public AddressBarViewModel(MarkdownFetcher fetcher, IUrlLauncher launcher);
    public string AddressText { get; set; }
    public AddressBarState State { get; }
    public string? LastFetchedMarkdown { get; }
    public string? DeclinedUrl { get; }
    public Task SubmitAsync(CancellationToken ct = default);  // validate -> decline(NotMarkdown) or fetch(Loading->Loaded/Broken)
    public void OpenDeclinedInBrowser();                      // launch DeclinedUrl via IUrlLauncher
}
```

### Testing standards summary

- **Framework:** xUnit (existing `App.Tests` versions). **STA:** `Xunit.StaFact` `[StaFact]` only for the address-bar construction test (reuse `ShellTestHelpers.CreateWindow()`). Everything else — validator, fetcher, launcher/decline, state machine — is a plain `[Fact]` with NO window and NO network.
- **No real network:** the fetcher test injects a **stub `HttpMessageHandler`** that captures the request (asserting `Accept: text/markdown`, `GET`) and returns a canned response. No socket. **No real browser:** the decline test injects a **fake `IUrlLauncher`** recording the URL — no `Process.Start`.
- **No-tautology:** assert against the REAL `MarkdownFetcher`/`AddressBarValidation`/`AddressBarViewModel` and the REAL constructed `MainWindow` (the `.md only` tag is read off the actual control), not re-declared stubs of themselves.
- **No regression:** all 3.1 `App.Tests` and `Rendering.Tests` stay green; `NoEmbeddedBrowserTests` and `DependencyBoundaryTests` stay green (no forbidden dep, no reverse reference); the nav-button order walker still collects exactly Back/Forward/Reload.
- **STA/headless discipline:** construct the window on the `[StaFact]` STA thread, never `.Show()`; inspect synchronously; no cross-thread access, no live message pump, no pixels, no timing-dependent assertions.

### Project Structure Notes

- Aligns with architecture `clients/windows/{App,Rendering,Agent}`: this story adds App-only code (the address bar, validator, fetcher, launcher, VM) and App.Tests-only tests. No new project, no `.sln`/CI edit. `Rendering` stays the pure bedrock; `App` owns networking — exactly the architecture's "App/ … fetch raw .md" + "Rendering pure (no net, no AI)" boundary.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.2: `.md`-only address bar and fetch] (lines 299–310) — user story + ACs (FR-9, FR-14 consume, UX-DR5, UX-DR7).
- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.7: Content negotiation] (lines 266–278) — the producer side this story consumes (`Accept: text/markdown` → raw `.md`, `Vary: Accept`).
- [Source: _bmad-output/planning-artifacts/architecture.md] — `App/` = "shell, window, navigation, fetch raw .md"; `Rendering/` isolated/pure (no networking, no AI), App depends on Rendering never the reverse; FC-1 no embedded browser engine; content-negotiation Function `/api/negotiate/<slug>` (Accept → HTML | raw .md).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/EXPERIENCE.md] — Component Patterns "address-bar … accepts and loads `.md` URLs only; non-`.md` input is rejected or coerced … Shows lock + host/path + `.md only` tag"; State Patterns (Loading; Broken/missing `.md` never a crash; Non-`.md` address declines + offers system browser); Interaction Primitives (external `http(s)` → system browser); Voice and Tone (tag `.md only`); Accessibility Floor.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/DESIGN.md] — address-bar visual (lock + host/path + `.md only` tag) within `client-toolbar`.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/mockups/native-client.png] — toolbar layout: nav buttons · address bar (lock · host/path · `.md only`) · personality chip (Epic 4).
- [Source: clients/windows/App/MainWindow.xaml, App/ShellViewModel.cs, App.Tests/*] — the Story 3.1 shell + tests this story builds on (reserved column 1; `ShellTestHelpers`; inherited boundary/no-Chromium guards).
- [Source: api/tests/negotiate.e2e.test.mjs] — the live endpoint contract: `GET /api/negotiate/<slug>` `Accept: text/markdown` → `200 text/markdown; charset=utf-8`, `Vary: Accept`; missing slug → `404`.
- [Source: .github/workflows/build-windows.yml] — windows-latest CI: restore → build -c Release → test -c Release on `TheMarkdownWeb.sln` (the sole verification surface).

## Dev Agent Record

### Agent Model Used

Opus 4.8 (1M context) — claude-opus-4-8[1m]

### Debug Log References

_(to be filled by the dev agent — WPF builds/runs Windows-only; verification is exclusively via `build-windows.yml` on `windows-latest`.)_

### Completion Notes List

_(to be filled by the dev agent)_

### File List

_(to be filled by the dev agent)_
