# Story 2.7 — AC Trace Report (Content negotiation: one URL, two representations)

- Generated: 2026-06-21
- Story: `2-7-content-negotiation-one-url-two-representations.md`
- Verdict: **DONE (Option 2)** — all 8 ACs covered by green tests; AC1/AC7 met as **Option 2** (markdown served at the Function endpoint `/api/negotiate/<slug>`; true same-URL `Accept` negotiation **DEFERRED** per the documented Azure-SWA limitation that route rules cannot branch on the request `Accept` header).

## Test totals (re-run 2026-06-21)

| Suite | Command | Result |
|-------|---------|--------|
| api unit/integration (CI backstop) | `cd api && node --test` | **45 pass / 3 skipped / 0 fail** (48 total) |
| web no-regression (157 baseline) | `cd web && npx playwright test` | **157 passed / 0 fail** |

- The 3 skipped api tests are the gated AC8b emulator endpoint tier (`negotiate.e2e.test.mjs`), correctly skipped without `RUN_SWA_E2E` + a running `func`/`swa` emulator (CI-impractical, documented).
- The web run logged the known `URI malformed` preview-server transient (vite middleware) but **no spec failed** — not a 2.7 regression (documented 2-6 transient).

## Implementation under test

- Handler / glue: `api/negotiate/negotiate.mjs` (pure handler), `api/negotiate/adapter.mjs` (`sanitizeSlug` + `handleNegotiate`), `api/negotiate/index.mjs` (Azure v4 `app.http` shim), `api/negotiate/vault.mjs` (closed-map `readMd`).
- Build/bundle: `api/scripts/build-content.mjs` (verbatim `.md` copy + slug→file manifest via shared `web/src/lib/slug.mjs` `pathToSlug`).
- Routing/deploy: `web/public/staticwebapp.config.json` (`/api/negotiate/*` → `Vary: Accept`; `.md` mime; nav fallback), `.github/workflows/deploy-web.yml` (`api_location: 'api'`, `npm ci`, content-bundle step, `api/**`/`content/**` push paths).

## AC → test matrix

| AC | Summary | Status | Covering tests |
|----|---------|--------|----------------|
| **AC1** | `Accept: text/markdown` → 200 raw `.md` + `Content-Type: text/markdown; charset=utf-8` + `Vary: Accept` | **MET (deferred same-URL)** — served at `/api/negotiate/<slug>`, NOT at `/<slug>` (same-URL DEFERRED) | `negotiate.test.mjs` "AC1: …200 text/markdown…+ Vary: Accept", markdown-win matrix; `vault.test.mjs` "AC4/AC6 bundled readMd serves byte-equal", "handleNegotiate end-to-end"; gated `negotiate.e2e.test.mjs` "GET /api/negotiate/x …→ 200 raw .md" |
| **AC2** | HTML is default; markdown opt-in only; RFC 9110 §12.5.1 parser rigor (case-insensitive, param/q-value tolerant, `q=0`=reject, wildcards non-opt-in, malformed → safe HTML, never throws) | **MET** | `negotiate.test.mjs` `HTML_DEFAULT_ACCEPTS` (10 cases incl. `*/*`, `text/*`, `q=0`, browser blob, equal-q tie), `MALFORMED_ACCEPTS` (5 cases, no-throw), `MARKDOWN_WIN_ACCEPTS` (7 cases incl. mixed-case, charset param, higher-q) |
| **AC3** | `Vary: Accept` on BOTH branches + static-host HTML surface; value exactly `Accept` never `*` | **MET** | `negotiate.test.mjs` "Vary: Accept on markdown branch", "…on HTML/default branch", "Vary value exactly Accept never *"; `vault.test.mjs` end-to-end (`md.headers.Vary`/`html.headers.Vary`); `staticwebapp.config.json` `/api/negotiate/*` header |
| **AC4** | URL→`.md` mapping uses the SHARED `pathToSlug` (no drift); case-normalised | **MET** (CONFIRMED) | `negotiate.test.mjs` "representative pages resolve via shared pathToSlug"; `vault.test.mjs` "sanitizeSlug passes through nested slugs", "HIGH #3 lowercases/github-slugs", "/X resolves to same bytes as /x" |
| **AC5** | Missing slug → 404; closed-map lookup; decode-once + reject `..`/`%2F`/`%00`/double-encode/absolute/UNC; uniform 404, no leak/oracle | **MET** (CONFIRMED) | `negotiate.test.mjs` "missing slug → 404", "hostile slugs all → 404 indistinguishable" (13 hostile keys); `vault.test.mjs` "sanitizeSlug rejects every hostile key", "missing slug via real readMd → 404", "closed map keyed exactly by manifest slugs" |
| **AC6** | Byte fidelity: read-as-Buffer, no BOM inject/strip, no CRLF/trailing mutation, `Content-Length`==source bytes, `Buffer.equals` test | **MET** (CONFIRMED) | `negotiate.test.mjs` "byte-equal (Buffer.equals)", "no UTF-8 BOM injection", "Content-Length == source length"; `vault.test.mjs` "bundled readMd byte-equal"; `funcignore.test.mjs` "fixture x byte-equal to content/x.md" |
| **AC7** | SWA routing wires `Accept` negotiation: committed `staticwebapp.config.json` + `api_location` in `deploy-web.yml`; mechanism+trade-offs documented; no hard-coded secrets | **MET (deferred same-URL)** — config carries only correct `/api/negotiate/*` + `Vary: Accept` wiring; no misleading page→Function rewrite; same-URL routing DEFERRED (SWA cannot branch on `Accept`) | `staticwebapp.config.json` (committed), `deploy-web.yml` (`api_location: 'api'`, `npm ci`, bundle step, push paths); `funcignore.test.mjs` (bundle ships in deploy package); story Dev Agent Record (mechanism + trade-offs) |
| **AC8** | 157 prior web specs green + new negotiation tests pass; CI-runnable handler test + gated emulator endpoint test | **MET** | `cd web && npx playwright test` → 157 pass; `cd api && node --test` → 45 pass/3 gated-skip; `funcignore.test.mjs` (anti-regression: bundle survives `.funcignore`, map non-empty); `negotiate.e2e.test.mjs` (gated AC8b) |

