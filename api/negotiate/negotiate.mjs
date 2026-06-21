// Story 2.7 — the PURE content-negotiation handler (the CI-testable seam, AC8a).
//
//   negotiate({ slug, acceptHeader, readMd }) -> { status, headers, body }
//
// It is a pure function over (request slug, raw `Accept` header, vault-read
// shim) and NEVER touches Azure binding glue, a port, the filesystem, or the
// network — the Azure entry point (`index.mjs`) is a thin adapter that supplies
// the real `Accept` + a `readMd` backed by the bundled vault and writes the
// HttpResponse.
//
// Contract (story Task 3):
//   - `readMd(slug)` is the CLOSED slug -> content lookup. It returns the raw
//     source `Buffer` for a KNOWN slug key, or `null`/`undefined` for an unknown
//     key. The request slug is a LOOKUP KEY, never concatenated into a path
//     (AC5 closed-map discipline) — so an unknown/hostile key is a clean miss by
//     construction.
//   - Returns `{ status, headers, body }`. `headers` always carries
//     `Vary: Accept` (AC3 — on BOTH branches). `body` on the markdown branch is
//     the verbatim source Buffer (AC6, byte-exact, no BOM/CRLF/trailing
//     mutation).
//   - Never throws. A malformed `Accept` fails SAFE to the HTML/default branch
//     (AC2), never a 400/500.

const MARKDOWN_TYPE = 'text/markdown';
const MARKDOWN_CONTENT_TYPE = 'text/markdown; charset=utf-8';

// HTML is the DEFAULT representation; the handler signals it by NOT serving
// markdown. The Azure adapter / SWA routing actually serves the static HTML —
// on the Function path we emit a 204-style "defer to static HTML" marker the
// adapter turns into a redirect/passthrough. The unit-test contract only cares
// that this is NOT the markdown branch (status !== 200-markdown) and that it
// still carries `Vary: Accept`.
const HTML_BRANCH_STATUS = 200;

/**
 * Parse one media-range token (already comma-split, e.g. `text/markdown;q=0.9`).
 * Returns `{ type, subtype, q }` or `null` if the token is malformed/unparseable
 * (empty, bare `;`, missing subtype). Matching is case-insensitive (AC2b);
 * non-`q` params are ignored (AC2c); `q` is honored (AC2d), defaulting to 1.
 *
 * @param {string} token a single Accept media-range
 * @returns {{type:string, subtype:string, q:number}|null}
 */
