// Story 2.5 — the single source of truth for deriving a human-readable Title
// Case label from a route slug, shared by BOTH the catch-all route
// (`src/pages/[...slug].astro`, the never-empty <title>/<h1> fallback) and the
// generated browsable index (`src/pages/index.astro`, the link label), so the
// index label and the destination page title can never drift via copy-paste.
//
// Extracted (behaviour-preserving) from the inline `slugToTitle` that lived in
// `[...slug].astro` (Story 2.1). Takes the last path segment of a slug,
// replaces `-`/`_` runs with spaces, and Title-Cases each word.

/**
 * Derive a Title Case label from a route slug's last segment.
 *
 * @param {string} slug e.g. `sub/page`, `gear-guide`, `no-h1`
 * @returns {string} e.g. `Page`, `Gear Guide`, `No H1`. Returns `''` for
 *   nullish/empty/separator-only input (never throws) — callers add a final
 *   non-empty fallback (e.g. `|| entry.id`) where a blank label is unacceptable.
 */
export function slugToTitle(slug) {
  // Guard nullish/empty input so a missing slug can never throw on `.split`.
  if (!slug) return '';
  const last = slug.split('/').pop() ?? slug;
  return last
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase())
    .trim();
}
