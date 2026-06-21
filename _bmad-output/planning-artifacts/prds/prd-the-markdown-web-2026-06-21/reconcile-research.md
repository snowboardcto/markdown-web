# Research Reconciliation — Competitive Landscape vs PRD/Addendum

**Source:** `_bmad-output/planning-artifacts/research/market-markdown-web-competitive-landscape-research-2026-06-21.md`
**Targets:** `prd.md`, `addendum.md` (in `prds/prd-the-markdown-web-2026-06-21/`)
**Date:** 2026-06-21

## Method
Read the market-research report and both target docs. Credited what the targets already reflect, then isolated competitive realities, positioning constraints, and non-goals the research implies but the PRD/addendum do not capture. Gaps below are only the genuinely-missing items.

## What the targets already capture (no gap)
- Plumbing commoditizing → "do not over-invest in novel content-negotiation plumbing" (addendum §Mechanism, FR-14 note; addendum §Competitive context).
- Moat is non-technical: category ownership, publish↔read loop, opinionated client, community, personal-vault wedge (addendum §Competitive context).
- Native-client layer attackable by Comet/Dia/Atlas/A2UI (addendum §Competitive context).
- Not a universal AI browser; markdown-native, not whole-HTML-web reformatter (PRD §2.2, §5).

## Gaps to capture

### Gap 1 — No explicit non-goal: "don't reinvent the commoditizing plumbing" as a scoping rule, not just a caution
The research's strongest strategic instruction (Threat #1) is a hard *don't*: "Do NOT position as 'host your markdown' or 'serve md to agents' — that race is over." The addendum carries this as a soft engineering note ("do not over-invest"), and the rendering engine is "off-the-shelf for v0.1." But the PRD's **§5 Non-Goals** list — the canonical place a scoping rule belongs — has nothing forbidding the team from building bespoke markdown-hosting or content-negotiation infrastructure. Layer-2 incumbents (Hosted.md, Docsify-This, HackMD, GitHub Pages) already own "drop a folder / paste a `.md` URL → rendered page," which is almost exactly the v0.1 HTML-client slice. A non-goal like "Not novel hosting/serving infrastructure — use commodity plumbing (Cloudflare/Vercel/Azure patterns, off-the-shelf renderers); engineering effort goes to human-facing per-reader rendering" would make the research's #1 directive structurally binding instead of advisory.

### Gap 2 — Missing positioning constraint: the v0.1 HTML slice is the most-commoditized, least-defensible part — and is what SM-1/SM-2 measure
The research is explicit that Layers 1–2 (fixed author-controlled rendering of addressable markdown) are "mature and crowded" and that the *only* defensible position is the intersection — per-reader rendering by the reader's own agent ("Zero Shared Pixels for humans"). Yet the PRD's primary success metrics SM-1 and SM-2 validate exactly the commoditized HTML slice (FR-1–8, 17–18 — "beautiful static markdown site"), while the differentiating capability is SM-3 (tertiary in framing, last to ship). Nothing in the PRD warns that early traction on SM-1/SM-2 is *table stakes that incumbents already deliver* and is not evidence of a moat — only SM-3 tests the defensible claim. This is a positioning risk worth a note: success on the HTML slice must not be read as product-market fit for the thesis.

### Gap 3 — Missing risk: "render this `.md` URL beautifully for you" is a feature giants can ship, with no time-based moat noted
Research Threat #2 states the Stage-3 client is "attackable by giants" because Comet/Dia/Atlas/A2UI already hold both the client and the agent — "render this `.md` URL beautifully for you' is a feature they could ship." The addendum names the attackers but states the moat is "category ownership / publish↔read loop / community" *without* acknowledging the implied corollary: those moats are **non-technical and time-sensitive** — they must be established *before* a giant ships the feature, because the technical capability confers no lead time. The PRD's large-MVP note and HTML-first sequencing actually *delay* the differentiating SM-3 capability and the publish↔read/community loop, which is the only thing the research says is defensible. The tension between "build the safe HTML slice first" and "the moat must be planted before incumbents move" is unaddressed in either doc.

### Gap 4 (optional) — Thesis-validation framing absent: standards (llms.txt, Cloudflare, MCP, AGENTS.md) prove the bet but aim at machines
The research's Layer 3 finding is double-edged and load-bearing for positioning: the dual-serve architecture naethyn designed is *already an emerging 2025–26 industry standard* (llms.txt, Cloudflare Markdown-for-Agents, content negotiation across Vercel/Mintlify/Sentry, MCP, AGENTS.md) — which both **validates the thesis** ("you're not early-and-alone in a dead space") and **confirms the plumbing is given away free**. The industry consensus — "markdown for models, HTML for humans" — is the exact sentence this product is built to flip ("markdown for humans too, rendered by *their* agent"). Neither target states this one-line wedge/positioning frame, though it is the report's recommended way into the conversation (Threat #4). Capturing it would anchor the product's "why now / why us" against the named standards rather than leaving it implicit in the Vision prose.

## Note
Gaps 1–3 are concrete and recommended. Gap 4 is positioning/framing (lower priority — arguably belongs in the brief, not the PRD). No rewrites proposed; no edits made to PRD or addendum.
