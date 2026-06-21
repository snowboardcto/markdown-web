# Story 2.5 — TDD E2E Test Report (RED phase)

**Story:** 2.5 Browsable vault index
**Spec added:** `web/tests/2-5-index.spec.ts` (NEW — 22 tests)
**Harness:** `web/playwright.config.ts` (build + preview `dist/`, `reuseExistingServer:false`) — unchanged.
**Existing specs:** 94 tests across 8 files — unmodified.
**Total after add:** 116 tests (94 existing + 22 new).

## Result: RED confirmed

`cd web && npx playwright test` → **103 passed, 13 failed**.

- **All 13 failures are in `2-5-index.spec.ts`** and fail for **feature-absence**:
  `web/src/pages/index.astro` is still the Story-1.1 "Coming soon" placeholder
  (no generated listing, not rendered through the `Page` layout/theme), so the
  index has zero content links, the placeholder copy is present, and `/` is
  unthemed.
- **The 94 existing specs all pass** (no failure outside the new spec file).
- **9 of the 22 new 2-5 tests pass today** — they are *vacuously-true guards*
  (the placeholder has no content links to violate "no `*.md` href", "no
  `/sub/index` phantom", "no `/` self-link", "no astro-island", and it already
  has one `<h1>`/`<main>`/doctype shell). They will continue to pass and become
  meaningful once the listing exists. They are NOT false-green for the
  feature: every assertion that proves the listing *exists* (link presence,
  count, order, labels, theme, placeholder-gone) is in the 13 that fail.

The tests fail for the RIGHT reason (feature absent), not a harness/wrong-reason
error — no harness change was needed.

## The 13 failing tests (RED — must go green when Step 5 ships the index)

| # | Test | Why it fails today |
|---|------|--------------------|
| AC1 | contains a link to every known content route | no listing links exist |
| AC1 | completeness invariant: content-link count == 11 collection entries | 0 links, not 11 |
| AC1 | every listed href resolves to a live 200 page | no hrefs to follow |
| AC2 | label is a human-readable title (frontmatter → H1 → slug) | no labels exist |
| AC2 | the H1-only page /no-h1 label is slug-derived "No H1" | `/no-h1` link absent |
| AC3 | a page is reachable by clicking through from / | `/gear-guide` link absent |
| AC3 | 404 "Go back home" reaches the real index at / | `/` is placeholder, no `/x` link |
| AC4 | nested + index-collapsed routes are all present | `/sub`, `/sub/page`, … absent |
| AC4 | content links appear in pinned CI-stable sort-by-route order | no links to order |
| AC5 | the Story-1.1 "Coming soon" placeholder is gone | placeholder still present |
| AC5 | / is the real index (renders the listing) | `/x` link / `<li>` absent |
| AC6 | themed via the shared Page layout (github.css, light surface) | placeholder unthemed (bg `rgba(0,0,0,0)`, not `#ffffff`) |
| AC6 | the listing works with JavaScript disabled | `/x` link absent JS-off |

## The 9 currently-passing new 2-5 tests (vacuous guards, stay green)

AC1 no-phantom-/sub/index; AC2 no-`*.md`-href; AC2 hrefs-root-absolute;
AC4 no-`/sub/index`-and-no-`/`-self-link; AC5 `/`-not-self-listed;
AC6 one-`<h1>`-and-semantic-shell; AC6 doctype+balanced-tags;
AC6 no-astro-island/`client:*`; AC6 no-2.6-chrome.

## AC → test mapping

- **AC1 (lists EVERY page one-to-one, links resolve 200):** describe "Story 2.5
  AC1" — 4 tests. Pins the 11-route set, asserts content-link count EQUALS the
  collection count (no omissions/extras/self-link), GET each href → 200.
- **AC2 (resolved routes + readable labels, actual title precedence):** describe
  "Story 2.5 AC2" — 4 tests. No href is `*.md`; hrefs root-absolute; label ∈
  {destination `<title>`, slug-derived Title Case} — robust to **Decision D**
  either way (full-precedence `render()` OR cheap `data.title || slugToTitle`),
  never asserting a label the code cannot produce; pins `/no-h1` → "No H1"
  (both Decision-D paths collapse to slugToTitle since the page has no
  frontmatter title and no H1).
- **AC3 (reachability):** describe "Story 2.5 AC3" — 2 tests. Click-through from
  `/` to a page; 404 "Go back home" → `/` reaches the real index (placeholder
  gone, listing present).
- **AC4 (deterministic order + nested + no-dup/self-link):** describe "Story 2.5
  AC4" — 3 tests. Nested + index-collapsed `/sub` present; no `/sub/index`
  phantom, no `/` self-link; DOM order equals the pinned **Decision-B** sort
  (flat by route id, locale-pinned `'en'` == code-unit order — verified the two
  keys agree for this id set, so CI locale cannot reorder).
- **AC5 (placeholder retired / route ownership):** describe "Story 2.5 AC5" — 3
  tests. "Coming soon" count 0; `/` renders the listing; `/` not in its own
  listing.
- **AC6 (themed + JS-free + crawlable, listing-only):** describe "Story 2.5
  AC6" — 7 tests. One `<h1>` + semantic shell; doctype + balanced tags; themed
  via `Page` (light `#ffffff` surface, `#1f2328` fg, stylesheet linked);
  no `astro-island`/`client:*`; works JS-disabled; no 2.6 header/pitch/CTA
  chrome.
- **AC7 (no regression):** the FULL-suite run — 94 existing specs stay green
  alongside the new spec.

## Anti-tautology / honesty notes

- All hrefs and labels are read from the **rendered HTML**; link liveness uses a
  real **HTTP 200** (`page.request.get`). The in-test `slugTitle` helper is used
  ONLY to widen the label-acceptance set for Decision D — never to derive hrefs.
- The 11-route expected set and the sort order were derived from the actual
  `content/**/*.md` vault and verified (`localeCompare('en')` and code-unit sort
  produce the identical sequence).

## Known content vault (11 routes, source of the count test)

`/empty /gear-guide /my-notes /my-notes-dir/page /no-h1 /readme /sub /sub/page
/sub/page2 /sub/sibling /x` — `content/sub/index.md` index-collapses to `/sub`;
no root `content/index.md`, so no `/` self-link.
