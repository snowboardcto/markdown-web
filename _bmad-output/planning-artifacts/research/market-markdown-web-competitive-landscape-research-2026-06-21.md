---
stepsCompleted: [1, 2]
inputDocuments: ['_bmad-output/the-markdown-web.md', '_bmad-output/brainstorming/brainstorming-session-2026-06-20-233910.md']
workflowType: 'research'
lastStep: 1
research_type: 'market'
research_topic: 'The Markdown Web — competitive landscape ("who else is out there")'
research_goals: 'Identify adjacent and competing players across markdown publishing, digital gardens, PKM-that-publishes, .md hosting, and the agentic/AI-rendered web; map where each stops and where the Markdown Web white space is.'
user_name: 'naethyn'
date: '2026-06-21'
web_research_enabled: true
source_verification: true
---

# Research Report: Market — The Markdown Web Competitive Landscape

**Date:** 2026-06-21
**Author:** naethyn
**Research Type:** Market (competitive landscape)

---

## Research Overview

Goal: answer "who else is out there?" for **The Markdown Web** (themarkdownweb.com) — a paradigm where URLs point to `.md` files and the reader's own local AI agent renders presentation per-user. Scope set by naethyn: competitive/adjacent landscape sweep. Categories examined: markdown publishing & `.md` hosting, digital gardens, PKM-that-publishes, AI/agent-rendered web & "agentic web" protocols.


## Competitive Landscape — "Who Else Is Out There"

The space splits into **four layers**. The Markdown Web touches all four — but only sits *uniquely* in the gap between them.

