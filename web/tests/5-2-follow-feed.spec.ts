import { test, expect, type Page } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';

/**
 * Story 5.2 — Follow / Feed. (TDD RED phase.)
 *
 * These specs encode the contract for the static RSS feed at `/feed.xml`
 * BEFORE implementation. They MUST FAIL on the current build (no feed.xml,
 * no autodiscovery link, no subscribe affordance) and PASS once Story 5.2
 * ships the feed.
 *
 * AC1: /feed.xml exists; one <item> per vault page; each item has absolute
 *      canonical <link> + <guid isPermaLink="true">; title; pubDate;
 *      deterministic newest-first/id-tiebreak order.
 * AC2: new .md page surfaces as a new <item> after rebuild; guid build-stability.
 * AC3: well-formed valid RSS 2.0; XML-special chars escaped; Content-Type.
 * AC4: <link rel="alternate" type="application/rss+xml"> autodiscovery in
 *      page <head>; visible subscribe affordance in chrome.
 * AC6: regression — existing web specs still green.
 * AC7: harness over built dist/.
 *
 * Convention: mirrors 2-5-index.spec.ts + 5-1-living-link.spec.ts patterns.
 * Fetch /feed.xml via page.request.get('/feed.xml'); assert against the ACTUAL
 * rendered feed — never re-implement slug/URL logic in-test (anti-tautology).
 */

// The canonical origin (must match astro.config.mjs `site`).
const CANONICAL_ORIGIN = 'https://themarkdownweb.com';
const FEED_URL = '/feed.xml';
const FEED_ABSOLUTE_URL = `${CANONICAL_ORIGIN}/feed.xml`;

// The authoritative content-route set (one per content/**/*.md, with
// sub/index.md index-collapsed to /sub). Matches the 2.5 pinned set exactly,
// plus the committed 5-2-fixture.md added as the AC2 new-page-surfaces fixture.
// Order is the documented code-unit sort (Decision-B from 2.5 / tie-break for AC1).
const EXPECTED_ROUTES_SORTED = [
  '/5-2-fixture',
  '/empty',
  '/gear-guide',
  '/h1-only',
  '/my-notes',
  '/my-notes-dir/page',
  '/no-h1',
  '/readme',
  '/sub',
  '/sub/page',
  '/sub/page2',
  '/sub/sibling',
  '/x',
] as const;

const EXPECTED_COUNT = EXPECTED_ROUTES_SORTED.length; // 13

// Parse XML naively but reliably: extract all <item> blocks from the feed body.
// We use regex rather than a DOM parser because Playwright runs in Node context.
function extractItems(xml: string): { link: string; guid: string; isPermaLink: string; title: string; pubDate: string }[] {
  const items: { link: string; guid: string; isPermaLink: string; title: string; pubDate: string }[] = [];
  const itemMatches = xml.matchAll(/<item>([\s\S]*?)<\/item>/g);
  for (const m of itemMatches) {
    const block = m[1];
    const link = block.match(/<link>(.*?)<\/link>/)?.[1]?.trim() ?? '';
    const guidMatch = block.match(/<guid([^>]*)>(.*?)<\/guid>/);
    const guid = guidMatch?.[2]?.trim() ?? '';
    const isPermaLink = guidMatch?.[1]?.match(/isPermaLink="([^"]*)"/)?.[1] ?? '';
    const title = block.match(/<title>(.*?)<\/title>/)?.[1]?.trim() ?? '';
    const pubDate = block.match(/<pubDate>(.*?)<\/pubDate>/)?.[1]?.trim() ?? '';
    items.push({ link, guid, isPermaLink, title, pubDate });
  }
  return items;
}

