---
title: "Product Brief: The Markdown Web"
status: draft
created: 2026-06-21
updated: 2026-06-21
---

# Product Brief: The Markdown Web

> **Purpose of this brief:** a foundation for building — the north star for the build and the input to a PRD. Honest, right-sized, not investor theater.

## Executive Summary

**The Markdown Web** ([themarkdownweb.com](https://themarkdownweb.com)) is a new shape for the web: URLs point to `.md` files, and presentation is no longer baked in by the author. The author publishes pure markdown — structure and meaning. The reader's own AI agent, running locally, renders that markdown into whatever shape fits *them*. One source file, a personal experience for every reader. By design.

Today's web couples content and presentation; HTML makes the author dictate exactly how a page looks. The Markdown Web decouples them and hands presentation to the *consumer's* agent — the same way agents already prefer markdown over HTML internally. The bet: if markdown is the lingua franca for machines, it can be the lingua franca for humans too — rendered by each human's agent.

This is a developer-first beginning. The full product needs it all — the server-rendered HTML path, content negotiation, the native agent-rendering client, and sharing. v0.1 ships the first slice: a live site at themarkdownweb.com that renders markdown beautifully and casts the vision — the manifesto made real, dogfooded on the author's own files — with everything else sequenced behind it, none of it cut.

## The Problem

Developers live in markdown — READMEs, docs, notes, and an exploding pile of AI-tool output (Claude/Cursor/agent transcripts, briefs, plans). That content is *meaningful* but *homeless*: it sits in folders, gists, and repos, or gets force-fit into a CMS or static-site generator that bolts on a fixed, author-controlled look. `[ASSUMPTION: "developers" = devs and AI-power-users who already accumulate markdown, especially AI-generated output — confirm or narrow.]`

More deeply: the web's author-controls-presentation model can't adapt to the individual reader. Accessibility, translation, reading level, and format are expensive author obligations instead of automatic properties — and no amount of responsive design lets the *reader* truly own how content meets them.

## The Solution

A web where a `.md` URL is the canonical unit, and rendering is delegated:

- **Browsers** get clean, server-rendered HTML — works everywhere, crawlable, SEO-friendly, no agent required (born compatible with today's web).
- **A dedicated client** hands the raw markdown to the reader's *local* agent, which renders presentation per person — the "Zero Shared Pixels for humans" experience that is the heart of the product.
- **Content negotiation** lets one URL serve the right representation to each consumer (HTML to browsers, markdown to agents/clients).

The grounded entry point is a **personal markdown vault**: store, browse, and share your `.md` (plus media), made to look genuinely great — with the author's own files (including this project's manifesto and planning docs) as the first content.

## What Makes This Different

The market splits into four layers, and everyone picks one side:

- **Markdown publishers / digital gardens** (Obsidian Publish, Quartz, Hosted.md) make markdown addressable — but render *one fixed author-controlled look*. No per-reader agent.
- **Agentic-web standards** (llms.txt, Cloudflare "Markdown for Agents," content negotiation) serve markdown to *machines* to save tokens — the human still gets author-rendered HTML.
- **AI browsers / generative UI** (Perplexity Comet, Browser Company Dia, Google A2UI) render per-user — but from existing HTML/app data, *not a markdown content web*.

**The Markdown Web sits in the unclaimed intersection:** markdown as the canonical, URL-addressable content layer **and** the reader's own agent rendering the *human-facing* presentation, per person. A direct search for anyone doing "personalized agent rendering of markdown per reader" found none.

**Honest moat caveat (do not fabricate):** the *plumbing* — markdown hosting, content negotiation, md/HTML dual-serve — is commoditizing fast (Cloudflare/Vercel give it away; Hosted.md/Quartz own publishing), and the *client* layer is attackable by deep-pocketed entrants (Comet/Dia/Atlas/A2UI). The defensible advantages are therefore **not technical**: owning the category ("The Markdown Web" / themarkdownweb.com), the publish↔read loop, an opinionated and genuinely beautiful client, community, and the selfishly-useful personal-vault wedge that needs no network to deliver value.

## Who This Serves

**Primary: developers who live in markdown.** They already write READMEs, docs, and notes in `.md`, and increasingly generate large volumes of markdown via AI tools. They want their markdown stored, browsable, and beautiful without converting to HTML or wrestling a CMS — and they're the audience most likely to *get* the agent-rendering thesis and tolerate an early client. `[ASSUMPTION: developer-first; broader/non-dev audiences are a later expansion.]`

**Customer zero: the author (naethyn).** v0.1 must delight a single user — himself, viewing his own files — before any network exists.

## Success Criteria

**v0.1 (the bar the user set):**
- themarkdownweb.com is **live** and **renders markdown beautifully**.
- The site **communicates the vision** — the manifesto, rendered, *is* the proof of concept.
- The author's own markdown (manifesto + planning docs) is hosted there and is a genuine pleasure to read. `[ASSUMPTION: "renders + gives the vision" = the manifesto-as-rendered-site, dogfooded — confirm.]`

**Later signals (not v0.1 gates):** the native client demonstrates per-reader agent rendering; other developers publish a vault; content negotiation serves both audiences from one URL; repeat readers.

## Scope

Nothing is descoped — the full product requires all of it. Scope here is about **sequence**, not exclusion.

**Build first (v0.1):**
- Serve `.md` + media on Azure; inter-file linking so you can browse around.
- Server-side markdown → beautiful HTML (one `.md` = one page).
- Deploy at themarkdownweb.com with auto-publish on push (the snowboardcto.com model).
- Seed content: the manifesto and planning docs.

**Committed — sequenced, not cut:**
- **Content negotiation** — one URL serves HTML to browsers, markdown to agents/clients.
- **The native agent client** + **per-reader agent rendering** — the core differentiator ("Zero Shared Pixels for humans").
- **Sharing / feed** — the Living Link, send-the-doc, follow-a-vault.
- Accounts / multi-user and business model — required eventually; timing open.

## Open Questions

- 💰 **Business model** — none for now, parked deliberately (revisit before scaling).
- 🔍 **Identity & discovery** — how readers find each other's markdown spaces.
- 🖥️ **Client form factor** — extension vs app vs OS-agent vs CLI vs web (must run a local agent and "work everywhere").
- ✍️ **Author incentive** — why authors publish `.md` and surrender presentation control.

## Roadmap

| Stage | Ships | For |
|-------|-------|-----|
| **v0.1 (now)** | Live themarkdownweb.com rendering markdown beautifully; manifesto + docs hosted | Author / vision-caster |
| **v0.2** | Drop-a-folder vault: linking + media, one toggle to publish | Early-adopter developers |
| **v0.5** | Content negotiation; the public showcase & manifesto at scale | The world (via search) |
| **v1.0** | Native `.md` client (agent-rendered, no HTML) + sharing | The believers |

## Vision

If it works, "open it on the Markdown Web" becomes a normal thing to say. Authors publish meaning as plain markdown; every reader's agent renders it for them — accessible, translated, and shaped to the individual, for free. The web stops being one fixed page the author controls and becomes a personal surface the reader owns. themarkdownweb.com is where that starts — and the place that named it.
