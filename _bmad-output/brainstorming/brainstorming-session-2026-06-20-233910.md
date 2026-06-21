---
stepsCompleted: [1, 2]
inputDocuments: []
session_topic: 'The Markdown Web (themarkdownweb.com) — URLs to .md files where each user''s AI agent renders a personalized front-end'
session_goals: 'Broad divergence across vision/concept, architecture/how, and adoption wedge (option D). Origin: snowboardcto.com where every .md file is a page.'
selected_approach: 'progressive-flow'
techniques_used: ['What If Scenarios','Mind Mapping','SCAMPER','Dream Fusion Laboratory']
ideas_generated: []
context_file: ''
---

# Brainstorming Session Results

**Facilitator:** naethyn
**Date:** 2026-06-20

## Session Overview

**Topic:** The Markdown Web — a paradigm where URLs point to .md files instead of HTML. Content is authored as portable markdown; presentation is generated per-user by *their own* AI agent based on personality, preferences, and context. Origin seed: snowboardcto.com, where every markdown file is a page and adding a new .md creates a new page.

**Goals:** Broad divergence (option D) — explore the vision/concept space, the how (protocols & architecture), and the adoption wedge. Generate 100+ ideas before organizing.

### Session Setup

Core thesis confirmed by naethyn: HTML couples content + presentation; markdown + agents decouples them and hands the presentation layer to the *consumer's* AI rather than the author. Agents already prefer markdown — so stop translating to HTML and let each human's agent be the renderer.

## Technique Selection

**Approach:** Progressive Technique Flow (broad → focused)

**Progressive Techniques:**

- **Phase 1 — Expansive Exploration:** What If Scenarios — break every assumption baked into "the web"; generate wide.
- **Phase 2 — Pattern Recognition:** Mind Mapping — cluster ideas around the central concept; surface territories.
- **Phase 3 — Idea Development:** SCAMPER — refine top concepts through seven lenses.
- **Phase 4 — Action Planning:** Dream Fusion Laboratory — reverse-engineer concrete first moves from the full vision back to a shippable wedge on snowboardcto.com.

**Journey Rationale:** Mirrors how a paradigm matures — break assumptions, find structure, harden the design, land it in reality. The snowboardcto.com seed (every .md = a page) is the real-world launchpad for Phase 4.

## Ideas Generated

### Phase 1 — What If Scenarios