// Check that the XML string is at minimum parseable (no unclosed < tags that
// would break a real parser) by verifying balanced tag pairs for key elements.
function assertWellFormedXml(xml: string): void {
  // Must start with XML or RSS declaration
  expect(
    xml.trim().startsWith('<?xml') || xml.trim().startsWith('<rss'),
    'feed must begin with an XML/RSS declaration or <rss> root',
  ).toBe(true);

  // Count opening vs closing for critical structural elements.
  const count = (s: string, sub: string) => (s.split(sub).length - 1);
  expect(count(xml, '<rss'), '<rss> must appear exactly once').toBe(1);
  expect(count(xml, '</rss>'), '</rss> must appear exactly once').toBe(1);
  expect(count(xml, '<channel>'), '<channel> must appear exactly once').toBe(1);
  expect(count(xml, '</channel>'), '</channel> must appear exactly once').toBe(1);

  // Opening and closing item counts must match.
  const openItems = count(xml, '<item>');
  const closeItems = count(xml, '</item>');
  expect(openItems, '<item> open/close counts must match').toBe(closeItems);
}

// ── AC3 / AC7 — /feed.xml exists and is fetched successfully ──────────────────
test.describe('Story 5.2 AC3/AC7 — /feed.xml exists, 200, correct Content-Type', () => {
  test('GET /feed.xml responds 200', async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    expect(res.status(), '/feed.xml must return 200').toBe(200);
  });

  test('/feed.xml Content-Type is application/rss+xml or application/xml', async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    expect(res.status()).toBe(200);
    const ct = res.headers()['content-type'] ?? '';
    // Accept application/rss+xml (preferred) or application/xml / text/xml (acceptable per AC3).
    expect(
      ct.includes('application/rss+xml') || ct.includes('application/xml') || ct.includes('text/xml'),
      `Content-Type "${ct}" must be application/rss+xml (or application/xml / text/xml)`,
    ).toBe(true);
  });

  test('/feed.xml body is non-empty XML text', async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    expect(res.status()).toBe(200);
    const body = await res.text();
    expect(body.trim(), 'feed body must not be empty').not.toBe('');
    expect(body, 'feed body must contain XML-like content').toContain('<');
  });
});