### Layer 1 — Markdown publishing / digital gardens (PKM → static site)
Mature and crowded. Markdown in → **one fixed author-controlled look** out. No per-reader personalization.
- **Obsidian Publish** — polished paid (~$8–10/mo) vault → public knowledge site. [unmarkdown](https://unmarkdown.com/blog/obsidian-publish-alternatives)
- **Quartz** — free/OSS static-site generator; backlinks, graph, search; closest feature match to Obsidian Publish. [github](https://github.com/jackyzha0/quartz) · [site](https://quartz.jzhao.xyz/)
- **Flowershow** — Obsidian vault → site, cheaper tier. [xda](https://www.xda-developers.com/flowershow-is-fantastic-free-alternative-obsidian-publish/)
- **Static site generators** — Hugo, Jekyll, Eleventy, MkDocs; free, markdown → site.
- **Obsidian plugins** — Share Note, Invio, Enveloppe, JotBird.

### Layer 2 — ".md URL → rendered page" / markdown hosting (MOST DIRECT analog to v0.1/v0.2)
- **Hosted.md** — markdown-native hosted docs platform; connect a repo → site; flat monthly fee, custom domains. Squarely on the v0.2 "drop a folder, get a site" turf (docs-flavored). [hosted.md](https://hosted.md/)
- **Docsify-This** — paste a public `.md` URL → standalone rendered shareable page. Closest to the literal "URLs to .md files." [docsify-this.net](https://docsify-this.net/)
- **HackMD** — collaborative markdown editor; Publish → public URL. [hackmd](https://homepage.hackmd.io/)
- **GitHub Pages / Gists / md-hoster / Couscous / MkDocs / markdown.space** — render/host `.md`. [handoff](https://handoff.host/blog/publish-markdown-as-web-page/)

### Layer 3 — The agentic web / "markdown is the agent format" standards (VALIDATES the thesis; also commoditizes the plumbing)
The dual-serve architecture naethyn designed (#11/#14 — HTML for browsers, markdown via content negotiation) is **already an emerging 2025–26 industry standard** — but aimed at *machines*, not personalized humans.
- **llms.txt** (Jeremy Howard, Sept 2024) — markdown briefing for AI at domain root; 600+ adopters incl. Anthropic, Perplexity, Stripe, Cloudflare. [stellar](https://stellar-ai.co/blog/llms-txt-ai-search-optimization/)
- **Cloudflare "Markdown for Agents"** — `Accept: text/markdown` → edge converts HTML→markdown; cuts tokens ~80%. [cloudflare](https://blog.cloudflare.com/markdown-for-agents/)
- **Content negotiation, same URL** — Vercel, Mintlify, Sentry, Sanity, an nginx module, acceptmarkdown.com all ship "HTML for browsers / markdown for agents." [vercel](https://vercel.com/blog/making-agent-friendly-pages-with-content-negotiation) · [deployhq](https://www.deployhq.com/blog/making-your-documentation-ai-friendly-serving-markdown-to-ai-coding-assistants)
- **AGENTS.md** (OpenAI) + **MCP** (Anthropic → Agentic AI Foundation, Feb 2026) — agent-facing standards.
- **The format debate** — Karpathy & Anthropic's "Unreasonable Effectiveness of HTML" vs markdown. Consensus emerging: *"If your reader is a model, Markdown. If your reader is you, HTML."* — naethyn's bet flips the second half. [beam](https://beam.ai/agentic-insights/html-vs-markdown-which-format-actually-makes-ai-agents-more-useful) · [digiday](https://digiday.com/media/wtf-is-markdown-for-ai-agents/)

### Layer 4 — Agent-as-frontend / AI browsers / Generative UI (the GIANTS near Stage 3)
Per-user agent-rendered experiences — but **not from a markdown content web**; they reformat existing HTML or generate UI from app/agent data.
- **Google A2UI / Generative UI** (Dec 2025) — agent emits declarative JSONL UI across trust boundaries; renders natively on Web/Flutter/Android/iOS. The big-tech "agent is the front end." [google](https://research.google/blog/generative-ui-a-rich-custom-visual-interactive-user-experience-for-any-prompt/) · [a2ui](https://developers.googleblog.com/a2ui-v0-9-generative-ui/)
- **Perplexity Comet** — AI browser; transforms any page, highlight-to-explain, personalized. [perplexity](https://www.perplexity.ai/hub/blog/introducing-comet)
- **The Browser Company Dia**, **ChatGPT Atlas**, **Opera Neon**, **Brave Leo** — AI-native browsers reformatting the web per user. [beam](https://beam.ai/agentic-insights/ai-browsers-are-here-comet-dia-and-the-coming-battle-for-the-web)
- **CopilotKit / GenUI** — generative-UI frameworks. [copilotkit](https://www.copilotkit.ai/generative-ui)

## The White Space (where the Markdown Web is genuinely alone)

Every existing player picks ONE side:
- **Layers 1–2** make markdown addressable but render **one fixed look the author controls** — no per-reader agent.
- **Layer 3** serves markdown to **machines to save tokens** — the human still gets author-rendered HTML.
- **Layer 4** does **per-user agent rendering**, but **not from a `.md` content web** — they chew on HTML/app data.

**No one sits in the intersection:** *markdown as the canonical, URL-addressable content layer **AND** the reader's own local agent rendering the human-facing presentation, per person.* A direct search for a startup doing "personalized agent rendering of markdown per reader" surfaced **none**. That intersection — "**Zero Shared Pixels for humans**" — is the defensible position.

## Threats & Strategic Implications

1. **The plumbing is commoditizing fast.** Content negotiation + md/HTML dual-serve + markdown hosting are being given away by Cloudflare/Vercel/Mintlify and owned by Hosted.md/Quartz/Obsidian Publish. ⇒ **Do NOT position as "host your markdown" or "serve md to agents."** That race is over.
2. **The Stage-3 client is attackable by giants.** Comet/Dia/Atlas/A2UI already have the client + the agent; "render this `.md` URL beautifully for you" is a feature they could ship. ⇒ Moat is **not tech**; it's **category ownership ("The Markdown Web"), the publish+read loop, an opinionated beautiful client, and community.**
3. **The wedge is the safe harbor.** "Store + view + share *my own* markdown, gorgeous, per-reader" (the personal vault / BMAD-output use case) is under-served by all four layers and needs no network to be useful.
4. **Position on the human, not the machine.** The whole industry says "markdown for models, HTML for humans." Your one-line wedge into the conversation: *"What if markdown were for humans too — rendered by **their** agent?"*

**Bottom line:** The thesis is validated by a fast-moving market (good — you're not early-and-alone in a dead space), the exact intersection you want is unclaimed (good), but the surrounding plumbing is commoditizing and the client layer has deep-pocketed entrants (caution). Win by owning the **category + the human-facing rendering experience + the wedge**, not the infrastructure.
