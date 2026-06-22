# Epic 3 Retrospective: Read Markdown in the Native Client (Windows WPF)

**Epic:** Epic 3 — Read Markdown in the Native Client (Windows)
**Stories:** 3-1 through 3-6 (all done)
**Exit state:** Native WPF Markdown Web reader — shell, address bar, Markdig→FlowDocument render, syntax highlighting, in-client nav/media, and basic faithful default render. EPIC 3 EXIT logged in sprint-status.yaml: "native client = usable basic Markdown Web reader."

---

## 1. Summary

Epic 3 delivered the full rendering bedrock of the Windows native client: a .NET 10 WPF application that opens `.md` URLs, fetches raw markdown over HTTP with `Accept: text/markdown`, parses with Markdig 1.3.1, and renders a WPF FlowDocument covering GFM headings, bold/italic/strikethrough, paragraphs, lists, task lists, blockquotes, tables, inline and fenced code, images, and anchor scroll. Code blocks are syntax-highlighted via ColorCode.Core 2.0.15 (github-light palette). In-client navigation supports relative `.md` links (fetched in place), `#anchor` scroll, external http(s) links (open in system browser), vault images, and back/forward/reload. The default render uses a `RenderTheme.Basic` github-light basic theme that zeroed out existing 3-3/3-4/3-5 assertions — no regressions. The shell exposes labeled, keyboard-reachable WPF UI Automation controls (UX-DR9/NFR-6). A seam for Epic 4 personality selection (`RenderTheme.Basic` as default) was wired without building the AI layer.

The client has no Chromium, WebView2, CefSharp, or any embedded browser dependency — enforced by a failing automated test at every CI run.

---

## 2. FR/Goal Closure

| Requirement | Met? | Evidence |
|---|---|---|
| **FR-9** Open in native client (`.md` URL → loaded in the WPF client) | Yes | Story 3-2: address bar accepts absolute http/https `.md` URLs, fetches via `Accept: text/markdown`, displays rendered output (complete end-to-end by 3-5). 7/7 ACs. CI run #11 green (78/78 tests). |
| **FR-13 / NFR-1** No Chromium / works everywhere (Windows first, no embedded browser) | Yes (hard) | Story 3-1: `NoEmbeddedBrowserTests` — 3-tier guard: csproj PackageReference scan (authoritative), 1-hop transitive runtime closure scan, XAML/source grep. Enforced in every CI run from 3-1 onward. No-Chromium csproj + runtime tiers green at all six stories. |
| **NFR-1** No embedded browser dependency | Yes | Same guard. `MarkdownFetcher` uses `System.Net.Http` (framework); "open in browser" uses `Process.Start` shell-execute — neither introduces a webview. |
| **FR-2 (client-side)** Inter-file linking | Yes | Story 3-5: relative `.md` links fetched in place; `/api/negotiate/<slug>` mapping (github-slugger parity, Node-cross-validated); `#anchor` scroll; broken-link state (never a crash). 12 ACs. |
| **FR-3 (client-side)** Media embedding | Yes | Story 3-5: vault images resolve and render inline via the WPF FlowDocument image element (source recorded; no fetch in Rendering). 12 ACs. |
| **FR-6 (client-side)** Beautiful default presentation | Yes | Story 3-3 (GFM→FlowDocument), 3-4 (github-light ColorCode palette), 3-6 (RenderTheme.Basic additive github-light theme; G1/G2 constraints honored; zero prior assertions changed). 7/7 ACs (3-6). |
| **FR-8 (client-side)** Navigation | Yes | Story 3-5: Back/Forward/Reload wired; in-client nav + anchor scroll + external-to-browser + history stack. 12 ACs. |

---

## 3. What Went Well

- **Pure `Rendering` module + hard boundary.** The `Rendering` project (Markdig + System.Windows only — no networking, no AI) was established in 3-1 and held strictly across all six stories. The `DependencyBoundaryTests` (Rendering references neither App nor Agent; App references Rendering) made the constraint a CI gate rather than a convention. This meant 3-3 and 3-4 delivered a genuinely clean, independently-testable rendering library, which Epic 4 could extend without touching the fetch or nav layers.

- **No-Chromium guard as a first-class CI assertion.** Implementing NFR-1 as a *failing* automated test from day one (3-1) rather than relying on code review meant the constraint was enforced mechanically at every push. The three-tier design (csproj scan, 1-hop transitive runtime closure, XAML/source grep) caught classes of accidental dependency that a convention-only rule would miss.