// ── AC3 — Well-formed valid RSS 2.0 ───────────────────────────────────────────
test.describe('Story 5.2 AC3 — well-formed valid RSS 2.0', () => {
  let feedXml: string = '';

  test.beforeEach(async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    expect(res.status()).toBe(200);
    feedXml = await res.text();
  });

  test('feed is well-formed XML (balanced structural tags)', async () => {
    assertWellFormedXml(feedXml);
  });

  test('<rss version="2.0"> root with xmlns:atom namespace', async () => {
    expect(feedXml, 'rss root must declare version="2.0"').toContain('version="2.0"');
    expect(feedXml, 'rss root must declare xmlns:atom namespace').toContain(
      'xmlns:atom="http://www.w3.org/2005/Atom"',
    );
  });

  test('channel has required children: <title>, <link>, <description>', async () => {
    // Channel-level (outside <item> blocks): extract channel portion.
    const channelMatch = feedXml.match(/<channel>([\s\S]*?)<\/channel>/);
    expect(channelMatch, '<channel> block must exist').toBeTruthy();
    const channelBlock = channelMatch![1];

    // Remove all <item>…</item> blocks to check channel-level tags only.
    const channelOnly = channelBlock.replace(/<item>[\s\S]*?<\/item>/g, '');

    expect(channelOnly, 'channel must have <title>').toContain('<title>');
    expect(channelOnly, 'channel must have <link>').toContain('<link>');
    expect(channelOnly, 'channel must have <description>').toContain('<description>');
  });

  test('channel has <lastBuildDate> in RFC-822 format', async () => {
    const channelMatch = feedXml.match(/<channel>([\s\S]*?)<\/channel>/);
    const channelOnly = channelMatch![1].replace(/<item>[\s\S]*?<\/item>/g, '');
    expect(channelOnly, 'channel must have <lastBuildDate>').toContain('<lastBuildDate>');
    const lbd = channelOnly.match(/<lastBuildDate>(.*?)<\/lastBuildDate>/)?.[1]?.trim() ?? '';
    // RFC-822 date: e.g. "Mon, 22 Jun 2026 00:00:00 GMT" — must be non-empty and parseable.
    expect(lbd, '<lastBuildDate> must be non-empty').not.toBe('');
    expect(!isNaN(Date.parse(lbd)), `<lastBuildDate> "${lbd}" must be parseable`).toBe(true);
  });

  test('channel has <atom:link rel="self"> pointing at the feed absolute URL', async () => {
    expect(feedXml, 'feed must have atom:link rel="self"').toContain('rel="self"');
    expect(feedXml, 'atom:link self must point at the feed absolute URL').toContain(FEED_ABSOLUTE_URL);
  });

  test('each item has required children: <title>, <link>, <guid>, <pubDate>', async () => {
    const items = extractItems(feedXml);
    expect(items.length, 'feed must have at least one item to test item structure').toBeGreaterThan(0);
    for (const item of items) {
      expect(item.title, `item with link "${item.link}" must have a non-empty <title>`).not.toBe('');
      expect(item.link, `item with title "${item.title}" must have a non-empty <link>`).not.toBe('');
      expect(item.guid, `item with link "${item.link}" must have a non-empty <guid>`).not.toBe('');
      expect(item.pubDate, `item with link "${item.link}" must have a non-empty <pubDate>`).not.toBe('');
    }
  });

  test('<pubDate> values are RFC-822 parseable dates', async () => {
    const items = extractItems(feedXml);
    for (const item of items) {
      expect(
        !isNaN(Date.parse(item.pubDate)),
        `item "${item.link}" <pubDate> "${item.pubDate}" must be a valid RFC-822 date`,
      ).toBe(true);
    }
  });

  test('XML-special chars in titles are escaped (& → &amp;, < → &lt;)', async () => {
    // The feed body must not contain raw & that is NOT part of an entity reference.
    // A raw & (not followed by amp;/lt;/gt;/quot;/apos;/#) in content indicates un-escaped XML.
    // We test via the raw bytes — raw & that is NOT an entity is invalid XML.
    const rawAmpersands = feedXml.match(/&(?!(amp|lt|gt|quot|apos|#)[;])/g);
    expect(
      rawAmpersands,
      'feed body must not contain raw & (must be escaped as &amp;)',
    ).toBeNull();
  });

  test('feed has no double-escaped entities (&amp;amp; indicates double-escaping)', async () => {
    expect(feedXml, 'feed must not double-escape: no &amp;amp;').not.toContain('&amp;amp;');
    expect(feedXml, 'feed must not double-escape: no &amp;lt;').not.toContain('&amp;lt;');
  });
});

// ── AC1 — Feed completeness: one item per vault page ─────────────────────────
test.describe('Story 5.2 AC1 — one <item> per vault page, absolute canonical URLs', () => {
  let feedXml: string = '';

  test.beforeEach(async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    expect(res.status()).toBe(200);
    feedXml = await res.text();
  });

  test('item count EQUALS the known content-page count (one-to-one invariant)', async () => {
    const items = extractItems(feedXml);
    expect(
      items.length,
      `feed must have exactly ${EXPECTED_COUNT} items (one per vault page), got ${items.length}`,
    ).toBe(EXPECTED_COUNT);
  });

  test('no item links the bare site root https://themarkdownweb.com/ (index filtered)', async () => {
    const items = extractItems(feedXml);
    const indexLinks = items.filter(
      (item) => item.link === CANONICAL_ORIGIN || item.link === `${CANONICAL_ORIGIN}/`,
    );
    expect(
      indexLinks.length,
      `no feed item must link the bare site root (index is not a content page); found: ${JSON.stringify(indexLinks)}`,
    ).toBe(0);
  });

  test('each item <link> is an absolute https://themarkdownweb.com/<slug> URL', async () => {
    const items = extractItems(feedXml);
    for (const item of items) {
      expect(
        item.link.startsWith(`${CANONICAL_ORIGIN}/`),
        `item link "${item.link}" must start with "${CANONICAL_ORIGIN}/"`,
      ).toBe(true);
      // Must not be the bare root.
      expect(
        item.link.length,
        `item link "${item.link}" must be longer than the bare site root`,
      ).toBeGreaterThan(`${CANONICAL_ORIGIN}/`.length - 1 + 1);
    }
  });

  test('a representative item link is byte-equal to that page\'s <link rel="canonical"> href', async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    const xml = await res.text();
    const items = extractItems(xml);
    // Pick a known item (/x) and compare to the page's canonical.
    const xItem = items.find((item) => item.link === `${CANONICAL_ORIGIN}/x`);
    expect(xItem, 'feed must have an item for /x').toBeTruthy();

    // Fetch the page and read its canonical.
    await page.goto('/x');
    const canonicalHref = await page.locator('head link[rel="canonical"]').getAttribute('href');
    expect(canonicalHref, '/x must have a <link rel="canonical">').toBeTruthy();
    expect(
      xItem!.link,
      'feed item link must be byte-equal to the page\'s <link rel="canonical">',
    ).toBe(canonicalHref);
  });

  test('each item <guid> equals its <link> and carries isPermaLink="true"', async () => {
    const items = extractItems(feedXml);
    for (const item of items) {
      expect(
        item.guid,
        `item with link "${item.link}" must have <guid> equal to <link>`,
      ).toBe(item.link);
      expect(
        item.isPermaLink,
        `item with link "${item.link}" <guid> must carry isPermaLink="true"`,
      ).toBe('true');
    }
  });

  test('every expected route has a corresponding feed item', async () => {
    const items = extractItems(feedXml);
    const feedLinks = new Set(items.map((item) => item.link));
    for (const route of EXPECTED_ROUTES_SORTED) {
      const expectedLink = `${CANONICAL_ORIGIN}${route}`;
      expect(
        feedLinks.has(expectedLink),
        `feed must have an item for ${route} (expected link: ${expectedLink})`,
      ).toBe(true);
    }
  });

  test('each item has a non-empty <title>', async () => {
    const items = extractItems(feedXml);
    for (const item of items) {
      expect(item.title, `item with link "${item.link}" must have a non-empty <title>`).not.toBe('');
    }
  });
});

