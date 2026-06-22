# Story 5.2: Follow / Feed

Status: ready-for-dev

## Story

As a reader,
I want to follow a vault and receive its new pages,
so that I can keep up with authors I value.

## Context (Epic 5 — post-MVP; STATIC feed, NO backend — the chosen architecture)

FR-16 (Follow / Feed): "follow a vault and receive its new pages as a feed." The repo is a static SWA with no backend/auth/storage, and the architecture commits to no-server-side-rewrite (NFR-5) + a BYO/local client. The chosen mechanism (decided) therefore is **static + client-side**:
- **The feed is GENERATED AT BUILD TIME** from the vault by Astro (publish-on-push, FR-17) — no runtime service, no database, no auth, no cost.
- **"Following" lives in the NATIVE CLIENT** as a local subscription list (the reader's own machine), and the client POLLS the followed vaults' feeds and surfaces their pages. No server tracks who follows what.

This splits cleanly into a **web half** (generate the feed) and a **native half** (follow list + feed view), matching the two verification paths (web = local Playwright/build; native = windows CI).

## Decided scope (binding)

- **IN (web):** an Astro-built **vault feed** generated from the vault content at build time — a machine-readable JSON Feed (`feed.json`, JSON Feed 1.1) AND a standard **RSS/Atom** XML feed, listing the vault's `.md` pages with title, canonical `.md` URL, and a stable date (published/updated). Linked from the site (a `<link rel="alternate">` in `<head>` + a visible "Feed" affordance on the vault index).
- **IN (native):** a **Follow** feature in the WPF client — persist a local list of followed vault feed URLs (reuse the Epic-4 local-storage discipline; settings/JSON under `%LOCALAPPDATA%`, NOT DPAPI — feed URLs are not secrets), a **Feed view** that fetches + parses the followed feeds (the existing `MarkdownFetcher`/`HttpClient` seam, pure parser) and lists the vault's pages newest-first, and activating an item opens that `.md` in the client's existing reader (Story 3.5 navigation). "New pages surface" = the feed lists current pages; the client marks items seen vs unseen by comparing against the last-fetched set (local state).
- **OUT (Deferred-Work-Log):** server-side follow tracking; push notifications; auth/accounts; cross-device sync; real-time updates (the client polls/refreshes on demand or on open); web-side feed READING UI (the feed is consumed by the native client + standard feed readers — a web feed-reader page is out); per-page diffing beyond seen/unseen.

## Architecture conformance

- **No backend / NFR-5:** the feed is a static build artifact; following is client-local. Nothing rewrites content server-side; nothing new runs at runtime on the server.
- **Boundary (Epic 3/4 D3):** the feed PARSER is pure (feed text → ordered items; no WPF, no net) and lives in a testable seam (Agent or Rendering — pure module); the HTTP fetch + the WPF Feed view + the follow-list persistence live in App (App owns I/O, like fetch/image/browser/speech). Rendering stays pure.
- **No embedded browser (NFR-1/FC-1):** the Feed view is native WPF (a list), not an HTML webview.
- **CI discipline (Epic 3/4 D4):** native verification is windows-latest only (Linux dev box has no .NET SDK); the feed parser is a pure `[Fact]`; the fetch is faked (stub `HttpMessageHandler`); the follow-list persistence is faked (in-memory store behind an interface); WPF Feed view is `[StaFact]` construct-not-Show. No real network/disk/socket in CI. The web feed generation is verified LOCALLY (Playwright + `astro build`).

## Acceptance Criteria

### Web half (generated feed) — verified locally (Playwright + build)

1. **[A vault feed is generated at build time]** **Given** the vault content, **When** `astro build` runs, **Then** the site emits a **JSON Feed** (`/feed.json`, JSON Feed 1.1: `version`, `title`, `home_page_url`, `feed_url`, `items[]`) AND an **RSS/Atom** feed (`/feed.xml` or `/rss.xml`), each listing the vault's `.md` pages with: a title, the page's canonical `.md` URL, and a stable date. Proven by a Playwright/build test that loads the built feed file(s) and asserts the structure + that a known vault page appears with the right URL + title.

2. **[The feed is a stable, well-formed contract]** The JSON Feed validates against JSON Feed 1.1 required fields; the XML feed is well-formed and item links are absolute canonical `.md` URLs (the same shareable living-link URLs from Story 5.1). The feed is discoverable: a `<link rel="alternate" type="application/feed+json">` (and the RSS `application/rss+xml`) in the site `<head>`, plus a visible "Feed" link on the vault index page. Proven by a Playwright test (assert the `<head>` alternate links resolve + the index "Feed" affordance is present and points at the feed).

3. **[Feed reflects the vault's pages; new pages appear after a rebuild]** **Given** a new `.md` page added to the vault, **When** the site rebuilds (publish-on-push), **Then** the new page appears as a feed item. Proven by a build/test fixture: adding a fixture page yields a corresponding feed item (test against a fixture vault, not prod).

### Native half (follow + feed view) — verified on windows CI

4. **[A reader can follow a vault by its feed URL; follows persist locally]** **Given** the native client, **When** the reader follows a vault (enters/adds its feed URL), **Then** the feed URL is saved to a local follow list that survives restart. Proven by a `[Fact]` over a `FollowListStore` seam (in-memory fake in CI; real impl writes JSON under `%LOCALAPPDATA%\TheMarkdownWeb\follows.json`): add/remove/list/dedupe; persistence round-trips via the fake.

5. **[The feed parser is pure and total]** **Given** feed text (JSON Feed and/or RSS/Atom), **When** the parser runs, **Then** it returns the vault's items (title, `.md` URL, date) in feed order. Pure (no WPF/net), total (malformed/empty → empty list, never throws), deterministic. Proven by pure `[Fact]`s over fixtures (a JSON Feed fixture + an RSS fixture → expected items; malformed → empty; ordering preserved).

6. **[The Feed view lists followed vaults' pages and opens them]** **Given** one or more followed vaults, **When** the reader opens the Feed view, **Then** the client fetches each followed feed (faked `HttpClient` in CI), parses it, and lists the pages newest-first; activating an item opens that `.md` in the existing reader (Story 3.5 navigation). Items are marked seen/unseen by comparing to the last-fetched set (local state) so "new pages" are visibly surfaced. Proven by `[Fact]`/`[StaFact]`: a fake fetcher returns fixture feeds → the Feed view model lists the expected items in order, unseen flagged; activating an item invokes the navigation seam with the item's `.md` URL. Construct-not-Show; fetch faked.

7. **[Totality + boundary + no regression]** No key needed (feeds are public; not secrets). A fetch failure for one followed vault degrades gracefully (that vault shows an error/empty, others still list; never crashes). The parser is pure; Rendering untouched/pure; no embedded browser; the follow-list + Feed view live in App. The existing 3.x/4.x windows-CI suite stays green; the existing web Playwright suite stays green. Proven by the inherited purity/boundary/no-webview guards + a `[Fact]` for the per-vault fetch-failure isolation.

## Tasks / Subtasks

### Web half
- [ ] (AC1/AC3) Add an Astro endpoint/build step generating `/feed.json` (JSON Feed 1.1) from the vault content collection — items = vault `.md` pages with title + canonical `.md` URL + stable date (frontmatter date or git/last-modified fallback).
- [ ] (AC1/AC3) Add `/feed.xml` (RSS 2.0 or Atom) from the same source (reuse an Astro RSS helper if available, else hand-roll well-formed XML).
- [ ] (AC2) Add `<link rel="alternate">` discovery tags in the site `<head>` (JSON Feed + RSS) and a visible "Feed" affordance on the vault index page.
- [ ] (AC1/AC2/AC3) Playwright/build tests: feed files exist + validate (JSON Feed required fields; XML well-formed); a known/fixture page appears with absolute canonical URL; head alternate links + index "Feed" affordance present.

### Native half
- [ ] (AC5) Add a PURE `FeedParser` (feed text → ordered `FeedItem`s {Title, MarkdownUrl, Date}) in a pure module (Agent or Rendering — no WPF/net); total over malformed/empty. Pure `[Fact]` fixtures (JSON Feed + RSS).
- [ ] (AC4) Add a `FollowListStore` seam in App (interface + real `%LOCALAPPDATA%\TheMarkdownWeb\follows.json` impl + in-memory CI fake): add/remove/list/dedupe, round-trip.
- [ ] (AC6) Add a `FeedViewModel` + a WPF Feed view (a native list, NOT a webview): fetch each followed feed via the existing fetcher seam (faked in CI), parse, merge, sort newest-first, flag unseen vs last-fetched set; activating an item invokes the existing navigation seam with the `.md` URL.
- [ ] (AC6) Add a toolbar/menu affordance to open the Feed view + follow/unfollow the current vault (labeled, keyboard-reachable, per UX-DR9).
- [ ] (AC7) Per-vault fetch-failure isolation (one bad feed doesn't break the view); boundary/purity guards extended; no new project; csproj edits minimal.
- [ ] (AC7) Confirm windows-latest CI green (build + all tests) and the web Playwright suite green.

## Verification

- **Web half:** LOCAL on Linux — `cd web && npx astro build` (emits feed.json + feed.xml) + `npx playwright test` (feed structure/discovery/fixture tests green).
- **Native half:** windows-latest CI ONLY (build-windows.yml) — pure `FeedParser` `[Fact]`s, faked-fetch `FeedViewModel` `[Fact]`/`[StaFact]`, `FollowListStore` `[Fact]`; no real network/disk/socket; `[StaFact]` construct-not-Show for the Feed view.

## Dev Notes

- **Size (DECIDED — keep whole):** this is the larger Epic-5 story (web feed gen + a real native feature). The user reviewed the Size-M flag and chose to **keep 5.2 as one story** (do NOT split into 5.2a/5.2b). Implement the web half first (the static feed is the contract), then the native half consumes it. The two halves still verify on different paths (web = local Playwright/build; native = windows CI) — implement + verify the web half green before starting the native half, so the native feed-consumption is built against a known-good feed contract.
- **Dates:** prefer explicit frontmatter `date`/`updated`; fall back to a deterministic source (committed file mtime is NOT reliable in CI — prefer frontmatter or a content-hash-stable ordering). Feed ordering must be deterministic for the test.
- **Parser placement:** the pure `FeedParser` mirrors `ReadingOrderExtractor` (Story 4.4) — pure, in a no-WPF module, `[Fact]`-tested; the fetch + view + persistence are App (I/O owner). Reuse the stub `HttpMessageHandler` pattern for the fetch tests.
- **Follow-state is NOT a secret** → plain JSON under `%LOCALAPPDATA%`, not DPAPI (DPAPI is for the API key only).
- **"New pages surface":** MVP = the feed lists current pages, the client flags items not in the last-seen set as unseen. No push; the reader refreshes/opens the Feed view to pull. This satisfies FR-16's "surfaces to me as part of the vault's feed" without a backend.
