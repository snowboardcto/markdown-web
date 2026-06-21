# The Markdown Web

**The web, rewritten in markdown — where you publish meaning, and the reader's own AI renders it for them.**

> One source file. A different, personal experience for every single person who opens it. By design.

---

## The Idea

Today's web couples **content** and **presentation**. The author writes the words *and* dictates exactly how they look — fonts, layout, colors, the works. Your browser just obeys.

The Markdown Web breaks that coupling.

- **Authors publish `.md`** — pure markdown: structure and meaning, nothing about appearance.
- **Readers bring an agent** — their own AI, running locally, renders that markdown into whatever shape fits *them*.

A URL points to a markdown file. Open it, and **your** agent decides how it should look, read, and feel — based on you. The CLI native gets a crisp terminal readout. A casual visitor gets warm, photo-rich scrollytelling. Someone who's blind gets clean audio. A Spanish speaker gets it translated on render. Same `.md`. Different worlds.

**Agents already prefer markdown.** We've been translating everything to HTML for human eyes. The Markdown Web stops translating — and lets each human's agent be the renderer.

---

## Why It Matters

**Accessibility and translation become free.** When presentation is the reader's agent's job, a dyslexic reader's agent reflows and re-fonts, a low-literacy reader gets a 6th-grade version, a blind reader gets audio, any language renders on demand — *with zero extra work from the author.* What costs teams millions today becomes an automatic property of the system.

**The reader is sovereign.** Your agent can reorder, summarize, expand, omit — even pre-answer a page before you read it. No limit. The web finally adapts to you instead of the other way around.

**And you can trust it** — because the agent doing the rendering is *yours*, running locally, acting on your behalf. There's no platform between you and the page distorting it. Personalization is self-service, not imposed. You trust it the way you trust your own notes.

---

## How It Works

A single `.md` URL serves two audiences, gracefully:

1. **Browsers** get clean, server-rendered **HTML** — works everywhere, crawlable, SEO-friendly, no agent required. This is the on-ramp: the Markdown Web is *born compatible* with today's web.
2. **The native client** gets the raw markdown and lets the reader's local agent render it directly — no HTML at all. This is the magic tier.

Every HTML page is also a **doorway**: it quietly advertises how to open the same content in the client for the personalized experience. The content spreads on the old web while the agent layer grows underneath it.

---

## The Wedge: Your Markdown, Beautiful

The grand vision starts as a selfishly useful personal tool:

> *"I have a pile of markdown files. Let me store them, browse them, and make them look awesome."*

Drop a folder of `.md` + images/video, and get a browsable, beautiful space — every file a page, links between them just working, one toggle to publish. Your private vault and your published site are the *same files*, different visibility.

**Customer zero is real and immediate:** the markdown artifacts an AI-driven workflow already generates — briefs, plans, notes, this very document — finally have a gorgeous home. *(This page was born as one of those files.)*

---

## Sharing

Turn the vault outward and the thesis meets other people:

- **The Living Link** — text someone a `.md` URL; *their* agent renders it for *them*. You wrote once; everyone got their version.
- **Markdown as "send me the doc"** — kill the PDF/Google-Doc handoff. Share the `.md`; the recipient can read it, fork it, remix it, re-share their version.
- **The Markdown Feed** — follow someone's vault; new posts flow to your agent, rendered your way. RSS reborn with a universal format.
- **Portable & sovereign** — you share files, not an account. No platform lock-in, no link rot. The raw `.md` survives the service that delivered it.

---

## What It Unlocks

- **Free accessibility & translation** (the mission-grade headline)
- **Skill-adaptive docs** — beginners get hand-holding, experts get the API signature, from one source
- **The reshaping résumé** — one `resume.md`; each recruiter's agent surfaces what matters to them
- **Commerce your way** — product pages as `.md`; the spec-shopper gets a table, the vibe-shopper gets a photo wall
- **Read-it-for-me triage** — your agent answers the page before you scroll

---

## Roadmap

Foundation → compatibility → magic. Each stage ships independent value and de-risks the next.

| Stage | What ships | Who it's for |
|-------|-----------|--------------|
| **v0.1** *(now)* | BMAD/markdown files rendered beautiful, browsable, live at themarkdownweb.com | You (customer zero) |
| **v0.2** | Drop-a-folder vault: linking + media, one toggle to publish | Early adopters |
| **v0.5** | Server-side `.md`→HTML + content negotiation; the public manifesto & showcase | The world (via search) |
| **v1.0** | Native `.md` client (agent-rendered, no HTML) + sharing/feed network | The believers |

### v0.1 — this week
1. A `content/` folder of `.md` + media, seeded with real files
2. A build step: markdown → HTML, one `.md` = one page
3. Relative `.md` links rewritten to routes — browse around
4. Media embeds that just work
5. A genuinely beautiful default theme *(this is the wow)*
6. Deploy on **Azure Static Web Apps** + themarkdownweb.com + free SSL + GitHub Actions — *push a `.md`, the site updates*

---

## Decisions Locked

- **Name + home:** **The Markdown Web** → **themarkdownweb.com** *(owned)*
- **Architecture:** dual-path — server HTML for browsers, native client for agents
- **Trust model:** the reader's own local agent (ownership + locality, not author constraint)
- **Serving (Stage 1):** *static now, smart server later* — static HTML on Azure first; content-negotiation server and native client layer on top, nothing wasted
- **Client requirement:** must work everywhere; must run the user's local agent

## Open Questions

- 💰 **Business model** — how it sustains and wins
- 🔍 **Identity & discovery** — how people find each other's markdown spaces
- 🖥️ **Client form factor** — extension vs app vs OS-agent vs CLI vs web
- ✍️ **Author incentive** — why authors publish `.md` and surrender presentation control

---

*Forged in a brainstorming session on 2026-06-20 — from "what if every page were a markdown file" to a named, domained, shippable plan.*
