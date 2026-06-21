// Story 2.5 — the pure, testable core of the browsable index's listing logic:
// take the raw `getCollection('pages')` entries and produce the sorted list of
// { href, label, id } items the index renders. Extracted so the empty-vault
// ("No pages yet.") and single-page degenerate branches can be exercised by a
// unit-style test without building a whole alternate content vault (the real
// vault has many pages). `index.astro` imports and renders this — the same
// logic, the same order — so the test genuinely covers the index's behaviour.

import { slugToTitle } from './title.mjs';

/**
 * Build the deterministic, CI-stable list of index items from collection
 * entries. Filters the empty-`id` case (a root `content/index.md` collapses to
 * `''` and would self-link `/`), derives a never-empty label via the shared
 * `slugToTitle` precedence with a final `|| entry.id` fallback, and sorts by
 * route id using a code-unit comparison (ICU-independent — identical dev↔CI).
 *
 * @param {{ id: string, data?: { title?: unknown } }[]} entries
 * @returns {{ href: string, label: string, id: string }[]}
 */
export function buildIndexItems(entries) {
  return entries
    // Guard the empty-id case: a root `content/index.md` collapses to `''` (and
    // would route to `/`), so it must never appear as a `/` self-link.
    .filter((entry) => entry.id !== '')
    .map((entry) => {
      const frontmatterTitle =
        typeof entry.data?.title === 'string' ? entry.data.title.trim() : '';
      return {
        href: '/' + entry.id,
        // Final `|| entry.id` fallback so an empty-slug label can't be blank.
        label: frontmatterTitle || slugToTitle(entry.id) || entry.id,
        id: entry.id,
      };
    })
    // Deterministic, CI-stable sort by route id via a code-unit comparison —
    // ICU-independent, so dev and CI produce identical order.
    .sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}
