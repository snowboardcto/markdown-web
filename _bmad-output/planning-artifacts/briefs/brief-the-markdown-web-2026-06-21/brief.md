---
title: "Product Brief: The Markdown Web"
status: final
created: 2026-06-21
updated: 2026-06-21
---

# Product Brief: The Markdown Web

> **Purpose of this brief:** a foundation for building — the north star for the build and the input to a PRD. Honest, right-sized, not investor theater.

## Executive Summary

**The Markdown Web** ([themarkdownweb.com](https://themarkdownweb.com)) is a new shape for the web: URLs point to `.md` files, and presentation is no longer baked in by the author. The author publishes pure markdown — structure and meaning. The reader's own AI agent, running locally, renders that markdown into whatever shape fits *them*. One source file, a personal experience for every reader. By design.

Today's web couples content and presentation; HTML lets the author dictate exactly how a page looks. The Markdown Web decouples them and hands presentation to the *consumer's* agent — the same way agents already prefer markdown over HTML internally. The bet: if markdown is the lingua franca for machines, it can be the lingua franca for humans too — rendered by each human's agent.

The product needs it all — the HTML client, content negotiation, the native agent-rendering client, and sharing. v0.1 ships the first slice live at themarkdownweb.com: markdown rendered beautifully, casting the vision and dogfooded on the author's own files. Everything else is sequenced behind it, not cut.

## The Problem

People who work with AI agents are buried in markdown. Agents emit it constantly — outputs, transcripts, briefs, plans, notes — and `.md` is already how these users write READMEs and docs. That content is *meaningful* but *homeless*: it sits in folders, gists, and repos, or gets force-fit into a CMS or static-site generator that bolts on a fixed, author-controlled look. There's no good place to keep it, read it pleasurably, or share it.

More deeply: the web's author-controls-presentation model can't adapt to the individual reader. Accessibility, translation, reading level, and format are expensive author obligations instead of automatic properties — and no amount of responsive design lets the *reader* truly own how content meets them.

## The Solution

A web where a `.md` URL is the canonical unit, and rendering is delegated to two clients tied together by content negotiation:

- **The HTML client** — server-renders markdown to clean HTML for browsers. Works everywhere, crawlable, SEO-friendly, no agent required (born compatible with today's web).
- **The native client** — hands the raw markdown to the reader's *local* agent, which renders presentation per person, without HTML. This is the heart of the product: "Zero Shared Pixels for humans."
- **Content negotiation** ties them together — one URL serves the right representation to each consumer (HTML to browsers, markdown to agents and the native client).

The grounded entry point is a **personal markdown vault**: store, browse, and share your `.md` (plus media), made to look genuinely great — with the author's own files (including this project's manifesto and planning docs) as the first content.

## What Makes This Different

The market splits, and everyone picks one side:

- **Markdown publishers / digital gardens** (Obsidian Publish, Quartz, Hosted.md) make markdown addressable — but render *one fixed author-controlled look*. No per-reader agent.
- **Agentic-web standards** (llms.txt, Cloudflare "Markdown for Agents," content negotiation) serve markdown to *machines* to save tokens — the human still gets author-rendered HTML.
- **AI browsers / generative UI** (Perplexity Comet, Browser Company Dia, Google A2UI) render per-user — but from existing HTML/app data, *not a markdown content web*.

**The Markdown Web sits in the unclaimed intersection:** markdown as the canonical, URL-addressable content layer **and** the reader's own agent rendering the *human-facing* presentation, per person. A direct search for anyone doing "personalized agent rendering of markdown per reader" found none. The one-line wedge into the whole industry conversation: *everyone says markdown is for models and HTML is for humans — what if markdown were for humans too, rendered by their agent?*

## Who This Serves

**Primary: people who use AI agents and work with markdown files.** Agents emit markdown all day, and these users accumulate it faster than anyone — outputs, notes, docs, plans. They want it stored, browsable, and beautiful without converting to HTML or wrestling a CMS, and because they already live with agents, they intuitively grasp the agent-rendering thesis and will tolerate an early native client.

**Customer zero: the author (naethyn).** v0.1 must delight a single user — himself, viewing his own files — before any network exists.

## Success Criteria

**v0.1 — the bar the user set:** themarkdownweb.com is **live**, **renders markdown beautifully**, and **communicates the vision** — the manifesto, rendered, *is* the proof of concept. The author's own markdown (manifesto + planning docs) is hosted there and is a genuine pleasure to read.

**The two clients work, end to end:**
- **HTML client** — points at a folder of `.md` + media and produces a clean, browsable, linked site.
- **Native client** — renders a `.md` through the reader's *local* agent, with presentation visibly shaped to the individual (per-reader rendering working, not mocked).

**Later signals:** content negotiation serves both audiences from one URL; other people publish a vault; repeat readers.

## Scope

Nothing here is descoped. Scope is about **sequence**, not exclusion.

**Build first (v0.1):**
- Serve `.md` + media on Azure; inter-file linking so you can browse around.
- **HTML client:** server-side markdown → beautiful HTML (one `.md` = one page).
- Deploy at themarkdownweb.com with auto-publish on push (the snowboardcto.com model).
- Seed content: the manifesto and planning docs.

**Then (committed, sequenced — not cut):**
- **The native agent client** + **per-reader agent rendering** — the core differentiator.
- **Content negotiation** — one URL serves HTML to browsers, markdown to agents/clients.
- **Sharing / feed** — the Living Link, send-the-doc, follow-a-vault.
- Accounts / multi-user — required eventually; timing open.

## Open Questions

- 💰 **Business model** — none for now, parked deliberately (revisit before scaling).
- 🔍 **Identity & discovery** — how readers find each other's markdown spaces.
- 🖥️ **Native client form factor** — extension vs app vs OS-agent vs CLI vs web (must run a local agent and "work everywhere").
- ✍️ **Author incentive** — why authors publish `.md` and surrender presentation control.

## Roadmap

| Stage | Ships | For |
|-------|-------|-----|
| **v0.1 (now)** | Live themarkdownweb.com + HTML client rendering markdown beautifully; manifesto + docs hosted | Author / vision-caster |
| **v0.2** | Drop-a-folder vault: linking + media, one toggle to publish | Early agent-and-markdown users |
| **v0.5** | Native client + per-reader agent rendering; content negotiation | The believers |
| **v1.0** | Sharing / feed; the public Markdown Web at scale | The world |

## Vision

If it works, "open it on the Markdown Web" becomes a normal thing to say. Authors publish meaning as plain markdown; every reader's agent renders it for them — accessible, translated, and shaped to the individual, for free. The web stops being one fixed page the author controls and becomes a personal surface the reader owns. themarkdownweb.com is where that starts — and the place that named it.
