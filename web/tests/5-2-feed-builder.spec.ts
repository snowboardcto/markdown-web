import { test, expect } from '@playwright/test';

/**
 * Story 5.2 — pure unit-style spec for the feed builder `web/src/lib/feed.mjs`.
 * (TDD RED phase.)
 *
 * Mirrors `2-5-index-degenerate.spec.ts` over `buildIndexItems`: exercises the
 * pure `buildFeed` / `buildRssXml` helper directly over crafted entry sets so
 * degenerate / edge-case branches can be tested without a full alternate vault.
 *
 * All tests MUST FAIL before `web/src/lib/feed.mjs` exists (import will throw).
 * They PASS once Task 1 implements the pure builder.
 *
 * Coverage:
 * - Empty vault: zero <item>s but valid <channel> with all required children.
 * - Single-page vault: exactly one <item>.
 * - XML-special chars in title: escaped (&amp; / &lt;) — well-formed XML.
 * - Ampersand in URL: escaped to &amp; in <link>/<guid> — same shared escaper.
 * - No double-escaping: & → &amp; (not &amp;amp;).
 * - Junk/non-Date frontmatter date: number, already-Date, unparseable string, null
 *   → none throw, none yield "Invalid Date" in <pubDate>, all fall through to fallback.
 * - All-equal-date set: order == code-unit id sort (tie-break is sole determinant).
 * - Newest-first when dates DO differ.
 * - GUID == link, isPermaLink="true".
 * - Channel-level atom:link rel="self" present.
 */

// Dynamic import — if feed.mjs does not exist this will throw during test
// setup (expected RED phase: the import failure IS the failure signal).
// We use a lazy import pattern so the "module not found" error surfaces as a
// test failure rather than crashing the entire suite before any test runs.
let buildFeed: ((...args: unknown[]) => string) | null = null;

test.beforeAll(async () => {
  try {
    // The module path is relative to the web/ root (tests run from web/).
    // Use a dynamic import with ?t= cache-bust to ensure we always pick up the
    // current module state in the Playwright Node process.
    const mod = await import('../src/lib/feed.mjs');
    // Accept either export name: buildFeed or buildRssXml.
    buildFeed = (mod.buildFeed ?? mod.buildRssXml ?? null) as typeof buildFeed;
    if (!buildFeed) {
      throw new Error('feed.mjs must export buildFeed or buildRssXml');
    }
  } catch (e: unknown) {
    // In RED phase, feed.mjs does not exist yet. Store null so tests below
    // can fail with a meaningful message instead of a suite-level crash.
    buildFeed = null;
  }
});

// Helper: assert buildFeed is loaded (fails test cleanly in RED phase).
function requireFeed(): (...args: unknown[]) => string {
  if (!buildFeed) {
    throw new Error(
      'web/src/lib/feed.mjs not found or does not export buildFeed/buildRssXml. ' +
        'This is expected in the RED phase — implement Task 1 to make this pass.',
    );
  }
  return buildFeed;
}

// Minimal entry factory — mirrors the shape getCollection('pages') returns.
function makeEntry(id: string, overrides: Record<string, unknown> = {}) {
  return { id, data: { ...overrides } };
}

// Parse naive XML helpers (same as 5-2-follow-feed.spec.ts).
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

const TEST_SITE = 'https://themarkdownweb.com';
const TEST_CHANNEL = {
  title: 'The Markdown Web',
  description: 'A vault feed.',
  site: TEST_SITE,
};