// ── AC1 — Ordering: newest-first, code-unit id tie-break (all-equal-date boundary) ──
test.describe('Story 5.2 AC1 — deterministic ordering (all-equal-date tie-break)', () => {
  test('feed items appear in deterministic code-unit id order when all dates are equal', async ({ page }) => {
    // With the current all-undated vault, every item resolves to the same
    // build date. The ONLY ordering determinant is the code-unit id tie-break,
    // which MUST produce the same order as buildIndexItems' sort — i.e. the
    // EXPECTED_ROUTES_SORTED order defined above.
    const res = await page.request.get(FEED_URL);
    expect(res.status()).toBe(200);
    const feedXml = await res.text();
    const items = extractItems(feedXml);

    // Extract the route path from each item link (strip the origin).
    const feedRoutes = items.map((item) => item.link.replace(CANONICAL_ORIGIN, ''));

    // All pubDates must be equal (the all-undated-vault case: tie-break is the only determinant).
    const dates = items.map((item) => item.pubDate);
    const allEqual = dates.every((d) => d === dates[0]);

    if (allEqual) {
      // When all dates are equal, the order MUST match the code-unit id sort exactly.
      expect(
        feedRoutes,
        `when all items have equal dates, feed order must equal the code-unit id sort (EXPECTED_ROUTES_SORTED); got: ${JSON.stringify(feedRoutes)}`,
      ).toEqual([...EXPECTED_ROUTES_SORTED]);
    } else {
      // When dates differ: items with a later date must appear before items with an earlier date.
      for (let i = 0; i < items.length - 1; i++) {
        expect(
          new Date(items[i].pubDate).getTime(),
          `item at index ${i} must be >= item at index ${i + 1} (newest-first)`,
        ).toBeGreaterThanOrEqual(new Date(items[i + 1].pubDate).getTime());
      }
    }
  });

  test('feed order is CI-stable (identical on two consecutive requests)', async ({ page }) => {
    const res1 = await page.request.get(FEED_URL);
    const xml1 = await res1.text();
    const routes1 = extractItems(xml1).map((item) => item.link);

    const res2 = await page.request.get(FEED_URL);
    const xml2 = await res2.text();
    const routes2 = extractItems(xml2).map((item) => item.link);

    expect(routes1, 'feed item order must be identical across two requests (CI-stable)').toEqual(routes2);
  });
});

