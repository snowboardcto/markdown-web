# Story 2.6 — AC Trace Report (Site header + pitch-card)

- **Story:** `_bmad-output/implementation-artifacts/2-6-site-header-and-pitch-card.md`
- **Date:** 2026-06-21
- **Verification command:** `cd web && npx playwright test`
- **Result:** **157 passed, 0 failed, 0 skipped** (confirmed on two consecutive runs)
- **Suite composition:** 121 prior baseline (with the AC7 reconciliations applied) + 36 new `2-6-chrome` specs
- **Build/typecheck (per Dev Agent Record, re-confirmed green):** `npm run build` → exit 0 (16 pages incl. `/get`, `/vision`); `npx astro check` → 0 errors

## Implementation under trace

- `web/src/components/SiteHeader.astro` — sticky banner header (wordmark + "the vision" + "Get the client")
- `web/src/components/PitchCard.astro` — end-of-page pitch-card (`<footer>`/contentinfo)
- `web/src/layouts/Page.astro` — shared layout wiring chrome before `<main>` / after `</main>`
- `web/src/pages/get.astro` — `/get` client-download stub (Decision A)
- `web/src/pages/vision.astro` — `/vision` vision/manifesto stub (Decision B)
- `web/src/styles/github.css` — `scroll-padding-top` added (review fix #1); asserted tokens untouched

## AC → Test matrix (7 ACs)

| AC | Acceptance criterion (short) | Covering tests | Status |
|----|------------------------------|----------------|--------|
| **AC1** | Sticky site-header on every surface: `.md` wordmark + "the vision" + "Get the client" CTA; `banner` landmark; exact accessible names; `position: sticky` normal-flow | `2-6-chrome.spec.ts` `AC1 — sticky site-header on every surface` (×4 surfaces: `/x`, `/sub/page`, `/`, 404 — `<header>`/`banner` count==1, wordmark `.md`+text, role+exact-name "the vision"/"Get the client"; plus `position: sticky` computed-style test ×4). Index surface independently re-asserted by `2-5-index.spec.ts:327` (`header` count==1 + header-scoped exact-name "Get the client"). | COVERED ✅ |
| **AC2** | End-of-page pitch-card on every surface: headline + body anchor + "Get the Markdown Web client" + "Why a markdown web?"; named contentinfo/region | `2-6-chrome.spec.ts` `AC2 — end-of-page pitch-card on every surface` (×4 surfaces: `contentinfo`/named-region landmark, verbatim headline, "Same file. Your shape." anchor, role+exact-name pitch CTA + why link) | COVERED ✅ |
| **AC3** | Chrome lives in shared `Page.astro`; inherited by content + index + 404 (one lever) | Inherited-presence proven by AC1/AC2 firing identically across all 4 surfaces (`/x`, `/sub/page`, `/`, 404) in `2-6-chrome.spec.ts`; stub pages `/get`+`/vision` also inherit (`AC7` describe). No per-page duplication — single shared layout. | COVERED ✅ |
| **AC4** | Microcopy verbatim per EXPERIENCE.md; header vs pitch CTA strings distinct; decorative glyphs out of accessible names | `2-6-chrome.spec.ts` `AC4 — microcopy verbatim` (headline exact two-sentence no "!"; em-dash U+2014 + "Same file. Your shape." cadence; `.md` chip; header "Get the client" vs pitch "Get the Markdown Web client" DISTINCT via header-scoped exact-name == 1 / pitch-name-in-header == 0; all anchor labels verbatim). Glyph-exclusion enforced transitively by the role+exact-name assertions throughout. | COVERED ✅ |
| **AC5** | "Get the client"/"Get the Markdown Web client" → `/get`; "the vision"/"Why a markdown web?" → `/vision`; documented stubs, resolve 200, no dangle | `2-6-chrome.spec.ts` `AC5 — stub links resolve to their documented target` (4 tests: each reads `href` from rendered DOM, asserts `=== '/get'` or `=== '/vision'` AND `request.get` → 200). Decisions A/B documented in Dev Agent Record. | COVERED ✅ |
| **AC6** | Themed + JS-free + crawlable + single `<h1>` (wordmark/pitch headline NOT `<h1>`) | `2-6-chrome.spec.ts` `AC6 — themed / JS-free / crawlable / single <h1>` (×4 surfaces: `h1` count==1, wordmark-not-h1, pitch-headline-not-h1; single `<html lang>` + stylesheet linked; no `astro-island`/`client:*`) + JS-disabled crawlable test (header+pitch+4 links present with JS off). Suite-wide single-`<h1>` assertions stay green (`ac1:19`, `2-3:239`, `2-5:200/276`, `ac3:37` `main,article`-scoped). | COVERED ✅ |
| **AC7** | Build exits 0; 121 prior specs pass with the two chrome-absence/`href=/` reconciliations; new chrome specs incl. stub-page single-`<h1>` | Reconciliations verified in tree: (1) `2-5-index.spec.ts:327` flipped "no chrome" → "chrome present"; (2) `2-3-linking-nav.spec.ts:242` left valid (wordmark is non-link span, `a[href="/"]`==1 holds); (3) collateral `<head` → `<head>` tightening at `ac3-crawlable-shell.spec.ts:49` + `2-5-index.spec.ts`; (4) `indexContentHrefs()` scoped to `main`. Stub-page single-`<h1>` in `2-6-chrome.spec.ts` `AC7 — /get + /vision stub pages` (un-skipped, real 200 routes). Full suite **157 passed**. | COVERED ✅ |

## No-regression sweep

- Full suite: **157 passed, 0 failed, 0 skipped** (two runs).
- Single-`<h1>` count assertions across the prior suite remained green (chrome adds no `<h1>`: wordmark = `<span>`, pitch headline = `<p class="pitch-headline">` non-heading after review fix #5).
- `2-3-linking-nav.spec.ts:242` (`a[href="/"]` count==1) green without modification — wordmark is a non-link span.

## Gaps / deferred (acceptable)

- **#10 (LOW, deferred):** `/get` + `/vision` stub pages render chrome whose CTAs point back at `/get`/`/vision` → self-referential dead-ends. Intended stub behavior; revisit at Epic 3 / real vision content.
- **#11 (LOW, deferred):** `PitchCard` `<code>.md</code>` lives outside `<article>`, so it hand-copies a code-chip rule rather than reusing github.css's `article` rule → maintenance drift risk. Pre-existing scoped-CSS isolation trade-off; low risk.
- **Preview-server flakiness (deferred):** Astro preview throws `URI malformed` / `ERR_INVALID_FILE_URL_PATH` on encoded-slash routes under parallel load (log noise; not a test failure here). Pre-existing harness/Astro-preview quirk; green on this run and on retry. Not a 2.6 regression.

All other review items (#1–#9) were applied directly to the branch (see story Review Findings / Review action items). 7/7 ACs covered + green; 0 critical findings.

## Conclusion

All 7 acceptance criteria are mapped to covering, non-tautological tests that read from rendered HTML; the full suite is green (157/157). The two load-bearing AC7 reconciliations (2-5 chrome-present flip, 2-3 `a[href="/"]` validity) plus the two collateral reconciliations (`<head>` tightening, `main`-scoped index href count) are in place. Story 2.6 meets its Definition of Done.
