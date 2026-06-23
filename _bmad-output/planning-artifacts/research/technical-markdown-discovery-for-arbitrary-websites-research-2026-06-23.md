---
stepsCompleted: ['web-research', 'empirical-probes', 'synthesis']
inputDocuments: ['_bmad-output/planning-artifacts/epics.md', 'clients/windows/App/MarkdownFetcher.cs', 'clients/windows/App/PageEndpointResolver.cs', 'api/negotiate/negotiate.mjs']
workflowType: 'research'
lastStep: 1
research_type: 'Technical Research'
research_topic: 'Reliable protocol for a native desktop client to discover and fetch a markdown representation of an arbitrary website URL (de-risking Epic 6 — Windows WPF "Markdown Lens")'
research_goals: 'Validate an ordered discovery cascade (alternate link → content negotiation → convention probes), estimate real-world hit rate with evidence, and define the "no markdown available" determination rule for a non-Chromium native client.'
user_name: 'naethyn'
date: '2026-06-23'
web_research_enabled: true
source_verification: true
---

# Research Report: Technical Research

**Date:** 2026-06-23
**Author:** naethyn
**Research Type:** Technical Research
**Topic:** Markdown discovery & fetch protocol for arbitrary websites (Epic 6 — "Markdown Lens")

---

## Research Overview

**Question.** If a user points the native Windows client (WPF "Markdown Lens", Epic 6) at *any* website URL (e.g. a marketing `.com`), can the client reliably (a) discover whether a markdown representation exists, and (b) fetch it — using plain HTTP, no Chromium/WebView (NFR-1), reusing the existing Markdig render and the Story 2.7 content-negotiation contract (FR-14)? When no markdown exists, the client must say *"no markdown available"* with no HTML fallback.

**Method.**
- **A) Web research** (WebSearch) into the `llms.txt` spec and quantified adoption, the "append `.md`" serving convention (Mintlify, GitHub, SSGs), real-world `<link rel="alternate" type="text/markdown">` usage, content-negotiation-for-markdown practice, and known pitfalls (soft-404s, HTML-served-as-`.md`, User-Agent / bot blocking, robots/rate-limit politeness).
- **B) Empirical probes** (WebFetch) against a basket of 11 real sites — known adopters (Stripe, Anthropic/Claude docs, Cursor docs, Vercel AI SDK, Cloudflare, Vercel, Mintlify) and ordinary sites (NYT, BBC, Coca-Cola, a small personal blog, a GitHub repo page) — recording pass/fail per signal.

**Hard tool constraints honored in the findings.** WebFetch (1) upgrades HTTP→HTTPS, (2) **cannot send a custom `Accept` header**, and (3) answers via a small model over the *already-markdown-converted* page. Consequences, stated explicitly where they bite:
- The `Accept: text/markdown` content-negotiation signal **could not be live-tested**; it is assessed from documented vendor behavior + the project's own server contract.
- For `.md`-sibling probes, the small model sees converted-to-markdown content either way, so it cannot by itself distinguish *raw markdown served by the origin* from *HTML the origin rendered then WebFetch converted*. Where a probe returned tell-tale **raw source artifacts** (MDX/JSX components like `<Tip>`/`<Card>`, frontmatter) I treat it as confirmed raw markdown; otherwise I mark it **unverifiable via this tool**.

The native client has **none** of these limitations — it is plain `HttpClient`, so it CAN set `Accept`, read the real `Content-Type`, and byte-sniff the body. The protocol below is designed for that client, not for WebFetch.

---

## Executive Summary

**Recommendation — implement an ordered, first-hit-wins cascade, but invert the user's hypothesis order on signals 1 vs 3.** The native client should run, per URL:

1. **GET the page URL, `Accept: text/markdown` + parse `<link rel="alternate" type="text/markdown">` from the returned HTML head** — fold content-negotiation and alternate-link autodiscovery into the *one* request you already have to make.
2. **`.md` sibling probe** — append `.md` to the path (the dominant real convention; Mintlify, many SSGs, GitHub raw).
3. **`/llms.txt`** at the site root — *as a capability hint / index, not a page renderer* (it describes the site; it is rarely the markdown of the page the user typed).

