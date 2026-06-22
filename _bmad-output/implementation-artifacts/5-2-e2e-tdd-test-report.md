---
story: 5-2-follow-feed
phase: RED (TDD — tests written before implementation)
generated: 2026-06-22
---

# Story 5.2 Follow/Feed — TDD E2E Test Report (RED Phase)

## Summary

Two new test files generated for Story 5.2. All NEW-behavior tests fail as expected in RED phase. Regression/AC6 tests pass (existing elements still present). RED phase confirmed.

## Test Files Created

| File | Type | Count |
|------|------|-------|
| `web/tests/5-2-follow-feed.spec.ts` | Playwright E2E (build+preview harness) | 46 tests |
| `web/tests/5-2-feed-builder.spec.ts` | Playwright pure-helper unit-style spec | 26 tests |
| **Total** | | **72 tests** |

## RED Phase Confirmation

### `web/tests/5-2-follow-feed.spec.ts` — 41 failed, 5 passed

**41 tests fail** as expected (no `/feed.xml` exists, no autodiscovery link, no subscribe affordance). Root failure: `/feed.xml` returns 404 (no `feed.xml.ts` endpoint implemented yet).

**5 tests pass** (pre-existing behavior — regression lock):
- `feed order is CI-stable (identical on two consecutive requests)` — vacuously passes because the 404 body is identical on two calls
- AC6: `adding subscribe link does not remove "Get the client" CTA from header` — CTA still exists
- AC6: `adding subscribe link does not remove "Copy link" button from header` — button still exists
- AC6: `adding subscribe link does not add a second <h1>` — single h1 invariant still holds
- AC6: `canonical <link> in <head> is still present and correct after 5.2 additions` — 5.1 canonical still present

### `web/tests/5-2-feed-builder.spec.ts` — 26 failed, 0 passed

**26 tests fail** as expected: `web/src/lib/feed.mjs` does not exist → dynamic import fails → `requireFeed()` throws `"web/src/lib/feed.mjs not found or does not export buildFeed/buildRssXml"`.

## AC Coverage Map

| AC | Tests (file:describe:test summary) | Status |
|----|-------------------------------------|--------|
| **AC1** — `/feed.xml` generated, one item/page, absolute canonical URLs + dates, newest-first order | | |
| | `5-2-follow-feed.spec.ts` → AC1 one item per vault page (8 tests): item count equals 12, no bare-root item, each link is absolute `https://themarkdownweb.com/<slug>`, representative link byte-equals canonical, guid==link+isPermaLink="true", every expected route has an item, non-empty title | RED |
| | `5-2-follow-feed.spec.ts` → AC1 ordering (2 tests): code-unit id order when all dates equal, CI-stable order | RED (order test); PASS (CI-stable — vacuous) |
| | `5-2-feed-builder.spec.ts` → empty vault (5 tests): zero items, valid rss root, required channel children, lastBuildDate+atom:link self, empty-id filtered | RED |
| | `5-2-feed-builder.spec.ts` → single-page vault (6 tests): one item, absolute link, guid==link+isPermaLink, frontmatter title used, slug-derived fallback, non-empty pubDate | RED |
| | `5-2-feed-builder.spec.ts` → all-equal-date ordering (2 tests): sorted by code-unit id, matches buildIndexItems sort | RED |
| | `5-2-feed-builder.spec.ts` → newest-first when dates differ (1 test): new(2026)>mid(2025)>old(2024) | RED |
| **AC2** — new page surfaces after rebuild; guid build-stability | | |
| | `5-2-follow-feed.spec.ts` → new page surfaces (1 test): fixture `content/5-2-fixture.md` appears as `https://themarkdownweb.com/5-2-fixture` item | RED |
| | `5-2-follow-feed.spec.ts` → guid build-stability (1 test): `/x` item guid byte-identical across two requests | RED |
| | `5-2-feed-builder.spec.ts` → junk date inputs (5 tests): number/Date/unparseable/null/undefined → no throw, no "Invalid Date", pubDate parseable | RED |
| | `5-2-feed-builder.spec.ts` → guid stability (1 test): guid == canonical URL, byte-identical across two calls | RED |
| **AC3** — well-formed valid RSS 2.0; XML-escaped; Content-Type | | |
| | `5-2-follow-feed.spec.ts` → /feed.xml exists (3 tests): 200, Content-Type application/rss+xml, non-empty XML | RED |
| | `5-2-follow-feed.spec.ts` → well-formed RSS 2.0 (8 tests): balanced tags, rss version="2.0"+xmlns:atom, channel title/link/description, lastBuildDate RFC-822, atom:link self, item children, pubDate RFC-822, no raw &, no double-escaping | RED |
| | `5-2-feed-builder.spec.ts` → XML-special chars (4 tests): & → &amp;, < → &lt;, both combined, no double-escaping | RED |
| | `5-2-feed-builder.spec.ts` → & in URL (1 test): slug & → &amp; in link/guid, not double-escaped | RED |
| | `5-2-feed-builder.spec.ts` → atom:link rel="self" (1 test): present, references /feed.xml | RED |
| **AC4** — autodiscovery `<link rel="alternate">` + visible subscribe affordance | | |
| | `5-2-follow-feed.spec.ts` → autodiscovery on 3 surfaces (9 tests): /x, /, /sub/page each have exactly one `<link rel="alternate" type="application/rss+xml">`, href == absolute feed URL, present in static HTML | RED |
| | `5-2-follow-feed.spec.ts` → JS-disabled autodiscovery (1 test): link present with JS off | RED |
| | `5-2-follow-feed.spec.ts` → 404 autodiscovery (1 test): 404 page inherits autodiscovery | RED |
| | `5-2-follow-feed.spec.ts` → subscribe link (6 tests): exists in header, keyboard-focusable, href points to /feed.xml, non-empty accessible name, /feed.xml returns 200, present on index | RED |
| **AC5** — scope guardrail | | | 
| | Covered by `web/tests/5-2-follow-feed.spec.ts` AC6 regression suite (pre-existing elements still present) — scope compliance is validated implicitly by the no-regression tests + the fact no new backend/dependency appears | N/A (verified structurally) |
| **AC6** — no regression; existing web specs stay green | | |
| | `5-2-follow-feed.spec.ts` → AC6 regression (4 tests): "Get the client" CTA present, "Copy link" button present, single h1, canonical href correct | PASS (pre-existing behavior) |
| **AC7** — harness over built dist/ | | |
| | `5-2-follow-feed.spec.ts` → AC7 (2 tests): /feed.xml is served as static asset (200), each item URL resolves to live 200 page | RED |

