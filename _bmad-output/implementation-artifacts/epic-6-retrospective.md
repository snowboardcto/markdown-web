# Epic 6 Retrospective — The Markdown Lens (Windows)

- **Date:** 2026-06-23
- **Author:** naethyn (automated — BMAD epic retrospective)
- **Epic status:** done / CLOSED
- **Scope:** FR-19, FR-20, FR-21, FR-22 (PRD §4.7). Stories 6.1–6.4.
- **Verification surface:** `build-windows.yml` on `windows-latest` (the dev/Linux host has no .NET SDK; WPF builds/runs only on Windows).
- **Final state:** GREEN on `windows-latest` (485 tests, incl. 8 skipped live probes) on the branch (e7d9301) and GREEN on the `main` merge (2a11e15).

---

## 1. Outcome vs. Goal

**Goal.** Turn the native Windows client outward — from a reader of *our* Vault into the native reader for the *markdown-native web*: default to themarkdownweb.com, accept any `http(s)` URL, discover an available markdown representation, render it through the existing Markdig pipeline, and say "no markdown available" when none exists. No Chromium/WebView (NFR-1); Rendering stays pure (NFR-5); reuse Markdig + content-negotiation (FR-14).

**Outcome — delivered and CI-verified:**

| FR | Requirement | Delivered | Verified |
|----|-------------|-----------|----------|
| FR-19 | Client default home (themarkdownweb.com on launch + Home action) | `HomeNavigator` (single canonical `Uri`), `Loaded`-hook launch nav, Home button in its own toolbar column | Story 6.1 — 6/6 ACs, green CI |
| FR-20 | Open any `http(s)` URL; non-`http(s)` declined; UX-DR5 `.md only` → `.md-discoverable` | `IsAcceptableUrl` predicate (added, not mutating `IsLoadableMarkdownUrl`), 3-way submission routing, revised tag copy + a11y name | Story 6.2 — 6/6 ACs, green CI |
| FR-21 | Ordered first-hit-wins discovery cascade, Content-Type + doctype byte-sniff (zero false positives), honest UA, 403 distinct, bounded budget | `MarkdownDiscoveryService` (Step1 negotiation+alt-link → Step2 `.md` sibling → Step3 `/llms.txt` index), `MarkdownCandidateValidator`, `AlternateLinkParser`, `MaxProbes=4`, per-probe timeout, `MaxAutomaticRedirections=5`, honest UA, 403/401 short-circuit | Story 6.3 — 8/8 ACs, green CI |
| FR-21 integ. / FR-22 | Render discovered `PageMarkdown` through gateway+Markdig (per-reader); explicit no-markdown / blocked / llms-index states (no HTML fallback) | `DiscoveryOutcomeDispatcher`, `ShowNoMarkdown`/`ShowBlocked`/`ShowLlmsIndex`, generation-token last-wins | Story 6.4 — 7/7 ACs, green CI |

**Honest positioning achieved.** The headline product decision — that "point at any `.com`" is **niche, not universal**, and that "no markdown available" (FR-22) is a **first-class expected outcome, not a failure** — was made *before any code* on the strength of the discovery research, and the PRD was reframed ("the native reader for the markdown-native web") accordingly. No HTML fallback / no reader-mode shipped; the §5 "not a universal AI browser" non-goal stayed intact.

**Non-functional outcome.** Zero new package dependencies (regex `<head>` scan chosen over AngleSharp). `Rendering` and `Agent` untouched and pure — discovered markdown is just a string into the existing `FlowDocumentRenderer`. `NoEmbeddedBrowserTests` + `DependencyBoundaryTests` stayed green. The discovery service is the only new networking surface, over an injected `HttpClient` seam, fully fake-handler-testable.

---

## 2. What Went Well

