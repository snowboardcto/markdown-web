// @ts-check
import { defineConfig } from 'astro/config';
import remarkGfm from 'remark-gfm';
import rehypeMdLinks from './src/lib/rehype-md-links.mjs';

// Story 2.1 — render the content/ vault to static HTML with full GFM support.
// Story 2.2 — switch Shiki to the bundled GitHub-light theme for the reading
// surface (replaces the default `github-dark`).
// Story 2.3 — rewrite relative `.md` links to their resolved page routes at
// build time via the `rehypeMdLinks` rehype plugin (the web half of FR-2).
//
// GFM (tables, strikethrough, task lists, autolinks) is enabled via `remark-gfm`.
// Fenced code is highlighted by Astro's built-in Shiki using the bundled
// `github-light` theme, matching DESIGN.md's light code palette; the code-block
// background/radius (#f6f8fa / 6px) is applied in web/src/styles/github.css.
// `rehypeMdLinks` runs at the HAST stage (after remark-gfm + Shiki), mutating
// each `<a href>` to a `/route` — a build-time string rewrite, so the emitted
// HTML stays plain `<a href="/route">`. No integrations, no `client:*` islands —
// content must be pure server-rendered static HTML (FR-5/FR-7).
export default defineConfig({
  markdown: {
    gfm: true,
    remarkPlugins: [remarkGfm],
    rehypePlugins: [rehypeMdLinks],
    shikiConfig: {
      theme: 'github-light',
    },
  },
});
