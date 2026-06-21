// @ts-check
import { defineConfig } from 'astro/config';
import remarkGfm from 'remark-gfm';

// Story 2.1 — render the content/ vault to static HTML with full GFM support.
//
// GFM (tables, strikethrough, task lists, autolinks) is enabled via `remark-gfm`.
// Astro's built-in Shiki default runs for fenced code; the GitHub-style syntax
// palette/theme is intentionally deferred to Story 2.2. No integrations, no
// `client:*` islands — content must be pure server-rendered static HTML (FR-5/FR-7).
export default defineConfig({
  markdown: {
    gfm: true,
    remarkPlugins: [remarkGfm],
  },
});