1. **Research-first de-risking turned a vague ask into honest scope before any code.** The PM intake assumed "point at any website." A focused technical-research spike (empirical probes against an 11-site basket + the `llms.txt` adoption literature) produced two load-bearing corrections that shaped the whole epic: (a) **invert the cascade** — content-negotiation + alternate-link autodiscovery folded into the one request you already make, *then* `.md` sibling, *then* `/llms.txt`; (b) `/llms.txt` is a **site index, not page markdown**. It also quantified the hit rate (~8–10% of domains have `llms.txt`; page-level `.md` rarer) and reframed the product as niche. This is the single biggest win: the expensive mistake (building a "universal HTML lens") was avoided on paper.

2. **Branch-CI-first for native epics caught a real correctness bug plus 4 other rounds before `main`.** Because the host can't run `dotnet`, every acceptance bar was compile-by-construction or an xUnit test, and the only truth is `windows-latest`. The integration token can't `workflow_dispatch` (403), so CI was run on the branch first by temporarily adding the branch to `build-windows.yml`'s push-trigger list (fe6b892), then removing it before merge (4f7e6da) — keeping `main` continuously green. Five CI rounds ran on the branch before any merge.

3. **Adversarial code review caught two HIGH AC-commitments that were "documented as done" but not actually enforced — which the green tests missed.** The consolidated review (PASS WITH ITEMS: 0 CRIT, 2 HIGH, 4 MED, 3 LOW) flagged that the per-probe timeout and the `MaxRedirects=5` cap were *described* in the story but not wired (a dead `const MaxRedirects` existed with no enforcing logic). All 9 items were fixed (+18 tests), proving the value of a review that audits AC *enforcement*, not AC *prose*.

4. **Zero-new-dependency / purity discipline held under real pressure.** The tempting move (a managed HTML parser like AngleSharp for the `<head>` parse) was declined in favor of a bounded regex scan, keeping the no-webview substring guard trivially green and adding no binary weight. `Rendering`/`Agent` gained nothing across all four stories.

---

## 3. What Went Wrong / Risks Realized

1. **Native is un-runnable locally, so everything is compile-by-construction — the safety net is entirely CI.** There is no "run it and look at the window." Every behavior had to be expressed as a `[Fact]`/`[Theory]` (pure logic, injected fetch/discovery seams) or a `[StaFact]` construct-not-`Show` window test. This is workable but fragile: a behavior that *isn't* asserted is invisible until a real Windows user hits it.

2. **A real correctness bug shipped past the implement step and was only caught by CI.** `MarkdownCandidateValidator.BeginsWithHtmlMarker` stripped **all** whitespace before sniffing, collapsing `"<!doctype html>"` to `"<!doctypehtml>"` — so the doctype marker no longer matched and **soft-404 HTML was accepted as markdown** (a false positive — exactly the FR-21 "zero false positives" invariant). This was the core promise of the epic, and the happy-path tests were green; only the CI round that exercised the HTML-rejection cases (5 tests) caught it. The fix (c1a8b0b) preserves internal spaces while normalizing.

