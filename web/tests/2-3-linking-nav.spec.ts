import { test, expect } from '@playwright/test';

/**
 * Story 2.3 — Inter-file linking and navigation (TDD / RED phase).
 *
 * These specs are written FIRST, before the rehype link-rewrite plugin
 * (`web/src/lib/rehype-md-links.mjs`) and the custom not-found page
 * (`web/src/pages/404.astro`) exist. They are EXPECTED TO FAIL until Step 5
 * implements those: today relative `.md` links still emit their literal
 * `*.md` hrefs and there is no custom 404 (Astro serves a default not-found).
 *
 * Everything is asserted against the BUILT/PREVIEW output (the harness runs
 * `npm run build && npm run preview`), so the assertions prove the build-time
 * rewrite — no client JS is required. Hrefs are read via
 * `locator.getAttribute('href')` from the rendered HTML, and the 404 status is
 * read from the real navigation response (`page.goto(...).status()`), never
 * re-derived from a re-implementation of the slug function.
 *
 * AC -> test mapping:
 *   AC1  relative `.md` link -> resolved route href + in-place navigation
 *   AC2  resolution edge cases (nested / `..` / `./` / encoded / index / fragment)
 *   AC3  pass-through untouched (external / mailto / root-absolute / anchor / asset)
 *   AC4  missing target -> true HTTP 404 status on a custom not-found page
 *   AC5  browser Back/Forward via plain <a> full-page nav
 *   AC6  covered by the FULL suite (the existing 39 specs stay green)
 *
 * `href` helper: return the href of the FIRST link whose visible text matches,
 * scoped to a page already navigated to. We match by exact accessible name to
 * avoid grabbing the wrong link from the fixture's link list.
 */
async function hrefOf(page: import('@playwright/test').Page, name: string): Promise<string | null> {
  return page.getByRole('link', { name, exact: true }).first().getAttribute('href');
}

test.describe('Story 2.3 AC1 — relative .md link rewritten to a route href + in-place nav', () => {
  test.beforeEach(async ({ page }) => {
    const response = await page.goto('/x');
    expect(response, 'route /x should exist').not.toBeNull();
    expect(response!.status(), '/x should return 200').toBe(200);
  });

  test('`[guide](gear-guide.md)` renders as <a href="/gear-guide"> (build-time, not literal .md)', async ({
    page,
  }) => {
    const href = await hrefOf(page, 'guide');
    expect(href, 'relative .md link must be rewritten to the resolved page route').toBe(
      '/gear-guide',
    );
    expect(href, 'must NOT be the literal dead .md href').not.toBe('gear-guide.md');
  });

  test('clicking the guide link navigates in place to /gear-guide (full-page nav)', async ({
    page,
  }) => {
    await page.getByRole('link', { name: 'guide', exact: true }).first().click();
    await expect(page).toHaveURL(/\/gear-guide$/);
    await expect(page.locator('h1', { hasText: 'Gear Guide' })).toHaveCount(1);
  });
});

test.describe('Story 2.3 AC2 — relative resolution edge cases (assert emitted href values)', () => {
  test('same-dir + nested resolution from /x', async ({ page }) => {
    const res = await page.goto('/x');
    expect(res!.status()).toBe(200);
    expect(await hrefOf(page, 'guide'), 'same-dir gear-guide.md -> /gear-guide').toBe(
      '/gear-guide',
    );
    expect(await hrefOf(page, 'nested'), 'nested sub/page.md -> /sub/page').toBe('/sub/page');
  });

  test('cross-file fragment is preserved: gear-guide.md#heading-one -> /gear-guide#heading-one', async ({
    page,
  }) => {
    await page.goto('/x');
    expect(await hrefOf(page, 'other heading')).toBe('/gear-guide#heading-one');
  });

  test('space + percent-encoded filenames both decode+slug to /my-notes', async ({ page }) => {
    await page.goto('/x');
    expect(await hrefOf(page, 'notes space'), 'My Notes.md -> /my-notes').toBe('/my-notes');
    expect(await hrefOf(page, 'notes encoded'), 'My%20Notes.md -> /my-notes').toBe('/my-notes');
  });

  test('mixed-case filename is github-slugged (lower-cased): Gear-Guide.md -> /gear-guide', async ({
    page,
  }) => {
    await page.goto('/x');
    expect(await hrefOf(page, 'Gear Guide cased')).toBe('/gear-guide');
  });

  test('index.md collapses to its parent route: sub/index.md -> /sub', async ({ page }) => {
    await page.goto('/x');
    expect(await hrefOf(page, 'sub index')).toBe('/sub');
  });

  test('parent `..` resolution from a nested page /sub/page', async ({ page }) => {
    const res = await page.goto('/sub/page');
    expect(res!.status()).toBe(200);
    expect(await hrefOf(page, 'home'), '../x.md from /sub/page -> /x').toBe('/x');
    expect(await hrefOf(page, 'guide'), '../gear-guide.md from /sub/page -> /gear-guide').toBe(
      '/gear-guide',
    );
  });

  test('sibling + leading `./` resolution from /sub/page', async ({ page }) => {
    await page.goto('/sub/page');
    expect(await hrefOf(page, 'sibling two'), 'page2.md from /sub/page -> /sub/page2').toBe(
      '/sub/page2',
    );
    expect(await hrefOf(page, 'sibling'), './sibling.md from /sub/page -> /sub/sibling').toBe(
      '/sub/sibling',
    );
  });

  test('a `..` chain to the vault root collapses to /: ../index.md -> /', async ({ page }) => {
    await page.goto('/sub/page');
    expect(await hrefOf(page, 'root'), '../index.md from /sub/page -> / (never // or empty)').toBe(
      '/',
    );
  });
});