- **CI-as-verification loop.** The absence of a local .NET SDK forced every AC to be expressed as a compiler check or a `dotnet test`-runnable assertion on `windows-latest`. This discipline produced a thorough, self-contained test suite (14 tests in 3-1 growing to 293 by 4-4) with no "run the app and look" gaps. The CI run number was recorded in sprint-status.yaml for every story, creating an auditable verification trail (runs #6, #11, #16, #19, #28, #31).

- **WPF UI Automation and accessibility wired from the start.** `AutomationProperties.Name` exact strings and `TabIndex` ordering were specified and asserted in 3-1 (shell toolbar) and 3-2 (address bar), not bolted on at 3-6. The shell accessibility exit (UX-DR9) was locked at 3-6 review APPROVED with no rework.

- **Scope discipline across six stories.** Each story deferred the next story's concerns explicitly (e.g., 3-2 deliberately leaves `ContentHost` empty for 3-3; 3-3 defers color/nav to 3-4/3-5; 3-6 installs the personality seam without building it). No story accrued scope from its successors, which kept CI green continuously without large-scale rework.

---

## 4. What Was Hard / What We Would Change

- **Linux dev box with no .NET SDK — verification only via windows-latest CI.** This was the defining constraint of the entire epic. Every check had to be expressed as `[Fact]` (pure logic) or `[StaFact]` (WPF STA construct-not-show). The round-trip time from code to verification was one CI push; reasoning errors that would be caught instantly in a local run instead appeared as red CI runs. We would carry this discipline forward unchanged — but accept the slower iteration loop as the cost of the platform.

- **STA/WPF test discipline with `Xunit.StaFact` + `DisableTestParallelization`.** WPF UI objects have STA thread-affinity; any test constructing a `Window`/`Control` on an MTA thread throws `InvalidOperationException`. Story 3-2's CI run #9 caught a `System.IO.Packaging` test-parallelism race that produced a `InvalidOperationException` on a background thread inside WPF's FlowDocument infrastructure. The fix was `[assembly: CollectionBehavior(DisableTestParallelization = true)]` on `App.Tests`. This should be established in the first story that creates WPF test objects rather than discovered by a failing CI run mid-epic.

- **Repeated missing-using compile failures in CI.** Story 3-3's review returned CHANGES-REQUESTED specifically for a missing `using System.Windows.Automation` in a test helper. Story 3-5 required two CI rounds for missing-using compile fixes before reaching the substantive protocol-relative scheme bug. On a dev machine with IDE tooling these would be caught before the first push; on Linux without a .NET SDK they only surface on CI. A pre-push checklist or a namespace/using linter stage in CI would catch these before burning a round-trip.

- **The `System.IO.Packaging` test-parallelism race (3-2, CI run #9).** WPF's FlowDocument uses `System.IO.Packaging` internally; parallel test execution across multiple test threads contended on its static state. This is a non-obvious transitive dependency of WPF test objects. The fix (`DisableTestParallelization`) is cheap but must be known in advance. Document this as a WPF test project setup requirement rather than a bug to discover.

- **Protocol-relative scheme bug (3-5, CI run #28 was the third round).** An `//host/path` protocol-relative URL slipped past the URL classifier because `Uri.TryCreate` accepts it as a relative URI and the scheme check was absent on that path. Three CI rounds in story 3-5 (two missing-using compile fixes + one real protocol-relative scheme bug) illustrate that edge cases in URL handling only become visible when real rendered markdown exercises the nav code on the Windows runner.

---

## 5. Lessons / Carry-Forward

These practices directly enabled Epic 4:

1. **Establish `[assembly: CollectionBehavior(DisableTestParallelization = true)]` on every WPF test project in story 1 of the project that introduces WPF tests.** Do not discover this via a CI race mid-epic.

2. **Express every hard architectural constraint (NFR-1, dependency boundary) as a failing test from the first story.** The no-Chromium guard and `DependencyBoundaryTests` pattern carried through all of Epic 3 and Epic 4 unchanged and required no revisits.

3. **Keep `Rendering` pure (no networking, no AI) and assert it mechanically.** The `DependencyBoundaryTests` pattern cost one `[Fact]` per project and saved every subsequent story from accidental coupling. Epic 4's `Agent` project added cleanly on top without touching `Rendering`.

4. **Record CI run numbers and commit SHAs in sprint-status.yaml inline notes.** This creates an auditable, linkable verification trail with zero overhead per story, and makes retrospectives like this one grounded rather than speculative.

5. **Define seams for the next epic before closing the current one.** Story 3-6 installed `RenderTheme.Basic` as the default and the personality seam as a named hook without building the AI layer. Epic 4 story 4-1 could add `PersonalizationGateway` and `AnthropicLlmClient` without restructuring 3-6's output.

6. **Use a stub `HttpMessageHandler` pattern for all fetch tests.** This pattern (introduced in 3-2) enabled network-dependent behavior to be fully tested on a headless Windows runner with no real sockets, and was reused unchanged through 3-5.

---

## 6. Success Assessment

Epic 3 delivered a fully functional, no-Chromium native WPF Markdown Web reader — all six stories done, all FRs closed, all CI runs green — within the constraints of a Linux dev environment with no local .NET SDK; the CI-as-verification discipline worked end-to-end and produced a clean, well-bounded codebase for Epic 4 to build on.
