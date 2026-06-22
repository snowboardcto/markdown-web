# E2E TDD Test Report — Story 5.1: Living Link

Generated: 2026-06-22
Phase: RED (pre-implementation)
Epic: 5 | Story: 5-1

---

## Test Files Created

| File | Surface | Tests |
|------|---------|-------|
| `/home/user/markdown-web/web/tests/5-1-living-link.spec.ts` | Web (Playwright) | 33 |
| `/home/user/markdown-web/clients/windows/App.Tests/ShareLinkBuilderTests.cs` | Native WPF (xUnit) | 23 |

---

## AC Coverage Map

| AC | Description | Tests | File |
|----|-------------|-------|------|
| AC1 | Regression lock — page URL serves static HTML (Epic 2/2.7) | 3 web tests | `5-1-living-link.spec.ts` L85–121 |
| AC2 | Regression lock — native round-trip parity (`ToFetchEndpoint(ToShareUrl(x))`) | 5 native `[Fact]`s (round-trip `[Theory]` + trailing-slash + query+fragment + non-app-host) | `ShareLinkBuilderTests.cs` L165–248 |
| AC3 | Web canonical `<link rel="canonical">` in `<head>` | 13 web tests (surface loop × 3 assertions + static HTML check + index check + static-no-island check + 2 JS-disabled) | `5-1-living-link.spec.ts` L122–222 |
| AC4 | Web "Copy link" button — existence, keyboard, clipboard value, feedback, edge cases | 13 web tests (button existence × 3 surfaces + keyboard focus × 2 + accessible name × 2 + clipboard value gated + feedback + index copy + JS-disabled + clipboard-unavailable + writeText-rejection) | `5-1-living-link.spec.ts` L224–500 |
| AC5 | Native `ShareLinkBuilder.ToShareUrl` + toolbar button + clipboard seam | 23 native tests: 12 pure `[Fact]`s (edge-case floor + null/relative) + 5 round-trip `[Fact]`s + 3 copy-action `[Fact]`s + 4 `[StaFact]`s (toolbar button existence, name, keyboard, tab order) | `ShareLinkBuilderTests.cs` |
| AC6 | Scope guardrail (no new backend/persistence) | Covered implicitly: tests verify the canonical URL is the page's own URL; no new endpoint assertions | — |
| AC7 | No regression — 157 web specs + WPF suite green | 3 web tests asserting additive-only behavior (header CTAs intact, single h1, sticky position) | `5-1-living-link.spec.ts` L502–532 |
| AC8 | Verification harness — CI-safe, clipboard assertions gated | Gating pattern applied to clipboard-read assertions; backstop (canonical href wiring) CI-green | `5-1-living-link.spec.ts` L272–318 |

---

## Web Playwright Results (Red Phase Confirmation)

**Command:** `cd /home/user/markdown-web/web && npx playwright test 5-1-living-link.spec.ts`

**Result: 26 FAILED / 7 PASSED — RED PHASE CONFIRMED for new behavior**

### Tests that PASS (regression lock + already-true assertions)

| # | Test | Why it passes pre-implementation |
|---|------|----------------------------------|
| 1 | AC1: GET /x responds 200 text/html | Epic 2 static HTML already ships |
| 2 | AC1: raw HTML payload contains rendered markdown | Epic 2 page renders heading content |
| 3 | AC1: plain browser request returns HTML | Epic 2 content-negotiation in place |
| 15 | AC3: canonical is static (no astro-island in head) | True now (and stays true after impl adds static `<link>`) |
| 31 | AC7 regression: "Get the client" CTA present | 2.6 chrome already ships |
| 32 | AC7 regression: single `<h1>` on /x | 2.6 layout already correct |
| 33 | AC7 regression: header position: sticky | 2.6 sticky header already ships |

### Tests that FAIL as expected (RED — new behavior not yet implemented)

All AC3 tests (canonical `<link>` not present yet):
- content route /x: exactly one `<link rel="canonical">` (no `site` in astro.config.mjs, no link in Page.astro)
- content route /x: canonical href is absolute
- content route /x: canonical href has no query/fragment artifacts
- nested content route /sub/page: (same 3 assertions)
- vault index /: (same 3 assertions)
- canonical `<link>` present in raw response text (no JS)
- index / canonical resolves to site root
- JS-disabled: canonical present (AC3 JS-free source)
- JS-disabled: index canonical present

