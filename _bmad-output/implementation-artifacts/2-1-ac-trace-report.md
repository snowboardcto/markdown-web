# Story 2-1 — AC Trace Report

Story: **2-1 Render a `.md` file to an HTML page**
Date: 2026-06-21
Reviewer: naethyn (BMAD STEP 10 — AC trace)
Branch: `claude/npx-bmad-method-install-ne8bw9`

## Verification

- Command: `cd web && npx playwright test`
- Result: **20 passed** (0 failed, 0 flaky), ~6.0s, 1 chromium project.
- Build gate (per story Debug Log): `cd web && npm run build` → exit 0 (7 pages); `npx astro check` → 0/0/0.

## Implementation under test

- `web/astro.config.mjs` — GFM via `remark-gfm`.
- `web/src/content.config.ts` — `glob()` collection sourced from the repo-root `../../content` vault (single source of truth).
- `web/src/pages/[...slug].astro` — dynamic route, `getStaticPaths()` over `getCollection()`, total title derivation, duplicate-slug build guard.
- `web/src/layouts/Page.astro` — minimal HTML5 document shell (`<!doctype>`, `<html lang>`, `<head>` charset/title/viewport, semantic `<main>`/`<article>`).

## AC → Test Matrix

| AC | Description | Covering test (file :: test name) | Result |
|----|-------------|------------------------------------|--------|
| **AC1** | file-as-page: `content/x.md` → `/x` with correct GFM HTML (`<h1>`..`<h6>`, `<strong>`/`<em>`, `<ul>`/`<ol>`/`<li>`, `<code>`/`<pre><code>`, real `<table>`) | `ac1-gfm-core.spec.ts` :: renders heading levels h1..h6; renders bold (`<strong>`) and italic (`<em>`); renders unordered (`<ul>`) and ordered (`<ol>`) lists with `<li>`; renders inline `<code>` and fenced `<pre><code>`; renders a real GFM `<table>` with thead/tbody/tr/th/td (5 tests) | **PASS** |
| **AC2** | readable JS-disabled — body in initial HTML payload, no `client:*` hydration | `ac2-js-disabled.spec.ts` :: article body content is present with JS disabled (`javaScriptEnabled:false` context); body text exists in the raw HTML payload (no client render dependency) (2 tests) | **PASS** |
| **AC3** | well-formed, crawlable HTML — single `<html lang>` → `<head>` (charset/title/viewport) → `<body>`, semantic container, no unclosed tags, no JS dependency | `ac3-crawlable-shell.spec.ts` :: has a single `<html lang>` root; has `<head>` with charset, title and viewport; content lives inside a semantic `<main>`/`<article>` container; serves a doctype and no obviously unclosed structural tags (4 tests) | **PASS** |
| **AC4** | content-driven routing from the shared repo-root vault — Astro sources `content/*.md` (not a copy in `web/`); new file → new route, no code change | *No dedicated spec.* Covered transitively: `content.config.ts` `glob({ base: '../../content' })` is the only source of the routes asserted in AC1/AC2/AC3/AC5/AC6 (`/x`, `/my-notes`, `/sub/page`, `/empty`, `/no-h1`) — each `200` proves a `content/*.md` became a route with no per-file code. Auditor verified `find web/src -name '*.md'` empty (no copies). | **PASS (indirect)** |
| **AC5** | deterministic, safe slugging + empty-file resilience — `My Notes.md`→`/my-notes`, `sub/page.md`→`/sub/page`, near-empty `empty.md` builds a valid shell, total `<title>` | `ac5-slugging-edge.spec.ts` :: `My Notes.md` served at `/my-notes`; `sub/page.md` served at `/sub/page`; `My Notes.md` `<title>` is slug-derived "My Notes"; no-H1/no-front-matter `no-h1.md` falls back to slug-derived `<title>`; near-empty `empty.md` builds a valid document shell at `/empty` (5 tests) | **PASS** |
| **AC6** | full GFM extensions + escaping — strikethrough→`<del>`/`<s>`, task list→disabled `<input type=checkbox>` `<li>`, bare URL→`<a href>`, `<`/`&` HTML-escaped | `ac6-gfm-extensions.spec.ts` :: strikethrough renders as `<del>`/`<s>`; task list renders `<li>` with disabled `<input type=checkbox>`; bare URL becomes an `<a href>` autolink; special characters `<` and `&` are HTML-escaped in the raw output (accepts numeric or named refs) (4 tests) | **PASS** |

## Totals

- **20 / 20 tests passing.**
- Test files: `ac1-gfm-core.spec.ts` (5), `ac2-js-disabled.spec.ts` (2), `ac3-crawlable-shell.spec.ts` (4), `ac5-slugging-edge.spec.ts` (5, incl. the 2 title tests added in code-review fixes), `ac6-gfm-extensions.spec.ts` (4).

## Coverage gaps / notes

- **AC4 has no isolated assertion** — it is an architectural single-source-of-truth invariant, proven transitively by every other AC's routes resolving from the `glob()` collection (no `.md` copies exist in `web/src`). A dedicated guard test ("fail if any `content/*.md` is duplicated into `web/`") was raised as a deferred, non-blocking hardening item in the story's Review Findings.
- **AC6 escaping** accepts numeric char-refs (`&#x3C;`/`&#x26;`) in addition to named (`&lt;`/`&amp;`) — Astro's bundled `rehype-stringify` emits numeric refs; both are spec-valid escaping. Documented as an intentional, AC-preserving deviation in the Dev Agent Record.
- No AC is left without coverage. All 6 ACs are confirmed green; code review (3 adversarial layers) is resolved with all 5 patch action items applied.

## Verdict

All 6 ACs traced and green (5 direct, AC4 indirect). Story 2-1 meets its Definition of Done → **DONE**.
