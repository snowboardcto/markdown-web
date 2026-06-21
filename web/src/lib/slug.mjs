// Story 2.3 — the single source of truth for content-file -> route-slug
// derivation, shared by BOTH the catch-all route (`src/pages/[...slug].astro`)
// and the relative-`.md`-link rewrite plugin (`src/lib/rehype-md-links.mjs`),
// so route emission and link resolution can never drift.
//
// This mirrors Astro's glob() id derivation (content/utils.js
// getContentEntryIdAndSlug): drop the `.md` extension, slugify each path
// segment with github-slugger (which lower-cases), join with `/`, and collapse
// a trailing `/index` to its parent route. github-slugger is already a
// resolvable dependency (used by the route layer since Story 2.1).
import { slug as githubSlug } from 'github-slugger';

/**
 * Slug a single relative POSIX path (forward-slash, no leading `/`), dropping a
 * trailing `.md` (case-insensitive), github-slugging each segment, and
 * collapsing a trailing `/index` (or a bare `index`) to the parent route.
 *
 * Operates purely in slug space — the caller is responsible for having already
 * resolved `./`, `..`, and nested directories against the page's location.
 *
 * @param {string} relPosixPath e.g. `sub/page.md`, `gear-guide.md`, `index.md`
 * @returns {string} the route slug WITHOUT a leading `/` (e.g. `sub/page`,
 *   `gear-guide`, or `''` for the vault root). Never `//` or a bare separator.
 */
export function pathToSlug(relPosixPath) {
  const withoutExt = relPosixPath.replace(/\.md$/i, '');
  return withoutExt
    .split('/')
    .map((segment) => githubSlug(segment))
    .join('/')
    .replace(/\/index$/, '')
    .replace(/^index$/, '');
}