// ── AC2 — New page surfaces after rebuild ─────────────────────────────────────
test.describe('Story 5.2 AC2 — new page surfaces in the feed after rebuild', () => {
  /**
   * This test verifies the core epic outcome: add a .md page → it appears as
   * a new <item> in /feed.xml after the next build.
   *
   * The harness here tests that a known fixture page (5-2-fixture.md) already
   * committed to content/ appears as an item in the feed. The fixture MUST be
   * a committed file so the build includes it; if it doesn't yet exist, the
   * test fails in RED phase (the fixture won't appear until both the file
   * and the feed.xml.ts endpoint exist).
   *
   * NOTE: The fixture file `content/5-2-fixture.md` is expected to be created
   * as part of the Task 4 implementation. For the RED phase test, this test
   * ALSO fails because /feed.xml doesn't exist at all.
   */
  test('a known fixture page content/5-2-fixture.md appears as an item in the feed', async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    expect(res.status(), '/feed.xml must return 200').toBe(200);
    const feedXml = await res.text();
    const items = extractItems(feedXml);
    const fixtureLink = `${CANONICAL_ORIGIN}/5-2-fixture`;
    const fixtureItem = items.find((item) => item.link === fixtureLink);
    // This will fail in RED phase (no feed.xml; also the fixture file may not exist).
    expect(
      fixtureItem,
      `feed must contain an item for /5-2-fixture (link: ${fixtureLink}); if this is RED phase, the file or the feed endpoint does not yet exist`,
    ).toBeTruthy();
    expect(fixtureItem!.link, 'fixture item link must be the absolute canonical URL').toBe(fixtureLink);
    expect(fixtureItem!.title, 'fixture item must have a non-empty title').not.toBe('');
    expect(fixtureItem!.pubDate, 'fixture item must have a pubDate').not.toBe('');
  });
});

// ── AC2 — GUID build-stability (unchanged page must not re-surface) ────────────
test.describe('Story 5.2 AC2 — guid build-stability (no re-surfacing on rebuild)', () => {
  test('an existing item guid is byte-identical across two requests (build-stable)', async ({ page }) => {
    // The guid is derived from the canonical URL (entry.id), NOT from pubDate or
    // a build-derived token. Therefore it MUST be byte-identical across builds.
    const res1 = await page.request.get(FEED_URL);
    expect(res1.status()).toBe(200);
    const xml1 = await res1.text();
    const items1 = extractItems(xml1);

    // Find the /x item as the representative stable page.
    const xItem1 = items1.find((item) => item.link === `${CANONICAL_ORIGIN}/x`);
    expect(xItem1, 'feed must have an item for /x on first request').toBeTruthy();

    const res2 = await page.request.get(FEED_URL);
    const xml2 = await res2.text();
    const items2 = extractItems(xml2);
    const xItem2 = items2.find((item) => item.link === `${CANONICAL_ORIGIN}/x`);
    expect(xItem2, 'feed must have an item for /x on second request').toBeTruthy();

    expect(
      xItem1!.guid,
      'guid for /x must be byte-identical across two requests (build-stable — unchanged page must not re-surface)',
    ).toBe(xItem2!.guid);
  });
});