**[Vision #1]: Zero Shared Pixels**
_Concept_: A `.md` URL has no canonical appearance. Two people opening the exact same link see entirely different renderings — a CLI user gets a terminal readout, a casual visitor gets warm photo-rich scrollytelling — because each person's own agent renders the source to fit them. Author publishes meaning; consumer's agent owns presentation.
_Novelty_: Goes beyond responsive design/theming — there is no "master" look the author controls. Presentation is fully delegated to the consumer side. Validated as exciting by everyone naethyn has shared it with.

**[Principle #2]: Total Client Sovereignty (No Limit)**
_Concept_: The consumer's agent has unlimited license to restructure, reorder, summarize, omit, and even add to a markdown page. No author-imposed ceiling. "It gives the client all the power."
_Novelty_: Inverts the entire authority model of the web. Today the server/author controls presentation and the browser obeys. Here the client is sovereign and the author's file is raw material, not a finished product.

**[Use Case #3]: Personal Markdown Vault — "View & Enjoy My Markdowns"**
_Concept_: The grounded, real job-to-be-done: naethyn has a pile of existing markdown files. He wants effortless storage, beautiful viewing, and genuine enjoyment of them — without converting to HTML or fighting a CMS. The file stays markdown; the experience becomes a pleasure.
_Novelty_: Reframes the grand "replace HTML" vision as a personal tool first. The wedge is selfish-useful: it has to delight a single user (the author) before it ever needs a network.

**[Direction #4]: Sharing Layer (expansion focus — to be developed)**
_Concept_: Turning the personal vault outward — how a stored/viewed markdown becomes a shareable artifact whose presentation adapts to whoever opens it.
_Novelty_: Sharing is where personal vault meets the Zero Shared Pixels thesis — the same link delights both sender and recipient, differently.

**[Endorsed]:** naethyn likes sharing ideas #5–#9 (Living Link, Drop-a-Folder, Markdown-as-Send-the-Doc, Markdown Feed, Portable & Sovereign) — all accepted into the concept pool.

**[Principle #10]: Born Compatible — Adoption via Regular HTML**
_Concept_: The Markdown Web cannot require an agent to be useful. Every `.md` URL must also serve a perfectly good plain-HTML rendering for today's browsers, crawlers, link-unfurlers, and agent-less humans — and then *progressively enhance* into the personalized agent-rendered experience when an agent is present. Compatibility is the on-ramp to adoption, not a compromise.
_Novelty_: Treats HTML as the *fallback/distribution layer* rather than the authoring layer. Flips progressive enhancement: markdown is the source of truth, HTML is the lowest-common-denominator projection, agent-rendering is the top tier.

### Phase 1 (cont.) — HTML Bridge angles

**[#11]: Content Negotiation at the URL**
_Concept_: One `.md` URL, many responses. A normal browser's request gets server-rendered HTML; an agent (signaling via Accept header / capability flag) gets the raw markdown to render itself. Same address, audience-appropriate payload.
_Novelty_: Reuses 30-year-old HTTP content negotiation to make the markdown web invisible-by-default and powerful-when-equipped — no new browser required to start.

**[#12]: Crawlable & Unfurlable by Default**
_Concept_: Because there's always a clean HTML projection, markdown pages are natively SEO-friendly and produce rich link previews in Slack/iMessage/social. The new web is discoverable on the old web's terms.
_Novelty_: Solves the chicken-and-egg distribution problem — the content spreads through existing channels while the agent layer grows underneath.

**[#13]: The Upgrade Moment**
_Concept_: An agentless visitor sees a tasteful default HTML page with a subtle "open this with your agent for a personalized view" affordance — the moment that converts a normal web user into a Markdown Web user.
_Novelty_: Builds the adoption funnel *into the artifact itself*; every shared page is a recruiting surface for the paradigm.

**[Decision #14]: Dual-Path Architecture — Server HTML for Browsers, Dedicated Client for Agents**
_Concept_: Settled direction. If someone hits an `.md` URL from a normal browser, the server renders clean HTML (Path A — maximum compatibility, crawlers, no JS dependency). For the personalized experience, "we provide a client" that the user runs to render the markdown with their own agent. Two clearly separated rendering paths off the same `.md` source.
_Novelty_: Author controls nothing about the agent path; the server's HTML is just the universal fallback. The personalization lives entirely in a purpose-built client, keeping the "client has all the power" principle intact without sacrificing day-one compatibility.

**[#15]: The Handoff — HTML Pages Advertise the Client**
_Concept_: When a visitor lands on the server-rendered HTML version, the page needs a built-in, standardized way to show them how to open the same content in our client for the personalized agent experience. Every fallback HTML page is a doorway/recruiting surface that teaches the upgrade path (e.g. a standard banner, a "open in client" affordance, the raw `.md` URL always one step away).
_Novelty_: Makes onboarding a property of the artifact, not a marketing funnel — the content distributes itself on the old web and carries its own instructions for entering the new one.

**[Constraint #16]: "Works Everywhere" Is the Client's Defining Requirement**
_Concept_: The client's form factor is still open (extension / app / OS-agent / CLI / web app), but the non-negotiable requirement is ubiquity — it must work everywhere, on any device/platform. "Works everywhere" is the criterion that will ultimately decide the client's shape.
_Novelty_: Prioritizes reach over richness for the v1 client decision — the paradigm only spreads if the reader side has near-zero friction across all environments.

**[Principle #17]: Trust = Your Own Local Agent**
_Concept_: The trust model for "no limit / client has all the power" is resolved by locality and ownership: the agent that rewrites, reorders, omits, or augments the page is the *reader's own AI agent, running locally from their own client, acting on their behalf*. There is no third party between author and reader doing the manipulation — the reader's agent is an extension of the reader.
_Novelty_: Sidesteps the censorship/distortion fear entirely. You're not trusting the author or a platform to render honestly; you're trusting your own agent the same way you trust your own note-taking. Distortion becomes self-service, not imposed.

**[Endorsed]:** naethyn accepts use-cases #18–#23 (Free Accessibility & Translation, Skill-Adaptive Docs, Reshaping Resume, Commerce-Your-Way, Read-It-For-Me Triage, Portable Lens) — all into the pool. #18 flagged as potential mission-grade / fundable headline.

**[Use Case #24 — Customer Zero: Beautiful BMAD Output Vault]**
_Concept_: naethyn's own grounded need — save his BMAD-generated markdown output files (brainstorming sessions, briefs, PRDs, architecture, retrospectives) somewhere for later viewing, and have them *look awesome*. The output of his AI-driven workflow is markdown; he wants a beautiful home and reader for it.
_Novelty_: Perfect dogfooding wedge — the artifacts this very methodology produces are the first content for the Markdown Web client. The session document being written right now is itself the first test file. Self-referential, immediately useful, zero cold-start.

## Phase 2 — Pattern Recognition (Mind Map)

Central node: **THE MARKDOWN WEB** — URLs to `.md`; author publishes meaning, reader's local agent owns presentation.

**Cluster A — The Thesis (why it's different & why it's safe)**
- #1 Zero Shared Pixels · #2 Total Client Sovereignty · #17 Trust = Your Own Local Agent · #23 The Portable Lens
- *Through-line:* decouple content from presentation; hand presentation to the reader's own local agent. Trust comes from ownership/locality, not author constraint.

**Cluster B — The Wedge / Customer Zero (what we build first)**
- #3 Personal Markdown Vault · #24 Beautiful BMAD Output Vault · #6 Drop-a-Folder, Get-a-World
- *Through-line:* a selfish-useful personal tool — store, view, enjoy your own `.md` (incl. BMAD outputs) and make them look awesome. Delights a single user before any network.

**Cluster C — Sharing & Social / Distribution**
- #5 The Living Link · #7 Markdown as "Send-the-Doc" · #8 The Markdown Feed · #9 Portable & Sovereign sharing
- *Through-line:* personal vault turned outward; same link delights every recipient differently; no platform lock-in.

**Cluster D — Adoption / The HTML Bridge**
- #10 Born Compatible · #11 Content Negotiation at the URL · #12 Crawlable & Unfurlable · #13 The Upgrade Moment · #14 Dual-Path Architecture · #15 The Handoff
- *Through-line:* browser → clean server HTML (works everywhere, crawlable); agent path → our client. Every HTML page is a doorway that advertises the client.

**Cluster E — The Client (biggest open question)**
- #14 Dual-Path (client side) · #16 "Works Everywhere" requirement
- *Through-line:* form factor undecided (extension / app / OS-agent / CLI / web app); must run the user's local agent AND work everywhere. Real design tension.

**Cluster F — Killer Use-Cases / What It Unlocks**
- #18 Free Accessibility & Translation (mission-grade) · #19 Skill-Adaptive Docs · #20 The Reshaping Resume · #21 Commerce Your Way · #22 Read-It-For-Me Triage
- *Through-line:* the "holy cow" applications that HTML can't touch.

**Gaps the map reveals (untouched territory):**
1. **Business model / why this wins** — sustainability, incentives, who pays.
2. **Identity & Discovery** — how people find each other's markdown spaces (feed hints at it; no mechanism yet).
3. **The Protocol / Spec** — conventions for `.md` URLs, content negotiation, the "born compatible" standard.
4. **Author incentive** — why would authors publish `.md` and surrender presentation control?

## Phase 3/4 — Development & Roadmap (Cluster E + build sequence)

**[#25 — The Serving Layer (Azure)]**
_Concept_: Foundation stage. Host the `.md` files on Azure, but not as isolated documents — with **inter-file linking** (markdown links between `.md` files resolve and navigate) and **media content** (images/video embedded and served), so a reader can *browse around* a space the way you browse a website. Turns a folder of files into a navigable vault/site.
_Novelty_: Establishes the "Drop-a-Folder, Get-a-World" capability (#6) as the literal first deliverable — the substrate everything else renders from.

**[#26 — The Build Roadmap (3 stages)]**
_Concept_: Agreed sequencing:
  1. **Serve** — `.md` on Azure with linking + media (browsable foundation).
  2. **HTML Renderer** — browser hits the URL → clean server-rendered HTML (the "born compatible" path).
  3. **Native MD Client** — a client that renders markdown *without HTML* — the reader's agent drives native presentation directly (the purest expression of Zero Shared Pixels / Client Sovereignty).
_Novelty_: Foundation → compatibility → magic. Each stage ships independent value and de-risks the next; HTML is built before the native client so adoption/reach exists before the differentiated experience.

**[Pending Decision]:** Stage-1 serving path not yet chosen — Path 1 (thin server / content negotiation), Path 2 (static + pre-built HTML), or Path 3 (static now, server later). Linking (relative `.md` links) and media (markdown embeds alongside files in Azure) accepted as defaults.

## Naming Thread — brainstorming a name + registerable domain

Goal: name the whole paradigm/product so naethyn can register a domain. The `.md` TLD (Moldova, widely used for markdown) is an on-theme domain-hack option.

### Naming — findings & shortlist

**Availability reality (checked live via Verisign RDAP):** nearly all dictionary + obvious coined `.com` names are taken (facet, prism, lens, mosaic, mirador, vellum, markway, mdweb, markdownweb, etc. — all gone).

**Confirmed AVAILABLE `.com` (buyable today, no check needed):**
- `themarkdownweb.com` · `marklace.com` · `markdle.com` · `vellumweb.com` · `velloum.com` · `readme-md.com`

**Strategic recommendation: go `.md`.** The `.md` TLD makes the domain itself express the concept (the Markdown Web on a markdown TLD). Niche TLD → good words more likely free. Cannot be RDAP-checked here → verify on GoDaddy (matches the desired buy-workflow).

**`.md` hit-list to check on GoDaddy (ranked, on-theme):**
1. Verb/noun hacks: `read.md` · `view.md` · `render.md` · `open.md` · `browse.md` · `web.md` · `my.md` · `see.md` · `show.md` · `feed.md`
2. Concept words: `facet.md` · `prism.md` · `lens.md` · `mosaic.md` · `vellum.md` · `mirador.md` · `kaleido.md`

**Agreed domain-buying workflow (run when ready):**
1. Pick top candidate from the `.md` hit-list.
2. Search it on GoDaddy.
3. If available AND naethyn likes it → buy.
4. If taken or not loved → next candidate. Repeat.
5. Fallback: if no `.md` lands, grab a confirmed-available `.com` from the list above.

### DECISION: .com on GoDaddy (chosen by naethyn)

`.md` is unavailable via GoDaddy; naethyn chose to stay on GoDaddy with a `.com`.

**Confirmed-FREE `.com` shortlist (verified live via Verisign RDAP):**

*Descriptive / category name:*
- `themarkdownweb.com` · `getmarkdownweb.com` · `mymarkdownweb.com`

*Literary / "prose & paper":*
- `prosepage.com` · `vellumweb.com` · `vellumgarden.com` · `prosereader.com` · `quireweb.com`

*Brandable / coined:*
- `marklace.com` · `markdle.com` · `marblio.com` · `markaby.com` · `mellowmd.com` · `velliox.com`

**Buy-workflow:** search top pick on GoDaddy → if available & liked → buy → else next.
**Mary's ranked rec:** 1) prosepage.com  2) themarkdownweb.com  3) vellumweb.com  4) marklace.com  5) mellowmd.com

### ✅ DOMAIN SECURED

**`themarkdownweb.com` — PURCHASED** (registered via GoDaddy, 2026-06-21).

The product/paradigm now has its name and home: **The Markdown Web** → themarkdownweb.com. Owns the category name and is SEO-strong for "what is the markdown web."

**Still open / next up:**
- ⏸️ Stage-1 serving path decision: Path 1 (thin server / content negotiation) · Path 2 (static + pre-built HTML) · Path 3 (static now, server later).
- Untouched map gaps: 💰 business model · 🔍 identity & discovery.
