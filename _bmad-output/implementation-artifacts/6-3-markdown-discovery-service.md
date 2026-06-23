# Story 6.3: Markdown discovery service

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader,
I want the client to reliably find a URL's markdown representation,
so that the right markdown is rendered and false positives are not.

## Context note — THIRD and LOAD-BEARING Epic-6 story; folds in the discovery spike

> This is the core of the Markdown Lens. It builds the ordered, first-hit-wins discovery cascade that, for any `http(s)` URL (the 6.2 acceptance), determines whether a markdown representation exists and returns it (or a precise "no markdown" / "blocked" outcome). It is grounded ENTIRELY in the 2026-06-23 discovery research (`_bmad-output/planning-artifacts/research/technical-markdown-discovery-for-arbitrary-websites-research-2026-06-23.md`) — the cascade order, the validation rule, the budget, the UA strategy, and the bot-block distinction are all that document's recommendations. This story produces the discovery SERVICE only; 6.4 wires its result into the render pipeline + the UI states.
>
> **The research's headline corrections baked into this story:** (1) the cascade is `(content-negotiation + alternate-link autodiscovery in ONE request) → .md sibling → /llms.txt`, NOT the user's original signal order; (2) `/llms.txt` is a SITE INDEX hint, not the page body — never rendered as "the page"; (3) every candidate is validated by `Content-Type` AND an HTML-doctype byte-sniff to kill soft-404s / HTML-served-as-markdown (zero false positives); (4) an honest non-spoofed User-Agent, and a `403`/refusal treated as a DISTINCT "blocked" outcome from a genuine miss.

**What exists today (verified — generalize from these, do not fork):**
- `clients/windows/App/MarkdownFetcher.cs` — GETs a URL with `Accept: text/markdown`, returns `FetchResult.Success(body)` ONLY on a 2xx whose `Content-Type` media type == `text/markdown` (charset ignored), with a non-empty body bounded at 8 MiB; NEVER throws (non-2xx → Failure, wrong content-type → Failure, `HttpRequestException`/`OperationCanceledException` → Failure). **It does NOT yet: HTML-doctype byte-sniff, set a User-Agent, parse a `<link rel=alternate>`, distinguish a 403 bot-block, accept `text/plain`+structure-sniff, or probe siblings/`llms.txt`.** 6.3 ADDS the discovery cascade ON TOP of (and partly extending) this fetcher's content-type/size discipline. Constructed over an injectable `HttpClient` (line 58) — the existing test seam (`MarkdownFetcherTests` uses a stub `HttpMessageHandler`).
- `clients/windows/App/PageEndpointResolver.cs` — `IsAppHost` + `ToFetchEndpoint` (app-host `.md` page → `/api/negotiate/<slug>`; non-app-host returned as-is). Discovery generalizes BEYOND the app host: for the app host the existing negotiate endpoint is the natural first hit; for arbitrary hosts the cascade runs.
- `clients/windows/App/MarkdownFetcherTests.cs` — the stub-`HttpMessageHandler` pattern (a `HttpMessageHandler` subclass whose `SendAsync` captures the request and returns a canned `HttpResponseMessage`) — the EXACT seam 6.3's deterministic unit tests use; NO real network in CI.
- `api/negotiate/negotiate.mjs` — the Story 2.7 server contract (`Vary: Accept`, `text/markdown; charset=utf-8`) the project itself honors — the model the cascade's content-negotiation step expects.

### ⚠️ ENVIRONMENT CONSTRAINT — read before writing any code or test