// ── AC4 — Autodiscovery <link rel="alternate" type="application/rss+xml"> ────
test.describe('Story 5.2 AC4 — autodiscovery <link rel="alternate"> in <head>', () => {
  // Test autodiscovery on multiple surfaces (mirrors 5.1 SURFACES loop pattern).
  const AUTODISCOVERY_SURFACES = [
    { name: 'content route /x', path: '/x' },
    { name: 'vault index /', path: '/' },
    { name: 'nested content /sub/page', path: '/sub/page' },
  ];

  for (const s of AUTODISCOVERY_SURFACES) {
    test(`${s.name}: exactly one <link rel="alternate" type="application/rss+xml"> in <head>`, async ({ page }) => {
      const res = await page.goto(s.path);
      expect(res!.status(), `${s.path} must return 200`).toBe(200);

      const autodiscoveryLinks = page.locator(
        'head link[rel="alternate"][type="application/rss+xml"]',
      );
      await expect(
        autodiscoveryLinks,
        `${s.path} must have exactly one autodiscovery <link rel="alternate" type="application/rss+xml">`,
      ).toHaveCount(1);
    });

    test(`${s.name}: autodiscovery href is the absolute /feed.xml URL`, async ({ page }) => {
      await page.goto(s.path);
      const href = await page
        .locator('head link[rel="alternate"][type="application/rss+xml"]')
        .getAttribute('href');
      expect(href, `autodiscovery href on ${s.path} must be non-empty`).toBeTruthy();
      expect(
        href,
        `autodiscovery href on ${s.path} must be the absolute feed URL`,
      ).toBe(FEED_ABSOLUTE_URL);
    });

    test(`${s.name}: autodiscovery <link> is static HTML (present in raw response, no JS)`, async ({ page }) => {
      const res = await page.goto(s.path);
      const html = await res!.text();
      expect(html, `${s.path} raw HTML must contain rel="alternate"... rss`).toContain(
        'rel="alternate"',
      );
      expect(html, `${s.path} raw HTML autodiscovery must reference the feed URL`).toContain(
        '/feed.xml',
      );
    });
  }

  test('autodiscovery <link> is present with JavaScript disabled', async ({ browser }) => {
    const ctx = await browser.newContext({ javaScriptEnabled: false });
    const noJsPage = await ctx.newPage();
    const res = await noJsPage.goto('/x');
    expect(res!.status()).toBe(200);

    await expect(
      noJsPage.locator('head link[rel="alternate"][type="application/rss+xml"]'),
      'autodiscovery <link> must be present even with JavaScript disabled (static HTML)',
    ).toHaveCount(1);

    const href = await noJsPage
      .locator('head link[rel="alternate"][type="application/rss+xml"]')
      .getAttribute('href');
    expect(href, 'autodiscovery href must be present with JS off').toBe(FEED_ABSOLUTE_URL);

    await ctx.close();
  });

  test('404 page also carries the autodiscovery <link> (inherited from shared Page.astro layout)', async ({ page }) => {
    await page.goto('/this-route-does-not-exist-5-2-test');
    // The 404 page also inherits Page.astro, so autodiscovery must be present.
    await expect(
      page.locator('head link[rel="alternate"][type="application/rss+xml"]'),
    ).toHaveCount(1);
  });
});

// ── AC4 — Visible subscribe affordance in the chrome ─────────────────────────
test.describe('Story 5.2 AC4 — visible subscribe/RSS feed link in the chrome', () => {
  test('a "Subscribe" or "RSS feed" link exists in the header', async ({ page }) => {
    await page.goto('/x');
    // The subscribe link is a real <a> (navigates to a static asset — no JS).
    // Match either "Subscribe" or "RSS feed" as the accessible name.
    const subscribeLink = page.locator('header').getByRole('link', {
      name: /Subscribe|RSS feed/i,
    });
    await expect(
      subscribeLink,
      'header chrome must have a visible Subscribe/RSS feed link',
    ).toHaveCount(1);
  });

  test('subscribe link is keyboard-focusable', async ({ page }) => {
    await page.goto('/x');
    const subscribeLink = page.locator('header').getByRole('link', {
      name: /Subscribe|RSS feed/i,
    });
    await subscribeLink.focus();
    await expect(subscribeLink, 'subscribe link must be keyboard-focusable').toBeFocused();
  });

  test('subscribe link href points to /feed.xml', async ({ page }) => {
    await page.goto('/x');
    const subscribeLink = page.locator('header').getByRole('link', {
      name: /Subscribe|RSS feed/i,
    });
    const href = await subscribeLink.getAttribute('href');
    expect(href, 'subscribe link href must not be empty').toBeTruthy();
    expect(
      href!.includes('/feed.xml') || href === FEED_ABSOLUTE_URL,
      `subscribe link href "${href}" must point at /feed.xml or the absolute feed URL`,
    ).toBe(true);
  });

  test('subscribe link has a non-empty accessible name', async ({ page }) => {
    await page.goto('/x');
    const subscribeLink = page.locator('header').getByRole('link', {
      name: /Subscribe|RSS feed/i,
    });
    await expect(subscribeLink, 'subscribe link must be visible').toBeVisible();
  });

  test('subscribe link navigates to the feed (GET /feed.xml → 200)', async ({ page }) => {
    // Verify the link target is actually live.
    const feedRes = await page.request.get('/feed.xml');
    expect(feedRes.status(), 'subscribe link target /feed.xml must return 200').toBe(200);
  });

  test('subscribe link is present on the index / as well', async ({ page }) => {
    await page.goto('/');
    const subscribeLink = page.locator('header').getByRole('link', {
      name: /Subscribe|RSS feed/i,
    });
    await expect(
      subscribeLink,
      'subscribe link must be present on the index page too',
    ).toHaveCount(1);
  });
});

