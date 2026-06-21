# Story 2-7 — TDD Test Report (RED phase)

**Story:** 2.7 Content negotiation — one URL, two representations
**Component:** `api/` (Azure Functions, Node/TypeScript) — NOT `web/`
**Phase:** RED (failing-first tests; handler intentionally NOT implemented)
**Date:** 2026-06-21
**Author:** naethyn

## Summary

Established a minimal, dependency-light test setup in `api/` using Node's
built-in `node:test` + `node:assert` (no new heavy deps, no Azure emulator for
the CI tier). Wrote failing-first unit tests for the pure
`negotiate({ slug, acceptHeader, readMd })` handler covering ACs 1–6, plus a
GATED emulator endpoint test (AC8b) that is skipped in CI.

RED is confirmed: the unit test file fails to import the not-yet-existing
`api/negotiate/negotiate.mjs` (`ERR_MODULE_NOT_FOUND`), so every unit assertion
fails by construction. The handler is NOT implemented (Step 5 deliberately
skipped).

## Test setup

- **Runner:** `node --test` (Node v22.22.2; `node:test` + `node:assert/strict`).
  No vitest/jest/playwright dependency added to `api/`.
- **`api/package.json`** (NEW): `"type": "module"`, `test` script `node --test`,
  `test:e2e` script gates the emulator tier behind `RUN_SWA_E2E=1`.
- **Command (CI / RED tier):** `cd api && node --test`

## Files

| File | Purpose | Status |
|------|---------|--------|
| `api/package.json` | Minimal Functions-app scaffold + `test` script (`node --test`) | NEW |
| `api/tests/negotiate.test.mjs` | Pure-handler unit tests (AC1–AC6) — the CI-always-green backstop | NEW |
| `api/tests/negotiate.e2e.test.mjs` | GATED emulator endpoint test (AC8b); skipped unless `RUN_SWA_E2E=1` + `BASE_URL` | NEW |

**Test count:** 2 test files. The unit file contains 30+ assertions across
named test cases (parameterized over Accept-header and hostile-slug matrices);
the e2e file has 3 gated cases (all skipped in CI).

Tests inject a **fake closed slug→Buffer map** (built via the shared
`web/src/lib/slug.mjs` `pathToSlug`, reading REAL `content/*.md` bytes) as the
`readMd` shim — no real FS traversal, no emulator. Byte assertions compare
against the actual files on disk (anti-tautology, per Story 2.2), using
`Buffer.equals`.

## AC → test mapping

| AC | Coverage in `api/tests/negotiate.test.mjs` |
|----|--------------------------------------------|
| **AC1** markdown branch | `Accept: text/markdown` → 200, `Content-Type: text/markdown; charset=utf-8`, `Vary: Accept`, Buffer body. |
| **AC2** Accept routing rigor | HTML-default matrix: absent / empty / `text/html` / `*/*` / `text/*` / browser blob / `text/markdown;q=0` / `*/*, text/markdown;q=0` / equal-q tie vs explicit HTML → HTML branch. Markdown-win matrix: plain / `;charset=utf-8` / `; charset=UTF-8` / `Text/Markdown` (case) / `q=0.9 > html q=0.8` / tie vs `*/*` (more specific → markdown). Malformed (`;`, `text/`, `@@@`, non-ASCII, empty media-range) → safe HTML, never throw, never 400/500. |
| **AC3** Vary on both branches | `Vary: Accept` (exact value) asserted on markdown branch AND HTML branch; value never `*`. |
| **AC4** slug mapping no-drift | Drives expected slugs from the SHARED `pathToSlug`; asserts `x.md`→`/x`, `gear-guide`, `sub/page`, `My Notes.md`→`my-notes`, `sub/index.md`→`sub`, and that the handler serves the matching file's bytes. |
| **AC5** security | Missing slug → 404 (+ `Vary: Accept`, non-crashing). Hostile slugs (`..`, `../../etc/passwd`, `%2F`, `%2fetc%2fpasswd`, `%2e%2e%2f`, `%252F`, `%252e%252e`, `%00`, `x%00.md`, `/etc/passwd`, `C:\Windows`, `\\unc\share`, `sub%2Fpage`) → 404 indistinguishable from missing (same status + same body), no path/`content/` leak, never returns another page's bytes, hostile key never a map key (closed-map lookup). |
| **AC6** byte fidelity | `Buffer.equals(body, readFileSync(content/<rel>.md))` for all fixtures; no BOM injected (BOM presence mirrors source); `Content-Length` (if present) == source byte length. |
| **AC8a** CI seam | This whole unit file: pure function, no Azure glue, no port, no emulator — runs under `node --test`. |
| **AC8b** local fidelity (gated) | `api/tests/negotiate.e2e.test.mjs` — `fetch` against `func start`/`swa start`; SKIPPED in CI unless `RUN_SWA_E2E=1`. |

> AC7 (SWA `staticwebapp.config.json` + `api_location` wiring) is implementation
> config, not a unit-testable handler behavior — it is covered by the gated
> emulator tier (AC8b) at local fidelity, and is out of scope for the RED unit
> phase.

## RED confirmation

```
cd api && node --test
```

Output (trimmed):
```
Error [ERR_MODULE_NOT_FOUND]: Cannot find module
  '/home/user/markdown-web/api/negotiate/negotiate.mjs'
  imported from .../api/tests/negotiate.test.mjs
not ok 2 - tests/negotiate.test.mjs   (failureType: testCodeFailure)
# pass 0  # fail 1  # skipped 3
```

- The unit suite FAILS because `negotiate()` is not implemented (module absent).
- The 3 gated e2e cases are SKIPPED (no emulator required for RED/CI).

This is the expected RED bar. Implementation (Task 3) is deliberately NOT done.

## Web non-regression

`git status --short -- web/` is empty — no `web/` file was touched. The 157
`web/tests/*.spec.ts` are unaffected and independent (they run `astro preview`
on port 4321 and never execute `api/` or `node --test`).

## Exact command to run the api tests

```
cd api && node --test
```

Gated local fidelity tier (requires emulator):
```
cd api && RUN_SWA_E2E=1 BASE_URL=http://localhost:4280 node --test tests/negotiate.e2e.test.mjs
```

## Next (GREEN phase, not done here)

Implement `api/negotiate/negotiate.mjs` exporting
`negotiate({ slug, acceptHeader, readMd }) -> { status, headers, body }` (pure
RFC 9110 §12.5.1 Accept parsing + closed-map lookup + verbatim Buffer body),
plus the Azure binding adapter, the Task-1 content-availability mechanism, and
the AC7 SWA routing/deploy wiring.