test.describe('Story 2.3 AC3 — non-internal links pass through unchanged', () => {
  test.beforeEach(async ({ page }) => {
    const res = await page.goto('/x');
    expect(res!.status()).toBe(200);
  });

  test('external http(s) link keeps its absolute href', async ({ page }) => {
    expect(await hrefOf(page, 'the site')).toBe('https://themarkdownweb.com');
  });

  test('mailto: link is untouched', async ({ page }) => {
    expect(await hrefOf(page, 'mail us')).toBe('mailto:hello@themarkdownweb.com');
  });

  test('root-absolute link is untouched', async ({ page }) => {
    expect(await hrefOf(page, 'already a route')).toBe('/already-a-route');
  });

  test('pure same-page #anchor is left untouched', async ({ page }) => {
    expect(await hrefOf(page, 'jump to lists')).toBe('#lists');
  });

  test('non-.md asset link is NOT rewritten to a route (Story 2.4 boundary)', async ({ page }) => {
    expect(await hrefOf(page, 'a pdf')).toBe('report.pdf');
  });

  test('a malformed %-escape target is left unrewritten (decode fails, never throws)', async ({
    page,
  }) => {
    expect(await hrefOf(page, 'malformed')).toBe('bad%zz.md');
  });

  test('a `..` chain that escapes the vault root is left unrewritten (not clamped, no /../)', async ({
    page,
  }) => {
    const href = await hrefOf(page, 'escape');
    expect(href, '../escape.md escapes content/ -> leave as authored').toBe('../escape.md');
    expect(href, 'must not emit a broken /../ route').not.toMatch(/^\/\.\./);
  });
});

test.describe('Story 2.3 AC4 — missing target -> true HTTP 404 on a custom not-found page', () => {
  test('the broken link still rewrites to its would-be route /does-not-exist', async ({ page }) => {
    await page.goto('/x');
    expect(await hrefOf(page, 'missing')).toBe('/does-not-exist');
  });

  test('visiting a missing route returns a real 404 status (not a soft-200)', async ({ page }) => {
    const res = await page.goto('/does-not-exist');
    expect(res, 'navigation response should exist').not.toBeNull();
    expect(res!.status(), 'unmatched route must carry a 404 status, not a soft-200').toBe(404);
  });

  test('the 404 response is the custom themed not-found page with a way home', async ({ page }) => {
    await page.goto('/does-not-exist');
    // Custom, well-formed not-found shell (same JS-free semantic Page layout).
    await expect(page.locator('html')).toHaveCount(1);
    expect(await page.locator('html').getAttribute('lang')).toBeTruthy();
    await expect(page.locator('main, article').first()).toBeVisible();
    // Exactly one <h1> and a clear "not found" message.
    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.locator('body')).toContainText(/not found/i);
    // Offers a way back home.
    await expect(page.locator('a[href="/"]')).toHaveCount(1);
  });

  test('clicking the broken link from /x lands on the not-found page (never a crash)', async ({
    page,
  }) => {
    await page.goto('/x');
    const [res] = await Promise.all([
      page.waitForNavigation(),
      page.getByRole('link', { name: 'missing', exact: true }).first().click(),
    ]);
    expect(res, 'navigation response should exist').not.toBeNull();
    expect(res!.status(), 'broken link target must 404').toBe(404);
    await expect(page).toHaveURL(/\/does-not-exist$/);
    await expect(page.locator('body')).toContainText(/not found/i);
  });
});

test.describe('Story 2.3 AC5 — browser Back/Forward via plain <a> full-page navigation', () => {
  test('follow internal link, Back returns to prior page, Forward re-advances', async ({ page }) => {
    const res = await page.goto('/x');
    expect(res!.status()).toBe(200);
    await expect(page.locator('h1', { hasText: 'Heading One' })).toHaveCount(1);

    // A -> B (internal link nav).
    await page.getByRole('link', { name: 'guide', exact: true }).first().click();
    await expect(page).toHaveURL(/\/gear-guide$/);
    await expect(page.locator('h1', { hasText: 'Gear Guide' })).toHaveCount(1);

    // Back -> A.
    await page.goBack();
    await expect(page).toHaveURL(/\/x$/);
    await expect(page.locator('h1', { hasText: 'Heading One' })).toHaveCount(1);

    // Forward -> B.
    await page.goForward();
    await expect(page).toHaveURL(/\/gear-guide$/);
    await expect(page.locator('h1', { hasText: 'Gear Guide' })).toHaveCount(1);
  });
});