// ── AC6 — Regression: existing chrome elements still present ──────────────────
test.describe('Story 5.2 AC6 — no regression on existing 5.1 / 2.6 chrome elements', () => {
  test('adding subscribe link does not remove "Get the client" CTA from header', async ({ page }) => {
    await page.goto('/x');
    await expect(
      page.locator('header').getByRole('link', { name: 'Get the client', exact: true }),
      '"Get the client" CTA must still be present in header after 5.2 adds subscribe link',
    ).toHaveCount(1);
  });

  test('adding subscribe link does not remove "Copy link" button from header', async ({ page }) => {
    await page.goto('/x');
    await expect(
      page.locator('header').getByRole('button', { name: 'Copy link', exact: true }),
      '"Copy link" button must still be present in header after 5.2 adds subscribe link',
    ).toHaveCount(1);
  });

  test('adding subscribe link does not add a second <h1>', async ({ page }) => {
    await page.goto('/x');
    await expect(page.locator('h1'), 'there must be exactly one <h1> after 5.2 additions').toHaveCount(1);
  });

  test('canonical <link> in <head> is still present and correct after 5.2 additions', async ({ page }) => {
    await page.goto('/x');
    await expect(page.locator('head link[rel="canonical"]')).toHaveCount(1);
    const href = await page.locator('head link[rel="canonical"]').getAttribute('href');
    expect(href, 'canonical must still be the absolute /x URL').toBe('https://themarkdownweb.com/x');
  });
});

// ── AC7 — Harness: dist/feed.xml is present as a build artifact ───────────────
test.describe('Story 5.2 AC7 — harness over built dist/feed.xml', () => {
  test('/feed.xml is served from dist/ (static asset, not SSR)', async ({ page }) => {
    // The feed is a static endpoint emitting dist/feed.xml — verify it is
    // served as a normal static asset (200, not redirected or SSR-generated per-request).
    const res = await page.request.get(FEED_URL);
    expect(res.status(), 'dist/feed.xml must be served at /feed.xml').toBe(200);
  });

  test('each feed item URL resolves to a live 200 page', async ({ page }) => {
    const res = await page.request.get(FEED_URL);
    expect(res.status()).toBe(200);
    const feedXml = await res.text();
    const items = extractItems(feedXml);
    // Verify a sample of item links are live (avoid requesting every single one for perf).
    const sampleItems = items.slice(0, Math.min(5, items.length));
    for (const item of sampleItems) {
      // Convert absolute URL to path for the local preview server.
      const localPath = item.link.replace(CANONICAL_ORIGIN, '');
      const pageRes = await page.request.get(localPath);
      expect(
        pageRes.status(),
        `feed item ${item.link} must resolve to a live 200 page (local path: ${localPath})`,
      ).toBe(200);
    }
  });
});
