import { test, expect } from '@playwright/test';
// Import the SHARED pure helper that `src/pages/index.astro` actually renders
// from, so these assertions genuinely cover the index's listing logic — not a
// re-implementation. (Anti-tautology: same function, same module.)
import { buildIndexItems } from '../src/lib/index-entries.mjs';

/**
 * Story 2.5 — degenerate-vault coverage (code-review Finding #2).
 *
 * The empty-vault ("No pages yet.") and single-page branches of `index.astro`
 * are driven entirely by the length and contents of `buildIndexItems(entries)`.
 * The real content vault always has many pages, so a full empty-vault E2E is
 * impractical; instead we exercise the extracted pure function on 0- and
 * 1-entry inputs. `index.astro` renders `items.length === 0 ? <p>No pages
 * yet.</p> : <ul>…`, so `buildIndexItems([]) === []` is exactly the condition
 * that selects the empty-state branch, and a single entry yields exactly one
 * <li>.
 */

type Entry = { id: string; data?: { title?: unknown } };

test.describe('Story 2.5 — empty / single-page degenerate listing (buildIndexItems)', () => {
  test('empty vault yields zero items -> selects the "No pages yet." branch', () => {
    const items = buildIndexItems([] as Entry[]);
    expect(items).toEqual([]);
    // index.astro renders the empty-state precisely when items.length === 0.
    expect(items.length === 0, 'an empty vault must select the empty-state branch').toBe(true);
  });

  test('an all-empty-id vault still yields zero items (no `/` self-link)', () => {
    // A root content/index.md collapses to id `''`; it must be filtered out, so
    // even a "non-empty" collection of only that entry degrades to empty-state.
    const items = buildIndexItems([{ id: '' }] as Entry[]);
    expect(items).toEqual([]);
  });

  test('single-page vault yields exactly one item (-> exactly one <li>)', () => {
    const items = buildIndexItems([{ id: 'only-page' }] as Entry[]);
    expect(items.length, 'a single-page vault must yield exactly one item').toBe(1);
    expect(items[0]).toEqual({
      href: '/only-page',
      label: 'Only Page',
      id: 'only-page',
    });
  });

  test('single-page with frontmatter title uses the title; label is never blank', () => {
    const items = buildIndexItems([{ id: 'only', data: { title: 'My Title' } }] as Entry[]);
    expect(items.length).toBe(1);
    expect(items[0].label).toBe('My Title');
    // Final `|| entry.id` fallback guards against a blank label even if both the
    // frontmatter title and the slug-derived title are empty.
    const fallback = buildIndexItems([{ id: 'x', data: { title: '   ' } }] as Entry[]);
    expect(fallback[0].label, 'label must never be blank').not.toBe('');
  });
});
