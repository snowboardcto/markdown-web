// @ts-check
import { defineConfig } from 'astro/config';
import remarkGfm from 'remark-gfm';

// Story 2.1 — render the content/ vault to static HTML with full GFM support.
// Story 2.2 — switch Shiki to the bundled GitHub-light theme for the reading
// surface (replaces the default `github-dark`).
//
// GFM (tables, strikethrough, task lists, autolinks) is enabled via `remark-gfm`.
// Fenced code is highlighted by Astro's built-in Shiki using the bundled
// `github-light` theme, matching DESIGN.md's light code palette; the code-block
// background/radius (#f6f8fa / 6px) is applied in web/src/styles/github.css.
// No integrations, no `client:*` islands — content must be pure server-rendered
// static HTML (FR-5/FR-7).
export default defineConfig({
  markdown: {
    gfm: true,
    remarkPlugins: [remarkGfm],
    shikiConfig: {
      theme: 'github-light',
    },
  },
});