## Deferred-same-URL honesty note (AC1 / AC7)

AC1 and AC7 are recorded **"met (deferred same-URL)"**, not "fully met". The ideal end-state — `GET /<slug>` with `Accept: text/markdown` returning raw `.md` at the **same URL** a browser reads HTML — is **DEFERRED** because Azure Static Web Apps route rules cannot branch on the request `Accept` header (`rewrite`/`redirect` are path-only). Under the documented **Option 2** decision (naethyn, 2026-06-21):

- The page URL `/<slug>` stays **pure static HTML** (live site untouched, fast static host).
- Raw markdown is exposed **now** at the Function endpoint `/api/negotiate/<slug>` with `Content-Type: text/markdown; charset=utf-8` + `Vary: Accept` + byte-faithful source bytes.
- The native client (Epic 3, Story 3.2) — the only consumer — fetches markdown from `/api/negotiate/<slug>`.
- True same-URL negotiation is recorded as KNOWN-DEFERRED (`deferred-work.md`) and revisited when Epic 3 lands.

The gated AC8b e2e test (`negotiate.e2e.test.mjs`) reflects this: it hits `/api/negotiate/x` for markdown and `/x` for static HTML, not same-URL.

## Gaps / caveats

- **Same-URL negotiation (AC1/AC7 ideal):** DEFERRED by decision — a documented platform limitation, not a defect. Markdown is reachable today at a different URL (`/api/negotiate/<slug>`).
- **Production SWA fidelity unverified in CI:** the AC8b emulator endpoint tier is gated/skipped in CI (no `func`/`swa` emulator). The CI backstop is the pure-handler + real-runtime-path tests (`negotiate.test.mjs`, `vault.test.mjs`) plus the `funcignore.test.mjs` anti-regression guard that the bundled `.md` ship. Local run is documented in `api/README.md` (`RUN_SWA_E2E=1 BASE_URL=… npm run test:e2e`).
- **Markdown-type aliases** (`text/x-markdown`, `application/markdown`) → HTML, not raw `.md` (deferred LOW; only `text/markdown` is the AC1 contract).

## Conclusion

8/8 ACs covered by green, CI-protected tests; all code-review findings resolved or explicitly deferred (Option 2). api 45 pass / 3 gated-skip, web 157 pass. AC1/AC7 met as Option 2 with the same-URL ideal honestly recorded as DEFERRED. Story 2.7 is **DONE**; it is the last Epic 2 story, so **Epic 2 is COMPLETE**.