**Headline hit-rate finding: "point at any `.com`" is NICHE, not universal.** Markdown availability today is concentrated in **developer-facing documentation** (docs platforms, dev-tool SaaS, technical blogs). Across the open web, an `llms.txt` exists on roughly **8–10%** of domains ([SE Ranking, 300k-domain study](https://seranking.com/blog/llms-txt/): **10.13%**; [BuiltWith via Presenc](https://presenc.ai/research/state-of-llms-txt-2026): **~844k sites** Oct 2025), and a *page-level* `.md` sibling is rarer still outside Mintlify-class docs. My evidence-based estimate for an arbitrary, non-docs URL yielding usable **page** markdown is **single-digit percent**; for a docs/dev-tool URL it is **high (~70–90%+)**. So Markdown Lens is best positioned as **"a great reader for the markdown-native web (docs/dev sites), with a clean 'no markdown available' for everything else"** — not as a universal HTML-to-markdown lens. This matches the product's no-HTML-fallback stance.

**Three load-bearing risks (detail in Risks section):** (1) **soft-404 / HTML-served-as-`.md`** false positives — must be defeated by `Content-Type` check + doctype/HTML sniff; (2) **non-browser blocking** — NYT and BBC both refused our non-browser fetch outright, so UA strategy and graceful-degradation matter; (3) **`llms.txt` is a site index, not page markdown** — treating it as the answer to "give me *this page* in markdown" produces wrong content.

---

## Per-Signal Analysis

| Signal | How the native client detects it | Adoption / feasibility | Estimated hit rate (evidence + confidence) | False-positive & failure risks |
|---|---|---|---|---|
| **1a. Content negotiation** `Accept: text/markdown` on the page URL | Set `Accept: text/markdown` (client already does this — `MarkdownFetcher.cs:73`); accept only `2xx` whose real `Content-Type` is `text/markdown` | **Feasible, low ambient adoption.** Documented on Mintlify ("send `Accept: text/markdown` or `text/plain` to any page URL") and is the project's own Story 2.7 contract (`Vary: Accept`). Few *general* sites honor it. | **Docs platforms: medium-high. Arbitrary web: ~low single digits.** Confidence: **Medium** (could NOT live-test — WebFetch can't send `Accept`; relying on [Mintlify docs](https://www.mintlify.com/docs/ai/markdown-export) + Cloudflare guidance + project contract). | Servers that ignore `Accept` return HTML `200` → must reject on `Content-Type`. Some CDNs `Vary`-cache incorrectly and cross-serve. |
| **1b. `<link rel="alternate" type="text/markdown">` autodiscovery** | Parse the `<head>` of the HTML already fetched in step 1a; resolve `href` (may be relative) | **Standards-clean, growing, still rare.** `rel=alternate` is WHATWG-spec since HTML4; `text/markdown` is RFC 7763. Real implementations exist: WordPress plugin (Joost.blog), Drupal markdownify module, Eleventy recipes, personal blogs. | **Low overall, but high-precision when present.** Confidence: **Medium-high** on mechanism; **Low** on prevalence (no large quantified census found; evidence is example-level, not %-level). | Stale/incorrect `href`; relative-URL resolution bugs; sites that point the alternate at a non-markdown file. Low false-positive rate because it's explicit. |
| **2. `.md` sibling probe** (`<path>.md`) | GET `pageUrl + ".md"`; verify `Content-Type` + sniff body isn't HTML | **The dominant real convention.** [Mintlify auto-serves every page at `.md`](https://x.com/mintlify/status/1889358844847071660); GitHub exposes raw `.md` via raw.githubusercontent.com; many SSGs (11ty/Kitty Giraudel) emit `.md` siblings. **Empirically confirmed raw on a live cross-host probe** (see below). | **Docs/dev sites: high (~70–90%). Arbitrary web: low.** Confidence: **High** for docs platforms (direct probe + vendor docs); **High** that it's low elsewhere (ordinary sites have no `.md` build step). | **Highest false-positive risk:** SPA/catch-all routers and soft-404s return `200 text/html` for any `*.md` path. MUST gate on `Content-Type` AND HTML-doctype sniff. `.md` on a non-docs path often 404s cleanly (good — Coca-Cola/Cursor returned clean 404s). |
| **3. `/llms.txt` (+ `/llms-full.txt`)** at site **root** | GET `https://host/llms.txt`; if markdown, treat as a *capability hint / index of markdown URLs* — optionally follow links | **Best-quantified signal. ~10% of domains.** [SE Ranking: 10.13% of 300k domains](https://seranking.com/blog/llms-txt/); [BuiltWith ~844k sites](https://presenc.ai/research/state-of-llms-txt-2026); [community directory ~784 sites mid-2025](https://presenc.ai/research/state-of-llms-txt-2026). Confirmed present at Stripe, Anthropic, Cloudflare, Vercel, AI-SDK. | **~8–10% of *domains* have one; far fewer map to the *page* the user typed.** Confidence: **High** on the 10% domain figure (300k-domain study); **High** that it answers "site index" not "this page". | **Semantic false positive:** it is a site-level index, NOT the markdown of the requested page — rendering it as "the page" is wrong content. Soft-404 roots (homepage served at `/llms.txt`) — sniff for markdown structure. **Note caveat:** major AI crawlers barely request it ([408 of 500M+ bot hits in 90 days](https://seranking.com/blog/llms-txt/)) — adoption ≠ usage, but for *our* discovery purpose presence is what matters. |

### Recommended order — and *why*

- **Fold 1a+1b into one request.** You must GET the page to do anything; sending `Accept: text/markdown` *and* parsing the returned head for an alternate link costs **zero extra round-trips** and catches the two highest-precision signals first. If the origin honors negotiation you're done in one hop; if not, the same response's HTML head may hand you the exact `.md` href (no guessing).
- **`.md` sibling second.** It's the single most *common* mechanism in the markdown-native corner of the web and is a deterministic, cheap second probe — but it has the worst false-positive profile, so it sits behind the explicit signals and behind a strict `Content-Type`/doctype gate.
- **`/llms.txt` last and demoted in meaning.** It's the best-*quantified* signal but it answers a *different question* (site index, not page). Use it to (a) confirm the *site* is markdown-capable and (b) optionally resolve the typed URL to a listed `.md`. Do not render `/llms.txt` itself as "the page."
- **First-hit-wins, bounded.** Stop at the first signal that yields verified `text/markdown`. Cap total probes (see budget) so an arbitrary `.com` fails fast and cheap.

---

## Empirical Probe Results

Probes run 2026-06-23 via WebFetch (HTTPS-only, no custom `Accept`, small-model body read). Legend: ✅ confirmed · ❌ confirmed absent/blocked · ⚠️ present-but-unverifiable-raw-vs-rendered via this tool · — not separately tested.

| Site | `/llms.txt`? | `.md` sibling? | md `alternate` link? | Notes |
|---|---|---|---|---|
| **stripe.com** | ✅ valid markdown (`# Stripe`, `> …`, `[Full Documentation](…/llms-full.txt)`) | — | — | Textbook `llms.txt` index; also advertises `llms-full.txt`. |
| **docs.anthropic.com** | ✅ (301 → `platform.claude.com/docs/llms.txt`, valid markdown index, ~1,752 EN pages) | ✅ **raw confirmed**: `…/docs/en/docs/intro.md` returned **raw MDX** (`<Tip>`, `<Card>`, tables) — origin-served source, not HTML | — | Cross-host redirect on `llms.txt` — client must follow redirects. Strongest single confirmation of the `.md` convention. |
| **docs.cursor.com** | ❌ (308 → `cursor.com/docs`, then `/docs/llms.txt` = **404**) | — | — | Redirect chain then no `llms.txt`; clean 404 (good negative). |
| **ai-sdk.dev (Vercel AI SDK)** | ✅ markdown index (`# AI SDK`, "prefer targeted Markdown pages") | ⚠️ `…/docs/introduction.md` — WebFetch couldn't confirm raw vs rendered (tool limitation) | — | Index explicitly steers consumers to per-page `.md`. |
| **cloudflare.com** | ✅ valid markdown (`# Cloudflare`, `> …`) | — | — | Marketing-adjacent but dev-heavy company; full `llms.txt`. |
| **vercel.com** | ✅ valid markdown (points to `…/docs/llms-full.txt`) | — | — | `# Vercel Documentation` index. |
| **mintlify.com/docs** | — | ⚠️ `…/quickstart.md` — WebFetch couldn't confirm raw vs rendered | — | Vendor docs state `.md` + `Accept: text/markdown` both supported platform-wide ([Mintlify](https://www.mintlify.com/docs/ai/markdown-export)). |
| **gilesthomas.com (small blog)** | ✅ site-level `/llms.txt` | ✅ per-post `.md` (e.g. `/2025/03/llmstxt.md`) | ✅ **`<link rel="alternate" type="text/markdown">` in head** | **All three signals on one small personal blog** — the model adopter; proves the full cascade is real, not just big-vendor. |
| **github.com (repo blob)** | — (n/a per-repo) | ✅ raw via `…/raw/refs/heads/<branch>/<path>` → `raw.githubusercontent.com` | ❌ | GitHub needs a **host/path transform**, not a naïve `.md` append; special-case if desired. |
| **nytimes.com** | ❌ **"unable to fetch"** (non-browser request refused) | — | — | **Bot/UA block signal** — ordinary big-media site, no markdown + actively blocks non-browser clients. |
| **bbc.com** | ❌ **"unable to fetch"** (non-browser request refused) | — | — | Same as NYT — blocked outright. |
| **coca-cola.com** | ❌ clean HTTP 404 | — | — | Ordinary marketing `.com`: no `llms.txt`, clean negative. |

**Read of the basket.** Every confirmed positive is a **developer/docs property**. Every *ordinary* site (Coca-Cola, NYT, BBC) returned **no markdown** — two of them *blocked the non-browser client entirely*. This is the empirical backbone of the "niche, not universal" conclusion and validates a fast, clean "no markdown available" path.

---

## The "No Markdown Available" Determination Rule

Goal: avoid **false negatives** (missing markdown that's really there) and **false positives** (rendering HTML/soft-404 garbage as if it were markdown). Declare *"no markdown available"* only after the bounded cascade fails **every** verified check.

**A candidate response counts as REAL markdown only if ALL hold:**
1. **HTTP status is `2xx`** after following redirects (bounded, see below). A `3xx` to another host is followed once per hop up to the cap.
2. **`Content-Type` media type is `text/markdown`** (case-insensitive; ignore `charset`). This already gates `MarkdownFetcher.cs:84-89`. **`text/plain` is accepted only as a weak fallback** for `/llms.txt` and `.md` siblings where servers misconfigure the type — and *only* if the body also passes the markdown-structure sniff below.
3. **Body is NOT HTML.** Sniff the first ~512 non-whitespace bytes: reject if it begins with `<!doctype html`, `<html`, `<head`, `<?xml`, or a `<body`/`<script`/`<meta` cluster (defeats SPA catch-alls and soft-404s that return `200 text/html`).
4. **Body is non-empty and within size bound** (8 MiB cap already enforced, `MarkdownFetcher.cs:48`).
5. **For `/llms.txt` specifically:** require minimal markdown structure (a leading `#` heading and/or markdown links) so a homepage soft-served at `/llms.txt` is rejected; and remember it is an **index**, not the requested page.

**Soft-404 / HTML-as-`.md` defenses (the two nastiest false positives):**
- The `Content-Type` check + doctype sniff together kill the common "router returns the SPA shell (`200 text/html`) for any unknown path" case.
- A `.md` URL that returns `200` but with `Content-Type: text/html` is a **failure**, not a hit (current code already does this).
- Prefer servers that 404 cleanly (observed: Cursor, Coca-Cola) — a clean 404 is a *trustworthy* negative.

**Declare "no markdown available" when:** the page GET (with `Accept: text/markdown`) yields non-markdown AND has no `text/markdown` alternate link AND the `.md` sibling fails checks 1–4 AND `/llms.txt` is absent/non-markdown — OR the host blocks the client (network refusal / 403 to non-browser UA). Surface blocking distinctly if possible ("site blocked the request") vs. plain absence, for honest UX.

---

## Desktop-Client Specifics (non-Chromium, plain HTTP + parse)

- **No Chromium / no JS execution (NFR-1).** Everything is `HttpClient` GET + a lightweight HTML-head parser (e.g. AngleSharp or a tolerant regex/streaming scan limited to `<head>`) for the alternate-link signal, then Markdig to render the fetched markdown (FR-14, already wired in `Rendering`). No DOM, no script — so JS-only "markdown" routes are out of scope by design (acceptable; the markdown-native web serves static files).
- **User-Agent strategy.** Send a **descriptive, honest UA** identifying the client (e.g. `MarkdownLens/0.1 (+https://themarkdownweb.com)`). Two empirically observed realities: (a) some sites refuse unknown non-browser clients outright (NYT, BBC) — an honest UA won't fix a hard block, and spoofing a browser is fragile + impolite; (b) treat a refusal/`403` as a clean "no markdown available (site blocked request)". Respect `robots.txt` for the probe paths where practical; keep an allowlisted, low-volume footprint.
- **Redirect handling.** Follow redirects but **bound them** (e.g. ≤ 5 hops) and follow cross-host redirects (required: `docs.anthropic.com/llms.txt` → `platform.claude.com`, `docs.cursor.com` → `cursor.com`). Re-apply the `Content-Type`/sniff checks on the *final* response only. Watch redirect-to-homepage as a soft-404 tell.
- **Timeouts.** Short per-request timeout (e.g. **5 s connect / 10 s total**) so a dead probe doesn't stall the cascade; the whole discovery should fail within a few seconds for a no-markdown site.
- **Probe budget / politeness.** **First-hit-wins, hard cap on requests per user action.** Worst case ≈ **4 GETs**: (1) page w/ `Accept: text/markdown` (also yields the head for the alternate link), (2) `.md` sibling, (3) `/llms.txt`, (4) optional one resolved `.md` from the llms.txt index. No retries on `4xx`; single retry only on transient network/`5xx`. No concurrent fan-out against the same host.
- **Caching.** Cache **negative** results per-origin (TTL ~ hours) so re-typing a no-markdown `.com` is instant and doesn't re-probe; honor `ETag`/`Last-Modified` and `Cache-Control` for positive markdown fetches (WebFetch itself caches 15 min — mirror that politeness). Cache the per-origin *capability* ("this host has llms.txt / honors negotiation") to skip dead branches next time.
- **Reuse, don't rebuild.** `MarkdownFetcher` (the `Accept`/`Content-Type`/size discipline) and `PageEndpointResolver` (URL→endpoint mapping, host policy) are the right seams to generalize from the app-host-only case to arbitrary hosts; extend rather than fork.

---

## Risks & Open Questions

**Top risks**
1. **Soft-404 / HTML-served-as-`.md` false positives.** Catch-all routers and SPAs return `200 text/html` for `*.md`. *Mitigation:* `Content-Type` + doctype/HTML byte-sniff (rule above). This is the #1 correctness risk.
2. **Non-browser blocking & anti-bot.** NYT and BBC refused the request entirely; Cloudflare/CDN bot-management can `403` an unknown UA even when a markdown file exists. *Mitigation:* honest UA, bounded politeness, treat blocks as a distinct "no markdown (blocked)" outcome; do not spoof.
3. **`llms.txt` = site index ≠ page markdown.** Rendering `/llms.txt` as "the page the user typed" shows the wrong content. Also, adoption ≠ crawler usage ([408 of 500M+ bot requests](https://seranking.com/blog/llms-txt/)) — but for *discovery* presence is sufficient. *Mitigation:* treat it as a capability hint/index, optionally resolve the typed URL against its link list.

**Other risks:** content-negotiation could not be live-verified with WebFetch (Medium confidence — validate in the spike with real `HttpClient`); GitHub needs a special host/path transform (raw.githubusercontent.com), not a naïve `.md` append; relative-`href` resolution bugs on alternate links; `text/plain` mislabeling forcing reliance on body-sniffing.

**Open questions:** Should the client follow `llms.txt` links automatically, or only use it as a yes/no capability flag? Is `llms-full.txt` ever worth fetching (large)? How aggressively to honor `robots.txt` for a *user-initiated, single-page* fetch (arguably not a crawler)?

**Recommended thin validation slice (smallest spike that proves the protocol on real sites):**
A headless .NET console spike (no WPF, no UI) that takes a URL and runs the exact cascade with a **real `HttpClient`** (so `Accept: text/markdown` IS sent and real `Content-Type` IS read):
1. GET page w/ `Accept: text/markdown` → check `Content-Type`; parse `<head>` for `<link rel="alternate" type="text/markdown">`.
2. GET `<path>.md` → `Content-Type` + HTML-sniff gate.
3. GET `/llms.txt` → markdown-structure check.
Run it against this exact basket (Stripe, Anthropic docs, ai-sdk.dev, gilesthomas.com, Coca-Cola, NYT). **Success criterion:** correct PASS on every dev/docs adopter (with the *right* representation), correct "no markdown available" on the ordinary/blocked sites, and **zero false positives** (no HTML rendered as markdown). This both proves the negotiation branch WebFetch couldn't test and de-risks Epic 6's core promise before any WPF work.

---

## Sources

- [SE Ranking — LLMs.txt: Why Brands Rely On It and Why It Doesn't Work (300k-domain study, 10.13% adoption; 408 of 500M+ bot hits)](https://seranking.com/blog/llms-txt/)
- [Search Engine Journal — LLMs.txt Shows No Clear Effect On AI Citations, Based On 300k Domains](https://www.searchenginejournal.com/llms-txt-shows-no-clear-effect-on-ai-citations-based-on-300k-domains/561542/)
- [Presenc AI — State of llms.txt 2026 (BuiltWith ~844k sites; community directory ~784 sites; adopter trajectory; IDE-agent usage)](https://presenc.ai/research/state-of-llms-txt-2026)
- [Codersera — llms.txt Explained (May 2026): Spec, Adoption, How to Ship One](https://codersera.com/blog/llms-txt-complete-guide-2026/)
- [Mintlify — Markdown export (append `.md`; `Accept: text/markdown` / `text/plain` content negotiation)](https://www.mintlify.com/docs/ai/markdown-export)
- [Mintlify on X — "Introducing .md support … just append .md to the URL"](https://x.com/mintlify/status/1889358844847071660)
- [Giles Thomas — Adding /llms.txt (alternate link + per-post `.md` + site `/llms.txt` on a small blog)](https://www.gilesthomas.com/2025/03/llmstxt)
- [Kitty Giraudel — Serving Markdown to LLMs With Eleventy (`<link rel="alternate" type="text/markdown">` + `.md` siblings)](https://kittygiraudel.com/2026/03/11/serving-markdown-to-llms-with-11ty/)
- [Evil Martians — Making your site visible to LLMs: 6 techniques that work, 8 that don't](https://evilmartians.com/chronicles/how-to-make-your-website-visible-to-llms)
- [Joost.blog — My WordPress take on Markdown for Agents (`rel=alternate type=text/markdown` on every page)](https://joost.blog/markdown-alternate/)
- [Drupal.org — markdownify: Output `<link rel="alternate" type="text/markdown">` on HTML entity](https://www.drupal.org/project/markdownify/issues/3512568)
- [WHATWG HTML Standard — Links (rel=alternate)](https://html.spec.whatwg.org/multipage/links.html)
- [Goodie — LLMs.txt & Robots.txt: Optimizing for AI Bots (UA blocking, robots interplay)](https://higoodie.com/blog/llms-txt-robots-txt-ai-optimization/)
- [robotstxt.com — AI / LLM User-Agents: Blocking Guide](https://robotstxt.com/ai)
- [DataDome — How to Block AI bots, LLMs, scrapers and crawlers (anti-bot reality)](https://datadome.co/learning-center/block-ai-bots/)
- [earezki.com / Dev Journal — I Audited 30 llms.txt Files in the Wild: 5 Anti-Patterns (soft-404 / quality pitfalls)](https://earezki.com/ai-news/2026-05-20-i-audited-30-llmstxt-files-in-the-wild-5-anti-patterns-are-already-forming/)
- Live probes (WebFetch, 2026-06-23): stripe.com/llms.txt, docs.anthropic.com/llms.txt → platform.claude.com, platform.claude.com/docs/en/docs/intro.md, ai-sdk.dev/llms.txt, cloudflare.com/llms.txt, vercel.com/llms.txt, docs.cursor.com (404), coca-cola.com/llms.txt (404), nytimes.com / bbc.com (blocked), gilesthomas.com, github.com/microsoft/vscode.
- Project sources: `clients/windows/App/MarkdownFetcher.cs`, `clients/windows/App/PageEndpointResolver.cs`, `api/negotiate/negotiate.mjs` (Story 2.7 content-negotiation contract, `Vary: Accept`, `text/markdown`).
