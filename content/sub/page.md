# Nested Page

A nested fixture at `content/sub/page.md`, to prove the slug `/sub/page`.

## Links (AC: 2.3)

Relative resolution from a nested page (`/sub/page`):

- A parent `..` link: [home](../x.md) (-> `/x`)
- A parent `..` link to guide: [guide](../gear-guide.md) (-> `/gear-guide`)
- A sibling link: [sibling two](page2.md) (-> `/sub/page2`)
- A leading `./` link: [sibling](./sibling.md) (-> `/sub/sibling`)
- A vault-root `..` link: [root](../index.md) (-> `/`)