**Linux dev box, NO .NET SDK; WPF builds/runs ONLY on Windows. Verification is EXCLUSIVELY `build-windows.yml` on `windows-latest`.** The discovery service is PURE LOGIC over an INJECTED HTTP SEAM — NO live network in CI:
- The service takes an injected HTTP fetch seam (an `HttpMessageHandler`/`HttpClient` per the existing `MarkdownFetcher` pattern, OR a narrow `IHttpProbe` interface) so EVERY cascade branch, sniff, budget cap, redirect, and 403 path is asserted with a **fake/stub handler** that returns canned responses — deterministic, no socket. This is the hard testability requirement: "deterministic unit tests with a fake HTTP handler."
- A SEPARATE **gated/skippable live-probe test** against the research basket (Stripe, docs.anthropic.com, ai-sdk.dev, gilesthomas.com, Coca-Cola, NYT) is included but `[Trait]`/skip-gated so it does NOT run in CI by default (mirrors the project's emulator-gating precedent). Its purpose is to validate the cascade against real sites manually — it must NEVER be required for a green CI.
- The service is `[Fact]`/`[Theory]` testable with NO window (no `[StaFact]` needed — this is pure App logic, not UI). The HTML-head parse for the alternate link must be a lightweight, NON-Chromium, non-JS parser (a tolerant regex/streaming scan limited to `<head>`, OR a managed HTML parser library IF added behind the no-embedded-browser guard — see AC7 dependency rule).

## Acceptance Criteria

> Source: [_bmad-output/planning-artifacts/epics.md#Story 6.3] (lines 496–508): **Given** an http(s) URL **When** discovery runs **Then** it tries, first-hit-wins within a bounded probe budget: (1) GET with `Accept: text/markdown` and parse `<head>` for `<link rel="alternate" type="text/markdown">`; (2) `.md` sibling (`<path>.md`); (3) `/llms.txt` as a site-index hint (not the page body) **And** every candidate is validated by Content-Type and an HTML-doctype byte-sniff, rejecting soft-404s and HTML-served-as-markdown (zero false positives) **And** the service is pure/total over inputs, uses an honest non-spoofed User-Agent, distinguishes a bot-block (e.g. 403) from a genuine miss, and is covered by deterministic unit tests with a fake HTTP handler (plus a gated/manual live-probe test against the research basket). (FR-21; research 2026-06-23.)

1. **[Ordered, first-hit-wins cascade — exact step order]** **Given** an absolute `http(s)` URL, **When** `MarkdownDiscoveryService.DiscoverAsync(uri, ct)` runs, **Then** it executes, in THIS order, stopping at the first VALIDATED (AC4) markdown hit:
   - **Step 1 — content-negotiation + alternate-link autodiscovery in ONE request:** GET the page URL with `Accept: text/markdown` and an honest UA (AC5). If the final response (after bounded redirects, AC6) is VALIDATED markdown (AC4), that IS the hit (a `PageMarkdown` result). ELSE parse the returned HTML `<head>` for `<link rel="alternate" type="text/markdown" href="...">`; if present, resolve the (possibly relative) `href` against the final URL and GET it (one probe) — if VALIDATED markdown, that is the hit.
   - **Step 2 — `.md` sibling probe:** construct `<path>.md` (append `.md` to the URL's path; e.g. `…/docs/intro` → `…/docs/intro.md`; document the handling of a path that already ends `/` and of a trailing-slash path) and GET it; if VALIDATED markdown, that is the hit. (Document the GitHub host/path special-case as a NOTE — research §"GitHub needs a host/path transform" — but a naive `.md` append is the baseline; GitHub raw transform is optional/deferred unless trivially added.)
   - **Step 3 — `/llms.txt` at the SITE ROOT:** GET `https://<host>/llms.txt`; if VALIDATED markdown WITH minimal markdown structure (AC4 step-5), surface it as a **site-index hint** (an `LlmsIndex` result carrying the llms.txt body / extracted links) — explicitly NOT as the page body. Optionally resolve the typed URL against the index's link list (DECIDE-AND-DOCUMENT: yes/no; the research leaves this open).
   - First validated hit wins; later steps are not run. *(AC1 — epics.md 6.3 step order; research "Recommended order — and why" §63–68; PRD FR-21 cascade.)*

2. **[Bounded probe budget + politeness — fail fast and cheap]** **Given** the cascade, **When** it runs for any URL, **Then** the total number of HTTP requests per discovery is HARD-CAPPED at the research budget — worst case ≈ **4 GETs**: (1) page w/ `Accept: text/markdown` (also yields the head for the alternate link), (2) `.md` sibling, (3) `/llms.txt`, (4) optional one resolved `.md` from the llms.txt index / the resolved alternate-link href. No concurrent fan-out against the same host; no retries on `4xx`; at most a single retry on a transient network/`5xx`. Per-request timeouts are short (research: ~5 s connect / 10 s total) so a no-markdown site fails within a few seconds. The service exposes/enforces the cap (a constant + a probe counter) and a `[Fact]` asserts the cap is never exceeded for a worst-case all-miss URL. *(AC2 — research "Probe budget / politeness" §121, "Timeouts" §120; PRD FR-21 "bounded probe budget"; feature NFR "bounded probe count … sane timeouts".)*

3. **[Pure/total over inputs — never throws, defined for every input]** **Given** ANY input (null/relative/non-http(s) Uri — though 6.2 should only pass absolute http(s); the service is still defensive), **When** `DiscoverAsync` runs, **Then** it NEVER throws — every failure (network exception, timeout, cancellation, malformed response, parse error) is surfaced as a discovery RESULT, not an exception. A null/relative/non-http(s) input returns a defined `NoMarkdown` (or an `Invalid`) result. Cancellation via the `CancellationToken` returns promptly with a defined result (no throw). The service holds NO mutable static state across calls (pure per-call; any caching is an explicit injected dependency, AC8-optional). *(AC3 — epics.md 6.3 "the service is pure/total over inputs"; the codebase's total-never-throws discipline, mirrored from `MarkdownFetcher`/`NavigationController`.)*

4. **[Validation rule — Content-Type + HTML-doctype byte-sniff; ZERO false positives]** **Given** any candidate response, **When** it is validated, **Then** it counts as REAL markdown ONLY if ALL hold (the research "No Markdown Available Determination Rule" §99–105):
   1. HTTP status is `2xx` after bounded redirects (AC6);
   2. `Content-Type` media type is `text/markdown` (case-insensitive; charset ignored) — REUSE the `MarkdownFetcher` check — **OR** `text/plain` ACCEPTED ONLY as a weak fallback for `.md` siblings and `/llms.txt` AND only if the body also passes the markdown-structure sniff (step 5);
   3. **Body is NOT HTML:** byte-sniff the first ~512 non-whitespace bytes and REJECT if it begins with `<!doctype html`, `<html`, `<head`, `<?xml`, or a `<body`/`<script`/`<meta` cluster (defeats SPA catch-alls / soft-404s returning `200 text/html`);
   4. Body is non-empty and within the 8 MiB size bound (REUSE `MarkdownFetcher`'s bound);
   5. **For `/llms.txt` specifically:** require minimal markdown structure (a leading `#` heading and/or markdown links) so a homepage soft-served at `/llms.txt` is rejected.

   A `.md`/page URL that returns `200` but `Content-Type: text/html` is a FAILURE, not a hit. The validation predicate is pure and `[Fact]`-testable in isolation (feed it canned `(status, content-type, body)` tuples). *(AC4 — epics.md 6.3 "validated by Content-Type and an HTML-doctype byte-sniff … zero false positives"; research determination rule §99–110; PRD FR-21 "a `200 text/html` response to a `.md` probe is rejected".)*

5. **[Honest User-Agent + bot-block (403) as a DISTINCT outcome]** **Given** the client probes arbitrary hosts, **When** it issues every discovery request, **Then** it sends a **descriptive, honest, non-spoofed** User-Agent identifying the client (e.g. `MarkdownLens/0.1 (+https://themarkdownweb.com)`) — NOT a spoofed browser UA. **When** a host refuses the non-browser client — a `403 Forbidden` (or `401`, or a hard network refusal that the research observed at NYT/BBC) — **Then** the discovery RESULT is a DISTINCT `Blocked` outcome (e.g. `DiscoveryResult.Blocked`), separable from a genuine `NoMarkdown`, so 6.4 can message "site blocked the request" vs "no markdown available" differently. A `403` is NOT retried and NOT treated as a miss-with-fallback; it short-circuits to `Blocked`. *(AC5 — epics.md 6.3 "honest non-spoofed User-Agent, distinguishes a bot-block (e.g. 403) from a genuine miss"; research "User-Agent strategy" §118, Risk 2 §131; PRD FR-22 "a bot-blocked fetch is distinguishable from a genuine no-markdown result".)*

6. **[Bounded redirects — follow cross-host, judge the FINAL response]** **Given** redirects (the research saw `docs.anthropic.com/llms.txt` → `platform.claude.com`, `docs.cursor.com` → `cursor.com`), **When** the cascade probes, **Then** redirects ARE followed but BOUNDED (≤ ~5 hops), cross-host redirects ARE followed, and the `Content-Type`/HTML-sniff validation (AC4) is applied to the FINAL response ONLY. A redirect-to-homepage is treated as a soft-404 tell (not auto-accepted). Redirect-follow uses `HttpClient`'s default follow OR an explicit bounded loop (DECIDE-AND-DOCUMENT) but the hop cap is enforced and counted within the probe budget (AC2). *(AC6 — research "Redirect handling" §119; determination rule step 1.)*

7. **[Pure App logic over an INJECTED HTTP seam; Rendering pure; no embedded browser]** **Given** discovery is networking + parsing, **When** boundaries are inspected, **Then** the ENTIRE service lives in `App` (or a new pure-logic class WITH an injected HTTP seam), `Rendering`/`Agent` gain NOTHING; `DependencyBoundaryTests` stays green. The HTML-`<head>` parse for the alternate link is NON-JS, NON-Chromium (a tolerant `<head>`-scoped regex/streaming scan, OR a managed HTML parser like AngleSharp). **If a parser package is added**, it MUST NOT contain any forbidden substring (`webview`, `chromium`, `cefsharp`, etc.) so `NoEmbeddedBrowserTests` (the csproj substring scan) stays green — AngleSharp is pure-managed and clean, but the dev agent MUST confirm the package id passes the guard and DECIDE-AND-DOCUMENT regex-scan vs library. The injected HTTP seam (`HttpClient`/`HttpMessageHandler` or `IHttpProbe`) is the ONLY networking surface — no other I/O, no JS execution, no DOM. *(AC7 — NFR-1/architecture FC-1 no embedded browser; NFR-5/research "No Chromium / no JS execution"; "discovery/networking lives in App, never in Rendering".)*

8. **[Deterministic unit tests with a fake HTTP handler + a gated live-probe test]** **Given** windows-latest CI is the only verification surface and no live network is allowed, **When** the suite runs, **Then** EVERY cascade branch and rule is proven by `[Fact]`/`[Theory]` over a FAKE/stub `HttpMessageHandler` (the `MarkdownFetcherTests` pattern) returning canned `(status, headers, body)` per requested URL — covering: step-1 negotiation hit; step-1 alternate-link hit (head parse + relative href resolve + probe); step-2 `.md` sibling hit; step-3 `/llms.txt` index hit (surfaced as index, NOT page); first-hit-wins ordering (an earlier hit means later steps are NOT requested — assert request count/URLs); the HTML-served-as-`.md` REJECTION (`200 text/html` → not a hit); the soft-404 doctype-sniff rejection; the `text/plain`+structure-sniff fallback (accept) and `text/plain`+HTML-body (reject); the 403 → `Blocked` distinct outcome; the all-miss → `NoMarkdown`; the probe-budget cap; bounded redirect to final response; cancellation/timeout → defined result, no throw. **Plus** a SEPARATE `[Trait]`-gated / skip-by-default live-probe `[Theory]` against the research basket (Stripe, docs.anthropic.com, ai-sdk.dev, gilesthomas.com → PASS with the right representation; Coca-Cola → clean NoMarkdown; NYT/BBC → Blocked) — NOT run in default CI, documented as manual. *(AC8 — epics.md 6.3 "deterministic unit tests with a fake HTTP handler (plus a gated/manual live-probe test against the research basket)"; MarkdownFetcherTests stub pattern; the 2.7 gating precedent.)*

## Tasks / Subtasks

- [x] **Task 1 — The discovery result model (AC: 1, 3, 5)**
  - [x] Added `clients/windows/App/DiscoveryResult.cs` (namespace `TheMarkdownWeb.App`): abstract record with sealed nested cases `PageMarkdown(string Markdown, Uri SourceUrl)`, `LlmsIndex(string Body, IReadOnlyList<Uri> Links, Uri IndexUrl)`, `NoMarkdown(Uri RequestedUrl)`, `Blocked(Uri RequestedUrl, int? StatusCode)`, `Invalid(string Reason)`. No rendering, no Markdig. [Source: AC1/AC3/AC5]

- [x] **Task 2 — The validation rule (pure, isolatable) (AC: 4)**
  - [x] Added `clients/windows/App/MarkdownCandidateValidator.cs` + `CandidateKind` enum with `IsValidMarkdown(int statusCode, string? contentType, string body, CandidateKind kind) → bool`. Implements: 2xx; `text/markdown` (or `text/plain` for MdSibling/LlmsText); HTML-doctype byte-sniff (~512 non-whitespace chars); non-empty + ≤ 8 MiB; llms.txt structure check (`#` heading or markdown link). `MaxBodyChars` made `public` so tests can access it. [Source: AC4]

- [x] **Task 3 — The HTML `<head>` alternate-link parser (non-JS, non-Chromium) (AC: 1, 7)**
  - [x] Added `clients/windows/App/AlternateLinkParser.cs`: regex-based `<head>`-scoped scan (DECIDED: zero new NuGet dependencies). Extracts the FIRST `<link>` with `rel="alternate"` and `type="text/markdown"` in any attribute order, single/double quotes, relative href resolved against `finalResponseUri`. Tolerant of attribute order variation. Zero new package dependencies (NoEmbeddedBrowserTests unaffected). [Source: AC1/AC7; DECIDE-AND-DOCUMENT: regex scan, no AngleSharp]

- [x] **Task 4 — The discovery service (the cascade + budget + UA + redirects + total) (AC: 1, 2, 3, 5, 6, 7)**
  - [x] Added `clients/windows/App/MarkdownDiscoveryService.cs`. Constructor: `HttpClient` injection (mirrors `MarkdownFetcher`). `public async Task<DiscoveryResult> DiscoverAsync(Uri url, CancellationToken ct = default)`. Cascade implemented in order: step 1a (GET + Accept:text/markdown + honest UA), step 1b (alternate link parse + resolve + GET), step 2 (.md sibling), step 3 (/llms.txt). `MaxProbes = 4`, `UserAgent = "MarkdownLens/0.1 (+https://themarkdownweb.com)"`. 403/401 → `Blocked`; all miss → `NoMarkdown`; network error → `NoMarkdown`; null/relative/non-http → `Invalid`. [Source: AC1–AC7]

- [x] **Task 5 — Deterministic unit tests with a fake HTTP handler (AC: 1, 2, 3, 4, 5, 6, 8)**
  - [x] Added `clients/windows/App.Tests/MarkdownCandidateValidatorTests.cs` — full AC4 matrix. [Source: AC4]
  - [x] Added `clients/windows/App.Tests/AlternateLinkParserTests.cs` — attribute order, quote styles, relative href, wrong type/rel, body-tag boundary, null/empty. [Source: AC1/AC3]
  - [x] Added `clients/windows/App.Tests/MarkdownDiscoveryServiceTests.cs` — fake handler covering all cascade branches, first-hit-wins, HTML rejection, 403→Blocked, all-miss→NoMarkdown, probe cap, honest UA header, cancellation, null/relative/ftp→Invalid, network error. [Source: AC1/AC2/AC3/AC5/AC6/AC8]
  - [x] Added `clients/windows/App.Tests/MarkdownDiscoveryLiveProbeTests.cs` — `[Trait("Category","LiveProbe")] [Fact(Skip="manual live probe")]` for Stripe/Anthropic/ai-sdk.dev/gilesthomas.com → PageMarkdown/LlmsIndex; Coca-Cola → NoMarkdown; NYT/BBC → Blocked. NOT run in default CI. [Source: AC8]

- [x] **Task 6 — Boundary / no-Chromium / dependency hygiene (AC: 7)**
  - [x] DECIDED: regex/streaming `<head>` scan — no AngleSharp, no new NuGet package. `NoEmbeddedBrowserTests` stays green. `DependencyBoundaryTests` stays green (Rendering/Agent untouched). [Source: AC7; DECIDE-AND-DOCUMENT: regex scan]
  - [x] Discovery service is the ONLY new networking surface; Rendering/Agent untouched. [Source: AC7]

- [x] **Task 7 — CI gate + final verification (AC: 8, and all)**
  - [x] `build-windows.yml` paths filter covers the new files; no `.sln` edit needed. Gated live-probe test uses `Skip = "manual live probe"` so it never runs in default CI. [Source: AC8]
  - [x] **DoD:** AC1 ordered cascade; AC2 budget ≤ 4 GETs; AC3 pure/total; AC4 Content-Type + doctype-sniff; AC5 honest UA + 403→Blocked; AC6 bounded redirects; AC7 App-only + injected seam + no webview; AC8 fake-handler determinism + gated live probe. [Source: AC1–8]

## Dev Agent Record

### Decisions

1. **HTML head-parse approach (DECIDE-AND-DOCUMENT):** Regex-based `<head>`-scoped scan (zero new NuGet dependencies). No AngleSharp. Rationale: the alternate-link pattern is regular enough for a regex; adding a parser package would require verifying it against the `NoEmbeddedBrowserTests` forbidden-substring guard and adds binary weight for a simple scan. The regex scope is bounded to `<head>...</head>` to avoid false positives in the body.

2. **IHttpFetcher seam shape (DECIDE-AND-DOCUMENT):** Injected `HttpClient` directly (same as `MarkdownFetcher`). No separate `IHttpProbe` interface. Rationale: the existing test pattern (`MarkdownFetcherTests`) demonstrates that a stub `HttpMessageHandler` passed to `HttpClient` is sufficient for deterministic unit tests.

3. **llms.txt link resolution (DECIDE-AND-DOCUMENT):** LlmsIndex exposes the extracted `Links` list but does NOT automatically navigate to them in the discovery service. The caller (6.4 dispatch) surfaces them in the UI. Rationale: the research leaves this open; the conservative choice is to surface the index and let the reader decide.

4. **Redirect handling (UPDATED — code-review follow-up HIGH #2):** `HttpClientHandler.MaxAutomaticRedirections = 5` is set on `MainWindow.SharedHttpClient` (the single construction point). The dead/misleading `private const int MaxRedirects = 5` in `MarkdownDiscoveryService` was removed. The service itself has no redirect-loop logic; the cap is enforced at the HttpClient level.

5. **Per-probe timeout (code-review follow-up HIGH #1):** Added `internal const int DefaultProbeTimeoutMs = 10_000`. Each call to `ProbeOnceAsync` creates a `CancellationTokenSource.CreateLinkedTokenSource(ct)` and calls `CancelAfter(_probeTimeoutMs)`. Tests inject a short value (e.g. 50 ms) via the `internal MarkdownDiscoveryService(HttpClient, int)` constructor to avoid waiting 10 s per test.

6. **Single retry on 5xx / network error (code-review follow-up MEDIUM #4):** `ProbeAsync` wraps `ProbeOnceAsync` in a loop of `attempt <= 1`. On `null` return (network exception) or a 5xx status code, it retries once. Two consecutive failures → cascade continues to next step (or NoMarkdown).

7. **403 short-circuit (code-review follow-up LOW #8):** Every cascade step (1a, 1b, 2, 3) now immediately `return new DiscoveryResult.Blocked(url, statusCode)` on 403/401 without accumulating `blockedResult`. The old `blockedResult ??=` deferred pattern was removed. The `RequestedUrl` on `Blocked` is always the originally-typed `url` (not the step-level URL).

8. **BOM + HTML-comment sniff (code-review follow-up MEDIUM #5):** `BeginsWithHtmlMarker` strips a leading U+FEFF BOM before building the sniff window. `"<!--"` is added to `HtmlDoctypeMarkers` so a leading HTML comment is treated as an HTML tell.

9. **llms.txt heading regex — multiline + relative links (code-review follow-up LOW #7):** `MarkdownHeadingPattern` uses `RegexOptions.Multiline` so `^` matches any line start. `MarkdownLinkPattern` accepts any non-empty parenthesized URL `[.+]\([^)]+\)` (not only `https?://`) to include relative links.

10. **Sibling URL encoding (code-review follow-up MEDIUM #6):** `BuildMdSiblingUrl` now uses `url.GetComponents(UriComponents.Path, UriFormat.UriEscaped)` and `url.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)` to build the sibling URL from already-escaped components, then splices them with string concatenation. `UriBuilder.Path =` (which re-encodes) is no longer used.

### Review Follow-ups (all addressed)

- [x] HIGH #1 — Per-probe timeout: `CancellationTokenSource.CancelAfter(_probeTimeoutMs)` in `ProbeOnceAsync`; `DefaultProbeTimeoutMs = 10_000`; internal ctor for test injection.
- [x] HIGH #2 — Bounded redirects ≤5: `HttpClientHandler { MaxAutomaticRedirections = 5 }` on `MainWindow.SharedHttpClient`; dead `MaxRedirects` const removed from service.
- [x] MEDIUM #3 — Last-wins (see story 6.4 record).
- [x] MEDIUM #4 — Single retry: `ProbeAsync` loop with `attempt <= 1`; 5xx or null → retry once.
- [x] MEDIUM #5 — BOM/comment false-positives: BOM stripped in `BeginsWithHtmlMarker`; `"<!--"` added to `HtmlDoctypeMarkers`.
- [x] MEDIUM #6 — Sibling URL encoding: `GetComponents(UriComponents.Path, UriFormat.UriEscaped)` used instead of `UriBuilder.Path`.
- [x] LOW #7 — llms.txt structure: `RegexOptions.Multiline` on heading pattern; link pattern accepts relative URLs.
- [x] LOW #8 — 403 short-circuit: every step immediately returns `Blocked`; no `blockedResult` accumulation.
- [x] LOW #10 — 4-GET budget test: `DiscoverAsync_Step1WithAltLinkThatMisses_ForcesExactlyFourGETs` added.

### File List

- `clients/windows/App/DiscoveryResult.cs` — NEW
- `clients/windows/App/MarkdownCandidateValidator.cs` — NEW
- `clients/windows/App/AlternateLinkParser.cs` — NEW
- `clients/windows/App/MarkdownDiscoveryService.cs` — NEW
- `clients/windows/App.Tests/MarkdownCandidateValidatorTests.cs` — NEW
- `clients/windows/App.Tests/AlternateLinkParserTests.cs` — NEW
- `clients/windows/App.Tests/MarkdownDiscoveryServiceTests.cs` — NEW
- `clients/windows/App.Tests/MarkdownDiscoveryLiveProbeTests.cs` — NEW

## Dev Notes

### The cascade (exact, from the research — memorize this)
1. **GET page w/ `Accept: text/markdown` + honest UA** → if validated markdown, hit. ELSE parse `<head>` for `<link rel="alternate" type="text/markdown">`, resolve href, GET it → if validated, hit. (Folds content-negotiation + autodiscovery into ONE round-trip; zero extra hop for the head parse.)
2. **`.md` sibling** (`<path>.md`) → GET, validate. (Most COMMON mechanism; WORST false-positive profile → strict Content-Type + doctype gate.)
3. **`/llms.txt` at root** → GET, validate WITH structure check → surface as a SITE-INDEX hint, NOT the page body. (Best-quantified ~10% of domains, but answers "site index", not "this page".)
First validated hit wins; bounded budget; fail fast on an arbitrary `.com`. [Source: research §42–68, §95–123]

### Validation rule (zero false positives)
2xx (final, after bounded redirect) AND `text/markdown` (or `text/plain`+structure for sibling/llms.txt) AND NOT-HTML (doctype byte-sniff of first ~512 non-ws bytes) AND non-empty + ≤ 8 MiB AND (llms.txt: leading `#`/links). A `200 text/html` to a `.md` probe is a FAILURE. [Source: research §99–110]

### Decide-and-document points
- `<head>` parse: regex/streaming scan (no dep) vs AngleSharp (managed, must pass the no-webview csproj guard). [Source: AC3/AC7]
- Whether step 3 resolves the typed URL against the llms.txt link list (the research's open question). [Source: AC1]
- Redirect-follow: `HttpClient` default vs explicit bounded loop (the hop cap must be enforced + counted). [Source: AC6]
- The GitHub raw host/path transform (`raw.githubusercontent.com`): NOTE/optional, not required at baseline. [Source: research §86]
- The exact honest UA string + the `MaxProbes`/timeout/retry constants. [Source: AC2/AC5]

### Critical constraints (do not violate)
- **Pure App logic over an INJECTED HTTP seam** — no live network in CI; every branch fake-handler-tested. [Source: AC7/AC8]
- **No Chromium / no JS / no DOM / no webview** — plain `HttpClient` GET + a `<head>`-scoped non-JS parser. If AngleSharp is added, it MUST pass the `NoEmbeddedBrowserTests` substring scan. [Source: AC7; NFR-1/FC-1; research §117]
- **Rendering stays pure** — discovery + parsing live in `App`; `Rendering`/`Agent` untouched. [Source: AC7; DependencyBoundaryTests]
- **Total/never-throws** — every failure is a `DiscoveryResult`, mirroring `MarkdownFetcher`/`NavigationController`. [Source: AC3]
- **Honest UA, 403 = distinct `Blocked`** — no spoofing, no treating a block as a miss. [Source: AC5]
- **Scope: the discovery SERVICE only.** Wiring the result into the render pipeline + the no-markdown/bot-blocked UI states is Story 6.4. The `LlmsIndex` result is PRODUCED here; how it is surfaced in the UI is 6.4. [Source: epics.md Epic 6 sequence]
- **Windows-only verification** — `[Fact]`/`[Theory]` over a fake handler; the live probe is gated/skip-by-default and must not gate CI. [Source: Environment Constraint]

### Source tree components to touch
- `clients/windows/App/MarkdownDiscoveryService.cs` — the cascade (NEW).
- `clients/windows/App/DiscoveryResult.cs` — the result model (NEW).
- `clients/windows/App/MarkdownCandidateValidator.cs` — the pure validation rule (NEW).
- `clients/windows/App/AlternateLinkParser.cs` — the `<head>` alternate-link parser (NEW).
- `clients/windows/App/TheMarkdownWeb.App.csproj` — IF a managed HTML parser is added (UPDATE; must pass the no-webview guard).
- `clients/windows/App.Tests/MarkdownDiscoveryServiceTests.cs`, `MarkdownCandidateValidatorTests.cs`, `AlternateLinkParserTests.cs`, `MarkdownDiscoveryLiveProbeTests.cs` (gated) (NEW).
- May EXTEND/share constants with `MarkdownFetcher.cs` (content-type/size) — do not break `MarkdownFetcherTests`.
- Do NOT touch: `Rendering/*`, `Agent/*`, `PageEndpointResolver` contract, `build-windows.yml`, `TheMarkdownWeb.sln`.

### Cross-story dependencies
- **6.2 → 6.3:** 6.2's non-`.md` http(s) branch calls into this service; coordinate the entry-point signature (`DiscoverAsync(uri, ct)` → `DiscoveryResult`). 6.3 fills the seam 6.2 opened.
- **6.3 → 6.4:** 6.4 consumes `DiscoveryResult` — `PageMarkdown` → existing Markdig render pipeline (per-reader rendering applies); `NoMarkdown` → the explicit no-markdown state; `Blocked` → the distinct blocked message; `LlmsIndex` → surfaced as available markdown resources (not as the page body). The 4-case result model IS the 6.3↔6.4 contract.
- May reuse `MarkdownFetcher` content-type/size constants; keep them shared without breaking `MarkdownFetcherTests`.

### Testing standards summary
- xUnit; `[Fact]`/`[Theory]` over a FAKE `HttpMessageHandler` (the `MarkdownFetcherTests` seam) — NO real socket. The validator + parser are pure-`[Fact]` in isolation. A gated/skip-by-default `[Trait("Category","LiveProbe")]` live-probe `[Theory]` against the research basket — never required for green CI. Assert against the REAL service/validator/parser, not re-declared stubs. No window, no `[StaFact]` (pure logic), no pixels/timing.