## Failures Breakdown (Expected RED)

### Primary failure cause: `web/src/lib/feed.mjs` does not exist

All 26 `5-2-feed-builder.spec.ts` tests fail with:
```
web/src/lib/feed.mjs not found or does not export buildFeed/buildRssXml.
This is expected in the RED phase — implement Task 1 to make this pass.
```

### Secondary failure cause: `/feed.xml` returns 404

All `5-2-follow-feed.spec.ts` tests touching `/feed.xml` fail with:
```
Error: /feed.xml must return 200
Expected: 200
Received: 404
```

### Tertiary failure causes (cascade from feed.xml 404):

- All autodiscovery tests: `head link[rel="alternate"][type="application/rss+xml"]` has count 0
- Subscribe link tests: `header` does not have a link matching `Subscribe|RSS feed`
- AC2 fixture test: `/5-2-fixture` item not found in (non-existent) feed

### Known pre-existing transient (NOT a 5.2 failure):

A small number of `URI malformed` / `ERR_INVALID_FILE_URL_PATH` preview-server lines may appear intermittently under parallel load. Per the story's Dev Notes and prior sprint experience (2-6/5-1 deferred), these are pre-existing transient noise — retry if encountered.

## Implementation Checklist (what must pass after implementation)

For GREEN phase, the following must be implemented:

1. **`web/src/lib/feed.mjs`** — pure `buildFeed(entries, channelMeta)` builder (Task 1)
2. **`web/src/pages/feed.xml.ts`** — Astro static endpoint emitting `dist/feed.xml` with `Content-Type: application/rss+xml` (Task 2)
3. **`web/src/layouts/Page.astro`** — add `<link rel="alternate" type="application/rss+xml" href="...">` to `<head>` (Task 3)
4. **`web/src/components/SiteHeader.astro`** — add visible "Subscribe"/"RSS feed" `<a>` link (Task 3)
5. **`content/5-2-fixture.md`** — committed fixture file for AC2 new-page-surfaces test

## Commands

```bash
# Run just the 5.2 tests (will fail in RED phase):
cd /home/user/markdown-web/web && npx playwright test 5-2-follow-feed.spec.ts
cd /home/user/markdown-web/web && npx playwright test 5-2-feed-builder.spec.ts

# Run both 5.2 specs together:
cd /home/user/markdown-web/web && npx playwright test 5-2-follow-feed.spec.ts 5-2-feed-builder.spec.ts

# Full suite (including prior specs):
cd /home/user/markdown-web/web && npx playwright test
```