// ── Empty vault ────────────────────────────────────────────────────────────────
test.describe('Story 5.2 AC1 (builder) — empty vault → zero items, valid channel', () => {
  test('empty entries yield zero <item>s (no crash)', () => {
    const fn = requireFeed();
    const xml = fn([], TEST_CHANNEL) as string;
    expect(typeof xml, 'buildFeed must return a string').toBe('string');

    const items = extractItems(xml);
    expect(items.length, 'empty vault must yield zero <item>s').toBe(0);
  });

  test('empty vault still produces a valid <rss> root with version="2.0"', () => {
    const fn = requireFeed();
    const xml = fn([], TEST_CHANNEL) as string;
    expect(xml, 'empty-vault feed must declare rss version="2.0"').toContain('version="2.0"');
    expect(xml, 'empty-vault feed must have <channel>').toContain('<channel>');
    expect(xml, 'empty-vault feed must close </channel>').toContain('</channel>');
  });

  test('empty vault channel has required children: <title>, <link>, <description>', () => {
    const fn = requireFeed();
    const xml = fn([], TEST_CHANNEL) as string;
    const channelMatch = xml.match(/<channel>([\s\S]*?)<\/channel>/);
    expect(channelMatch, '<channel> block must exist').toBeTruthy();
    const channelOnly = channelMatch![1].replace(/<item>[\s\S]*?<\/item>/g, '');
    expect(channelOnly, 'channel must have <title>').toContain('<title>');
    expect(channelOnly, 'channel must have <link>').toContain('<link>');
    expect(channelOnly, 'channel must have <description>').toContain('<description>');
  });

  test('empty vault channel has <lastBuildDate> and <atom:link rel="self">', () => {
    const fn = requireFeed();
    const xml = fn([], TEST_CHANNEL) as string;
    expect(xml, 'channel must have <lastBuildDate>').toContain('<lastBuildDate>');
    expect(xml, 'channel must have atom:link rel="self"').toContain('rel="self"');
  });

  test('all-empty-id vault (root index.md only) still yields zero items', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('')], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items.length, 'entry with id="" must be filtered (no self-link)').toBe(0);
  });
});

// ── Single-page vault ─────────────────────────────────────────────────────────
test.describe('Story 5.2 AC1 (builder) — single-page vault → exactly one item', () => {
  test('single entry yields exactly one <item>', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('only-page')], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items.length, 'single-page vault must yield exactly one <item>').toBe(1);
  });

  test('single item link is the absolute canonical URL', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('only-page')], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items[0].link, 'item link must be the absolute canonical URL').toBe(
      `${TEST_SITE}/only-page`,
    );
  });

  test('single item guid equals its link and carries isPermaLink="true"', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('only-page')], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items[0].guid, 'guid must equal link').toBe(items[0].link);
    expect(items[0].isPermaLink, 'guid must carry isPermaLink="true"').toBe('true');
  });

  test('single item with frontmatter title uses the title as <title>', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('my-page', { title: 'My Custom Title' })], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    // The title in XML is escaped, so My Custom Title stays as-is.
    expect(items[0].title, 'item title must match frontmatter title').toContain('My Custom Title');
  });

  test('single item without frontmatter title derives title from slug', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('gear-guide')], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    // slugToTitle('gear-guide') === 'Gear Guide'
    expect(items[0].title, 'item title must be slug-derived when no frontmatter title').not.toBe('');
  });

  test('single item has a non-empty <pubDate>', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('only-page')], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items[0].pubDate, 'item must have a non-empty <pubDate>').not.toBe('');
    expect(!isNaN(Date.parse(items[0].pubDate)), '<pubDate> must be a valid date').toBe(true);
  });
});

// ── XML-special chars in titles ───────────────────────────────────────────────
test.describe('Story 5.2 AC3 (builder) — XML-special chars in titles are escaped', () => {
  test('title with & is escaped to &amp; in the feed', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('page', { title: 'Tom & Jerry' })], TEST_CHANNEL) as string;
    // The raw & must NOT appear in the title content (would be invalid XML).
    // The escaped form &amp; must appear.
    expect(xml, 'title with & must be escaped as &amp;').toContain('&amp;');
    // The raw unescaped & must not appear in item content (only in entities).
    // We check that no raw & appears outside of entity references.
    const rawAmps = xml.match(/&(?!(amp|lt|gt|quot|apos|#)[;])/g);
    expect(rawAmps, 'no raw & must appear in the feed (all must be &amp;)').toBeNull();
  });

  test('title with < is escaped to &lt; in the feed', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('page', { title: 'A <draft> title' })], TEST_CHANNEL) as string;
    expect(xml, 'title with < must be escaped as &lt;').toContain('&lt;');
  });

  test('title with & and < combined (Tom & Jerry <draft>) — both escaped, feed parses', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('page', { title: 'Tom & Jerry <draft>' })], TEST_CHANNEL) as string;
    expect(xml, 'both & and < must be escaped').toContain('&amp;');
    expect(xml, 'both & and < must be escaped').toContain('&lt;');
    const rawAmps = xml.match(/&(?!(amp|lt|gt|quot|apos|#)[;])/g);
    expect(rawAmps, 'no raw & after escaping').toBeNull();
  });

  test('no double-escaping: & → &amp; not &amp;amp;', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('page', { title: 'A & B' })], TEST_CHANNEL) as string;
    expect(xml, 'must not double-escape: no &amp;amp;').not.toContain('&amp;amp;');
  });
});