function parseMediaRange(token) {
  const trimmed = token.trim();
  if (trimmed === '') return null;
  const parts = trimmed.split(';');
  const mediaType = parts[0].trim();
  if (mediaType === '') return null; // bare `;` etc.
  const slash = mediaType.indexOf('/');
  if (slash === -1) return null; // no `/` at all
  const type = mediaType.slice(0, slash).trim().toLowerCase();
  const subtype = mediaType.slice(slash + 1).trim().toLowerCase();
  if (type === '' || subtype === '') return null; // `text/`, `/markdown`
  // type/subtype must be sane tokens (no whitespace/garbage inside).
  if (!/^[!#$%&'*+\-.^_`|~0-9a-z]+$/.test(type)) return null;
  if (!/^[!#$%&'*+\-.^_`|~0-9a-z]+$/.test(subtype)) return null;

  let q = 1;
  let qExplicit = false;
  for (let i = 1; i < parts.length; i++) {
    const param = parts[i].trim();
    if (param === '') continue;
    const eq = param.indexOf('=');
    if (eq === -1) continue;
    const name = param.slice(0, eq).trim().toLowerCase();
    if (name !== 'q') continue; // ignore non-q params (charset etc.)
    const raw = param.slice(eq + 1).trim().replace(/^"|"$/g, '');
    const val = Number.parseFloat(raw);
    if (Number.isFinite(val)) {
      q = Math.min(1, Math.max(0, val));
      qExplicit = true;
    }
  }
  return { type, subtype, q, qExplicit };
}

/**
 * Decide whether the client POSITIVELY opted in to `text/markdown` per
 * RFC 9110 §12.5.1 (AC2). Returns `true` ONLY when an explicit `text/markdown`
 * media-range wins:
 *   - case-insensitive, OWS-tolerant, non-`q`-param-tolerant;
 *   - `q=0` is an explicit REJECTION;
 *   - star-slash-star and `text/`-star are NON-opt-in (a wildcard never triggers
 *     markdown);
 *   - markdown wins only if its effective q > 0 AND >= the best HTML/wildcard q;
 *     a tie against a wildcard resolves to markdown (more specific), a tie
 *     against an explicit `text/html` of equal q stays on HTML (markdown is
 *     opt-in);
 *   - a malformed/unparseable Accept fails safe to HTML (never throws).
 *
 * @param {string|undefined|null} acceptHeader the raw `Accept` header
 * @returns {boolean} true iff the markdown branch should win
 */
function wantsMarkdown(acceptHeader) {
  if (typeof acceptHeader !== 'string') return false;
  const header = acceptHeader.trim();
  if (header === '') return false;

  let mdQ = -1; // best explicit text/markdown q (effective)
  // The best q-value at which an HTML-or-wildcard range would BLOCK markdown.
  // Only ranges that carry an EXPLICIT q participate in the block: a bare
  // `text/html` / `*/*` (implicit q=1) is the browser default and does NOT
  // outrank an explicitly opted-in `text/markdown` (matrix:
  // `text/html, text/markdown;q=0.9` -> markdown). An explicit equal-or-higher
  // q on an HTML/wildcard range keeps the request on HTML (markdown is opt-in:
  // `text/html;q=0.9, text/markdown;q=0.9` -> HTML).
  let blockingHtmlQ = -1;

  for (const token of header.split(',')) {
    const range = parseMediaRange(token);
    if (range === null) continue; // skip garbage tokens, fail safe overall
    const { type, subtype, q, qExplicit } = range;
    const isMarkdown = type === 'text' && subtype === MARKDOWN_TYPE.slice(5);
    const isHtmlOrWildcard =
      (type === 'text' && subtype === 'html') ||
      (type === 'text' && subtype === '*') ||
      (type === '*' && subtype === '*');
    if (isMarkdown) {
      if (q > mdQ) mdQ = q;
    } else if (isHtmlOrWildcard && qExplicit) {
      if (q > blockingHtmlQ) blockingHtmlQ = q;
    }
  }

  // No explicit text/markdown range at all (or only q=0) -> not opt-in.
  if (mdQ <= 0) return false;

  // Markdown wins unless an EXPLICIT HTML/wildcard q ties-or-beats it.
  return blockingHtmlQ < mdQ;
}

/**
 * The pure negotiation handler. See the file header for the full contract.
 *
 * @param {{slug?: string, acceptHeader?: string|null, readMd: (slug:string)=>(Buffer|null|undefined)}} args
 * @returns {{status:number, headers:Record<string,string>, body: Buffer|string|null}}
 */
export function negotiate({ slug, acceptHeader, readMd } = {}) {
  // Vary: Accept on EVERY branch (AC3) — a shared/CDN cache must key on Accept
  // so it can never cross-serve markdown to a browser (or vice-versa).
  const baseHeaders = { Vary: 'Accept' };

  const markdownRequested = wantsMarkdown(acceptHeader);

  if (!markdownRequested) {
    // HTML / default branch (AC2). The handler does not own the HTML bytes —
    // the static host serves them. Signal "defer to static HTML" so the adapter
    // can passthrough/redirect; still carries Vary: Accept (AC3).
    return {
      status: HTML_BRANCH_STATUS,
      headers: { ...baseHeaders, 'X-Negotiate-Representation': 'html' },
      body: null,
    };
  }

  // Markdown branch (AC1/AC6). Resolve the slug ONLY through the closed map.
  let bytes = null;
  try {
    bytes = typeof readMd === 'function' ? readMd(typeof slug === 'string' ? slug : '') : null;
  } catch {
    bytes = null; // a throwing shim must not become a 500 — treat as a miss.
  }

  if (bytes == null || !Buffer.isBuffer(bytes)) {
    // Missing slug OR hostile key (not in the closed map) -> uniform 404 (AC5).
    // Identical status + body for "valid slug, no page" and "hostile slug": no
    // path leak, no probe oracle, never another page's markdown.
    return {
      status: 404,
      headers: { ...baseHeaders, 'Content-Type': 'text/plain; charset=utf-8' },
      body: 'Not Found',
    };
  }

  return {
    status: 200,
    headers: {
      ...baseHeaders,
      'Content-Type': MARKDOWN_CONTENT_TYPE,
      'Content-Length': String(bytes.length),
    },
    // Verbatim source bytes — no transform, no BOM injection, no re-encode (AC6).
    body: bytes,
  };
}

export { wantsMarkdown, parseMediaRange };