3. **"Documented as done ≠ done."** Beyond the validator bug, the review found doc/code drift in two more places: (a) the per-probe timeout and redirect cap were written up as implemented but were not enforced (HIGH #1/#2); (b) the 6.4 last-wins behavior was described as "routes through `NavigationController`" when it actually ran parallel to it — the XML doc comment was a lie until corrected (MED #3, fixed with a real `_discoveryGeneration` token). Story records and code disagreed; only an adversarial reading reconciled them.

4. **Two test-harness/compile rounds were pure self-inflicted friction** from not being able to compile locally: CS1010 (a literal tab/newline embedded in `InlineData`), xUnit2014 (`Assert.Throws` on an async method instead of `ThrowsAsync`), and a fake-handler keyed by `Uri.ToString()` which decodes `%20`, making a `%20` sibling test flaky against .NET URI normalization (e7d9301). None were product bugs, but each cost a full CI cycle.

---

## 4. Action Items

1. **For native (un-runnable-locally) epics, ALWAYS run branch CI *before* code review, not just before merge.** The HTML-sniff false-positive bug and the compile errors would have been caught a full review cycle earlier. Make "branch is green on `windows-latest`" a precondition for opening the review, not a post-review gate. *(Owner: dev workflow.)*

2. **When an AC says "bounded X", require a test that exercises the bound — not just the happy path.** The "≤5 redirects" and "per-probe timeout" caps were claimed-but-unenforced and the happy-path tests passed regardless. An AC asserting an invariant must have a test that *fails* if the invariant is removed (e.g. a 6th-redirect-rejected test, a timeout-fires test). *(Owner: story authoring + review.)*

3. **Have the implement step assert exact-value / invariant tests for the load-bearing promise, not just shape tests.** The "zero false positives" promise needed an explicit "`<!doctype html>` with surrounding/internal whitespace is rejected" assertion at implement time. Treat the epic's headline invariant as a first-class, exact-value test obligation. *(Owner: dev / TDD discipline.)*

4. **Consider a thin pre-merge CI smoke for `clients/windows` on every push.** The temporary-branch-trigger dance (add branch to push list, remove before merge) worked but is manual and error-prone. A lightweight always-on smoke (restore + build + a fast subset) for native changes would remove the manual toggling and shorten the feedback loop. *(Owner: infra / `.github/workflows`.)*

---

## 5. Per-Story Metrics

| Story | FR | ACs | Key new tests | Review items (resolved) | CI rounds it contributed to |
|-------|-----|-----|---------------|--------------------------|------------------------------|
| 6.1 Default home | FR-19 | 6/6 | `HomeNavigatorTests` (home URL + launch/Home drive controller), `HomeButtonWindowTests` (`[StaFact]`) | — (clean) | — |
| 6.2 Open any URL | FR-20 | 6/6 | `AddressBarValidationTests` (`IsAcceptableUrl` matrix + subset prop), `AddressBarWindowTests` (revised tag) | — (clean) | xUnit2014 / CS1010 InlineData fixes |
| 6.3 Discovery service | FR-21 | 8/8 | `MarkdownDiscoveryServiceTests`, `MarkdownCandidateValidatorTests`, `AlternateLinkParserTests`, gated `MarkdownDiscoveryLiveProbeTests` (8 SKIP) | 2 HIGH (per-probe timeout, redirect cap) + 4 MED + 3 LOW, +18 tests | HTML-sniff false-positive bug (the real one); %20 harness fix |
| 6.4 Render + states | FR-21 int / FR-22 | 7/7 | `DiscoveryOutcomeDispatcherTests`, `DiscoveryRenderFlowTests`, `DiscoveryStateWindowTests` (`[StaFact]`) | 1 MED (last-wins token + doc-comment correction) | — |
| **Epic total** | FR-19..22 | **27/27** | **485 tests** (incl. 8 skipped live probes) | **0 CRIT / 2 HIGH / 4 MED / 3 LOW — all 9 fixed** | **5 branch rounds, then green on merge (2a11e15)** |

**CI round summary (all on the branch, before `main`):** (1) CS1010 literal tab/newline in `InlineData`; (2) xUnit2014 `Assert.Throws` on async; (3) the real correctness bug — `BeginsWithHtmlMarker` collapsing `<!doctype html>` (5 tests); (4) consolidated review fixes (9 items, +18 tests); (5) test-harness `Uri.ToString()`/`%20` normalization. Then GREEN on branch (485 tests, e7d9301) and GREEN on the `main` merge (2a11e15).

---

## 6. Forward Note — Epic 7 (iOS, FR-23)

Epic 7 (iOS native reader, FR-23) remains a **stub in `backlog`**. It is a separate architecture fork: the WPF `FlowDocument` renderer does **not** port directly, so the iOS render-stack choice (SwiftUI-native vs .NET MAUI vs shared-core-with-native-render) is an **open architecture decision** that must be made at epic start, honoring the same no-Chromium/no-WebView constraint (NFR-1). Recommend a thin architecture spike (mirroring the Epic 6 research-first pattern that paid off here) before story elaboration.
