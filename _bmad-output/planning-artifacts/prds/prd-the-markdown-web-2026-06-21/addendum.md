# Addendum — The Markdown Web PRD

Tech-how and downstream depth kept out of the capability-level PRD. For architecture/solution-design (Winston) and UX (Sally).

## Mechanism / transport decisions (from brief + research)
- **Hosting / serving:** Azure (brief decision). Recommended: **Azure Static Web Apps** — global CDN, free SSL, custom domain (themarkdownweb.com), GitHub Actions CI/CD. Rationale in brief.
- **Serving strategy (Stage 1):** "static now, smart server later" — pre-build `.md` → HTML at deploy time for v0.1 (FR-5, FR-17); add request-time content negotiation (FR-14) when the native client lands.
- **Rendering engine (HTML client):** off-the-shelf for v0.1 (e.g. markdown-it / Astro / Eleventy), with a path to a custom engine as the product matures. Decision deferred to architecture.
- **Content negotiation (FR-14):** HTTP `Accept` header → HTML for browsers, `text/markdown` for agents/native client; `Vary: Accept` for caches. This is an established 2025–26 pattern (Cloudflare "Markdown for Agents," Vercel, Mintlify) — see market research. Implication: do not over-invest in building novel plumbing here; the differentiator is per-reader *human-facing* rendering, not the negotiation itself.
- **Linking (FR-2):** relative `.md` links rewritten to routes at build (HTML client) and resolved by the native client at render time.
- **Native client (FR-9–FR-13):** must run the reader's local agent and "work everywhere"; form factor (extension / app / OS-agent / CLI / web) is an open architecture decision (PRD §8.2).

## Competitive / defensibility context (carried from brief addendum)
- Plumbing (markdown hosting, content negotiation) is commoditizing; native-client layer is attackable by Comet/Dia/Atlas/A2UI. Moat is non-technical: category ownership, the publish↔read loop, an opinionated beautiful client, community, the personal-vault wedge. (Detail: brief addendum + market research.)

## Source artifacts
- Brief + addendum: `_bmad-output/planning-artifacts/briefs/brief-the-markdown-web-2026-06-21/`
- Manifesto: `_bmad-output/the-markdown-web.md`
- Market research: `_bmad-output/planning-artifacts/research/market-markdown-web-competitive-landscape-research-2026-06-21.md`
- Brainstorm: `_bmad-output/brainstorming/brainstorming-session-2026-06-20-233910.md`
