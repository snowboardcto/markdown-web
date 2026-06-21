# Mixed Case Dir Page

A fixture page living in a **non-slug-stable** directory (`content/My Notes Dir/`,
route `/my-notes-dir/page`). Story 2.4 review fix #1 (CRITICAL): a same-dir media
embed must resolve against the page's VERBATIM on-disk directory (`My Notes Dir`),
NOT its slugged route directory (`my-notes-dir`), because the copy step writes the
asset to `dist/My Notes Dir/pic.png` verbatim. A slugged `src` would 404.

## Media (AC: 2.4, review fix #1)

A same-dir embed whose served `src` must match the verbatim-copied path:

<img src="pic.png" alt="mixed case dir same-dir">
