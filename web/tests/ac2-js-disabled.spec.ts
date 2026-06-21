import { test, expect } from '@playwright/test';

/**
 * AC2 — readable with JavaScript disabled.
 *
 * The full article body must be present and readable in the served HTML; the
 * content lives in the initial HTML payload, NOT injected by client-side JS.
 * We use a `javaScriptEnabled: false` browser context to prove this.
 */
test.describe('AC2: /x is readable with JavaScript disabled', () => {
  test.use({ javaScriptEnabled: false });

  test('article body content is present with JS disabled', async ({ page }) => {
    const response = await page.goto('/x');
    expect(response, 'route /x should exist').not.toBeNull();
    expect(response!.status(), '/x should return 200').toBe(200);

    // Content visible without any client-side hydration.
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('table')).toBeVisible();
    await expect(page.getByText('bold text')).toBeVisible();
    await expect(page.getByText('Hello')).toBeVisible();
  });

  test('body text exists in the raw HTML payload (no client render dependency)', async ({ page }) => {
    await page.goto('/x');
    const html = await page.content();
    // Representative text from every major block must be in the served markup.
    expect(html).toContain('Heading One');
    expect(html).toContain('<strong>');
    expect(html).toContain('<table');
    expect(html).toContain('<pre');
    // No client-island hydration directive on the content path.
    expect(html).not.toMatch(/astro-island|client:(load|idle|visible|only)/);
  });
});
