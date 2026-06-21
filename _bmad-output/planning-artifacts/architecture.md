---
stepsCompleted: [1]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-the-markdown-web-2026-06-21/prd.md
  - _bmad-output/planning-artifacts/prds/prd-the-markdown-web-2026-06-21/addendum.md
  - _bmad-output/planning-artifacts/briefs/brief-the-markdown-web-2026-06-21/brief.md
  - _bmad-output/planning-artifacts/research/market-markdown-web-competitive-landscape-research-2026-06-21.md
workflowType: 'architecture'
project_name: 'The Markdown Web'
user_name: 'naethyn'
date: '2026-06-21'
---

# Architecture Decision Document — The Markdown Web

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Foundational Constraints (confirmed before decisions)

**FC-1 — Two render paths, deliberately different engines:**
- **HTML render path (FR-5/FR-6, browser audience):** server-rendered HTML, consumed by browsers — Chromium and all engines. Chromium is the *target*, by design. Compatibility layer.
- **Native client render path (FR-9–FR-13):** renders markdown to **native UI**, via a **non-HTML, non-webview** path. No embedded browser engine (Chromium, WebKit, WebView2) — embedding any HTML webview reproduces the browser ("just the same thing") and defeats the client's reason to exist.

**FC-2 — Agent output contract:** because the native client draws native UI, the **local agent emits a declarative UI structure (or native primitives), never HTML.** Shape: `markdown + reader context → agent → declarative UI description → native renderer → native widgets`. Candidate pattern to weigh: A2UI-style declarative UI (from market research) — agent emits structured UI, rendered natively per platform.

_Confirmed by naethyn, 2026-06-21._