All AC4 tests ("Copy link" button not in SiteHeader yet):
- "Copy link" `<button>` exists in header chrome (× 3 surfaces)
- "Copy link" button is keyboard-focusable (× 2)
- "Copy link" button has non-empty accessible name (× 2)
- activating "Copy link" copies canonical URL (clipboard gated, backstop also fails — no button)
- "Copy link" button shows "Copied" feedback
- index /: "Copy link" copies root canonical URL
- JS-disabled: canonical `<link>` present (AC3 prerequisite, fails because no canonical link)
- clipboard unavailable: page does not throw (button doesn't exist — test gracefully skips click but canonical check fails)
- clipboard writeText rejection: no unhandled rejection (button doesn't exist — canonical check fails)

**Root causes of RED failures:**
1. `web/astro.config.mjs` has no `site` key → `Astro.site` is `undefined` → no absolute URL base
2. `web/src/layouts/Page.astro` `<head>` has no `<link rel="canonical">` element
3. `web/src/components/SiteHeader.astro` has no "Copy link" `<button>` element

These are EXACTLY the three new additions required by Tasks 1 and 2.

---

## Native WPF Results

**Cannot run dotnet on Linux** (windows-latest CI only, per project constraint established in Epics 3/4).

**Expected RED failures once compiled on windows-latest:**
- All `[Fact]`s over `ShareLinkBuilder` will fail: `TheMarkdownWeb.App.ShareLinkBuilder` does not exist (Task 4 creates it)
- All `[Fact]`s over `IClipboard` / `FakeClipboard` will fail: `IClipboard` interface does not exist (Task 5 creates it)
- All `[StaFact]`s for `ShareLinkButton` will fail: no `Button` named `ShareLinkButton` in `MainWindow.xaml` (Task 5 adds it)

**Compilation note:** `ShareLinkBuilderTests.cs` references:
- `ShareLinkBuilder.ToShareUrl(Uri?)` — to be created in `clients/windows/App/ShareLinkBuilder.cs`
- `IClipboard` interface — to be created in `clients/windows/App/IClipboard.cs`
- `FakeClipboard` (defined in this test file itself as a test double)
- `PageEndpointResolver.ToFetchEndpoint` / `IsAppHost` — already implemented (green)
- `ShellTestHelpers.CreateWindow()` / `FindButton()` — already implemented (green)

---

## Native Test Count Breakdown

| Category | Count | Status |
|----------|-------|--------|
| `[Theory]` / `[Fact]` — `ShareLinkBuilder.ToShareUrl` edge-case floor | 12 | RED (class missing) |
| `[Fact]` — AC2 round-trip parity | 5 | RED (class missing) |
| `[Fact]` — copy action with `FakeClipboard` | 3 | RED (interface missing) |
| `[StaFact]` — toolbar button (existence, name, keyboard, tab order + regression) | 4 | RED (button missing) |
| **Total native** | **23** | **All RED (CI-only)** |

---

## Summary

| Surface | Total Tests | Pass (pre-impl) | Fail (RED — expected) |
|---------|-------------|-----------------|----------------------|
| Web (Playwright) | 33 | 7 | 26 |
| Native (xUnit, CI-only) | 23 | 0 | 23 (all — class/interface/button missing) |
| **Total** | **56** | **7** | **49** |

**RED phase confirmed.** All new-behavior tests fail as expected. Regression-lock tests (AC1, AC7) pass. The 7 pre-passing tests are either regression locks or assertions that are true now and will remain true after implementation (the "no astro-island in head" check).

---

## Files

- Web spec: `/home/user/markdown-web/web/tests/5-1-living-link.spec.ts`
- Native spec: `/home/user/markdown-web/clients/windows/App.Tests/ShareLinkBuilderTests.cs`
- This report: `/home/user/markdown-web/_bmad-output/implementation-artifacts/5-1-e2e-tdd-test-report.md`
