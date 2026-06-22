/**
 * Story 5.2 — Follow / Feed: pure RSS 2.0 feed builder.
 *
 * Exports `buildFeed(entries, channelMeta)` — a pure, total function that takes
 * raw `getCollection('pages')` entries (or crafted test entries) plus channel
 * metadata and returns a valid, well-formed RSS 2.0 XML string.
 *
 * Design decisions (recorded in Dev Agent Record):
 *   - RSS 2.0, hand-emitted (no @astrojs/rss dep — AC5/NFR-7/2.5 no-dep precedent).
 *   - Date policy: frontmatter `date` or `pubDate` if parseable → else build-time
 *     channel date (passed in via channelMeta.buildDate or computed once per call).
 *     All dates formatted as RFC-822 UTC. Never "Invalid Date". Never drops a page.
 *   - GUID = canonical absolute URL (entry.id-derived, build-stable) — NOT pubDate.
 *     Feed readers key "already seen" off guid; using the canonical URL means an
 *     unchanged page never re-surfaces as new even though its pubDate moves.
 *   - Ordering: newest-first by resolved date, tie-broken by code-unit entry.id
 *     (ICU-independent, mirrors buildIndexItems — so when all pages are undated,
 *     the feed order == the index order exactly).
 *   - Escaping: a single shared xmlEscape() applied at every dynamic node (titles,
 *     descriptions, URLs). Applied once — never double-escaped.
 *   - Empty vault: zero <item>s but the channel block is still fully formed.
 *   - REUSES buildIndexItems from index-entries.mjs for the page set / labels / sort.
 */

import { buildIndexItems } from './index-entries.mjs';

// ── XML escaper ───────────────────────────────────────────────────────────────
/**
 * Escape a string for use as XML text content or attribute value.
 * Applied ONCE at serialization; never re-applied to already-escaped text.
 * Handles: & < > " '
 * @param {string} s
 * @returns {string}
 */
