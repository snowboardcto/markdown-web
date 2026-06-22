// @ts-check
import { defineConfig } from 'astro/config';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';
import rehypeMdLinks from './src/lib/rehype-md-links.mjs';
import rehypeMdMedia from './src/lib/rehype-md-media.mjs';
import copyVaultMedia from './src/lib/copy-vault-media.mjs';

// Story 2.1 — render the content/ vault to static HTML with full GFM support.
// Story 2.2 — switch Shiki to the bundled GitHub-light theme for the reading
// surface (replaces the default `github-dark`).
// Story 2.3 — rewrite relative `.md` links to their resolved page routes at
// build time via the `rehypeMdLinks` rehype plugin (the web half of FR-2).
// Story 2.4 — rewrite relative media `src`/`poster` (`<img>`/`<video>`/`<audio>`/
// `<source>`) to their served root-absolute URLs at build time via the
// `rehypeMdMedia` rehype plugin, AND copy the vault's non-`.md` assets into
// `dist/` via the `copyVaultMedia` integration (`astro:build:done`) — the web
// half of FR-3 (inline media served from the vault path).
//
// GFM (tables, strikethrough, task lists, autolinks) is enabled via `remark-gfm`.
// Fenced code is highlighted by Astro's built-in Shiki using the bundled
// `github-light` theme, matching DESIGN.md's light code palette; the code-block
// background/radius (#f6f8fa / 6px) is applied in web/src/styles/github.css.
//
// `rehypeMdLinks` (`<a href>`) and `rehypeMdMedia` (media `src`/`poster`) run at
// the HAST stage — string rewrites, so the emitted HTML stays plain
// `<a href="/route">` / `<img src="/served">`. No `client:*` islands; content is
// pure server-rendered static HTML (FR-5/FR-7).
//
// astro:assets bypass (CRITICAL for markdown `![]()`): Astro's markdown image
// pipeline (`rehypeImages` + `getImage`) only rewrites an `<img>` to an optimised
// `/_astro/*.webp` (and CRASHES on a missing asset) when the `<img>`'s `src`
// still matches an entry collected in `vfile.data.astro.localImagePaths`. USER
// rehype plugins run BEFORE Astro's internal `rehypeImages`, so `rehypeMdMedia`
// rewrites a relative `media/x.jpg` -> `/media/x.jpg` FIRST; `rehypeImages` then
// no longer matches it (the src is already root-absolute, not in localImagePaths)
// and leaves it as a plain `<img src="/media/x.jpg">`. That both makes markdown
// `![](media/powder.jpg)` emit a verbatim served `<img>` (rewritten by our plugin
// + served by our copy hook, NOT optimised) AND prevents the missing-asset (AC6)
// reference from crashing the build via the assets pipeline — no `<Image>`/assets
// optimisation, no `image` service override, no markdown-image flag needed; the
// ordering of our rehype plugin ahead of `rehypeImages` is the bypass.
// `rehypeMdMedia` additionally CLEARS `vfile.data.astro.localImagePaths` so
// Astro's `rehypeImages` early-returns entirely (it otherwise `decodeURI`s every
// `<img src>` and would throw on the malformed-`%` smuggle fixture / crash on the
// missing-asset reference) — see the bypass comment in `rehype-md-media.mjs`.
//
// `rehypeRaw` runs FIRST so that media authored as RAW HTML in markdown
// (`<video>`/`<audio>`/`<source>` — GFM has no native video syntax) is parsed
// into real HAST `<img>/<video>/<audio>/<source>` ELEMENT nodes BEFORE
// `rehypeMdMedia` visits the tree (Astro's own internal `rehypeRaw` runs only at
// the very end of the pipeline, after our plugins, so without this the raw media
// would still be opaque `raw` string nodes our element-visitor can't see). It is
// idempotent: Astro's later `rehypeRaw` finds no `raw` nodes left to parse.
// Story 5.1 — Living Link (AC3): set the production canonical origin so Astro.site
// is defined and Page.astro can build the absolute canonical URL for each page.
// This is the ONLY astro.config.mjs change for 5.1 — the markdown pipeline,
// integrations, and all rehype/Shiki config are left exactly as-is.
export default defineConfig({
  site: 'https://themarkdownweb.com',
  markdown: {
    gfm: true,
    remarkPlugins: [remarkGfm],
    rehypePlugins: [rehypeRaw, rehypeMdLinks, rehypeMdMedia],
    shikiConfig: {
      theme: 'github-light',
    },
  },
  integrations: [copyVaultMedia()],
});
