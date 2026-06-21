import { test, expect } from '@playwright/test';

/**
 * AC6 — full GFM extension coverage + correct HTML escaping.
 *
 *   - strikethrough `~~text~~` -> <del> (or <s>)
 *   - task list `- [ ]` / `- [x]` -> <li> with a disabled <input type="checkbox">
 *   - bare URL autolink -> <a href>
 *   - special chars `<` and `&` HTML-escaped (raw HTML contains &lt;/&amp;,
 *     not unescaped <tags> that would corrupt the document for a crawler)
 */
test.describe('AC6: GFM extensions + HTML escaping on /x', () => {
  test('strikethrough renders as <del> or <s>', async ({ page }) => {
    await page.goto('/x');
    await expect(page.locator('del, s')).not.toHaveCount(0);
    await expect(page.locator('del, s').first()).toContainText('struck through');
  });

  test('task list renders <li> with disabled <input type="checkbox">', async ({ page }) => {
    await page.goto('/x');
    const checkboxes = page.locator('li input[type="checkbox"]');
    await expect(checkboxes).toHaveCount(2);
    // GFM task-list checkboxes are disabled.
    await expect(checkboxes.first()).toBeDisabled();
    // One unchecked, one checked.
    await expect(page.locator('li input[type="checkbox"]:checked')).toHaveCount(1);
  });

  test('bare URL becomes an <a href> autolink', async ({ page }) => {
    await page.goto('/x');
    await expect(
      page.locator('a[href="https://themarkdownweb.com"]'),
    ).not.toHaveCount(0);
  });

  test('special characters < and & are HTML-escaped in the raw output', async ({ page }) => {
    const response = await page.goto('/x');
    const raw = await response!.text();
    // Escaped character references must be present. Astro's HTML stringifier
    // emits numeric refs (`&#x3C;`/`&#x26;`); named refs (`&lt;`/`&amp;`) are an
    // equally valid escaping. Accept either form — the AC is "escaped, not raw".
    expect(raw).toMatch(/&lt;|&#x3[cC];|&#60;/);
    expect(raw).toMatch(/&amp;|&#x26;|&#38;/);
    // ...and the literal injected markup must NOT appear unescaped in body text.
    expect(raw).not.toContain('code with <tags>');
  });
});
