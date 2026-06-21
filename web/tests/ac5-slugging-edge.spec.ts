import { test, expect } from '@playwright/test';

/**
 * AC5 — deterministic, safe slugging + empty-file resilience.
 *
 * Edge-case filenames must slug deterministically via Astro's glob() loader:
 *   - `My Notes.md` -> `/my-notes`  (spaces + uppercase normalised)
 *   - `sub/page.md` -> `/sub/page`  (nested path preserved)
 * A near-empty file (`empty.md`, single `#` heading) must still build to a
 * valid, non-crashing document shell rather than a broken page.
 */
test.describe('AC5: deterministic slugging + empty-file resilience', () => {
  test('`My Notes.md` is served at the deterministic slug /my-notes', async ({ page }) => {
    const response = await page.goto('/my-notes');
    expect(response, 'route /my-notes should exist').not.toBeNull();
    expect(response!.status(), '/my-notes should return 200').toBe(200);
    await expect(page.locator('h1', { hasText: 'My Notes' })).toHaveCount(1);
  });

  test('`sub/page.md` is served at the nested slug /sub/page', async ({ page }) => {
    const response = await page.goto('/sub/page');
    expect(response, 'route /sub/page should exist').not.toBeNull();
    expect(response!.status(), '/sub/page should return 200').toBe(200);
    await expect(page.locator('h1', { hasText: 'Nested Page' })).toHaveCount(1);
  });

  test('`My Notes.md` <title> is the slug-derived "My Notes" (H1 wins, asserts the value)', async ({ page }) => {
    await page.goto('/my-notes');
    // H1 "My Notes" wins the title chain; assert the rendered <title> value
    // end-to-end (covers the slug-derived title path, not just the <h1>).
    expect((await page.title()).trim()).toBe('My Notes');
  });

  test('no-H1 / no-front-matter `no-h1.md` falls back to the slug-derived <title>', async ({ page }) => {
    const response = await page.goto('/no-h1');
    expect(response, 'route /no-h1 should exist').not.toBeNull();
    expect(response!.status(), '/no-h1 should return 200').toBe(200);
    // With no front-matter title and no `# H1`, the title chain must fall back
    // to slugToTitle(entry.id): `no-h1` -> "No H1". Exercises the fallback that
    // every other fixture (each has an H1) leaves untested.
    expect((await page.title()).trim()).toBe('No H1');
  });

  test('near-empty `empty.md` builds to a valid document shell at /empty', async ({ page }) => {
    const response = await page.goto('/empty');
    expect(response, 'route /empty should exist').not.toBeNull();
    expect(response!.status(), '/empty should return 200').toBe(200);

    // Valid shell even with a content-light body.
    await expect(page.locator('html')).toHaveCount(1);
    expect(await page.locator('html').getAttribute('lang')).toBeTruthy();
    await expect(page.locator('head meta[charset]')).toHaveCount(1);
    // Title must never be empty (fallback to slug/H1).
    expect((await page.title()).trim()).not.toBe('');
    await expect(page.locator('main, article').first()).toBeVisible();
  });
});
