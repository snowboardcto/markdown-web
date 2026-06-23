---
type: epic-retrospective
epic: 5
epic_name: Sharing & Feed
date: 2026-06-23
author: naethyn (automated — bmad-retrospective, non-interactive)
status: done
stories: [5-1-living-link, 5-2-follow-feed]
frs_covered: [FR-15, FR-16]
---

# Epic 5 Retrospective — Sharing & Feed

**Date:** 2026-06-23
**Author:** naethyn (automated retrospective run)
**Epic:** 5 (post-MVP) — Sharing & Feed
**Stories:** 5.1 Living Link (FR-15), 5.2 Follow / Feed (FR-16)
**Final status:** CLOSED / done — both stories done & fully verified; FR-15 + FR-16 closed (the last two post-MVP requirements)

---

## 1. Outcome vs Goal

Epic 5 was the deliberately-deferred post-MVP epic (per PRD MVP scope = FR-1–FR-14). The user chose to run it "as-is" and then "full auto." Both stories ran the full 11-step `enhanced-automated-sprint` pipeline sequentially (create → elicit → validate → TDD E2E (RED) → implement → review → fix → trace → status).

**Did it deliver FR-15 + FR-16? Yes — both, fully verified.**

| FR | Story | Outcome | Evidence |
|----|-------|---------|----------|
| FR-15 Living Link | 5.1 | A shared `.md` URL renders as HTML in a browser and per-personality in the native client; a canonical absolute URL + one-click "Copy link" affordance surface it on both surfaces. | 8/8 ACs covered; AC trace report = no gaps. |
| FR-16 Follow / Feed | 5.2 | "Follow" delivered as the commodity open-web mechanism: a build-time static RSS 2.0 `/feed.xml` generated from the vault + autodiscovery `<link>` + visible "Subscribe" affordance. New pages surface on rebuild (FR-17 publish-on-push makes them live). | 7/7 ACs covered; AC trace report = no gaps. |

**Acceptance summary.** 5.1 = 8/8 sub-ACs; 5.2 = 7/7 sub-ACs. The broad single epic-level AC of each story was decomposed into testable sub-ACs; AC1/AC2 of 5.1 were locked as *regression contracts* over already-shipped Epic 2/3/4 behavior rather than rebuilt.

**Test totals (final).**
- Web Playwright: **268/268 green** (157 prior + 35 new in 5.1 + 76 new in 5.2 [30 builder + 46 E2E]); `astro check` 0 errors; `npm run build` exit 0 with `dist/feed.xml` (13 items, valid RSS 2.0).
- Native WPF (windows-latest, run #54 / 27994933934 / b421be0): **324 tests green** (incl. 23 new xUnit tests for 5.1's `ShareLinkBuilder` / `IClipboard` / toolbar button).

**Deploy status.** Web deployed to Azure SWA and the Windows installer released on the Epic-5 merge (`9347f5b`). Native turned green only after a one-round post-merge CI fix (`b421be0`), and the epic was formally closed at `eb086e3`.

---

## 2. What Went Well

1. **Lean-scope discipline + aggressive reuse.** 5.1 correctly recognized that *almost all* of the epic-level AC was already shipped: browser-opens-URL→HTML (Epic 2 / Story 2.7 Option-2 static-HTML page URL), native-opens-URL→per-personality render (Epic 3 fetch path + Epic 4 personalization), and native fetch of a pasted `.md` URL (Epic 3 `PageEndpointResolver`/`SlugDeriver`). Net-new surface was only (a) canonical absolute URLs and (b) the Copy-link affordances. 5.2 likewise reused `getCollection('pages')` + `buildIndexItems`/`slugToTitle` (Story 2.5) and `Astro.site` (Story 5.1) so the feed's page set and item URLs cannot drift from the index/canonical. This kept both stories genuinely small and honored NFR-7 (don't reinvent commodity plumbing).

2. **"Follow" implemented as commodity plumbing, not a backend.** 5.2 shipped a pure build-time RSS 2.0 `/feed.xml` via an Astro static endpoint + a hand-emitted XML builder — **no backend, no accounts, no subscription store, no notification service, no database, no new Azure Function, and no new dependency** (`@astrojs/rss` deliberately avoided). The "follow" act is the reader subscribing in their own feed reader. This is exactly the right altitude for a static-first, zero/low-JS stack.

3. **TDD red→green worked as designed.** Both stories generated failing tests first and confirmed RED: 5.1 = 26 web fail / 7 pass (regression locks) pre-impl; 5.2 = 67 fail (feed.mjs missing / `/feed.xml` 404) with only the AC6 regression-lock tests passing vacuously. Implementation then drove them green. The pre-written specs anchored the implementation surface precisely.

