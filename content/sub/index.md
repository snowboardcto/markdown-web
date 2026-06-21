# Sub Index

The directory index page for `content/sub/`, routing to `/sub`.

It exercises the F2 fix: a relative link authored on a `content/<dir>/index.md`
page must resolve against `<dir>`, not the vault root.

- A sibling link: [sibling two](page2.md) (-> `/sub/page2`, not `/page2`)
- A nested-from-index link: [nested sibling](sibling.md) (-> `/sub/sibling`)