function xmlEscape(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

// ── RFC-822 date formatter ────────────────────────────────────────────────────
const RFC822_DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const RFC822_MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/**
 * Format a Date as RFC-822 UTC (e.g. "Mon, 22 Jun 2026 00:00:00 GMT").
 * Fixed-locale, UTC — identical dev↔CI (no toLocaleString).
 * @param {Date} d
 * @returns {string}
 */
function toRfc822(d) {
  const day = RFC822_DAYS[d.getUTCDay()];
  const dd = String(d.getUTCDate()).padStart(2, '0');
  const mon = RFC822_MONTHS[d.getUTCMonth()];
  const yyyy = d.getUTCFullYear();
  const hh = String(d.getUTCHours()).padStart(2, '0');
  const mm = String(d.getUTCMinutes()).padStart(2, '0');
  const ss = String(d.getUTCSeconds()).padStart(2, '0');
  return `${day}, ${dd} ${mon} ${yyyy} ${hh}:${mm}:${ss} GMT`;
}

// ── Date resolution (total / CI-stable) ──────────────────────────────────────
/**
 * Resolve a page's publication date from frontmatter, with a fallback.
 *
 * Precedence:
 *   1. entry.data.date if it is a valid parseable Date (string or Date object).
 *   2. entry.data.pubDate if it is a valid parseable Date.
 *   3. fallbackDate (the channel build date, passed in — same for all undated pages).
 *
 * Total: never throws, never returns Invalid Date. A number, already-Date,
 * unparseable string, null, or undefined all fall through to the fallback.
 *
 * Trade-off documented (AC2 second-order note): because today NO page has a
 * frontmatter date, the fallback applies to ALL pages, so every undated page's
 * <pubDate> = the channel build date. Two consequences, both intentional:
 *   (a) Ordering degenerates to the code-unit id tie-break (feed order == index order).
 *   (b) <pubDate> is NOT a stable per-item identity across builds (it moves to each
 *       build date). Item identity MUST come from <guid isPermaLink="true"> = the
 *       canonical URL (derived from entry.id — build-stable).
 *
 * We use the channelBuildDate (the Date object created once per buildFeed call)
 * rather than Date.now() per-item to keep the feed diff quiet when content is
 * unchanged (all undated items share the same build date within one build).
 *
 * @param {{ data?: Record<string, unknown> }} entry
 * @param {Date} fallbackDate
 * @returns {Date}
 */
function resolveDate(entry, fallbackDate) {
  const candidates = [
    entry && entry.data ? entry.data.date : undefined,
    entry && entry.data ? entry.data.pubDate : undefined,
  ];

  for (const candidate of candidates) {
    if (candidate === null || candidate === undefined) continue;
    // Accept Date objects directly.
    if (candidate instanceof Date) {
      if (!isNaN(candidate.getTime())) return candidate;
      continue;
    }
    // Accept strings (ISO 8601, etc.).
    if (typeof candidate === 'string') {
      const parsed = new Date(candidate);
      if (!isNaN(parsed.getTime())) return parsed;
      continue;
    }
    // Numbers and everything else: fall through (do NOT interpret milliseconds —
    // a number like 12345 is ambiguous and should fall through to the fallback per AC2).
  }

  return fallbackDate;
}

// ── Main builder ──────────────────────────────────────────────────────────────
/**
 * Build a valid RSS 2.0 XML string from collection entries.
 *
 * @param {Array<{ id: string, data?: Record<string, unknown> }>} entries
 *   Raw getCollection('pages') entries (or crafted test entries).
 * @param {{
 *   title: string,
 *   description: string,
 *   site: string,
 *   buildDate?: Date,
 * }} channelMeta
 *   Channel metadata. `site` is the absolute origin (e.g. 'https://themarkdownweb.com').
 *   `buildDate` is optional — defaults to `new Date()` at call time (one fixed date
 *   per call so all undated items share the same <pubDate> within a single build).
 * @returns {string} A well-formed RSS 2.0 XML string.
 */
export function buildFeed(entries, channelMeta) {
  const {
    title = 'The Markdown Web',
    description = 'A vault feed.',
    site,
    buildDate,
  } = channelMeta ?? {};

  // One fixed build date per call — shared by all undated pages, so the feed diff
  // is quiet when content is unchanged (all undated items share the same date).
  const channelBuildDate = buildDate instanceof Date && !isNaN(buildDate.getTime())
    ? buildDate
    : new Date();

  // Normalize site to a string (accept URL object or string).
  const siteStr = site instanceof URL ? site.href : String(site ?? '');
  // Ensure trailing slash for joining.
  const siteOrigin = siteStr.replace(/\/$/, '');

  // REUSE buildIndexItems for filtering (empty-id), label derivation, and code-unit sort.
  // This ensures the feed set + labels CANNOT drift from the index.
  const indexItems = buildIndexItems(entries);

  // Resolve dates and sort: newest-first by date, tie-broken by code-unit entry.id.
  // Build a map from id -> resolved date for efficient lookup.
  const dateMap = new Map();
  for (const entry of entries) {
    if (entry.id === '') continue; // filtered by buildIndexItems
    dateMap.set(entry.id, resolveDate(entry, channelBuildDate));
  }

  // Sort: newest-first by date, then code-unit id ascending as tie-break.
  const sortedItems = [...indexItems].sort((a, b) => {
    const da = (dateMap.get(a.id) ?? channelBuildDate).getTime();
    const db = (dateMap.get(b.id) ?? channelBuildDate).getTime();
    if (da !== db) return db - da; // newest first
    // Tie-break: code-unit id ascending (same as buildIndexItems' sort — ICU-independent).
    return a.id < b.id ? -1 : a.id > b.id ? 1 : 0;
  });

  const feedUrl = `${siteOrigin}/feed.xml`;
  const lastBuildDate = toRfc822(channelBuildDate);

  // Emit channel metadata.
  const channelXml = [
    `    <title>${xmlEscape(title)}</title>`,
    `    <link>${xmlEscape(siteOrigin)}/</link>`,
    `    <description>${xmlEscape(description)}</description>`,
    `    <language>en</language>`,
    `    <lastBuildDate>${lastBuildDate}</lastBuildDate>`,
    `    <atom:link href="${xmlEscape(feedUrl)}" rel="self" type="application/rss+xml"/>`,
  ].join('\n');

  // Emit items.
  const itemsXml = sortedItems.map((item) => {
    const canonicalUrl = `${siteOrigin}${item.href}`;
    const escapedUrl = xmlEscape(canonicalUrl);
    const resolvedDate = dateMap.get(item.id) ?? channelBuildDate;
    const pubDate = toRfc822(resolvedDate);
    return [
      `    <item>`,
      `      <title>${xmlEscape(item.label)}</title>`,
      `      <link>${escapedUrl}</link>`,
      `      <guid isPermaLink="true">${escapedUrl}</guid>`,
      `      <pubDate>${pubDate}</pubDate>`,
      `    </item>`,
    ].join('\n');
  }).join('\n');

  return [
    `<?xml version="1.0" encoding="UTF-8"?>`,
    `<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">`,
    `  <channel>`,
    channelXml,
    itemsXml,
    `  </channel>`,
    `</rss>`,
  ].join('\n');
}

// Also export as buildRssXml alias for test compatibility.
export const buildRssXml = buildFeed;
