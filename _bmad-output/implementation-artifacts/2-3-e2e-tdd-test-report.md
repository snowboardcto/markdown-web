# Story 2.3 — E2E TDD Test Report (RED phase)

Story: `2-3-inter-file-linking-and-navigation.md` (inter-file linking + navigation)
Phase: **RED** (failing-first tests authored before the feature exists)
Spec added: `web/tests/2-3-linking-nav.spec.ts` (NEW — the existing 6 spec files / 39 tests were NOT modified)
Harness: `web/playwright.config.ts` (build + `astro preview`, `reuseExistingServer:false`)

## What was added

### New spec — `web/tests/2-3-linking-nav.spec.ts` (22 tests)

All assertions read from the **built/preview output**: hrefs via `locator.getAttribute('href')`
(proves the build-time rewrite, JS-free), navigation via `page.click()` + `goBack()`/`goForward()`
+ `page.url()`, and the 404 contract via the **real navigation response status**
(`page.goto(...).status() === 404`). No re-implementation of the slug function is asserted against
(avoids the 2.2 tautological-test trap).

### Link fixtures added to repo-root `content/` (fixtures only — no plugin, no 404 page)

- `content/gear-guide.md` (NEW) — the canonical epic target `[guide](gear-guide.md)` -> `/gear-guide`;
  links back to `x.md` and `sub/page.md` for the AC5 round-trip.
- `content/sub/page2.md` (NEW) — sibling target for `page2.md` -> `/sub/page2`.
- `content/sub/sibling.md` (NEW) — leading-`./` target for `./sibling.md` -> `/sub/sibling`.
- `content/x.md` (UPDATED) — added a `## Links (AC: 2.3)` section authoring every link class
  (internal `.md`, fragment, space/encoded, mixed-case, index-collapse, malformed-`%`, escape-vault,
  broken, anchor, external, mailto, root-absolute, non-`.md` asset). Uses `##`/`###` only (no 2nd `<h1>`),
  adds no 2nd `<table>`, keeps the existing autolink — so the 39 existing specs stay green.
  Note: the space-in-filename case is authored `[notes space](<My Notes.md>)` — angle brackets are
  required for a space in a markdown link destination; the parser emits it as `My%20Notes.md`.
- `content/sub/page.md` (UPDATED) — added `../x.md`, `../gear-guide.md`, `page2.md`, `./sibling.md`,
  `../index.md` to exercise parent-`..` / sibling / `./` / vault-root resolution from `/sub/page`.

The `../index.md` -> `/` case authors the link only (no `content/index.md` target is created — AC2
asserts the emitted href value, and the route note says `index.md` would route to `/index`, not `/`;
creating it is out of scope for a fixture). The broken `does-not-exist.md` and the escape/malformed
cases likewise need no target file.

No slug collisions introduced; `npm run build` exits 0 and emits 8 content pages
(x, sub/page, sub/page2, sub/sibling, my-notes, no-h1, empty, gear-guide).

## AC -> test mapping

| AC | Coverage | Tests |
|---|---|---|
| **AC1** relative `.md` -> route href + in-place nav | `gear-guide.md` -> `<a href="/gear-guide">`; click navigates to `/gear-guide` | 2 |
| **AC2** resolution edge cases (assert emitted href) | same-dir+nested; cross-file fragment preserved; space+`%20` decode->`/my-notes`; mixed-case->`/gear-guide`; `index.md`->`/sub`; parent `..`->`/x`,`/gear-guide`; sibling+`./`->`/sub/page2`,`/sub/sibling`; `../index.md`->`/` | 8 |
| **AC3** pass-through untouched | external `https`; `mailto:`; root-absolute `/`; same-page `#anchor`; non-`.md` asset; malformed-`%`; vault-escape `..` | 7 |
| **AC4** missing target -> true 404 on custom page | broken link rewrites to `/does-not-exist`; `status()===404`; custom themed not-found page (h1/"not found"/`a[href="/"]`); clicking broken link lands on 404 | 4 |
| **AC5** Back/Forward via plain `<a>` | follow link, `goBack()` -> prior page, `goForward()` -> next page | 1 |
| **AC6** existing 39 specs stay green | the full-suite run (no SPA router, JS-free shell, 2.2 theme intact) | (full suite) |

Total new tests: **22**.

## RED confirmation

Command: `cd web && npx playwright test` — **47 passed, 14 failed** (39 prior + 8 true-negative 2-3 +
14 feature-absent 2-3).

### Existing 39 specs — ALL GREEN (AC6)
`ac1-gfm-core` (5), `ac2-js-disabled` (2), `ac3-crawlable-shell` (4), `ac5-slugging-edge` (5),
`ac6-gfm-extensions` (4), `2-2-theme` (19) — unchanged and passing alongside the new fixtures.

### New 2-3 spec — 14 fail (feature-absent), 8 pass (true-negative)

**14 FAILING (correct — feature not yet built):**
- AC1 (2): href still emits literal `gear-guide.md`; click lands on `/gear-guide.md` (no rewrite).
- AC2 (8): every resolution case still emits the literal `*.md` href (e.g. `notes space` ->
  `My%20Notes.md` instead of `/my-notes`).
- AC4 (3): broken link still emits `does-not-exist.md`; no custom 404 page (default not-found lacks
  the "not found" h1/text and `a[href="/"]`); clicking the broken link goes to `/does-not-exist.md`.
- AC5 (1): the link still navigates to `/gear-guide.md`, so the back/forward round-trip never reaches
  the page route.

**8 PASSING (true-negatives — behavior already holds today):**
- AC3 (7): external / `mailto:` / root-absolute / same-page `#anchor` / non-`.md` asset / malformed-`%`
  / vault-escape links are already left unchanged (the plugin only needs to *preserve* this).
- AC4 (1): `visiting a missing route returns a real 404 status` — `astro preview` already returns a
  **404 status** for unmatched routes. This isolates the SEO status contract (already satisfied) from
  the *custom-page* contract (the 3 AC4 tests that require `404.astro`, which fail).

All failures are for **feature-absence**, not harness defects. One fixture fix was applied during
authoring: the space-filename link needed angle-bracket wrapping (`[..](<My Notes.md>)`) so the
markdown parser emits it as a real `<a>` — without it the locator timed out (wrong-reason failure),
now it fails correctly on the literal href value.

## Files

- `web/tests/2-3-linking-nav.spec.ts` (NEW)
- `content/gear-guide.md`, `content/sub/page2.md`, `content/sub/sibling.md` (NEW fixtures)
- `content/x.md`, `content/sub/page.md` (UPDATED fixtures)

## Next (Step 5 — GREEN)

Implement `web/src/lib/rehype-md-links.mjs` + wire `markdown.rehypePlugins` in `web/astro.config.mjs`,
and add `web/src/pages/404.astro` via the `Page` layout. The 14 failing tests then go green with no
change to this spec or the existing 39.
