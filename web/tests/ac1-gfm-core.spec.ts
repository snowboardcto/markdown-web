import { test, expect } from '@playwright/test';

/**
 * AC1 — file-as-page: `content/x.md` -> route `/x` with correct GFM HTML.
 *
 * A GFM fixture (headings, bold/italic, ordered+unordered lists, inline +
 * fenced code, and a real table) must render at `/x` as correct semantic
 * elements: <h1>..<h6>, <strong>/<em>, <ul>/<ol>/<li>, <code>/<pre><code>,
 * and a real <table>/<thead>/<tbody>/<tr>/<th>/<td>.
 */
test.describe('AC1: content/x.md renders at /x with correct GFM semantic HTML', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/x');
    expect(response, 'route /x should exist').not.toBeNull();
    expect(response!.status(), '/x should return 200').toBe(200);
  });

  test('renders heading levels h1..h6', async ({ page }) => {
    await expect(page.locator('h1')).toHaveCount(1);
    for (const tag of ['h2', 'h3', 'h4', 'h5', 'h6']) {
      await expect(page.locator(tag).first()).toBeVisible();
    }
  });

  test('renders bold (<strong>) and italic (<em>)', async ({ page }) => {
    await expect(page.locator('strong', { hasText: 'bold text' })).toHaveCount(1);
    await expect(page.locator('em', { hasText: 'italic text' })).toHaveCount(1);
  });

  test('renders unordered (<ul>) and ordered (<ol>) lists with <li>', async ({ page }) => {
    await expect(page.locator('ul')).not.toHaveCount(0);
    await expect(page.locator('ol')).not.toHaveCount(0);
    await expect(page.locator('ul > li').first()).toBeVisible();
    await expect(page.locator('ol > li').first()).toBeVisible();
  });

  test('renders inline <code> and fenced <pre><code>', async ({ page }) => {
    await expect(page.locator('code').first()).toBeVisible();
    await expect(page.locator('pre code')).not.toHaveCount(0);
  });

  test('renders a real GFM <table> with thead/tbody/tr/th/td', async ({ page }) => {
    await expect(page.locator('table')).toHaveCount(1);
    await expect(page.locator('table thead')).not.toHaveCount(0);
    await expect(page.locator('table tbody')).not.toHaveCount(0);
    await expect(page.locator('table tr')).not.toHaveCount(0);
    await expect(page.locator('table th').first()).toBeVisible();
    await expect(page.locator('table td').first()).toBeVisible();
  });
});
