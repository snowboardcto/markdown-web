# Story 2.5 — AC Trace Report (Browsable Vault Index)

**Story:** 2-5-browsable-vault-index
**Epic:** 2 (Publish & Read on the Web)
**Date:** 2026-06-21
**Status at trace:** review → done
**Reviewer/Author:** naethyn

## Summary

- **Total Playwright tests passing:** 121 / 121 (94 prior baseline + 27 new Story-2.5 specs).
- **`astro check`:** 0 errors, 0 warnings, 1 pre-existing hint (`ts(6387)` deprecation in `2-3-linking-nav.spec.ts`, unrelated to this story).
- **Build:** `npm run build` exits 0; 13 pages (12 content routes + `/`); `src/pages/index.astro` emits the real index (no "Coming soon").
- **AC coverage:** 7 of 7 ACs covered by green tests. No deferred ACs (note: ACs #5/#6 are fully covered here — the "deferred acceptable" note in the task did not apply; both are implemented and tested).
- Verification command: `cd web && npx playwright test`.

## Implementation under test

- `web/src/pages/index.astro` — the generated browsable index at `/`, rendered from `getCollection('pages')` through the shared `Page` layout (replaces the Story-1.1 "Coming soon" placeholder).
- `web/src/lib/index-entries.mjs` — pure `buildIndexItems()` (empty-id filter, never-empty label, ICU-independent code-unit sort).
- `web/src/lib/title.mjs` — shared `slugToTitle()` helper, extracted (behaviour-preserving) from `[...slug].astro` and imported by BOTH the catch-all route and the index.
- `web/src/pages/[...slug].astro` — imports `slugToTitle` from `../lib/title.mjs` (no behavioural change; anti-drift).
- `content/h1-only.md` — H1-only fixture exercising the Decision-D label/title divergence.

## AC → Test Matrix (7 rows)

| AC | Acceptance Criterion (short) | Covering tests | Spec file(s) | Status |
|----|------------------------------|----------------|--------------|--------|
| **AC1** | Index lists/links EVERY vault page one-to-one (count == `getCollection('pages')` entries, no omissions/extras, no `/sub/index` phantom), generated from the collection; empty-vault + single-page degenerate cases render valid pages without crashing. | "contains a link to every known content route"; "completeness invariant: content-link count EQUALS the collection entry count"; "no phantom /sub/index and no extra routes"; "every listed href resolves to a live 200 page"; degenerate: "empty vault yields zero items → No pages yet. branch"; "all-empty-id vault yields zero items"; "single-page vault yields exactly one item"; "single-page with frontmatter title; label never blank" | `2-5-index.spec.ts` (AC1 describe); `2-5-index-degenerate.spec.ts` (all 4) | COVERED — green |
| **AC2** | Every link href is a resolved root-absolute route (`'/' + entry.id`, never `*.md`/dead); label is a human-readable title via the SAME precedence (frontmatter `title` → H1 → slug Title Case) from a SHARED extracted helper; Decision-D H1 divergence resolved + asserted. | "no href is a literal *.md path"; "each content link href is root-absolute"; "label is a human-readable title (frontmatter title, else H1, else slug)"; "/no-h1 label is slug-derived No H1"; "Decision-D divergence genuinely exercised: /h1-only label DIFFERS from destination title" | `2-5-index.spec.ts` (AC2 describe) | COVERED — green |
| **AC3** | Reachability — index reachable at the stable `/` URL and links every page; 404 "Go back home" reaches the real listing; no orphan pages. | "a page is reachable by clicking through from /"; "404 'Go back home' reaches the real index at /" | `2-5-index.spec.ts` (AC3 describe) | COVERED — green |
| **AC4** | Nested pages included + presented coherently; ONE deterministic, CI-stable ordering (sort by `entry.id`, ICU-independent code-unit comparison); `sub/index.md` listed once at `/sub` (no `/sub/index` dup); no `/` self-link. | "nested + index-collapsed routes are all present"; "no /sub/index phantom and no / self-link in the listing"; "content links appear in the pinned, CI-stable sort-by-route order" | `2-5-index.spec.ts` (AC4 describe) | COVERED — green |
| **AC5** | Story-1.1 "Coming soon" placeholder retired; `/` is the real index rendered through the shared `Page` layout; route ownership intact (`/` = `index.astro`, content = `[...slug].astro`); empty-`id` guard prevents `/` self-link. | "the Story-1.1 'Coming soon' placeholder is gone"; "/ is the real index (renders the listing)"; "/ does not appear as a content link in its own listing"; (degenerate) "all-empty-id vault yields zero items (no `/` self-link)" | `2-5-index.spec.ts` (AC5 describe); `2-5-index-degenerate.spec.ts` | COVERED — green |
| **AC6** | Themed (github.css), server-rendered, JS-free, crawlable; single semantic shell (`<html lang>`→`<head>`→`<body>`→`<main>`/`<article>`, exactly one `<h1>`, valid doctype); no `client:*` island; listing-only (no 2.6 header/pitch/CTA chrome). | "exactly one <h1> and a single semantic shell"; "serves a doctype with balanced structural tags"; "themed via the shared Page layout (github.css, light surface)"; "no client island / runtime JS hydration directive"; "the listing works with JavaScript disabled"; "listing-only: no 2.6 site-header / pitch / get-client chrome yet" | `2-5-index.spec.ts` (AC6 describe) | COVERED — green |
| **AC7** | No regression — full Playwright suite stays green; build emits one page per `content/**/*.md` plus `/`; theme + 2.3 linking/404 + 2.4 media all still work; `astro check` 0 errors. | All 94 prior specs (`ac1-gfm-core`, `ac2-js-disabled`, `ac3-crawlable-shell`, `ac5-slugging-edge`, `ac6-gfm-extensions`, `2-2-theme`, `2-3-linking-nav`, `2-4-media`) pass unchanged alongside the 27 new index specs (121/121); `astro check` → 0 errors. | entire `web/tests/*.spec.ts` suite | COVERED — green |

## No-regression baseline (94 prior specs, all green)

| Spec file | Tests | Result |
|-----------|-------|--------|
| `ac1-gfm-core.spec.ts` | 5 | pass |
| `ac2-js-disabled.spec.ts` | 2 | pass |
| `ac3-crawlable-shell.spec.ts` | 4 | pass |
| `ac5-slugging-edge.spec.ts` | 5 | pass |
| `ac6-gfm-extensions.spec.ts` | 4 | pass |
| `2-2-theme.spec.ts` | 20 | pass |
| `2-3-linking-nav.spec.ts` | 26 | pass |
| `2-4-media.spec.ts` | 28 | pass |
| **Prior subtotal** | **94** | **pass** |
| `2-5-index.spec.ts` (new) | 23 | pass |
| `2-5-index-degenerate.spec.ts` (new) | 4 | pass |
| **Total** | **121** | **pass** |

## Gaps

- **None blocking.** All 7 ACs are covered by green tests.
- ACs #5 and #6 — noted in the task as "deferred acceptable" — are in fact fully implemented and tested here (placeholder retired + route ownership for AC5; themed/JS-free/crawlable/listing-only for AC6). No deferral was needed.
- Review findings (1 decision-needed + 3 patches) were all resolved before this trace: AC4 sort switched to an ICU-independent code-unit comparator; empty/single-page degenerate cases now unit-tested via `buildIndexItems`; Decision-D divergence genuinely exercised via `content/h1-only.md`; `slugToTitle` hardened against nullish/empty input. Two findings deferred as pre-existing/out-of-scope (no `content.config.ts` schema; `\b\w` title-case regex behaviour) — both unchanged by this story.

## Verdict

Story 2.5 meets all 7 acceptance criteria with 121/121 Playwright tests green and `astro check` at 0 errors. Ready to mark **done**.
