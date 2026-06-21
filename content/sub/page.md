# Nested Page

A nested fixture at `content/sub/page.md`, to prove the slug `/sub/page`.

## Links (AC: 2.3)

Relative resolution from a nested page (`/sub/page`):

- A parent `..` link: [home](../x.md) (-> `/x`)
- A parent `..` link to guide: [guide](../gear-guide.md) (-> `/gear-guide`)
- A sibling link: [sibling two](page2.md) (-> `/sub/page2`)
- A leading `./` link: [sibling](./sibling.md) (-> `/sub/sibling`)
- A vault-root `..` link: [root](../index.md) (-> `/`)

## Media (AC: 2.4)

Relative media resolution from a nested page (`/sub/page`), authored as raw HTML
(see the note in `content/x.md`):

A parent `..` image embed resolves against the page dir (-> `/media/powder.jpg`):

<img src="../media/powder.jpg" alt="from sub">

A sibling nested-dir image embed resolves to the page's own dir (-> `/sub/diagram.png`):

<img src="diagram.png" alt="sibling diagram">
