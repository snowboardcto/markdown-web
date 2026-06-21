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