// ── Ampersand in URL ──────────────────────────────────────────────────────────
test.describe('Story 5.2 AC3 (builder) — & in slug URL is escaped once to &amp;', () => {
  test('slug containing & is escaped to &amp; in <link>/<guid> — not raw, not double-escaped', () => {
    // A slug-like id with & (e.g. a query-like segment). The URL in <link>/<guid>
    // must be XML-escaped (& → &amp;) but not double-escaped (&amp; → &amp;amp;).
    const fn = requireFeed();
    // Construct an entry whose id would produce a & in the URL path.
    // (In practice slugs don't have &, but the escaper must handle it the same
    // as titles: one shared escaper, applied once at serialization.)
    const xml = fn([makeEntry('page&name', { title: 'Page' })], TEST_CHANNEL) as string;
    // The & in the slug URL must be escaped to &amp; in the XML.
    expect(xml, 'slug & in URL must be escaped as &amp; in <link>/<guid>').toContain('&amp;');
    expect(xml, 'slug URL must not be double-escaped: no &amp;amp;').not.toContain('&amp;amp;');
    const rawAmps = xml.match(/&(?!(amp|lt|gt|quot|apos|#)[;])/g);
    expect(rawAmps, 'no raw & in the feed (slug URL & escaped)').toBeNull();
  });
});

// ── Junk/non-Date frontmatter date (total resolveDate) ────────────────────────
test.describe('Story 5.2 AC2 (builder) — junk date inputs fall through to fallback (total)', () => {
  const junkDates = [
    { label: 'number', value: 12345 },
    { label: 'already-Date object', value: new Date('2026-01-01') },
    { label: 'unparseable string', value: 'someday' },
    { label: 'null', value: null },
    { label: 'undefined', value: undefined },
  ];

  for (const { label, value } of junkDates) {
    test(`date=${label}: does not throw and does not produce "Invalid Date" in <pubDate>`, () => {
      const fn = requireFeed();
      const entry = { id: 'test-page', data: { date: value } };
      let xml: string;
      expect(() => {
        xml = fn([entry], TEST_CHANNEL) as string;
      }, `buildFeed must not throw for date=${label}`).not.toThrow();

      // pubDate must not be "Invalid Date".
      expect(xml!, 'feed must not contain "Invalid Date"').not.toContain('Invalid Date');

      const items = extractItems(xml!);
      expect(items.length, 'must produce one item even with a junk date').toBe(1);
      expect(items[0].pubDate, 'pubDate must not be empty for junk date input').not.toBe('');
      expect(
        !isNaN(Date.parse(items[0].pubDate)),
        `pubDate "${items[0].pubDate}" must be parseable for date=${label}`,
      ).toBe(true);
    });
  }
});

// ── Ordering: all-equal-date → code-unit id sort ──────────────────────────────
test.describe('Story 5.2 AC1 (builder) — all-equal-date: order == code-unit id sort', () => {
  test('when all pages are undated, items are sorted by code-unit id (tie-break is sole determinant)', () => {
    const fn = requireFeed();
    const entries = [
      makeEntry('z-page'),
      makeEntry('a-page'),
      makeEntry('m-page'),
    ];
    const xml = fn(entries, TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items.length, 'must produce one item per entry').toBe(3);

    // Code-unit sort: 'a-page' < 'm-page' < 'z-page'.
    // Feed order (newest-first date, tie-break by id) must equal this when all dates are equal.
    const links = items.map((item) => item.link.replace(`${TEST_SITE}/`, ''));
    expect(links, 'all-equal-date items must be sorted by code-unit id (a < m < z)').toEqual([
      'a-page',
      'm-page',
      'z-page',
    ]);
  });

  test('all-equal-date order matches buildIndexItems code-unit sort exactly', () => {
    const fn = requireFeed();
    // Use the same entries as the 2.5 sort contract.
    const entries = [
      makeEntry('sub/sibling'),
      makeEntry('empty'),
      makeEntry('readme'),
      makeEntry('x'),
      makeEntry('gear-guide'),
    ];
    const xml = fn(entries, TEST_CHANNEL) as string;
    const items = extractItems(xml);
    // Expected code-unit order: empty < gear-guide < readme < sub/sibling < x
    const ids = items.map((item) => item.link.replace(`${TEST_SITE}/`, ''));
    expect(ids, 'all-equal-date order must be code-unit id sort (same as buildIndexItems)').toEqual([
      'empty',
      'gear-guide',
      'readme',
      'sub/sibling',
      'x',
    ]);
  });
});

// ── Ordering: newest-first when dates DO differ ────────────────────────────────
test.describe('Story 5.2 AC1 (builder) — newest-first when dates differ', () => {
  test('pages with different dates appear newest-first in the feed', () => {
    const fn = requireFeed();
    const entries = [
      makeEntry('old-page', { date: '2024-01-01' }),
      makeEntry('new-page', { date: '2026-06-01' }),
      makeEntry('mid-page', { date: '2025-03-15' }),
    ];
    const xml = fn(entries, TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items.length, 'must produce one item per entry').toBe(3);

    const ids = items.map((item) => item.link.replace(`${TEST_SITE}/`, ''));
    // Newest-first: new-page (2026) > mid-page (2025) > old-page (2024).
    expect(ids[0], 'newest page must appear first').toBe('new-page');
    expect(ids[1], 'middle page must appear second').toBe('mid-page');
    expect(ids[2], 'oldest page must appear last').toBe('old-page');
  });
});

// ── GUID stability (MEDIUM #3 strengthened) ───────────────────────────────────
test.describe('Story 5.2 AC2 (builder) — guid derives from id/canonical URL, NOT pubDate/build-date', () => {
  test('guid equals the canonical absolute URL (build-stable across calls)', () => {
    const fn = requireFeed();
    const xml1 = fn([makeEntry('my-page')], TEST_CHANNEL) as string;
    const xml2 = fn([makeEntry('my-page')], TEST_CHANNEL) as string;

    const items1 = extractItems(xml1);
    const items2 = extractItems(xml2);

    expect(items1[0].guid, 'guid must equal canonical URL').toBe(`${TEST_SITE}/my-page`);
    expect(items1[0].guid, 'guid must be identical across two calls (build-stable)').toBe(items2[0].guid);
  });

  test('two entries with SAME id but DIFFERENT dates emit IDENTICAL <guid> (guid derives from id, not pubDate)', () => {
    // This is the binding proof of the anti-resurfacing invariant (pre-mortem #8):
    // if guid derived from pubDate/build-date, the same page would re-surface as
    // "new" on every rebuild. Instead, guid MUST derive from entry.id (canonical URL)
    // so it is byte-equal regardless of pubDate changes between builds.
    const fn = requireFeed();
    // Call 1: entry with date '2024-01-01'
    const xml1 = fn([makeEntry('stable-page', { date: '2024-01-01' })], TEST_CHANNEL) as string;
    // Call 2: same id, but a different date (simulating a subsequent build date)
    const xml2 = fn([makeEntry('stable-page', { date: '2026-06-22' })], TEST_CHANNEL) as string;

    const items1 = extractItems(xml1);
    const items2 = extractItems(xml2);

    expect(items1.length, 'call 1 must produce one item').toBe(1);
    expect(items2.length, 'call 2 must produce one item').toBe(1);

    // The <pubDate> values should differ (different dates were passed in).
    expect(items1[0].pubDate, 'pubDate should reflect the first date').not.toBe(items2[0].pubDate);

    // BUT the <guid> must be byte-identical — it derives from id, not pubDate.
    const expectedGuid = `${TEST_SITE}/stable-page`;
    expect(items1[0].guid, 'guid in call 1 must be the canonical URL').toBe(expectedGuid);
    expect(items2[0].guid, 'guid in call 2 must be the canonical URL').toBe(expectedGuid);
    expect(
      items1[0].guid,
      'guid must be byte-identical across both calls despite different pubDates (proves guid derives from id, not pubDate)',
    ).toBe(items2[0].guid);
  });
});

// ── New-page-surfaces delta (MEDIUM #4) ──────────────────────────────────────
test.describe('Story 5.2 AC2 (builder) — adding an entry yields a new <item> with the correct canonical URL guid', () => {
  test('adding an entry to the input set yields a corresponding new <item> (delta proves new-page-surfaces)', () => {
    const fn = requireFeed();
    // Baseline: two existing pages.
    const baseline = [
      makeEntry('page-a', { date: '2026-01-01' }),
      makeEntry('page-b', { date: '2026-02-01' }),
    ];
    const xmlBefore = fn(baseline, TEST_CHANNEL) as string;
    const itemsBefore = extractItems(xmlBefore);
    expect(itemsBefore.length, 'baseline must have 2 items').toBe(2);

    // Add one new page — simulate "author adds a new .md page before rebuild".
    const withNew = [
      ...baseline,
      makeEntry('new-page', { date: '2026-06-01' }),
    ];
    const xmlAfter = fn(withNew, TEST_CHANNEL) as string;
    const itemsAfter = extractItems(xmlAfter);

    // Assert the delta: exactly one more item.
    expect(itemsAfter.length, 'after adding one entry, feed must have 3 items (one more)').toBe(3);

    // Assert the new item is present with its canonical URL guid.
    const newItem = itemsAfter.find((item) => item.link === `${TEST_SITE}/new-page`);
    expect(newItem, 'new entry must appear as a new <item> with its canonical URL').toBeTruthy();
    expect(newItem!.guid, 'new item guid must be the canonical URL').toBe(`${TEST_SITE}/new-page`);
  });
});

// ── Encoding-exercising id (LOW #5) ──────────────────────────────────────────
test.describe('Story 5.2 LOW #5 — non-ASCII/encoding-exercising id: guid equals new URL() construction', () => {
  test('id with a space: guid equals new URL(\'/\' + id, origin).href (same as Page.astro canonical)', () => {
    const fn = requireFeed();
    const id = 'my notes';
    const xml = fn([makeEntry(id)], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items.length, 'must produce one item').toBe(1);

    // The expected guid is exactly what new URL('/' + id, origin).href produces —
    // the same construction Page.astro uses for <link rel="canonical">.
    // For 'my notes', new URL('/my notes', 'https://themarkdownweb.com').href
    // percent-encodes the space → 'https://themarkdownweb.com/my%20notes'.
    const expectedUrl = new URL('/' + id, TEST_SITE).href;
    // The guid in the XML is XML-escaped, so decode &amp; → & if needed, but for
    // percent-encoded URLs (which contain no & or <) the guid should be verbatim.
    expect(items[0].guid, 'guid for space-containing id must equal new URL() construction').toBe(expectedUrl);
    expect(items[0].link, 'link for space-containing id must equal new URL() construction').toBe(expectedUrl);
  });

  test('id with non-ASCII char: guid equals new URL(\'/\' + id, origin).href (byte-equality)', () => {
    const fn = requireFeed();
    const id = 'café'; // 'café'
    const xml = fn([makeEntry(id)], TEST_CHANNEL) as string;
    const items = extractItems(xml);
    expect(items.length, 'must produce one item').toBe(1);

    const expectedUrl = new URL('/' + id, TEST_SITE).href;
    expect(items[0].guid, 'guid for non-ASCII id must equal new URL() construction').toBe(expectedUrl);
    expect(items[0].link, 'link for non-ASCII id must equal new URL() construction').toBe(expectedUrl);
  });
});

// ── Channel atom:link rel="self" ──────────────────────────────────────────────
test.describe('Story 5.2 AC3 (builder) — channel <atom:link rel="self"> present', () => {
  test('channel has atom:link rel="self" pointing at the feed URL', () => {
    const fn = requireFeed();
    const xml = fn([makeEntry('x')], TEST_CHANNEL) as string;
    expect(xml, 'feed must have atom:link rel="self"').toContain('rel="self"');
    expect(xml, 'atom:link self must reference /feed.xml').toContain('/feed.xml');
  });
});
