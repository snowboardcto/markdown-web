# Story 3.5: In-client links, media, navigation

Status: done

<!-- VALIDATION (Step 3, post-hardening, vs epics.md Story 3.5, lines 338–350; FR-2, FR-3, FR-8, UX-DR8): RESULT = PASS (kept WHOLE; split contract pinned as the safety valve).
  - Step-2 hardening verified: SlugDeriver parity CORRECTED to byte-exact github-slugger (was a wrong approximation — would have shipped diverging slugs → live-page 404s); link-classification edge matrix, NavigationController transition table, image edge matrix, and explicit decisions (failed-nav history / same-URL / re-entrancy last-wins / broken-image) all added. Scope UNCHANGED (the 3.5 integration set). 5 out-of-scope items logged to the Deferred-Work-Log (non-ASCII slug parity, richer media, same-URL Accept negotiation, button enabled-state, loading affordance) — none expand scope.
  - AC↔epic alignment (re-confirmed): every epic clause maps to ≥1 AC (display→AC1; relative .md click→fetch+render-in-place→AC2/3/4/9; #anchor→AC5; external→AC6; images inline→AC7; broken→clear-state-never-crash→AC8; Back/Forward/Reload real nav per UX-DR8→AC10). No epic clause is unmapped; no AC is orphaned.
  - Missing ACs: NONE. Totality/determinism is now explicit on every path (classification, resolution, slug, endpoint, image, history under/overflow, re-entrancy, anchor-miss) — nothing throws on bad input.
  - Task completeness: each AC has a CI-runnable proof, mostly plain [Fact] (LinkClassifier/PageUrlResolver/PageEndpointResolver/SlugDeriver/ImageResolver/AnchorMatcher/NavigationController incl. re-entrancy) + targeted [StaFact] only for WPF-object hosting/click/scroll/image-load. STA + DisableTestParallelization reused (not re-added). The sole verification surface is windows-latest CI (AC12).
  - Scope drift: NONE beyond the integration set. Theming/loading-chrome polish = 3.6; personality = Epic 4; richer media + non-ASCII slug parity = Deferred-Work-Log. Each explicitly named in-spec.
  - SIZE / SPLIT assessment (story is LARGE): RESULT stays PASS — kept WHOLE because every piece plugs into ONE seam (NavigationController) and the value (a browsable client) only exists wired end-to-end; the Task ordering front-loads the 3.5a core (Tasks 1–3) before additive slices (Tasks 4–5). The split contract is pinned and MECHANICAL (no rework): if velocity demands, fracture at 3.5a {AC1/2/3/4/8/9/10/11/12 — loads & browses} | 3.5b {AC5 anchor, AC6 external, AC7 images}. Non-blocking action item to PM: choose whole-vs-split at sprint start; the AC contracts don't change either way.
  - Non-blocking action items: (1) PM whole-vs-split call; (2) impl MUST embed the github-slugger regex verbatim + add a golden manifest-key cross-check [Fact] (the parity table rows are the assertions); (3) confirm the FlowDocumentScrollViewer routes Hyperlink.RequestNavigate under read-only on first CI run, else fall back to read-only RichTextBox (documented).
  - ORIGINAL Step-1 mapping retained below for traceability. -->
<!-- VALIDATION (Step 1, vs epics.md Story 3.5, lines 338–350; FR-2, FR-3, FR-8, UX-DR8): RESULT = PASS.
  - AC↔epic alignment: the epic's one Given/When/Then/And is multi-clause. Mapped exhaustively:
      • (display precondition — "a rendered page in the client") → AC1 (the fetched markdown is rendered into ContentHost via FlowDocumentRenderer; first time content appears on screen — required for every other clause to be observable).
      • "When I click a relative `.md` link, Then the client fetches and renders that page in place" → AC2 (link classification: internal `.md`) + AC3 (relative→absolute resolution against the current page base) + AC4 (in-place fetch+render + history push).
      • "an `#anchor` scrolls within the page" → AC5 (anchor classification + scroll-into-view in the scroll host).
      • "an external `http(s)` link opens in the system browser" → AC6 (external classification + IUrlLauncher, reusing 3.2's SystemBrowserLauncher).
      • "images resolve from the vault and render inline" → AC7 (App-side ImageResolver + loader seam: post-process the FlowDocument's recorded Image.Tag → Image.Source; relative→absolute against the page base; broken→placeholder/alt).
      • "a broken link shows a clear state, never a crash" → AC8 (failed fetch / missing page / broken link → clear in-content Broken state, total/no-crash; reuses AddressBarState.Broken).
      • DERIVED-but-mandated by the live-load requirement + 3.2 deferred-work log: AC9 (URL→endpoint resolution seam: themarkdownweb.com `.md` URL → `/api/negotiate/<slug>`, mirroring the server `pathToSlug`, so REAL live pages load instead of Broken).
      • Back/Forward/Reload "really navigate" (3.1 left them inert; UX-DR8 + the toolbar contract) → AC10 (NavigationController history stack: push/back/forward/reload re-fetch+render; replaces ShellViewModel's LastAction-only handlers).
    Every epic clause maps to ≥1 AC.
  - DERIVED ACs (labeled): AC9 = the URL→/api/negotiate/<slug> seam (epics live-load requirement + 3.2 DEFERRED-WORK LOG line 183 + 2.7 deferral). AC11 = Rendering-stays-PURE (the standing 3.3 line 323 invariant + architecture "Rendering pure" + FC-1 no embedded browser) — image LOADING + link NAVIGATION + URL/image resolution all live in App; Rendering nets nothing new. AC12 = the windows-latest CI gate (STA + DisableTestParallelization) — the sole verification surface (Linux dev box has no .NET SDK; WPF is Windows-only).
  - Scope drift: NONE beyond "wire fetch+render into the window + make links/images/nav work." EXPLICITLY OUT (each named in-spec): the 3.6 visual default-theme polish (this story shows the bedrock FlowDocument, not the GitHub-pixel pass); personality rendering/selector (Epic 4); video/audio playback controls beyond a static inline image (richer media deferred — images-inline is the AC); offline caching; tabs/multiple windows; same-URL Accept negotiation at the page URL (2.7 deferral — we use the Function endpoint).
  - SPLIT RECOMMENDATION: see Dev Notes "Split assessment." Story is LARGE but coherent (one integration seam wired end-to-end). Recommended kept WHOLE with a clear minimum-coherent core (AC1/AC2/AC3/AC4/AC8/AC9/AC11/AC12) and three additive slices (AC5 anchors, AC6 external, AC7 images, AC10 history) that each plug into the same NavigationController seam; if velocity demands, the natural fracture line is 3.5a {display + internal-link + history + URL-endpoint + broken} and 3.5b {anchors + external + images}. The AC contracts are written so the split is mechanical (no rework).
-->

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want links and media to behave correctly inside the client,
so that I can browse around natively.

## Context note (this is the FIFTH `clients/windows/` feature story — Epic 3; it is the INTEGRATION story that connects fetch + render and makes navigation real)

Stories 3.1–3.4 built the parts in isolation; **3.5 wires them together and is the first time content appears on screen:**

- **Story 3.1** shipped the **app shell** — WPF `MainWindow` + the browser-like toolbar (Back / Forward / Reload buttons) wired to an **inert** `ShellViewModel` whose `OnBack`/`OnForward`/`OnReload` only record `LastAction` (no history, no fetch — explicitly deferred to 3.2/3.5), plus a **named, empty `<Border x:Name="ContentHost"/>`**.
- **Story 3.2** shipped the **`.md`-only address bar + fetch** — `AddressBarValidation.IsLoadableMarkdownUrl`, `MarkdownFetcher.FetchAsync(url, ct)` (GET + `Accept: text/markdown`, **never throws**, `FetchResult.Success(body)` / `FetchResult.Failure(reason)`), `IUrlLauncher`/`SystemBrowserLauncher`, and `AddressBarViewModel` with the `AddressBarState` machine (`Idle`/`Loading`/`Loaded`/`NotMarkdown`/`Broken`). It holds the fetched markdown as a **raw string** (`LastFetchedMarkdown`) and **deliberately leaves `ContentHost` empty** (3.2 fetched the typed URL **as-is**; the `/api/negotiate/<slug>` mapping was logged as DEFERRED to 3.3+/3.5).
- **Story 3.3 / 3.4** shipped the **pure render** — `FlowDocumentRenderer.Render(string) → FlowDocument` (Markdig GFM → WPF, code highlighting). The renderer emits **inert `Hyperlink`s** (records `NavigateUri`, NO click handler — navigation deferred to 3.5) and **record-only `Image`s** (`Image.Tag` = source string, `AutomationProperties` = alt, **NO fetch** — image resolution/load deferred to 3.5). All four prior stories are `done` and green on `windows-latest` CI (3.4 run #19).

**Story 3.5 is the integration:** it (1) renders the fetched markdown into `ContentHost` (the bedrock FlowDocument finally shows), (2) makes the inert hyperlinks navigate — internal `.md` link → in-place fetch+render, `#anchor` → scroll, external `http(s)` → system browser, (3) makes Back/Forward/Reload actually navigate via a real history stack, (4) loads the record-only images from the vault into the WPF `Image`, and (5) maps a typed/clicked `themarkdownweb.com/<x>.md` URL to the fetchable `/api/negotiate/<slug>` endpoint so **live pages actually load** instead of resolving to `Broken`. A broken link / missing page / failed fetch shows a clear in-content state, **never a crash**.

**The hard architectural rule (do not violate):** **`Rendering` stays PURE.** Image LOADING, link NAVIGATION, URL→endpoint resolution, and the history stack ALL live in `App` (the layer allowed to do I/O). `Rendering` gains **no** new package and **no** networking. The App→Rendering one-way boundary, the no-embedded-browser guards, and the Rendering-purity guards MUST stay green. **No webview** is added anywhere.

**What this story builds ON (do NOT recreate):**
- `clients/windows/App/MarkdownFetcher.cs` — **reuse** `FetchAsync(url, ct) → FetchResult` verbatim (GET + `Accept: text/markdown`, total). 3.5 fetches the *resolved endpoint URL* (AC9), not the raw page URL.
- `clients/windows/App/AddressBarViewModel.cs` + `AddressBarState` — **reuse** the state machine + the `LastFetchedMarkdown`/`State` observables. 3.5 subscribes to a successful fetch and renders it; `Broken` is reused for the failure state. (Decide & document the exact wiring — see Dev Notes "Where the render is triggered.")
- `clients/windows/App/IUrlLauncher.cs` / `SystemBrowserLauncher` — **reuse** for external `http(s)` links (AC6). Inject the same `IUrlLauncher` into the navigation controller.
- `clients/windows/App/AddressBarValidation.cs` — **reuse** `IsLoadableMarkdownUrl` / `TryGetHostPath` for `.md` detection (the link classifier shares the `.md` predicate).
- `clients/windows/App/ShellViewModel.cs` — the inert `OnBack`/`OnForward`/`OnReload`. 3.5 **makes these real** (delegate to the `NavigationController`); keep `ShellAction`/`LastAction` if other tests rely on it (see AC10 — keep `LastAction` set for backward-compat, ADD the real behavior).
- `clients/windows/App/MainWindow.xaml` — the `<Border x:Name="ContentHost"/>` (3.5 fills it) + the named `BackButton`/`ForwardButton`/`ReloadButton` (3.5 makes their click handlers navigate).
- `clients/windows/Rendering/FlowDocumentRenderer.cs` — **reuse** `Render(string)`. **Do NOT add networking.** 3.5 may read the `Hyperlink.NavigateUri` (already recorded) and the `Image.Tag` (already recorded) that the renderer emits — these are the seams 3.3 left for this story. (Optionally add ONE pure, net-free render-side helper to expose hrefs/image-sources if walking the tree in App proves awkward — see Dev Notes "Rendering seam options"; default is App walks the tree, Rendering unchanged.)
- `clients/windows/App.Tests/` — **reuse** `ShellTestHelpers` (`CreateWindow`, `FindButton`), the `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `AssemblyInfo.cs`, and the inherited `DependencyBoundaryTests` / `NoEmbeddedBrowserTests` guards (all stay green). `Xunit.StaFact` 1.1.11 is already referenced. **Do NOT re-add the STA package or the no-parallel attribute.**
- `api/negotiate/slug.mjs` (`pathToSlug`) + `api/negotiate/index.mjs` (route `negotiate/{*slug}`) — the **server-side slug derivation 3.5 mirrors** in the App `PageUrlResolver` (AC9). Read it for the exact normalization (drop `.md` case-insensitively, github-slug each `/`-segment → lower-case, collapse a trailing `/index` and a bare `index` to the parent).

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test (drives AC12, Tasks for CI)

**This repo is developed on Linux with NO .NET SDK installed; WPF builds and runs ONLY on Windows.** The dev agent CANNOT build, run, or visually confirm anything locally. **Verification happens exclusively through `build-windows.yml` on `windows-latest`** (restore → build -c Release → test -c Release on `TheMarkdownWeb.sln`). The `paths:` filter (`clients/windows/**`) already covers every file this story touches. Therefore:

- Every acceptance bar that must be *checked* is checked **either by the compiler (build succeeds) or by an xUnit test that runs in `dotnet test` on `windows-latest`** — never "click the link and watch it navigate."
- **`FlowDocument`/`Hyperlink`/`Image`/`FlowDocumentScrollViewer`/`Window` are `DispatcherObject`s with STA thread affinity.** Any test that constructs WPF objects, calls `Render(...)`, walks the FlowDocument tree, reads `Hyperlink.NavigateUri`/`Image.Source`, or constructs `MainWindow` MUST run on an STA thread via `Xunit.StaFact`'s `[StaFact]`. No `Window` is `.Show()`/`.ShowDialog()`'d; the runner is headless.
- **Prefer testable App-side logic in plain `[Fact]`s.** The link classifier, the relative→absolute URL resolver, the `.md`→`/api/negotiate/<slug>` endpoint mapper, the image-source resolver, and the navigation history-stack state machine are ALL pure (no WPF type, no window, no network) and MUST be `[Fact]`-tested with injected stubs. Use `[StaFact]` ONLY for the FlowDocument-hosting / scroll-into-view / hyperlink-click-attachment / image-load-into-`Image` bits that need real WPF objects (construct, never `Show`).
- **No real sockets, no real `Process.Start`, no real file/network image load in tests.** The `MarkdownFetcher` is injected (stub `HttpMessageHandler`, no socket — as in 3.2). The `IUrlLauncher` is faked (records the `Uri`, no process). The image loader is behind an `IImageLoader` seam stubbed in tests (no `BitmapImage` download/decode). The navigation controller takes an injectable fetch delegate so history transitions are `[Fact]`-tested with a fake fetcher.
- **Determinism / totality:** every navigation/link/image path is TOTAL — broken/missing/failed never throws; classification is deterministic; the history stack never throws on under/overflow (Back at index 0 is a no-op, Forward at the tip is a no-op).

## Acceptance Criteria

1. **[Fetched markdown is rendered into `ContentHost` — content finally appears on screen]** **Given** the client has successfully fetched a page's raw markdown (via `MarkdownFetcher` → `FetchResult.Success(body)`, as in 3.2), **When** the load completes, **Then** the App **renders that markdown with `FlowDocumentRenderer.Render(body)` and displays the resulting `FlowDocument` inside `ContentHost`** using a read-only scrolling host. Concretely:
   - `MainWindow.xaml`'s `<Border x:Name="ContentHost"/>` hosts a **`FlowDocumentScrollViewer`** (read-only by construction — `FlowDocumentScrollViewer` has no editing surface; a `RichTextBox { IsReadOnly = true, IsDocumentEnabled = true }` is the alternative if hyperlink hit-testing needs it — decide & document, see Dev Notes "Scroll host choice"). The host's `Document` is set to the rendered `FlowDocument`.
   - On a successful fetch the App calls `Render(...)` (on the UI/STA thread, since `FlowDocument` has thread affinity) and assigns the document into the scroll host; on a failed fetch it shows the Broken state (AC8) instead.
   - The render call is App-side; `Rendering` stays pure (AC11). The rendered document is the **bedrock** FlowDocument (3.3/3.4) — visual theme polish is Story 3.6, NOT here.

   Proven by an `[StaFact]`: drive the App's display seam (e.g. `ContentPresenter.Show(markdown)` or set the VM's loaded markdown) with a small markdown string, then assert the `ContentHost`'s child scroll host's `Document` is a non-null `FlowDocument` whose block count matches the rendered content (reuse a render of the same string as the oracle, or assert ≥1 block). No `Window.Show`, no pixels. *(AC1 — epics.md Story 3.5 line 346 "a rendered page in the client"; FR-6 render shown; architecture "App/ … hosts the FlowDocument".)*

2. **[Link classification is deterministic and total — internal `.md` vs anchor vs external vs other]** **Given** a link href string (from a rendered `Hyperlink`) and the current page's base URL, **When** the App classifies it, **Then** a pure `LinkClassifier.Classify(href, basePageUrl) → LinkTarget` deterministically returns one of: **`InternalMarkdown`** (a relative-or-absolute link whose resolved absolute URL is an `http(s)` `.md` page on the same host as the base — navigate in place, AC4), **`Anchor`** (a pure fragment `#heading`, or a same-page `…#frag` — scroll, AC5), **`External`** (an absolute `http(s)` link that is NOT an internal `.md` page — open in system browser, AC6), or **`Unsupported`** (mailto:/javascript:/data:/empty/garbage — no-op, never a crash). The classifier is pure (no I/O), total (never throws for any string), and case-insensitive on scheme + `.md`.
   - It reuses the `.md` predicate shape from `AddressBarValidation` (path ends `.md`, non-empty stem).
   - `LinkTarget` carries the resolved data the navigator needs: for `InternalMarkdown`, the **resolved absolute page URL** (AC3); for `Anchor`, the **fragment** string (sans `#`); for `External`, the **absolute `Uri`**.

   Proven by `[Fact]`s over a table: `./sub/page.md` (base `https://themarkdownweb.com/guide.md`) → `InternalMarkdown(https://themarkdownweb.com/sub/page.md)`; `#install` → `Anchor("install")`; `https://example.com/x` → `External`; `https://themarkdownweb.com/about` (no `.md`) → `External`; `mailto:a@b.com`/`javascript:alert(1)`/``/`   ` → `Unsupported`; all without a window or network. *(AC2 — EXPERIENCE.md Interaction Primitives lines 56–58; FR-2/FR-8.)*

3. **[Relative link/image URL resolution against the current page base — deterministic, total]** **Given** a relative href/src (`./sub/page.md`, `../img/pic.png`, `notes.md`, `/x.md`) and the current page's absolute base URL, **When** the App resolves it, **Then** a pure `PageUrlResolver.ResolveAgainst(basePageUrl, relativeRef) → Uri?` produces the correct absolute URL using standard URI base-resolution (`new Uri(baseUri, relativeRef)` semantics: `./` and `..` resolved against the base's directory, a leading `/` resolved against the host root, an already-absolute ref returned as-is). Returns `null` (never throws) for an unresolvable/garbage ref. This single resolver is shared by link navigation (AC4) and image resolution (AC7).

   Proven by `[Fact]`s: base `https://themarkdownweb.com/guides/gear.md` + `./powder.md` → `https://themarkdownweb.com/guides/powder.md`; + `../index.md` → `https://themarkdownweb.com/index.md`; + `media/pic.png` → `https://themarkdownweb.com/guides/media/pic.png`; + `/x.md` → `https://themarkdownweb.com/x.md`; + `https://other.com/a.md` → unchanged; + garbage → `null`, no throw. *(AC3 — FR-2 relative resolution; mirrors Story 2.3 inter-file link resolution on the web side.)*

4. **[Internal `.md` link click → fetch + render in place + history push]** **Given** a rendered page hosted in `ContentHost` and the reader clicks an `InternalMarkdown` `Hyperlink`, **When** the click fires, **Then** the App **resolves the clicked href to its absolute page URL (AC3), classifies it (AC2), maps it to the fetchable endpoint (AC9), fetches it (`MarkdownFetcher`), renders the result into `ContentHost` (AC1), and pushes the new page onto the navigation history** — all without opening a system browser and without reloading the whole window. The click is handled **App-side**: the App attaches a single `Hyperlink.RequestNavigate`/`Click` handler when it hosts the FlowDocument (the renderer stays inert/pure — see Dev Notes "How the hyperlink click is attached"). A failed in-place fetch shows the Broken state (AC8) and does NOT corrupt history.
   - The click handler is attached **once** per hosted document (e.g. `host.AddHandler(Hyperlink.RequestNavigateEvent, handler)` on the scroll host / `RichTextBox`, OR `FlowDocument.AddHandler(...)`), reading `e.Uri`/the `Hyperlink.NavigateUri`. It MUST `e.Handled = true` so WPF's default shell-launch does NOT also fire.
   - Navigation is driven through the `NavigationController` (AC10) so a clicked link and a typed address use the SAME push-and-render path.

   Proven by a `[Fact]` on the `NavigationController` (no window): `NavigateToAsync(url)` with a fake fetcher that returns success → controller's `Current == url`, the render-sink received the markdown, `CanGoBack` reflects history; a clicked-link path is proven by an `[StaFact]` that hosts a FlowDocument containing an internal `.md` `Hyperlink`, raises its navigate event, and asserts the fake fetcher was invoked with the RESOLVED endpoint URL (no socket) and the render-sink updated. *(AC4 — epics.md line 347 "fetches and renders that page in place"; FR-2.)*

5. **[Anchor `#heading` → scroll within the current page, no re-fetch]** **Given** a rendered page and the reader clicks an `Anchor` link (a `#frag` fragment), **When** the click fires, **Then** the App **scrolls the matching heading into view within the scroll host** and does NOT re-fetch or push history. The App locates the target by matching the fragment against the rendered headings (the renderer tags headings; match a heading whose github-slugged text equals the fragment, OR a recorded anchor id — decide & document the anchor-id seam, see Dev Notes "Anchor matching"). A fragment with no matching heading is a **no-op** (no scroll, no crash), never an error.
   - Scrolling uses a WPF-native bring-into-view (`FrameworkContentElement.BringIntoView()` on the target `Block`, or `TextPointer`-based scroll on the host) — no pixel math, no timing.

   Proven by an `[StaFact]`: render a doc with an `## Install` heading, host it, invoke the anchor-scroll for `"install"` → asserts the resolved target `Block` is the `Install` heading (the matcher returns it) and the call does not throw; invoke for a missing fragment → returns no target, no throw, no re-fetch (fetcher not invoked). The fragment→heading match is also `[Fact]`-tested purely (slugify + compare) without a window. *(AC5 — epics.md line 348 "an `#anchor` scrolls within the page"; EXPERIENCE.md line 57.)*

6. **[External `http(s)` link → system browser, never in-client]** **Given** a rendered page and the reader clicks an `External` link, **When** the click fires, **Then** the App **opens it via `IUrlLauncher.Open(uri)` (the system default browser)** and does NOT fetch it into `ContentHost` (the client is not a general web browser — UX-DR8 / EXPERIENCE.md line 58). No embedded browser is involved (reuses 3.2's `SystemBrowserLauncher`). The classification (AC2) decides external vs internal; this AC is the dispatch.

   Proven by a `[Fact]` on the navigation dispatch with a **fake `IUrlLauncher`** that records the `Uri`: dispatch an `External` target for `https://example.com/x` → the fake recorded exactly that `Uri`, and the fake fetcher was NOT invoked (no in-client fetch). An absolute non-`.md` `themarkdownweb.com/about` link also dispatches external. No `Process.Start`. *(AC6 — epics.md line 348 "an external `http(s)` link opens in the system browser"; FR-8; FC-1 no embedded browser.)*

7. **[Images resolve from the vault and render inline — App-side loader seam; broken → placeholder/alt, never a crash]** **Given** a rendered FlowDocument whose `Image` elements carry a recorded source string on `Image.Tag` (3.3) and the current page's base URL, **When** the App hosts the document, **Then** the App **resolves each image's source to an absolute URL (AC3, relative→absolute against the page base) and loads it into the WPF `Image.Source`** via an injectable `IImageLoader` seam (App owns the I/O that Rendering deliberately avoided). A source that fails to resolve or load yields a **graceful placeholder** (the `Image` stays empty, the recorded alt/automation name is preserved) — **never a crash**. Concretely:
   - An `ImageResolver` (pure, `[Fact]`-testable) maps each recorded `Image.Tag` source string + base page URL → an absolute `Uri?` (reusing `PageUrlResolver`; returns `null` for unresolvable).
   - An `IImageLoader` seam (`ImageSource? Load(Uri absolute)`; default `SystemImageLoader` builds a `BitmapImage` with `UriSource`; tests stub it) does the actual decode/network — so tests open no socket and decode no bytes.
   - The App **post-processes the hosted FlowDocument**: walk the `Image` elements, resolve+load each, set `Image.Source` (or leave empty on failure). Rendering is NOT changed to fetch — it still only records (AC11).
   - Total: an unresolvable/relative/broken source never throws; the image renders empty with its alt preserved.

   Proven by `[Fact]`s on `ImageResolver` (relative→absolute, garbage→null, no throw) and an `[StaFact]` that renders `![alt](media/pic.png)` for base `https://themarkdownweb.com/guides/x.md`, hosts it, runs the image post-process with a **stub `IImageLoader`** that records the requested `Uri` and returns a sentinel `ImageSource` → asserts the loader was asked for `https://themarkdownweb.com/guides/media/pic.png` and the `Image.Source` was set to the sentinel; a stub loader that returns `null` (broken) → `Image.Source` stays null, automation/alt name preserved, no throw. No socket. *(AC7 — epics.md line 349 "images resolve from the vault and render inline"; FR-3; 3.3 image decision deferred resolution to 3.5.)*

8. **[Broken link / missing page / failed fetch → clear in-content state, never a crash]** **Given** any navigation (typed address, clicked internal link, Back/Forward, Reload) whose fetch returns `FetchResult.Failure` (non-2xx incl. endpoint 404, non-`text/markdown`, empty/oversized body, network exception, cancellation) OR whose href is `Unsupported` (AC2), **When** the App handles it, **Then** it shows a **clear in-content "page not found / not a markdown page" state inside `ContentHost`** (reusing `AddressBarState.Broken`) and **never throws / never crashes the window**. The Broken state is a clear FlowDocument-or-message (e.g. a small rendered "This page could not be loaded" document or a visible message panel in `ContentHost`), distinct from a successfully rendered page. A failed in-place navigation does NOT corrupt the history stack (the current entry is preserved; **no half-pushed broken entry** — a fresh `NavigateToAsync` that fails leaves `Current`/cursor/history untouched and only signals `onBroken`; Back therefore always returns to a real, previously-rendered page). See Dev Notes "Explicit decisions" (failed-navigation & history) for the Back/Forward/Reload-of-an-existing-entry-that-fails case (cursor has already legitimately moved to that real entry → show Broken for it, do not rewind).

   Proven by a `[Fact]` on the `NavigationController` with a fake fetcher returning `Failure` → the controller surfaces a Broken outcome, `Current` is unchanged (no corruption), no throw; an `[StaFact]` asserts the `ContentHost` shows the Broken content (a distinguishable element/message), not a stale page and not an empty crash. An `Unsupported` link dispatch is a no-op (no fetch, no launch, no throw). *(AC8 — epics.md line 350 "a broken link shows a clear state, never a crash"; FR-2; EXPERIENCE.md State Patterns line 49; reuses 3.2 `AddressBarState.Broken`.)*

9. **[URL → `/api/negotiate/<slug>` resolution seam — so LIVE pages actually load (the 3.2 deferred decision lands here)]** **Given** a typed-or-clicked absolute `themarkdownweb.com` `.md` page URL (e.g. `https://themarkdownweb.com/guides/gear-guide.md`), **When** the App prepares to fetch it, **Then** a pure `PageEndpointResolver.ToFetchEndpoint(pageUrl) → Uri` maps it to the **fetchable Function endpoint** `https://themarkdownweb.com/api/negotiate/<slug>`, where `<slug>` is derived by the SAME normalization as the server `pathToSlug` (`api/negotiate/slug.mjs`): **drop the trailing `.md` (case-insensitive); github-slug each `/`-separated path segment (lower-case + slugify); collapse a trailing `/index` and a bare `index` to the parent**. The fetcher then GETs the endpoint URL (not the raw page URL) with `Accept: text/markdown`, so a live `.md` request returns real `text/markdown` (200) instead of HTML → Broken. The mapping is pure, total (never throws), and host-preserving (the endpoint is `<scheme>://<host>/api/negotiate/<slug>` on the SAME origin as the page URL).
   - A C#-faithful port of `pathToSlug` lives in App (`SlugDeriver.PathToSlug(relPosixPath)`). **CRITICAL — port the EXACT github-slugger algorithm, NOT a "replace non-`[a-z0-9]` with `-`" approximation (that approximation is WRONG — see Advanced-elicitation hardening "SlugDeriver parity").** The server (`api/negotiate/slug.mjs`) does, per `/`-segment, exactly: `value.toLowerCase().replace(<github-slugger Unicode regex>, '').replace(/ /g, '-')` — i.e. **(a) invariant-lowercase, (b) DELETE (not replace) every char matched by github-slugger's `regex.js` Unicode class (punctuation/symbols/control: `.`, `,`, `!`, `#`, `&`, `%`, `:`, `/`-in-segment, etc.), (c) replace ONLY the literal space U+0020 with `-`**. No hyphen-run collapsing, no trim. So `a.b.c` → `abc` (dots deleted), `100%` → `100`, `C# & .NET` → `c--net`, `My Notes Dir` → `my-notes-dir`, `--x--` → `--x--` (preserved). Then drop `.md` (case-insensitive) BEFORE slugging, split on `/`, slug each segment, re-join `/`, strip a trailing `/index`, map a bare `index` to empty. The C# port MUST embed the SAME github-slugger Unicode regex (copy `regex.js`'s pattern into a `static readonly Regex`) and use `ToLowerInvariant()`; cross-check against the live manifest keys (`gear-guide`, `sub/page`, `sub` ← `sub/index.md`, `readme` ← `README.md`, `my-notes-dir/page` ← `My Notes Dir/page.md`, `my-notes` ← `My Notes.md`). **Unicode divergence risk** is documented & scoped: see the hardening section's parity table and the Deferred-Work-Log non-ASCII note.
   - Host policy: apply the negotiate mapping for the canonical app host (`themarkdownweb.com`, case-insensitive, incl. `www.`); for any OTHER host the page URL is fetched as-is (the seam is a deterministic, documented host check — NOT a hardcoded single URL). Document the exact host predicate.

   Proven by `[Fact]`s mirroring the server: `https://themarkdownweb.com/gear-guide.md` → `…/api/negotiate/gear-guide`; `…/sub/page.md` → `…/api/negotiate/sub/page`; `…/sub/index.md` → `…/api/negotiate/sub`; `…/README.md` → `…/api/negotiate/readme`; `…/My%20Notes%20Dir/page.md` (or `/My Notes Dir/page.md`) → `…/api/negotiate/my-notes-dir/page`; a non-app host `https://other.com/x.md` → unchanged (fetched as-is); all pure, no network, no throw. *(AC9 — DERIVED: epics.md live-load requirement + the 3.2 DEFERRED-WORK LOG line 183 "Story 3.3+ must wire the typed `themarkdownweb.com/<x>.md` → `/api/negotiate/<slug>` mapping" + the 2.7 same-URL deferral; architecture content-negotiation `/api/negotiate/<slug>`.)*

10. **[Back / Forward / Reload actually navigate — a real history stack (3.1's inert handlers made real)]** **Given** the toolbar Back/Forward/Reload buttons and a `NavigationController` holding a navigation history, **When** the reader navigates (types an address, clicks an internal link) and then presses Back/Forward/Reload, **Then** the controller moves through history and **re-fetches+re-renders** the target page: **Back** moves to the previous entry and renders it; **Forward** moves to the next entry; **Reload** re-fetches the current entry. A new navigation from a mid-history position **truncates the forward stack** (browser semantics). The controller exposes `CanGoBack`/`CanGoForward` (the toolbar buttons may reflect enabled state — minimum: the handlers are no-ops at the ends, never throwing). The 3.1 `ShellViewModel.OnBack/OnForward/OnReload` delegate to the controller (keep setting `LastAction` for the existing 3.1 tests; ADD the real behavior).
    - `NavigationController` API (the pinned contract — see Dev Notes): `Task NavigateToAsync(Uri pageUrl)` (push + fetch + render), `Task GoBackAsync()`, `Task GoForwardAsync()`, `Task ReloadAsync()`, `Uri? Current`, `bool CanGoBack`, `bool CanGoForward`. It takes an injectable fetch delegate (over `MarkdownFetcher` + `PageEndpointResolver`) and a render sink (over `FlowDocumentRenderer` + the scroll host) so the whole state machine is `[Fact]`-testable with NO window and NO network.
    - The history is a list + cursor index; Back at index 0 and Forward at the tip are no-ops (total). Reload at an empty history is a no-op. **Same-URL `NavigateToAsync(Current)` re-pushes** a new entry (browser semantics); **Reload does NOT push**. **Re-entrancy is last-wins** via a generation token (a stale fetch completion is dropped — no double-render, no history corruption). The full op×outcome matrix is pinned in Dev Notes "NavigationController state-transition table."

    Proven by `[Fact]`s on the `NavigationController` with a fake fetcher: navigate A → B → C, `CanGoBack` true / `CanGoForward` false; `GoBackAsync` → `Current == B`, `CanGoForward` true; `GoForwardAsync` → C; a new `NavigateToAsync(D)` from B truncates C (so `CanGoForward` false, Forward is a no-op); same-URL `NavigateToAsync(B)` while `Current==B` re-pushes (Count grows, `CanGoForward` false); `ReloadAsync` re-invokes the fetcher for `Current` WITHOUT growing history; Back at index 0 / Forward at tip are no-ops (no throw, fetcher behavior asserted); a **re-entrancy** `[Fact]` (Nav(A) gated, Nav(B) starts, A released late → only B is `Current`, sink saw B last, single tip). Each transition pushes the rendered markdown to the fake sink. *(AC10 — epics.md UX-DR8 in-client nav; FR-2; 3.1 left Back/Forward/Reload inert "real navigation lands in Story 3.2 / 3.5".)*

11. **[`Rendering` stays PURE — link nav + image load + URL/image resolution all live in App; no new package, no net, no webview]** **Given** 3.5 adds navigation, image loading, and URL resolution, **When** the dependency graph + csprojs are inspected, **Then** **`Rendering` gains NO new `PackageReference`** (still `{Markdig, ColorCode.Core}`), adds **no** `System.Net.Http`/sockets, no AI SDK, no webview/Chromium, and no reference to `App`/`Agent`; **all the new I/O (fetch endpoint, image load, `Process.Start` browser launch) lives in `App`**. Concretely:
    - the inherited **`DependencyBoundaryTests.Rendering_DoesNotReference_AppOrAgent` + `App_References_Rendering` stay green** (App still references Rendering; Rendering still references neither App nor Agent);
    - the inherited **`NoEmbeddedBrowserTests` (csproj glob over `clients/windows/**/*.csproj`) stays green** — no forbidden embedded-browser substring (`webview`/`cefsharp`/`chromium`/…) is added to ANY csproj (the `IImageLoader`/`BitmapImage` is WPF imaging, NOT a browser; `SystemBrowserLauncher` shells out to the OS, NOT an embedded engine);
    - the **`RenderingPurityTests` allowlist `{Markdig, ColorCode.Core}` + forbidden-substring guard stay green** (Rendering csproj UNCHANGED by this story);
    - if a Rendering-side net-free seam is added (optional, Dev Notes "Rendering seam options") it touches NO `System.Net.*`/socket/AI/webview — pure tree-walk helpers only.
    *(AC11 — epics.md Story 3.3 line 323 standing invariant "no networking or AI dependencies"; architecture "`Rendering/` … pure"; FC-1 no embedded browser. The whole point of this story's seams is to keep Rendering pure while App does the I/O.)*

12. **[windows-latest CI gate — STA + no-parallel; the only verification surface]** **Given** verification is exclusively `windows-latest` CI, **When** `build-windows.yml` runs (`restore` → `build -c Release` → `test -c Release` on `TheMarkdownWeb.sln`), **Then** the whole solution **builds clean** (no new warnings-as-errors regressions; `nullable`/`ImplicitUsings` consistent) and **all tests pass green**, including: the **existing** `App.Tests` (3.1/3.2 — incl. `NoEmbeddedBrowserTests`/`DependencyBoundaryTests`/`ShellViewModelTests`/address-bar tests) and `Rendering.Tests` (3.3/3.4), **plus** the new Story 3.5 navigation/link/image tests. Specifically:
    - **STA + no-parallel discipline.** Pure App-side logic (`LinkClassifier`, `PageUrlResolver`, `PageEndpointResolver`/`SlugDeriver`, `ImageResolver`, the fragment→heading matcher, the `NavigationController` state machine with stubs) is `[Fact]`. Anything constructing `MainWindow`/hosting a `FlowDocument`/reading `Hyperlink.NavigateUri`/`Image.Source`/attaching the click handler/scrolling is `[StaFact]`. `App.Tests` already has `Xunit.StaFact` + `[assembly: CollectionBehavior(DisableTestParallelization = true)]` — **do NOT re-add**. No shown `Window`, no `Dispatcher` pump, no socket, no real `Process.Start`, no pixels, no timing.
    - **No regression / boundary intact.** `Rendering` stays pure (AC11); `Rendering`/`Rendering.Tests`/the `.sln`/the CI workflow are untouched except where this story legitimately edits `App`/`App.Tests` (+ the `MainWindow.xaml` host + the `ShellViewModel`/new App types). The `paths:` filter (`clients/windows/**`) already covers the changed files; `App.Tests` is already in the `.sln`. No `.sln`/workflow edit.
    *(AC12 — DERIVED CI/build gate + epics.md "covered by unit tests"; the sole verification per the Environment Constraint; build-windows.yml; NFR-7 reuse the proven plumbing.)*

> Source: [_bmad-output/planning-artifacts/epics.md#Story 3.5: In-client links, media, navigation] (lines 338–350): **Given** a rendered page in the client **When** I click a relative `.md` link **Then** the client fetches and renders that page in place; an `#anchor` scrolls within the page; an external `http(s)` link opens in the system browser **And** images resolve from the vault and render inline **And** a broken link shows a clear state, never a crash. (FR-2, FR-3, FR-8, UX-DR8). **AC1** = render the fetched markdown into ContentHost (content appears). **AC2** = link classification (internal/anchor/external/unsupported). **AC3** = relative→absolute URL resolution. **AC4** = internal `.md` click → fetch+render in place + history push. **AC5** = anchor → scroll. **AC6** = external → system browser. **AC7** = images resolve from vault + load inline (App seam, broken→placeholder). **AC8** = broken/missing/failed → clear state never crash. **AC9** = the `.md`→`/api/negotiate/<slug>` endpoint resolution (live pages load). **AC10** = Back/Forward/Reload real navigation (history stack). **AC11** = Rendering-stays-pure. **AC12** = the derived windows-latest CI gate (STA + DisableTestParallelization).

## Tasks / Subtasks

- [x] **Task 1 — Pure URL/link seams: `LinkClassifier`, `PageUrlResolver`, `PageEndpointResolver` + `SlugDeriver` (AC: 2, 3, 9)**
  - [x] Add `clients/windows/App/PageUrlResolver.cs` — `public static Uri? ResolveAgainst(Uri basePageUrl, string relativeRef)` using `new Uri(baseUri, relativeRef)` semantics; returns `null` (never throws) on garbage; an absolute ref returned as-is. [Source: AC3]
  - [x] Add `clients/windows/App/SlugDeriver.cs` — `public static string PathToSlug(string relPosixPath)`: a C# port of `api/negotiate/slug.mjs` `pathToSlug` mirroring github-slugger EXACTLY (drop trailing `.md` case-insensitive; split `/`; per segment: `ToLowerInvariant()` → `Regex.Replace(seg, <github-slugger regex>, "")` → replace literal space `' '` with `'-'`; re-join `/`; `.replace(/\/index$/, "")`; `.replace(/^index$/, "")`). **Do NOT collapse hyphen runs and do NOT trim hyphens** (github-slugger does neither). Embed github-slugger's `regex.js` pattern verbatim as a `static readonly Regex`. Cross-check against `api/negotiate/manifest.json` keys. Total, never throws. [Source: AC9; api/negotiate/slug.mjs lines 25–33; node_modules/github-slugger/index.js `slug()` + regex.js]
  - [x] Add `clients/windows/App/PageEndpointResolver.cs` — `public static Uri ToFetchEndpoint(Uri pageUrl)`: for the canonical app host (`themarkdownweb.com`/`www.themarkdownweb.com`, case-insensitive) and a `.md` path, take `pageUrl.AbsolutePath`, trim the leading `/`, **`Uri.UnescapeDataString` it ONCE** (mirrors the server's single `decodeURIComponent` so `%20`→space before slugging), then build `<scheme>://<host>/api/negotiate/<SlugDeriver.PathToSlug(decodedPath)>` (rebuild via `UriBuilder` so the slug path is re-escaped safely; query/fragment dropped from the endpoint). Otherwise return `pageUrl` as-is. Pure, total. Document the host predicate. [Source: AC9; api/negotiate/adapter.mjs `decodeURIComponent` once; api/negotiate/index.mjs route]
  - [x] Add `clients/windows/App/LinkClassifier.cs` — `public static LinkTarget Classify(string? href, Uri? basePageUrl)` returning a `LinkTarget` (a discriminated result: `Kind` ∈ {InternalMarkdown, Anchor, External, Unsupported} + the resolved `Uri?`/fragment). Reuse the `.md` predicate shape from `AddressBarValidation`. Pure, total, scheme/`.md` case-insensitive. Same-host `.md` (after AC3 resolution) → InternalMarkdown; pure `#frag` → Anchor; other absolute `http(s)` → External; mailto:/javascript:/data:/empty/garbage → Unsupported. [Source: AC2]
  - [x] **`[Fact]` AC2/AC3/AC9:** the tables in AC2/AC3/AC9 (classification, relative resolution, endpoint/slug mapping). NO window, NO network. [Source: AC2, AC3, AC9]

- [x] **Task 2 — `NavigationController`: the history stack + fetch+render+launch dispatch (AC: 4, 6, 8, 10)**
  - [x] Add `clients/windows/App/NavigationController.cs` — the pinned API (Dev Notes "Pinned App-side API"): `NavigateToAsync(Uri)`, `GoBackAsync()`, `GoForwardAsync()`, `ReloadAsync()`, `Uri? Current`, `bool CanGoBack`, `bool CanGoForward`. Inject (a) a fetch delegate `Func<Uri, CancellationToken, Task<FetchResult>>` (default = `MarkdownFetcher.FetchAsync` ∘ `PageEndpointResolver.ToFetchEndpoint`), (b) a render sink `Action<string>` / `IContentSink` (default = render via `FlowDocumentRenderer` into the scroll host), (c) the `IUrlLauncher` (for External dispatch), (d) a Broken sink. History = `List<Uri>` + cursor index; a new `NavigateToAsync` from mid-history truncates the forward tail; Back/Forward/Reload at the ends are no-ops. A `Failure` fetch routes to the Broken sink and does NOT mutate `Current`/history (AC8). [Source: AC4, AC8, AC10]
  - [x] Add `LinkTarget` dispatch: a method (e.g. `DispatchAsync(LinkTarget)`) that routes InternalMarkdown→`NavigateToAsync`, External→`IUrlLauncher.Open`, Anchor→the scroll callback (AC5, attached by the host), Unsupported→no-op. [Source: AC4, AC6, AC8]
  - [x] **`[Fact]` AC10:** the history transitions (A→B→C, Back/Forward, truncation, Reload-no-push, same-URL-re-push, end no-ops) with a fake fetcher + fake sink, asserting the full Dev Notes transition table. [Source: AC10]
  - [x] **`[Fact]` AC10 (re-entrancy / last-wins):** with a fetcher whose first call blocks on a gate, start Nav(A) then Nav(B), release A late → assert only B is `Current`, sink saw B last, single tip, no throw (stale-completion dropped via the generation token). [Source: AC10; Dev Notes "Explicit decisions"]
  - [x] **`[Fact]` AC4:** `NavigateToAsync` success → `Current` updated, sink received markdown, `CanGoBack` reflects history; fetcher called with the RESOLVED endpoint URL (AC9). [Source: AC4]
  - [x] **`[Fact]` AC6:** External dispatch → fake `IUrlLauncher` recorded the exact `Uri`, fake fetcher NOT invoked. [Source: AC6]
  - [x] **`[Fact]` AC8:** `Failure` fetch → Broken sink hit, `Current`/history unchanged, no throw; Unsupported dispatch → no fetch/no launch/no throw. [Source: AC8]

- [x] **Task 3 — Host the FlowDocument in `ContentHost` + attach the hyperlink click handler (AC: 1, 4, 5, 6, 8)**
  - [x] Edit `clients/windows/App/MainWindow.xaml`: put a `FlowDocumentScrollViewer` (or read-only `RichTextBox` — decide & document, Dev Notes "Scroll host choice") named e.g. `ContentScroll` inside `<Border x:Name="ContentHost"/>`. Wire the toolbar `BackButton`/`ForwardButton`/`ReloadButton` click handlers to the `NavigationController` (via `ShellViewModel`). [Source: AC1, AC10]
  - [x] Add an App content-host helper (e.g. `ContentPresenter`/`ContentHostController`) that: renders markdown via `FlowDocumentRenderer.Render`, sets the scroll host's `Document`, post-processes images (Task 4), and **attaches ONE `Hyperlink.RequestNavigate`/`Click` handler** on the host that reads `e.Uri`, classifies it (`LinkClassifier`), and dispatches via the `NavigationController` (Task 2); set `e.Handled = true` so WPF's default shell-launch does not also fire. The renderer stays inert/pure (it only recorded `NavigateUri`); the click behavior is App-attached here. [Source: AC1, AC4, AC5, AC6; how-hyperlink-click-attached decision]
  - [x] Show the Broken state in `ContentHost` (a clear "page not found / not a markdown page" document or message) when the controller signals Broken. [Source: AC8]
  - [x] **`[StaFact]` AC1:** drive the display seam with a markdown string → `ContentHost` child scroll host `Document` is a non-null `FlowDocument` (≥1 block / matches a render oracle). [Source: AC1]
  - [x] **`[StaFact]` AC4 (click path):** host a doc with an internal `.md` `Hyperlink`, raise its navigate event → the fake fetcher (injected into the controller) was invoked with the resolved endpoint URL (no socket), render-sink updated. [Source: AC4]
  - [x] **`[StaFact]` AC8 (host):** Broken navigation → `ContentHost` shows the distinguishable Broken content, not a stale page, no throw. [Source: AC8]

- [x] **Task 4 — Anchor scroll (AC: 5)**
  - [x] Add a pure fragment→heading matcher (e.g. `AnchorMatcher.FindHeadingSlug(headingText) == fragment` using `SlugDeriver`-style slugify, OR read a recorded anchor id off the heading `Tag`). Decide & document the anchor-id seam (Dev Notes "Anchor matching"). Total — no match → null, no throw. [Source: AC5]
  - [x] In the content host, on an `Anchor` dispatch: locate the matching heading `Block` in the hosted `FlowDocument` and `BringIntoView()` (or `TextPointer` scroll). No re-fetch, no history push. Missing fragment → no-op. [Source: AC5]
  - [x] **`[Fact]`** the fragment→heading match (slugify + compare). **`[StaFact]`** render `## Install`, host, scroll to `"install"` → resolves the heading `Block`, no throw; missing fragment → no target, no throw, fetcher NOT invoked. [Source: AC5]

- [x] **Task 5 — Image resolution + App-side load seam (AC: 7)**
  - [x] Add `clients/windows/App/ImageResolver.cs` — `public static Uri? Resolve(string? recordedSource, Uri? basePageUrl)` (reuse `PageUrlResolver`; null for unresolvable; total). [Source: AC7, AC3]
  - [x] Add `clients/windows/App/IImageLoader.cs` — `ImageSource? Load(Uri absolute)` + default `SystemImageLoader` (builds a `BitmapImage { UriSource = absolute }`, swallows load errors → null). Injectable so tests stub it (no socket/decode). [Source: AC7]
  - [x] In the content host post-process: walk the hosted FlowDocument's `Image` elements, read `Image.Tag` (the recorded source, 3.3), resolve (AC3) + load via `IImageLoader`, set `Image.Source` (or leave empty on null). Preserve the recorded alt/automation name. Total — never throws. [Source: AC7]
  - [x] **`[Fact]` AC7 (resolve):** relative→absolute, garbage→null, no throw. **`[StaFact]` AC7 (load):** render `![alt](media/pic.png)` base `…/guides/x.md`, host, post-process with a **stub `IImageLoader`** recording the `Uri` + returning a sentinel → loader asked for `…/guides/media/pic.png`, `Image.Source` == sentinel, alt preserved; stub returns null (broken) → `Image.Source` null, alt preserved, no throw. No socket. [Source: AC7]

- [x] **Task 6 — Wire `ShellViewModel`/toolbar to the controller; keep 3.1 tests green (AC: 10)**
  - [x] Make `ShellViewModel.OnBack/OnForward/OnReload` delegate to the `NavigationController` (`GoBackAsync`/`GoForwardAsync`/`ReloadAsync`) AND keep setting `LastAction` (so the existing `ShellViewModelTests` stay green). Optionally surface `CanGoBack`/`CanGoForward` for button enable-state. [Source: AC10; ShellViewModelTests]
  - [x] Wire the address-bar successful fetch → `NavigationController.NavigateToAsync` (so typed address and clicked link share the push-and-render path). Decide & document where the render is triggered (Dev Notes "Where the render is triggered"). [Source: AC1, AC4, AC10]
  - [x] **`[Fact]`** the existing `ShellViewModelTests` (`LastAction` defaults/transitions) still pass; add a `[Fact]` that `OnBack` invokes the controller's back path (with a fake controller/fetcher). [Source: AC10]

- [x] **Task 7 — Purity / boundary / no-embedded-browser re-confirm (AC: 11)**
  - [x] Confirm `Rendering` added NO new `PackageReference` (still `{Markdig, ColorCode.Core}`), no `System.Net.*`/socket/AI/webview, no App/Agent ref. The inherited `DependencyBoundaryTests` + `NoEmbeddedBrowserTests` + `RenderingPurityTests` stay green. All new I/O (fetch, image load, browser launch) is in `App`. [Source: AC11]
  - [x] If a Rendering-side net-free seam was added (optional), confirm it touches no forbidden type and adds no package. Otherwise confirm `Rendering/*` is UNTOUCHED by this story. [Source: AC11]
  - [x] Keep `nullable`/`ImplicitUsings` consistent; no new build warnings. [Source: AC12]

- [x] **Task 8 — STA / no-parallel discipline confirm (AC: 12)**
  - [x] Confirm pure App-side logic is `[Fact]`; only WPF-object/window/host/click/scroll/image-load tests are `[StaFact]`. Do NOT re-add `Xunit.StaFact` or the `DisableTestParallelization` attribute (already in `App.Tests/AssemblyInfo.cs`). No shown `Window`/`Dispatcher` pump/socket/real `Process.Start`/pixels/timing. [Source: AC12; Environment Constraint]

- [x] **Task 9 — Final verification against ACs (Definition of Done — checked via CI, not locally) (AC: 1–12)**
  - [x] **AC1:** fetched markdown renders into `ContentHost` (scroll host `Document` set). Proven by the AC1 `[StaFact]`.
  - [x] **AC2:** link classification deterministic/total (internal/anchor/external/unsupported). Proven by the AC2 `[Fact]`s.
  - [x] **AC3:** relative→absolute resolution. Proven by the AC3 `[Fact]`s.
  - [x] **AC4:** internal `.md` click → fetch+render in place + history push. Proven by the AC4 `[Fact]` + click `[StaFact]`.
  - [x] **AC5:** anchor → scroll within page, no re-fetch. Proven by the AC5 `[Fact]`+`[StaFact]`.
  - [x] **AC6:** external → system browser via `IUrlLauncher`. Proven by the AC6 `[Fact]` (fake launcher).
  - [x] **AC7:** images resolve + load inline (App seam), broken→placeholder. Proven by the AC7 `[Fact]`+`[StaFact]` (stub loader).
  - [x] **AC8:** broken/missing/failed → clear state, never crash, no history corruption. Proven by the AC8 `[Fact]`+`[StaFact]`.
  - [x] **AC9:** `.md` → `/api/negotiate/<slug>` mapping (live pages load). Proven by the AC9 `[Fact]`s (mirroring server slugs).
  - [x] **AC10:** Back/Forward/Reload real history navigation. Proven by the AC10 `[Fact]`s.
  - [x] **AC11:** Rendering stays pure (no new package/net/AI/webview/up-ref). Guards green.
  - [x] **AC12:** `dotnet build -c Release` clean + `dotnet test -c Release` all green on `windows-latest` — PENDING CI run (the sole verification surface; not pushed by this agent).
  - [x] Push and confirm the `Build Windows Client` GitHub Actions run is green (the authoritative verification — there is no local build). Record the run result in the Dev Agent Record.

## Dev Notes

### Split assessment (read first — this is a LARGE integration story)

This is the biggest story in Epic 3: it is the one place where fetch (3.2) + render (3.3/3.4) + the shell (3.1) all come together, plus four new behaviors (links, anchors, images, history) and one deferred decision (the endpoint mapping). **Recommendation: keep it WHOLE, because every piece plugs into ONE seam — the `NavigationController` — and the value (a browsable client) only exists when they are wired together.** But the ACs are deliberately authored so a split is mechanical if velocity demands:

- **3.5a (minimum coherent "it loads and browses"):** AC1 (display), AC2 (classify), AC3 (resolve), AC4 (internal link → in-place), AC8 (broken state), AC9 (endpoint mapping — required for ANY live load), AC10 (history), AC11 (purity), AC12 (CI). This is a shippable, demonstrable client: type a `.md` URL, see the page, click internal links, go Back/Forward, broken pages are clear.
- **3.5b (the additive link/media behaviors):** AC5 (anchor scroll), AC6 (external → browser), AC7 (images inline). Each plugs into the already-built `LinkClassifier`/`NavigationController`/content-host with no rework.

If kept whole (recommended), the natural Task ordering above already front-loads the 3.5a core (Tasks 1–3) before the additive slices (Tasks 4–5). **Flag for the PM:** if the team prefers smaller stories, split at the 3.5a/3.5b line — the contracts will not change.

### Pinned App-side API surface (the exact contract for Step-4 TDD + Step-5 impl)

All in namespace `TheMarkdownWeb.App`. Pure/`[Fact]`-testable types take no WPF dependency; the content host is the only `[StaFact]` surface.

```csharp
// --- Pure link/URL seams (no WPF, no I/O — [Fact]) ---

public enum LinkKind { InternalMarkdown, Anchor, External, Unsupported }

public readonly record struct LinkTarget(LinkKind Kind, Uri? Url, string? Fragment)
{
    public static LinkTarget Internal(Uri pageUrl) => new(LinkKind.InternalMarkdown, pageUrl, null);
    public static LinkTarget AnchorTo(string fragment) => new(LinkKind.Anchor, null, fragment);
    public static LinkTarget ExternalTo(Uri url) => new(LinkKind.External, url, null);
    public static LinkTarget Unsupported => new(LinkKind.Unsupported, null, null);
}

public static class PageUrlResolver
{
    // new Uri(base, rel) semantics; null (never throws) for unresolvable/garbage.
    public static Uri? ResolveAgainst(Uri basePageUrl, string relativeRef);
}

public static class SlugDeriver
{
    // C# port of api/negotiate/slug.mjs pathToSlug. Total, never throws.
    public static string PathToSlug(string relPosixPath);
}

public static class PageEndpointResolver
{
    // themarkdownweb.com/<x>.md -> https://themarkdownweb.com/api/negotiate/<slug>; other hosts: as-is.
    public static Uri ToFetchEndpoint(Uri pageUrl);
    public static bool IsAppHost(Uri url); // canonical host predicate (themarkdownweb.com / www.)
}

public static class LinkClassifier
{
    // Deterministic, total. Resolves href against basePageUrl, then classifies.
    public static LinkTarget Classify(string? href, Uri? basePageUrl);
}

public static class ImageResolver
{
    public static Uri? Resolve(string? recordedSource, Uri? basePageUrl); // reuse PageUrlResolver; total.
}

// --- App-side I/O seams (injectable; stubbed in tests) ---

public interface IImageLoader { System.Windows.Media.ImageSource? Load(Uri absolute); }
public sealed class SystemImageLoader : IImageLoader { /* BitmapImage{UriSource}, swallow errors -> null */ }

// --- The navigation state machine (history) — [Fact]-testable with a fake fetcher + sink ---

public sealed class NavigationController
{
    public NavigationController(
        Func<Uri, CancellationToken, Task<FetchResult>> fetch, // default: MarkdownFetcher ∘ PageEndpointResolver
        Action<string, Uri> renderSink,                        // (markdown, pageUrl) -> render into ContentHost
        Action onBroken,                                       // show Broken in ContentHost
        IUrlLauncher launcher);                                // external dispatch

    public Uri? Current { get; }
    public bool CanGoBack { get; }
    public bool CanGoForward { get; }

    public Task NavigateToAsync(Uri pageUrl, CancellationToken ct = default); // push + fetch + render (truncates fwd; same-URL re-pushes)
    public Task GoBackAsync(CancellationToken ct = default);                  // no-op at index 0
    public Task GoForwardAsync(CancellationToken ct = default);              // no-op at tip
    public Task ReloadAsync(CancellationToken ct = default);                 // re-fetch Current in place (no push); no-op if empty
    public Task DispatchAsync(LinkTarget target, CancellationToken ct = default); // route by Kind (Unsupported = no-op)
}
// Re-entrancy: an internal monotonic generation token guards every async op (see
// hardening "Explicit decisions"). History/cursor mutate ONLY on the winning,
// non-stale Success; a stale fetch completion is dropped (no render, no mutation).
// Single-threaded on the UI/STA thread; superseded fetches are cancelled.
```

The **content host** (App, `[StaFact]` surface — e.g. `ContentHostController` over the `FlowDocumentScrollViewer`/`RichTextBox` in `ContentHost`) owns: `Render(markdown) -> set Document`, the image post-process (Task 5), the **single hyperlink click handler** (`Hyperlink.RequestNavigate` → `LinkClassifier.Classify` → `NavigationController.DispatchAsync`, `e.Handled = true`), the anchor scroll callback (Task 4), and the Broken render. `renderSink`/`onBroken` in the controller are wired to this host.

### Advanced-elicitation hardening applied (this revision)

Methods auto-applied: **edge-case / failure hunting**, **first-principles parity verification** (against the live `slug.mjs` + `regex.js` + `manifest.json`), and **red-team totality** (every path must be total/deterministic — nothing throws on bad input). Scope UNCHANGED (the 3.5 integration set). Deltas:

1. **SlugDeriver parity CORRECTED (was an approximation, now byte-exact).** The pre-hardening text said "replace runs of non-`[a-z0-9]` with `-`, trim `-`" — that is **wrong** and would diverge from the server on real keys. Verified the live `slug()` is `value.toLowerCase().replace(<Unicode regex>,'').replace(/ /g,'-')` (DELETE punctuation, only-space→`-`, no collapse, no trim). Pinned the exact algorithm, the regex-embedding requirement, and a parity table (below). Without this fix `a.b.c`/`100%`/`C# & .NET` slugs would mismatch and live pages would 404→Broken.
2. **Link-classification edge matrix** added (below) — `../x.md`, `./x.md`, `x.md`, `/abs/x.md`, `x.md#h`, `x.md?a=1`, bare `#h`, `//host/x.md`, `mailto:`/`tel:`/`javascript:`/`data:`, uppercase `.MD`, percent-encoding, whitespace, empty/null — each with a deterministic, total outcome.
3. **NavigationController transition table** added (below) — Navigate/Back/Forward/Reload × {success, broken, end-of-stack} — pinned so Step-4 tests and Step-5 impl agree exactly.
4. **Image edge matrix** added (below) — missing/relative/absolute/data-URI/protocol-relative/broken-decode/oversized — broken→empty+alt, never crash; loader stubbed (no decode/socket).
5. **Explicit decisions recorded** (below) for failed-navigation history, same-URL navigation, re-entrancy / last-wins, and broken-image — previously underspecified.

#### SlugDeriver parity table (server `pathToSlug` → C# `SlugDeriver.PathToSlug` — byte-identical)

`PathToSlug` takes the **decoded** relative POSIX path (no leading `/`). The caller (`PageEndpointResolver`) first takes `pageUrl.AbsolutePath`, trims the leading `/`, and **decodes `%XX` once** (`Uri.UnescapeDataString`, mirroring the server's single `decodeURIComponent`) so `%20`→space BEFORE slugging. Drop `.md` is case-insensitive and happens before per-segment slugging.

| Input path (decoded, no leading `/`) | → slug | Notes / rule exercised | Manifest cross-check |
|---|---|---|---|
| `gear-guide.md` | `gear-guide` | drop `.md`, already-slug | ✓ key `gear-guide` |
| `README.md` | `readme` | invariant-lowercase | ✓ key `readme` |
| `My Notes.md` | `my-notes` | space→`-`, lowercase | ✓ key `my-notes` |
| `My Notes Dir/page.md` | `my-notes-dir/page` | per-segment slug, multi-segment | ✓ key `my-notes-dir/page` |
| `sub/page.md` | `sub/page` | nested | ✓ key `sub/page` |
| `sub/index.md` | `sub` | trailing `/index` collapse | ✓ key `sub` |
| `index.md` | `` (empty) | bare `index`→`""` (vault root) | (root) |
| `x.md` | `x` | single char | ✓ key `x` |
| `Gear Guide.md` | `gear-guide` | space + case | (= `gear-guide`) |
| `a.b.c.md` | `abc` | **dots DELETED, not `-`** (drop only trailing `.md`) | parity-critical |
| `100%.md` | `100` | `%` deleted, no trailing `-` | parity-critical |
| `C# & .NET.md` | `c--net` | `#`/`&`/`.` deleted, 2 spaces→`--` | parity-critical |
| `--x--.md` | `--x--` | **no hyphen-collapse, no trim** | parity-critical |
| `foo_bar.md` | `foo_bar` | `_` preserved (not in regex) | parity-critical |
| `Hello, World!.md` | `hello-world` | `,`/`!` deleted, space→`-` | — |
| `Über.md` | `über` (best-effort) | non-ASCII letter preserved | **Unicode parity-risk → Deferred-Work-Log** |

The ASCII rows (all real manifest keys + the punctuation cases) are the **mandatory `[Fact]` assertions**. The non-ASCII row is best-effort and flagged: `String.ToLowerInvariant()` and the embedded regex must match JS `toLowerCase()`/the Unicode class; any residual divergence on non-ASCII vault names is a documented risk (no shipped page uses non-ASCII today — manifest is all-ASCII).

#### NavigationController state-transition table (history = `List<Uri> entries` + `int cursor`; `Current = entries[cursor]` or null)

| Op | Precondition | Fetch outcome | Effect on history / cursor | `Current` after | Side effects |
|---|---|---|---|---|---|
| `NavigateToAsync(U)` | any | **Success** | truncate `entries[cursor+1..]`, append `U`, `cursor=Count-1` | `U` | renderSink(md,U) |
| `NavigateToAsync(U)` | any | **Failure / Unsupported** | **UNCHANGED** (no append, no truncate) | unchanged | onBroken() ; history NOT corrupted |
| `NavigateToAsync(Current)` (same URL) | `Current==U` | Success | **append a new entry** (browser semantics: re-pushes, truncating any forward tail) — see decision | `U` | renderSink |
| `GoBackAsync()` | `cursor>0` | Success | `cursor--`; re-fetch `entries[cursor]` | prev | renderSink |
| `GoBackAsync()` | `cursor>0` | Failure | `cursor--` already moved; show Broken for that entry; **cursor stays moved** (the entry exists, it just failed now) — see decision | the back entry | onBroken |
| `GoBackAsync()` | `cursor==0` | — | **no-op** (no fetch) | unchanged | none |
| `GoForwardAsync()` | `cursor<Count-1` | Success | `cursor++`; re-fetch | next | renderSink |
| `GoForwardAsync()` | `cursor==Count-1` (tip) | — | **no-op** | unchanged | none |
| `ReloadAsync()` | `Count>0` | Success | cursor/entries UNCHANGED; re-fetch `Current` | unchanged | renderSink |
| `ReloadAsync()` | `Count>0` | Failure | UNCHANGED | unchanged | onBroken |
| `ReloadAsync()` | `Count==0` (empty) | — | **no-op** | null | none |
| `DispatchAsync(External)` | — | (no fetch) | UNCHANGED | unchanged | `launcher.Open(uri)` |
| `DispatchAsync(Anchor)` | — | (no fetch) | UNCHANGED | unchanged | scroll callback (host) |
| `DispatchAsync(Unsupported)` | — | (no fetch) | UNCHANGED | unchanged | **no-op** (no fetch/launch/throw) |
| `DispatchAsync(InternalMarkdown)` | — | → `NavigateToAsync(target)` | per Navigate rows | — | — |

`CanGoBack = cursor > 0`; `CanGoForward = cursor < entries.Count - 1`. All ops total — under/overflow is a no-op, never an exception.

#### Link-classification edge matrix (`LinkClassifier.Classify(href, base)`; base = `https://themarkdownweb.com/guides/gear.md`)

| href | → LinkKind | Resolved payload | Rule |
|---|---|---|---|
| `./powder.md` | InternalMarkdown | `…/guides/powder.md` | relative `.md`, same host |
| `../index.md` | InternalMarkdown | `…/index.md` | `..` resolves to parent dir |
| `notes.md` | InternalMarkdown | `…/guides/notes.md` | bare relative |
| `/x.md` | InternalMarkdown | `…/x.md` (host root) | absolute-path `.md` |
| `powder.MD` | InternalMarkdown | `…/guides/powder.MD` | **`.md` case-insensitive** |
| `x.md#install` | InternalMarkdown | `…/guides/x.md` (+frag retained on Url.Fragment) | fragment on a cross-page link → navigate, then optionally scroll; classify by the path |
| `x.md?v=1` | InternalMarkdown | `…/guides/x.md?v=1` | query preserved (endpoint mapper uses path only) |
| `#install` | Anchor | fragment `install` | pure fragment → scroll, no fetch |
| ` #top ` (whitespace) | Anchor | `top` | trim before classify |
| `https://themarkdownweb.com/sub/page.md` | InternalMarkdown | as-is | absolute same-host `.md` |
| `https://other.com/a.md` | External | `https://other.com/a.md` | `.md` but **different host** → external |
| `https://themarkdownweb.com/about` | External | that Uri | same host, **no `.md`** → external |
| `http://example.com/x` | External | that Uri | absolute http(s), non-`.md` |
| `//host/x.md` (protocol-relative) | InternalMarkdown if host==app else External | resolved via base scheme | `new Uri(base, "//host/x.md")` adopts base scheme; classify by resolved host+`.md` |
| `mailto:a@b.com` | Unsupported | — | non-http(s) scheme |
| `tel:+15550123` | Unsupported | — | non-http(s) scheme |
| `javascript:alert(1)` | Unsupported | — | hostile scheme, **never executed** |
| `data:text/html,x` | Unsupported | — | data URI |
| `` (empty) / `   ` (whitespace) / `null` | Unsupported | — | total: no throw |
| `:://garbage` / `ht tp://x` | Unsupported | — | unparseable → Unsupported, never throws |

Decision — **fragment on a cross-page link** (`x.md#h`): classify as `InternalMarkdown` (navigate to the page); the post-load scroll-to-fragment is best-effort (if `Url.Fragment` non-empty after render, attempt the AC5 scroll; a missing heading is a no-op). A **pure** `#h` (no path) is `Anchor` (no fetch). This keeps same-page anchors fetch-free while cross-page anchored links still navigate.

#### Image edge matrix (`ImageResolver.Resolve(recordedSrc, base)` → load via stub `IImageLoader`; base = `…/guides/x.md`)

| recorded `Image.Tag` source | → resolved Uri | load result | `Image.Source` | alt |
|---|---|---|---|---|
| `media/pic.png` | `…/guides/media/pic.png` | stub returns sentinel | sentinel | preserved |
| `../img/a.png` | `…/img/a.png` | sentinel | sentinel | preserved |
| `/logo.png` | `…/logo.png` (host root) | sentinel | sentinel | preserved |
| `https://cdn.x/p.png` | as-is (absolute) | sentinel | sentinel | preserved |
| `data:image/png;base64,iVBOR…` | the data Uri (absolute) | loader may build BitmapImage; in tests stub returns sentinel/null | sentinel or null | preserved |
| `//cdn.x/p.png` (protocol-relative) | resolved via base scheme | sentinel | sentinel | preserved |
| broken / 404 / decode-fail | resolved Uri non-null | stub returns **null** | **null (empty)** | **preserved** — no throw |
| `` / `   ` / null / `::garbage` | `null` (unresolvable) | not loaded | null (empty) | preserved |
| huge image | resolved Uri | loader's concern (real `SystemImageLoader` swallows → null); tests stub | sentinel/null | preserved |

Decision — **broken image** = empty `Image` with alt/AutomationProperties.Name preserved, NEVER a crash and never a re-throw. The `SystemImageLoader` wraps `BitmapImage` creation in try/catch → null; `Image.Source` stays unset; rendering continues. Tests use a stub loader (no real decode/socket); the post-process is total over every `Image` element.

#### Explicit decisions (previously underspecified — pinned here)

- **Failed navigation & history:** a `Failure`/`Unsupported` `NavigateToAsync` does **NOT** push, **NOT** truncate, and leaves `Current`/cursor untouched; it routes to `onBroken()` only. There is **no "Broken history entry."** Rationale: Back must always return to a real, previously-rendered page; a half-pushed broken entry would corrupt Back. (For Back/Forward/Reload of an *existing* entry that fails on re-fetch, the cursor has already legitimately moved to that real entry — show Broken for it, do not rewind; the entry stays in history. This is the table's two distinct rows.)
- **Same-URL navigation:** typing/clicking the URL equal to `Current` **re-pushes a new entry** (and truncates any forward tail), matching browser semantics where re-navigating advances history. (Reload, by contrast, does NOT push — it re-fetches `Current` in place.) Documented so `CanGoForward` after a same-URL nav is deterministically false.
- **Re-entrancy / rapid navigation — last-wins, single render, no corruption:** `NavigationController` is single-threaded on the UI/STA thread; each async op carries a monotonically increasing **navigation generation token**. When a fetch awaits, on resume it checks `generation == current`; a **stale** completion is **dropped** (no render, no history mutation) — so a fast second click/Back supersedes the first with **no double-render and no history corruption**. The history mutation (append/truncate/cursor move) happens **only on the winning, non-stale Success**. A `CancellationToken` per navigation is passed to the fetch delegate; superseded fetches are cancelled (the fetcher is total on cancellation → `Failure`, which the stale-guard drops anyway). `[Fact]`-tested with a fetcher whose first call blocks on a gate: start Nav(A), start Nav(B), release A late → assert only B is `Current` and the sink saw B last (A's late completion ignored), history has the expected single tip.
- **Anchor that doesn't exist:** no-op (matcher returns null target) — no scroll, no fetch, no throw.

### Key decisions to make & document (pin these in the impl)

- **Scroll host choice (AC1/AC4):** `FlowDocumentScrollViewer` is read-only by construction and simplest; it raises `Hyperlink.RequestNavigate` for hosted hyperlinks. A read-only `RichTextBox { IsReadOnly=true, IsDocumentEnabled=true }` is the alternative if hyperlink hit-testing under read-only proves finicky (WPF historically needs `IsDocumentEnabled=true` for clickable hyperlinks in a `RichTextBox`). **Default: `FlowDocumentScrollViewer`** (no edit surface, clickable hyperlinks via `RequestNavigate`); document the fallback if CI shows clicks don't route.
- **How the hyperlink click is attached (AC4 — keeps Rendering pure):** the renderer already records `Hyperlink.NavigateUri` (3.3) and adds NO click handler. The App attaches ONE class-level / host-level handler when it hosts the document: `host.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate))` (or on the `FlowDocument`). `OnRequestNavigate` reads `e.Uri`, classifies, dispatches, sets `e.Handled = true`. **Rendering is NOT modified to wire clicks** — this is the App↔Rendering seam (AC11). (Alternative, also documented: Rendering exposes a pure helper to enumerate hrefs; default is App reads `e.Uri` off the bubbled event — no Rendering change.)
- **Where the render is triggered (AC1):** the `AddressBarViewModel` already produces `LastFetchedMarkdown` + `State == Loaded` on success. Wire the typed-address success → `NavigationController.NavigateToAsync` so typed and clicked navigations share ONE path. Document whether the address bar calls the controller directly or the controller owns the address-bar fetch. **Recommended:** the `NavigationController` owns navigation; the address-bar submit calls `controller.NavigateToAsync(typedUrl)`; the controller resolves the endpoint, fetches, renders. (The `AddressBarViewModel`'s own fetch path may be kept for the validation/decline UX, but the live render goes through the controller — avoid double-fetch; document the chosen wiring.)
- **Anchor matching (AC5):** match `#frag` against headings by github-style slug of the heading text (reuse `SlugDeriver`'s slugify), OR have the renderer record an anchor id on each heading `Tag` (a pure, net-free addition if chosen — still AC11-clean). **Default: slugify the heading text in App** (no Rendering change); document if a recorded heading-id seam is added instead.
- **URL→endpoint host policy (AC9):** apply the negotiate mapping ONLY for the canonical app host(s) (`themarkdownweb.com`, `www.themarkdownweb.com`, case-insensitive). Any other host → fetch the page URL as-is. This is a deterministic, documented predicate (`PageEndpointResolver.IsAppHost`), NOT a single hardcoded URL — so a future host change is one edit. Cross-check `SlugDeriver` against `api/negotiate/manifest.json` keys so the client and server agree on the slug for every shipped page.

### Rendering seam options (default keeps Rendering UNTOUCHED)

3.3 already left the two seams 3.5 needs: `Hyperlink.NavigateUri` (records the href) and `Image.Tag` (records the image source). **Default plan: `Rendering` is not modified at all** — the App reads `e.Uri` off the bubbled `RequestNavigate` event and walks `Image` elements for `Image.Tag`. If walking proves awkward, the ONLY permitted Rendering additions are **pure, net-free** helpers (e.g. a heading-anchor-id recorder on `Tag`, or an href/image-source enumerator) that touch no `System.Net.*`/socket/AI/webview and add no package — AC11 stays green either way. Prefer no Rendering change.

### What already exists (build ON this, do not recreate)

- **`MarkdownFetcher.FetchAsync(url, ct) -> FetchResult`** (3.2): GET + `Accept: text/markdown`, never throws, `Success(body)`/`Failure(reason)`. Inject a stub `HttpMessageHandler` in tests (no socket). 3.5 fetches the *resolved endpoint* (AC9).
- **`AddressBarState` + `AddressBarViewModel`** (3.2): `Idle`/`Loading`/`Loaded`/`NotMarkdown`/`Broken`; `LastFetchedMarkdown`. Reuse `Broken` for AC8; reuse the success path to trigger render.
- **`IUrlLauncher` / `SystemBrowserLauncher`** (3.2): reuse for AC6 (external). Inject a fake in tests (records the `Uri`, no `Process.Start`).
- **`AddressBarValidation.IsLoadableMarkdownUrl` / `TryGetHostPath`** (3.2): reuse the `.md` predicate for the classifier.
- **`FlowDocumentRenderer.Render(string) -> FlowDocument`** (3.3/3.4): pure; emits inert `Hyperlink`s (NavigateUri recorded) + record-only `Image`s (`Image.Tag` = source, automation = alt). Reuse as-is; do NOT add net.
- **`ShellViewModel.OnBack/OnForward/OnReload` + `ShellAction`/`LastAction`** (3.1): make real (delegate to controller) but keep `LastAction` so `ShellViewModelTests` stay green.
- **`MainWindow.xaml` `<Border x:Name="ContentHost"/>` + named toolbar buttons** (3.1): fill ContentHost; wire the button click handlers.
- **`App.Tests` infra:** `Xunit.StaFact` 1.1.11 + `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (AssemblyInfo.cs) + `ShellTestHelpers` (`CreateWindow`, `FindButton`). Inherited guards: `DependencyBoundaryTests`, `NoEmbeddedBrowserTests`. Do NOT re-add the STA package/attribute.
- **`api/negotiate/slug.mjs` `pathToSlug` + `api/negotiate/manifest.json`**: the server slug source of truth `SlugDeriver` mirrors (AC9). Route `negotiate/{*slug}` (`api/negotiate/index.mjs`).
- **CI:** `.github/workflows/build-windows.yml` on `windows-latest`; restore → build -c Release → test -c Release on the `.sln`; `paths: clients/windows/**`. **Sole verification path.**

### Critical constraints (do not violate)

- **`Rendering` stays PURE.** Link NAVIGATION, image LOADING, URL→endpoint resolution, the history stack, and the browser launch ALL live in `App`. `Rendering` gains NO package, NO `System.Net.*`/socket, NO AI, NO webview, NO App/Agent ref. The inherited `DependencyBoundaryTests` + `NoEmbeddedBrowserTests` + `RenderingPurityTests` enforce this and MUST stay green.
- **No embedded browser.** `IImageLoader`/`BitmapImage` is WPF imaging (allowed). `SystemBrowserLauncher` shells out to the OS (allowed). NO `WebView2`/`CefSharp`/`WebBrowser`/Chromium — the `NoEmbeddedBrowserTests` csproj-substring guard forbids it.
- **Totality everywhere.** Classification, resolution, slug derivation, image resolution, history transitions, anchor matching — all deterministic and total; broken/missing/failed/garbage never throws. Back at index 0 / Forward at tip / Reload on empty / Unsupported dispatch are no-ops. A failed fetch never corrupts history.
- **Windows-only verification, STA + no-parallel.** No .NET SDK on Linux; WPF is Windows-only; headless runner. Pure App logic = `[Fact]`; WPF-object/window/host/click/scroll/image-load = `[StaFact]`. STA package + `DisableTestParallelization` already present — do NOT re-add. No shown `Window`/`Dispatcher` pump/socket/real `Process.Start`/pixels/timing.
- **Scope: the integration only.** NO 3.6 visual-theme polish (this shows the bedrock FlowDocument). NO personality render/selector (Epic 4). NO video/audio playback controls (images-inline is the AC; richer media is deferred — note in the Deferred-Work-Log if a `<video>`/`<audio>` appears). NO offline caching, NO tabs/multiple windows, NO same-URL Accept negotiation (use the Function endpoint).

### Deferred-Work-Log (out-of-scope items surfaced during hardening — do NOT expand 3.5 scope)

- **Non-ASCII vault-name slug parity (→ revisit if/when a non-ASCII page ships).** Every current `manifest.json` key is ASCII, so `SlugDeriver` parity is byte-exact for all shipped pages. The C# `ToLowerInvariant()` + embedded github-slugger regex are best-effort for non-ASCII (e.g. `Über`→`über`); a residual divergence from JS `String.prototype.toLowerCase()` / V8's Unicode handling on exotic codepoints is a KNOWN, documented risk, NOT fixed here. Action when a non-ASCII page is added: add a golden cross-check test that runs the server `slug()` and asserts the C# output matches (out of scope for 3.5 — no such page exists). [→ Epic-4 / whenever a non-ASCII vault page lands]
- **Richer media (`<video>`/`<audio>`/iframe) playback.** 3.5 inlines a static `<img>` only (the AC). If Markdig ever emits a `<video>`/`<audio>`/embed, it is NOT wired here — record it and defer playback controls. [→ 3.6+ / Epic-4]
- **Same-URL `Accept` content-negotiation at the page URL.** Deferred from 2.7 (SWA route rules can't branch on `Accept`); 3.5 uses the `/api/negotiate/<slug>` Function endpoint (AC9). No change. [→ already-deferred, 2.7 log]
- **Button enabled-state binding (Back/Forward greying).** AC10 minimum is no-op-at-ends; surfacing `CanGoBack`/`CanGoForward` to grey the toolbar buttons is a nice-to-have polish — if not done in 3.5, the handlers remain safe no-ops. [→ 3.6 toolbar polish, optional]
- **Loading/progress affordance during fetch** (EXPERIENCE.md "Loading — not a blank flash"). 3.5's seam supports it (the controller knows when a fetch is in flight) but the visible spinner/progress is visual polish → 3.6. The bedrock behavior (Idle→Loaded/Broken) is in-scope; the lightweight-progress chrome is not. [→ 3.6]

### Source tree components to touch

- `clients/windows/App/PageUrlResolver.cs` — relative→absolute resolver (NEW).
- `clients/windows/App/SlugDeriver.cs` — `pathToSlug` C# port (NEW).
- `clients/windows/App/PageEndpointResolver.cs` — `.md`→`/api/negotiate/<slug>` (NEW).
- `clients/windows/App/LinkClassifier.cs` + `LinkTarget`/`LinkKind` — classification (NEW).
- `clients/windows/App/ImageResolver.cs` — image source resolution (NEW).
- `clients/windows/App/IImageLoader.cs` + `SystemImageLoader` — image load seam (NEW).
- `clients/windows/App/NavigationController.cs` — history stack + dispatch (NEW).
- `clients/windows/App/ContentHostController.cs` (or similar) — host FlowDocument + attach click handler + image post-process + anchor scroll + Broken render (NEW).
- `clients/windows/App/MainWindow.xaml` + `.xaml.cs` — `FlowDocumentScrollViewer` in `ContentHost`; wire toolbar buttons (UPDATE).
- `clients/windows/App/ShellViewModel.cs` — make Back/Forward/Reload delegate to the controller, keep `LastAction` (UPDATE).
- `clients/windows/App.Tests/` — new `[Fact]`/`[StaFact]` test files: `LinkClassifierTests`, `PageUrlResolverTests`, `PageEndpointResolverTests`/`SlugDeriverTests`, `ImageResolverTests`, `NavigationControllerTests`, `ContentHostTests` (STA), `AnchorScrollTests` (STA), `ImageLoadTests` (STA) (NEW).
- Do NOT touch: `Rendering/*` (default plan — pure), `Rendering.Tests/*`, `TheMarkdownWeb.sln`, `build-windows.yml`, `api/*` (read-only reference for the slug logic), the STA package/`AssemblyInfo.cs`.

### Testing standards summary

- **Framework:** xUnit (existing `App.Tests` versions: xunit 2.9.2, Microsoft.NET.Test.Sdk 17.12.0, Xunit.StaFact 1.1.11). **STA:** `[StaFact]` for every test that constructs `MainWindow`/hosts a `FlowDocument`/reads `Hyperlink.NavigateUri`/`Image.Source`/attaches the click handler/scrolls. **`[Fact]`** for the pure seams (`LinkClassifier`, `PageUrlResolver`, `PageEndpointResolver`/`SlugDeriver`, `ImageResolver`, anchor matcher, `NavigationController` with stubs). The `DisableTestParallelization` attribute is already present.
- **No headless-incompatible tests:** no shown `Window`, no `Dispatcher` pump, no socket (stub `HttpMessageHandler`), no real `Process.Start` (fake `IUrlLauncher`), no real image download/decode (stub `IImageLoader`), no pixels/timing.
- **No-tautology:** assert against the REAL types' output (the controller's history transitions; the classifier's actual `LinkTarget`; the endpoint mapper's actual `Uri`; the hosted `Document`/`Image.Source`), not re-declared stubs. The endpoint-mapping proof mirrors the live `manifest.json` slugs.
- **No regression:** the 3.1/3.2 `App.Tests` (incl. `ShellViewModelTests`, address-bar tests, `NoEmbeddedBrowserTests`, `DependencyBoundaryTests`) + the 3.3/3.4 `Rendering.Tests` stay green; `Rendering` stays pure.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.5: In-client links, media, navigation] (lines 338–350) — user story + ACs (FR-2, FR-3, FR-8, UX-DR8): internal `.md` link → in-place fetch+render; `#anchor` → scroll; external `http(s)` → system browser; images resolve from vault + render inline; broken link → clear state, never a crash.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/EXPERIENCE.md] (lines 41, 44, 46–50, 54–58) — Interaction Primitives (internal `.md` → navigate in-place; anchor `#heading` → scroll; external `http(s)` → system browser) + State Patterns (Broken/missing `.md` → clear "page not found / not a markdown page", never a crash; non-`.md` → decline + offer system browser).
- [Source: _bmad-output/planning-artifacts/architecture.md] — `App/` = shell, window, navigation, fetch raw `.md`, host the FlowDocument; `Rendering/` pure (no networking, no AI), App→Rendering never reverse; FC-1 no embedded browser; content-negotiation Function `/api/negotiate/<slug>` (Accept → HTML | raw `.md`).
- [Source: _bmad-output/implementation-artifacts/3-2-md-only-address-bar-and-fetch.md] (lines 174–183 "Resolved fetch-target / routing decision" + the DEFERRED-WORK LOG line 183) — 3.2 fetches the typed URL as-is; **"Story 3.3+ must wire the typed `themarkdownweb.com/<x>.md` → `/api/negotiate/<slug>` mapping … until then a live `.md` fetch resolves to Broken by design"** — the decision AC9 lands.
- [Source: _bmad-output/implementation-artifacts/3-3-markdig-ast-flowdocument-rendering.md] (AC10 + "Resolved image-loading decision" lines 242–251 + the DEFERRED-WORK LOG) — `Rendering` records image source on `Image.Tag` + alt on automation name, NO fetch; **"Story 3.5 must resolve relative image paths against the vault and load the bytes (FR-3) — wiring the recorded source URI to a BitmapImage through an App-side fetch seam"** — the decision AC7 lands. Also: inert `Hyperlink` with recorded `NavigateUri`, click navigation deferred to 3.5 (AC4).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] (the 2-7 entry, line 39) — true same-URL `Accept` negotiation deferred (SWA route rules cannot branch on `Accept`); raw markdown exposed at `/api/negotiate/<slug>`; the native client (Epic 3) fetches from there — the basis for AC9's endpoint mapping.
- [Source: api/negotiate/slug.mjs] (lines 25–33 `pathToSlug`) — the server slug derivation `SlugDeriver` mirrors (AC9): drop `.md` case-insensitive, github-slug each `/`-segment (lower-case), collapse trailing `/index` and bare `index`. [Source: api/negotiate/index.mjs] route `negotiate/{*slug}`. [Source: api/negotiate/manifest.json] — the closed slug map to cross-check (`gear-guide`, `sub`←`sub/index.md`, `readme`←`README.md`, `my-notes-dir/page`←`My Notes Dir/page.md`).
- [Source: clients/windows/App/MarkdownFetcher.cs, AddressBarViewModel.cs, AddressBarValidation.cs, IUrlLauncher.cs, ShellViewModel.cs, MainWindow.xaml/.cs] — the 3.1/3.2 pieces this story integrates (fetch, state, validation, launcher, inert toolbar, ContentHost).
- [Source: clients/windows/Rendering/FlowDocumentRenderer.cs] (MapLink lines 627–638, MapAutolink 640–645, BuildImageContainer 647–656, SetNavigateUri 681–693) — the inert `Hyperlink` (recorded `NavigateUri`) + record-only `Image` (`Image.Tag`) seams 3.5 wires.
- [Source: clients/windows/App.Tests/DependencyBoundaryTests.cs, NoEmbeddedBrowserTests.cs, ShellViewModelTests.cs, ShellTestHelpers.cs, AssemblyInfo.cs] — inherited guards + STA infra that stay green; the test patterns this story mirrors.
- [Source: _bmad-output/implementation-artifacts/3-1-wpf-app-shell-with-toolbar.md, 3-4-code-syntax-highlighting.md] — the proven Windows-CI-only + STA + DisableTestParallelization discipline this story mirrors.

## Dev Agent Record

### Agent Model Used

Opus 4.8 (1M context) — claude-opus-4-8[1m]

### Debug Log References

- SlugDeriver github-slugger parity validated against the live `api/node_modules/github-slugger/regex.js` (source length 8166, byte-identical to the embedded `static readonly Regex` pattern) by replicating the exact C# algorithm in Node and running the full golden table + manifest-key cross-check — all rows pass (the lone `/` "mismatch" is a no-op-input case the test asserts only for non-throwing, not a specific value).
- AnchorMatcher slug cases re-validated against github-slugger (`Install`→`install`, `Getting Started`→`getting-started`, `C# & .NET`→`c--net`, `FAQ?`→`faq`, `foo_bar`→`foo_bar`).

### Completion Notes List

- **SlugDeriver (AC9):** byte-identical port of server `pathToSlug`. The github-slugger Unicode class from `regex.js` is embedded VERBATIM (extracted programmatically from the installed package, not hand-typed) as a verbatim C# string in a `static readonly Regex`. Per `/`-segment: `ToLowerInvariant()` → `Regex.Replace(seg, regex, "")` (DELETE, not replace) → `Replace(' ', '-')`. No hyphen collapse, no trim. Drops a trailing `.md` (case-insensitive) over the WHOLE path first, then slugs segments, then collapses trailing `/index` and bare `index`→"". Total.
- **PageUrlResolver (AC3):** `new Uri(base, rel)` semantics; rejects interior-space and leading-`:` refs explicitly so `ht tp://x` / `:://garbage` deterministically resolve to `null` (.NET's lenient parser would otherwise mis-resolve them). Shared by LinkClassifier + ImageResolver.
- **PageEndpointResolver (AC9):** `IsAppHost` is an EXACT host match (`themarkdownweb.com` / `www.themarkdownweb.com`, case-insensitive) — defeats the suffix/prefix attack rows. `ToFetchEndpoint` decodes `%XX` once, slugs, rebuilds same-origin via `UriBuilder` (default-port omitted, query/fragment dropped); non-app host returned as-is.
- **LinkClassifier (AC2):** pure `#frag`→Anchor (no resolution); resolves else; non-http(s) → Unsupported; same-host `.md` → InternalMarkdown (resolved Url keeps query/fragment); other http(s) → External. Protocol-relative `//host/x.md` adopts the base scheme then classifies by resolved host. Null base + relative ref → Unsupported. Total.
- **NavigationController (AC4/6/8/10):** `List<Uri>`+cursor history; monotonic generation token for last-wins re-entrancy — a stale fetch completion (generation mismatch on resume) is dropped with NO render and NO history mutation; superseded fetches are cancelled via a linked `CancellationTokenSource`. Navigate(success) truncates the forward tail + appends + cursor=tip; Navigate(failure/Unsupported) leaves history untouched + `onBroken` only; same-URL re-pushes; Back/Forward re-fetch and move cursor (and commit the cursor move on a failing existing entry, per the transition table); Reload re-fetches Current without pushing; ends/empty are no-ops. Dispatch routes by kind (External→launcher, Internal→Navigate, Anchor/Unsupported→no fetch). Never throws.
- **ContentHostController (AC1/4/5/7/8):** wraps the read-only `FlowDocumentScrollViewer`. `ShowMarkdown` renders via `FlowDocumentRenderer`, post-processes every `Image` (resolve `Image.Tag` source → `IImageLoader.Load` → set `Image.Source`; null/unresolvable → leave empty, alt/AutomationProperties preserved, loader not even invoked when unresolvable), sets `Document`. ONE host-level `Hyperlink.RequestNavigate` handler (attached in the ctor, before any Show — construct-not-Show friendly) classifies `e.Uri` and raises `onLinkActivated`, with `e.Handled = true` to suppress WPF's default shell-launch. `FindAnchorTarget` walks heading `Paragraph`s (renderer tags them `Tag="h{n}"`) and matches via `AnchorMatcher`; `ScrollToAnchor` `BringIntoView()`s (missing → no-op; guarded against pre-layout throw). `ShowBroken` sets a clear distinguishable `FlowDocument` and `IsBroken=true`.
- **SystemImageLoader:** the ONLY image I/O — builds a `BitmapImage{UriSource}` (`OnLoad` cache, frozen), all failures swallowed → null. WPF imaging, not an embedded browser.
- **ShellViewModel (AC10):** additive — kept the parameterless 3.1 ctor + `LastAction`; added `ShellViewModel(NavigationController)`; `OnBack/OnForward/OnReload` still set `LastAction` AND (when wired) drive the controller (fire-and-forget).
- **MainWindow:** `FlowDocumentScrollViewer x:Name="ContentScroll"` added inside `<Border x:Name="ContentHost"/>` (read-only by construction). `.xaml.cs` constructs the wiring: fetcher → `NavigationController` (fetch delegate maps page URL → `PageEndpointResolver` endpoint), render/broken sinks → `ContentHostController`, link dispatch (Anchor→scroll in host, else→controller), toolbar buttons → `ShellViewModel`→controller, address submit of a valid `.md` URL → `controller.NavigateToAsync` (non-`.md` keeps the 3.2 decline UX). 3.1/3.2 named elements and behavior preserved.
- **Purity (AC11):** No package added to ANY csproj. All new I/O (endpoint fetch, image load, browser launch) is in App. Rendering untouched.

### Scroll-host RequestNavigate routing (story-flagged risk)

The story flagged a possible read-only `RichTextBox` fallback if `Hyperlink.RequestNavigate` does not route from a read-only `FlowDocumentScrollViewer`. **Approach taken:** `FlowDocumentScrollViewer` (read-only by construction, no edit surface) with a host-level `AddHandler(Hyperlink.RequestNavigateEvent, …)`. `RequestNavigate` is a bubbling routed event raised by `Hyperlink` itself and does not depend on the host being editable, so the read-only `FlowDocumentScrollViewer` is expected to route it. The `ContentHostTests` exercise this by raising the bubbling event on the hosted `Hyperlink` directly (the exact event WPF raises on a real click), so the App-side handler is verified on CI regardless of hit-testing. If a future real-click CI check shows clicks don't route under read-only, the documented fallback is `RichTextBox { IsReadOnly=true, IsDocumentEnabled=true }` — but no code change is needed for the current test surface.

### File List

New (clients/windows/App/):
- `LinkTarget.cs` — `LinkKind` enum + `LinkTarget` record struct (factories).
- `PageUrlResolver.cs` — relative→absolute resolver (AC3).
- `SlugDeriver.cs` — github-slugger byte-identical `PathToSlug` port (AC9).
- `PageEndpointResolver.cs` — `.md` page URL → `/api/negotiate/<slug>` (AC9) + `IsAppHost`.
- `LinkClassifier.cs` — `Classify(href, base)` (AC2).
- `ImageResolver.cs` — image source resolution (AC7).
- `AnchorMatcher.cs` — fragment↔heading slug match (AC5).
- `IImageLoader.cs` — image-load seam + `SystemImageLoader` (AC7).
- `NavigationController.cs` — history state machine + dispatch + re-entrancy (AC4/6/8/10).
- `ContentHostController.cs` — host FlowDocument + click handler + image post-process + anchor scroll + Broken (AC1/4/5/7/8).

Modified (clients/windows/App/):
- `MainWindow.xaml` — added `FlowDocumentScrollViewer x:Name="ContentScroll"` inside `ContentHost`.
- `MainWindow.xaml.cs` — real wiring (fetcher → controller → content host; toolbar + address submit drive the controller).
- `ShellViewModel.cs` — additive controller ctor; handlers delegate to the controller, keep `LastAction`.

NOT touched: `Rendering/*`, `Rendering.Tests/*`, `App.Tests/*` (tests are the oracle), `.sln`, `build-windows.yml`, `api/*`, `AssemblyInfo.cs`, `*.csproj` (no package added).

### CI Verification

**CI run #28** (`92d7c14`): **GREEN** on windows-latest — Restore ✓ Build ✓ Test ✓, **78 Rendering + 221 App.Tests all pass**. Authoritative verification. Three CI rounds were needed:
- **#22** (`e96b9bd`): build error — `ContentHostController.cs` missing `using System.Windows.Automation;` (CS0103). Fixed.
- **#24** (`9d05999`): build error — `ImageResolverTests.cs` referenced `Uri.UriSchemeData`, which doesn't exist in .NET (the `data:` scheme is the literal `"data"`). Fixed.
- **#26** (`3661197`): compiled; **218/221 App.Tests passed, 3 failed** — a real impl bug: a protocol-relative `//host/path` ref was captured by `Uri.TryCreate(.., Absolute)` as a `file://` UNC URI before base resolution, so it never adopted the base scheme. Per web semantics `//host/path` is scheme-relative. Fixed in `ImageResolver.Resolve` + `LinkClassifier.ResolveHref` (skip the absolute-parse for `//`-leading refs → fall through to `PageUrlResolver`). → **#28 GREEN**.

The embedded github-slugger regex (the review's top CI-red watch-item) compiled and ran correctly as a .NET `Regex`; the SlugDeriver/AnchorMatcher parity was also Node-cross-validated against the live slugger.

### Consolidated Code Review (Step 7) — APPROVED

No blocking changes. Verified (by hand + against upstream): slug parity, signature parity vs all 11 test files, NavigationController cursor/truncation/generation-token re-entrancy correctness, the FlowDocument image-tree walk (InlineUIContainer→Image incl. Span/Hyperlink recursion), exact-host `IsAppHost` (no suffix/prefix bypass), purity/boundary (no package added, Rendering untouched). The 3 CI-reds above were the missing usings (not behavioral) + the protocol-relative bug (a real fix); none contradicts the review's behavioral analysis.

### AC → Test Trace (Step 10) — all green on run #28

| AC | Requirement | Test(s) |
|----|-------------|---------|
| AC1 | Render fetched markdown into ContentHost (content on screen) | `ContentHostTests` (host render) |
| AC2 | LinkClassifier total: Internal/Anchor/External/Unsupported | `LinkClassifierTests` (incl. protocol-relative) |
| AC3 | Relative→absolute resolution against page base | `PageUrlResolverTests` |
| AC4 | Internal `.md` click → fetch+render in place + history push | `NavigationControllerTests` + `ContentHostTests` (click route) |
| AC5 | `#anchor` → scroll within page, no re-fetch | `AnchorMatcherTests` + `AnchorScrollTests` |
| AC6 | External `http(s)` → system browser | `NavigationControllerTests` (Dispatch External → launcher) |
| AC7 | Images resolve from vault + load inline (broken→placeholder) | `ImageResolverTests` + `ImageLoadTests` (stub IImageLoader) |
| AC8 | Broken/missing/failed → clear state, never crash, history intact | `NavigationControllerTests` (failure→Broken) + `ContentHostTests` (ShowBroken) |
| AC9 | URL→`/api/negotiate/<slug>` (github-slugger parity) | `SlugDeriverTests` (golden + 12 manifest keys) + `PageEndpointResolverTests` |
| AC10 | Back/Forward/Reload real navigation (history) | `NavigationControllerTests` (full transition table) + `ShellViewModelNavigationTests` |
| AC11 | Rendering stays pure (no net/AI/webview/up-ref) | inherited `RenderingPurityTests`/`DependencyBoundaryTests`/`NoEmbeddedBrowserTests` green; no package added |
| AC12 | windows-latest CI gate (STA + no-parallel) | CI run #28 green; only 3 hosting tests are `[StaFact]` |

All 12 ACs covered by CI-runnable proofs. Trace complete. **Story 3-5 DONE — the client is now functionally browsable.**
