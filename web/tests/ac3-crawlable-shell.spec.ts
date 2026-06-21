import { test, expect } from '@playwright/test';

/**
 * AC3 — well-formed, crawlable, born-compatible HTML shell.
 *
 * A single top-level <html lang> -> <head> (charset, title, viewport) -> <body>
 * with content inside a semantic container (<main>/<article>), no unclosed
 * structural tags, and zero dependency on client JS to render the content.
 */
test.describe('AC3: well-formed crawlable document shell', () => {
  // Every shell assertion is guarded by a 200 status so it can only pass once
  // the `/x` route actually renders the fixture — not against Astro's 404 page.
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/x');
    expect(response, 'route /x should exist').not.toBeNull();
    expect(response!.status(), '/x should return 200').toBe(200);
  });

  test('has a single <html lang> root', async ({ page }) => {
    await expect(page.locator('html')).toHaveCount(1);
    const lang = await page.locator('html').getAttribute('lang');
    expect(lang, '<html> must declare a lang attribute').toBeTruthy();
  });

  test('has <head> with charset, title and viewport', async ({ page }) => {
    await expect(page.locator('head')).toHaveCount(1);
    await expect(page.locator('head meta[charset]')).toHaveCount(1);
    await expect(page.locator('head meta[name="viewport"]')).toHaveCount(1);
    const title = await page.title();
    expect(title.trim(), '<title> must not be empty').not.toBe('');
  });

  test('content lives inside a semantic <main>/<article> container', async ({ page }) => {
    const semantic = page.locator('main, article');
    await expect(semantic.first()).toBeVisible();
    // The rendered fixture heading must be inside the semantic container.
    await expect(semantic.locator('h1', { hasText: 'Heading One' })).toHaveCount(1);
  });

  test('serves a doctype and no obviously unclosed structural tags', async ({ page }) => {
    const response = await page.goto('/x');
    expect(response!.status()).toBe(200);
    const html = (await response!.text()).toLowerCase();
    expect(html).toContain('<!doctype html>');
    // Balanced counts for the major structural elements.
    const count = (s: string, sub: string) => s.split(sub).length - 1;
    expect(count(html, '<html')).toBe(1);
    expect(count(html, '</html>')).toBe(1);
    // `<head>` exactly — `<head` would also match the Story 2.6 `<header>` chrome.
    expect(count(html, '<head>')).toBe(1);
    expect(count(html, '</head>')).toBe(1);
    expect(count(html, '<body')).toBe(1);
    expect(count(html, '</body>')).toBe(1);
  });
});