4. **Code review caught real bugs the TDD tests missed.** This is the headline win. 5.1's adversarial review (PASS WITH ITEMS) found a genuine **"Copied"-feedback label-corruption bug** (the feedback toggled the whole button text, risking "Copied 🔗 Copied") that the tests did not assert against — plus a `ShareLinkBuilder` `%2F`/percent-encoded **decode/docstring mismatch** breaking round-trip parity. 5.2's review caught a **production-publishing test fixture** (`content/5-2-fixture.md` would have shipped test scaffolding to the live site), a **latent non-ASCII canonical-URL drift** (raw string concat vs `new URL('/' + id, site)`), and **tautological / too-loose AC2 tests** (guid-stability and new-page-surfaces that didn't actually bind the invariant). All were fixed in-sprint (5.1: 2 HIGH + 4 MED + 2 LOW fixed, 1 deferred; 5.2: 4 MED + 5 LOW fixed).

5. **Fast CI recovery.** When the post-merge `build-windows.yml` failed (see §3), root cause was found and fixed in a single round (`b421be0`) — scope the nav-StackPanel regression guards to column 0 — and windows-latest run #54 went green at 324 tests.

6. **Anti-drift via single source of truth held.** The web copy-link button reads the canonical href from the DOM (one source); the native `ShareLinkBuilder` reuses `PageEndpointResolver.IsAppHost` + `SlugDeriver`; the feed item URL/guid is `new URL('/' + entry.id, Astro.site)` byte-equal to the page's `<link rel="canonical">`. No second slug/URL implementation was introduced on either surface.

---

## 3. What Went Wrong / Risks Realized

### THE BIG ONE — native (WPF) work could not be executed in the dev/review/fix environment

The dev/review/fix loop runs on Linux with **no `dotnet` SDK**. All of 5.1's native work (`ShareLinkBuilder`, `IClipboard`/`SystemClipboard`, the toolbar `ShareLinkButton`, 23 xUnit tests) was therefore verified **"compile-by-construction" only** — the story, AC trace, and TDD reports all explicitly marked the native tests **"CI-only."** Native correctness was *assumed* through dev, review, fix, trace, and the merge to main.

**The risk realized:** after merge, `build-windows.yml` **FAILED**. Root cause: three *pre-existing* nav-StackPanel regression guards counted the toolbar buttons and asserted 3; once 5.1 added the Copy-link button, the real count became 4, so a true test-count regression slipped past every pre-merge gate because no machine in the loop could run `dotnet test`. It was caught only by CI *after* the code was on main. Fixed by scoping `ShellTestHelpers.NavStackButtons` to column 0 (the nav column), restoring the intended count; run #54 then green.

This is the single most important escape of the epic: **"compile-by-construction" is not verification for native code, and a real regression reached `main` because of it.**

### Other risks realized

- **A production-publishing test fixture nearly shipped to the live site.** 5.2 committed `content/5-2-fixture.md` to satisfy AC2's "add a `.md` → it surfaces in the feed" test. Because `content/` is the published vault, that file becomes a *live page on themarkdownweb.com*. Review caught it and required rewriting it from test-scaffolding into a legitimate "Markdown Formatting Sampler" page — but the instinct to commit a throwaway test page into the published source of truth is a recurring hazard.

- **TDD tests can be tautological or too loose; only adversarial review caught it.** 5.1's tests did not catch the label-corruption bug. 5.2's original AC2 guid-stability test was tautological (it didn't prove the guid derives from id rather than pubDate), and the new-page-surfaces test was weak. These passed green while not actually binding the invariant — the value of a *fresh-context adversarial review* over green tests was decisive.

- **The all-undated-vault second-order trap was real (anticipated, then guarded).** Because the vault has no frontmatter date schema, every feed item's `<pubDate>` collapses to the single build date. Had item identity been keyed off `<pubDate>` or a build-derived guid, every push would re-surface the *entire* vault as "new" in readers — the inversion of FR-16. This was anticipated in elicitation and guarded by making `<guid isPermaLink="true">` = the build-stable canonical URL, but it is a sharp edge that only careful second-order thinking surfaced.

---

## 4. Action Items / Process Improvements

| # | Action | Rationale (what it prevents) | Owner |
|---|--------|------------------------------|-------|
| A1 | **Gate any story touching `clients/windows/**` on an actual green `windows-latest` CI run BEFORE marking the story done** — not "compiles by construction." Run native tests pre-merge via CI on the feature branch. | Directly prevents the §3 BIG ONE: a real WPF test-count regression reached `main` because no in-loop machine could run `dotnet test`. | pipeline / coordinator |
| A2 | **Never commit throwaway test pages into `content/` (the published vault).** Prefer self-contained/in-memory fixtures (the pure-builder unit-test path) or an explicitly test-only fixture directory that is excluded from the build. If a committed fixture is unavoidable, it must be a legitimate, publishable page from the outset. | Prevents test scaffolding shipping to the live site (the 5.2 `5-2-fixture.md` near-miss). | dev / story author |
| A3 | **Make the E2E/TDD generation step assert exact values and identity/date invariants** — exact label text (`=== "Copied"`, not `:contains`), byte-equality, and "derives-from-X-not-Y" binding tests (e.g. same id + different date ⇒ identical guid). | Prevents the tautological/too-loose tests that let the 5.1 label bug and the 5.2 guid-stability gap through. | qa-generate-e2e step |
| A4 | **Keep the fresh-context adversarial code review as a mandatory gate after green tests** — it caught every real bug this epic. Continue running the three-layer (Blind Hunter / Edge Case Hunter / Acceptance Auditor) pass. | The review, not the tests, found the load-bearing defects. | review step |
| A5 | **When CI cannot run a surface locally, add an explicit "CI-deferred verification" flag on the story and treat `done` as conditional until that CI is green** (the sprint-status narrative already did `done(web)/CI-pending(native)` — formalize it as a real gate, not a note). | Makes the assumed-correctness window visible and blocking instead of implicit. | sprint-status discipline |
| A6 | **Add a content-frontmatter `date` schema (optional `z.date()`) when feed ordering matters**, so the all-undated degeneration in §3 stops being the permanent default. (Currently deferred; tracked.) | Lets newest-first ordering actually fire; reduces reliance on the id tie-break being the sole determinant. | future / web |

---

## 5. Metrics

| Story | ACs | Web tests added | Native tests added | Review items (fixed / deferred) | Files changed |
|-------|-----|-----------------|--------------------|----------------------------------|----------------|
| 5.1 Living Link | 8/8 | 35 (`5-1-living-link.spec.ts`) | 23 (`ShareLinkBuilderTests.cs`, CI-only) | 8 fixed (2 HIGH + 4 MED + 2 LOW) / 1 deferred (LOW) | 9 — `astro.config.mjs`, `Page.astro`, `SiteHeader.astro`, `5-1-living-link.spec.ts`, `ShareLinkBuilder.cs` (new), `IClipboard.cs` (new), `MainWindow.xaml`, `MainWindow.xaml.cs`, `ShareLinkBuilderTests.cs` (new) |
| 5.2 Follow / Feed | 7/7 | 76 (30 `5-2-feed-builder.spec.ts` + 46 `5-2-follow-feed.spec.ts`) | 0 (web-only by design) | 9 fixed (4 MED + 5 LOW) / 0 deferred | 7 — `feed.mjs` (new), `feed.xml.ts` (new), `Page.astro`, `SiteHeader.astro`, `5-2-follow-feed.spec.ts` (new), `5-2-feed-builder.spec.ts` (new), `2-5-index.spec.ts` (route-count reconcile) + `content/5-2-fixture.md` (new) |
| **Epic 5 total** | **15/15** | **111** | **23 (CI-only)** | **17 fixed / 1 deferred** | **~17 distinct files** |

Final suite totals: **web 268/268 green; native 324 green (windows-latest run #54)**. Epic-5 commits on main: `0234135` → `f5d9b9c` → `9b3ea7b` (5.1), `ae12544` → `b938e8d` → `137c792` (5.2), merge `9347f5b`, post-merge native fix `b421be0`, close `eb086e3`.

---

## 6. Open Decisions / Optional Items Still Outstanding

1. **`content/5-2-fixture.md` — keep or strip.** It was rewritten from a test scaffold into a legitimate "Markdown Formatting Sampler" page so it is *safe* to ship, but it exists in the published vault primarily to satisfy 5.2's AC2 new-page-surfaces E2E test. **Decision still open:** keep it as a permanent demo page, or strip it and re-point the AC2 E2E assertion at the (already strong) builder-level delta test. Stripping it would drop the route/feed count from 13 back to 12 and require reverting the `EXPECTED_ROUTES_SORTED` edits in `5-2-follow-feed.spec.ts` and `2-5-index.spec.ts`.

2. **Frontmatter `date` schema (deferred).** No `schema` on the `pages` collection means feed `<pubDate>` always falls back to the build date and ordering degenerates to the id tie-break. Adding an optional `date`/`pubDate` schema would enable real newest-first ordering (see Action A6). Pre-existing deferral carried from Story 2.5 review.

3. **5.1 deferred LOW — `ShareLinkBuilder` relative-Uri branch.** Returns the relative string unchanged; the handler would copy it if ever reached. Unreachable in current wiring (`NavigationController.Current` is always an absolute, successfully-navigated `Uri`). Revisit only if `ToShareUrl` gains a caller that can pass a relative `Uri`.

4. **Inherited Epic-2 deferrals still relevant to the share surface:** Story 2.7 Option-2 (true same-`Accept`-URL negotiation deferred; markdown lives at `/api/negotiate/<slug>`), `rehype-raw` with no sanitizer (trusted single-author vault), and the ~8 intermittent `URI malformed` preview-server transients under parallel load (retry, not a regression). None block Epic 5; listed for completeness.

---

*Epic 5 closed. FR-15 (Living Link) and FR-16 (Follow / Feed) — the final two post-MVP requirements — are delivered, tested (web 268/268, native 324), reviewed, deployed, and verified green on CI.*
