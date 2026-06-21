# Story 2.6 — E2E TDD Test Report (RED phase)

Story: **2-6 — Site header and pitch-card chrome**
Story file: `_bmad-output/implementation-artifacts/2-6-site-header-and-pitch-card.md`
Microcopy source: `_bmad-output/planning-artifacts/ux-designs/ux-the-markdown-web-2026-06-21/EXPERIENCE.md`
Date: 2026-06-21
Phase: **RED** (failing-first; chrome NOT yet implemented — implementation is Step 5)

## What was added

- **NEW spec:** `web/tests/2-6-chrome.spec.ts` — **36 tests** total
  - 22 failing (chrome absent — correct RED)
  - 12 passing (right-reason invariant guards: single-`<h1>`, single-`<html lang>` shell, JS-free no-island — these must STAY green after chrome ships)
  - 2 skipped (the `/get` and `/vision` stub-page single-`<h1>` checks — decision-agnostic: they self-skip until Decision A/B adds those routes)

No prior spec was modified (per instruction — the two reconciliations at `2-5-index.spec.ts:323–328` and `2-3-linking-nav.spec.ts:242` are Step 5's job; see "Deferred reconciliation" below).

## Surfaces under test

The chrome lives in the shared `Page.astro` layout, so the spec asserts it on every surface that routes through it:

| Surface | Path | Expected status |
| --- | --- | --- |
| content route | `/x` | 200 |
| nested content route | `/sub/page` | 200 |
| vault index | `/` | 200 |
| custom 404 | `/does-not-exist` | 404 |

## AC → test mapping

| AC | Coverage in `2-6-chrome.spec.ts` |
| --- | --- |
| **AC1** sticky site-header on every surface | `Story 2.6 AC1` describe: per-surface — exactly one `<header>` + `getByRole('banner')` count 1; wordmark contains `.md` chip + `the markdown web`; `getByRole('link', { name: 'the vision' \| 'Get the client', exact })` (accessible names == visible text, no `▣` leak); separate test asserts `getComputedStyle(header).position === 'sticky'`. |
| **AC2** end-of-page pitch-card on every surface | `Story 2.6 AC2` describe: per-surface — `contentinfo` OR named `region` landmark present; verbatim headline `You're reading one fixed view. There's a better one.` (curly `'`); body anchor `Same file. Your shape.`; `getByRole('link', { name: 'Get the Markdown Web client' \| 'Why a markdown web?', exact })`. |
| **AC4** microcopy verbatim | `Story 2.6 AC4` describe: exact headline string in HTML (no `!`); body contains em-dash `—` (U+2014) + `Same file. Your shape.`; `.md` chip in header; **header `Get the client` vs pitch `Get the Markdown Web client` asserted as two DISTINCT exact-name links**; all anchor labels (`the vision`, `Why a markdown web?`, wordmark) verbatim in body text. |
| **AC5** stub links resolve (no dangle/404) | `Story 2.6 AC5` describe: reads each href via `getAttribute('href')` from rendered HTML; asserts non-empty; if a real route (`/…`) → `page.request.get(href)` must be 200; bare `#` accepted as the documented stub. Decision-agnostic (Decision A/B not yet made). |
| **AC6** themed + JS-free + crawlable + single `<h1>` | `Story 2.6 AC6` describe: per-surface — exactly one `<h1>`; wordmark NOT an `<h1>` (matched on full `.md the markdown web` to avoid colliding with the index's legit `The Markdown Web` title); pitch headline NOT an `<h1>`; single `<html lang>` shell + stylesheet linked; no `astro-island`/`client:*`; plus a JS-disabled test (`javaScriptEnabled:false`) proving header + pitch + all four links present and followable with JS off. |
| **AC7** stub-page single-`<h1>` edge case | `Story 2.6 AC7` describe: `/get` and `/vision` — IF a real 200 route, assert exactly one `<h1>` + header + pitch present; else `test.skip` (until Decision A/B). |

A11y assertions (NFR-6/UX-DR9) are woven into AC1/AC2: `getByRole('banner')` (header), `getByRole('contentinfo')`/named `region` (pitch), and exact `getByRole('link', { name })` for all four controls — so a leaked `▣`/`→` glyph in any accessible name FAILS the test.

## RED confirmation

Full suite (`cd web && npx playwright test`):

```
22 failed
2 skipped
133 passed
```

- **22 failed** — ALL from `web/tests/2-6-chrome.spec.ts` (verified: a grep of failures excluding `2-6-chrome.spec.ts` was empty). They fail for the correct reason: no `<header>`, no `banner`/`contentinfo` landmark, no pitch headline/body, no chrome links exist in `Page.astro` yet. Failure modes observed: header `toHaveCount(0)` vs expected 1; missing pitch headline text; sticky-position lookups time out (no header element); stub-link `getAttribute` time out (no link).
- **133 passed** — all prior specs (ac1/ac2/ac3/ac5/ac6, 2-2/2-3/2-4, 2-5) remain green. The two prior chrome-absence assertions (`2-5:323–328` "no chrome", `2-3:242` `a[href="/"]` count) are STILL passing because chrome is not added yet — they will invert only when Step 5 ships the chrome, and are reconciled there.
- **2 skipped** — `/get` and `/vision` stub-page tests self-skip (routes don't exist; Decision A/B deferred to implementation).
- **12 of the 2-6 tests pass** as designed: the single-`<h1>`, single-`<html lang>`-shell, and JS-free/no-island invariant guards. These are intentionally green now and MUST stay green after chrome is added (AC6/AC7) — they prove the chrome adds no `<h1>` and no island.

`npx tsc --noEmit` on the spec: clean (no type errors).

## Harness fix applied (wrong-reason failure corrected)

Initial run showed `vault index /: exactly one <h1>` failing for the WRONG reason: the index's legitimate content `<h1>` is the title `The Markdown Web`, and the original assertion `locator('h1', { hasText: 'the markdown web' })` (case-insensitive substring) matched it, falsely flagging a wordmark-as-`h1`. Fixed by matching the wordmark on its full distinguishing string `.md the markdown web` (chip + sans), which the index title does NOT contain — keeping the guard meaningful (a real wordmark-`<h1>` still fails) without colliding with the index title. After the fix the test passes for the right reason and stays a valid RED guard.

## Deferred reconciliation (Step 5 — NOT done here, per instruction)

These two prior assertions WILL become wrong once Step 5 adds the chrome; they were deliberately left untouched in this RED step:

1. `web/tests/2-5-index.spec.ts:323–328` — `'listing-only: no 2.6 site-header / pitch / get-client chrome yet'` asserts `getByText('Get the client').toHaveCount(0)` and `locator('header').toHaveCount(0)`. INVERTS when chrome ships → flip to `toHaveCount(1)` / rename to a positive chrome-present test.
2. `web/tests/2-3-linking-nav.spec.ts:242` — 404 test asserts `locator('a[href="/"]').toHaveCount(1)`. Breaks ONLY IF the wordmark links to `/` (then wordmark + "Go back home" = 2). Reconcile by scoping to `main a[href="/"]` / `getByRole('link', { name: /go back home/i })`, OR by not linking the wordmark to `/`. Depends on the Step-5 wordmark decision.

## Files

- NEW: `web/tests/2-6-chrome.spec.ts`
- NEW: `_bmad-output/implementation-artifacts/2-6-e2e-tdd-test-report.md` (this report)
