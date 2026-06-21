# Heading One

A representative GFM fixture proving Story 2.1 rendering.

## Heading Two

### Heading Three

This paragraph has **bold text** and _italic text_ to prove inline emphasis.

#### Heading Four

##### Heading Five

###### Heading Six

## Lists

An unordered list:

- First unordered item
- Second unordered item
- Third unordered item

An ordered list:

1. First ordered item
2. Second ordered item
3. Third ordered item

## Code

Here is some inline `const answer = 42;` code.

```js
function greet(name) {
  return `Hello, ${name}!`;
}
const aVeryLongUnbreakableIdentifierThatExceedsTheReadingMeasure = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
```

## Quote

> A top-level blockquote.
>
> > A nested blockquote inside it (AC5 nested left-rule).

## Table

| Name  | Role     | Active |
| ----- | -------- | ------ |
| Alice | Author   | yes    |
| Bob   | Reviewer | no     |

## GFM Extensions (AC6)

This text is ~~struck through~~ to prove strikethrough.

A task list:

- [ ] An unfinished task
- [x] A finished task

A bare autolinked URL: https://themarkdownweb.com

## Escaping (AC6)

A literal less-than `<` and ampersand `&` must be HTML-escaped.

Inline `code with <tags> & ampersand` must also escape its angle brackets and ampersand.

## Links (AC: 2.3)

Internal `.md` links (rewritten to page routes at build time):

- A same-dir internal link: [guide](gear-guide.md) (-> `/gear-guide`)
- A nested internal link: [nested](sub/page.md) (-> `/sub/page`)
- A cross-file fragment link: [other heading](gear-guide.md#heading-one) (-> `/gear-guide#heading-one`)
- A space-in-filename link: [notes space](<My Notes.md>) (-> `/my-notes`)
- A percent-encoded link: [notes encoded](My%20Notes.md) (-> `/my-notes`)
- A mixed-case link: [Gear Guide cased](Gear-Guide.md) (-> `/gear-guide`)
- An index-collapse link: [sub index](sub/index.md) (-> `/sub`)
- A malformed-escape link: [malformed](bad%zz.md) (left unrewritten)
- An escape-the-vault link: [escape](../escape.md) (left unrewritten)
- An encoded leading-slash link: [encoded leading slash](%2Ffoo.md) (left unrewritten, never `//foo`)
- An encoded interior-slash link: [encoded interior slash](a%2Fb.md) (left unrewritten, never split into `/a/b`)
- An encoded path-traversal link: [encoded passwd](%2Fetc%2Fpasswd.md) (left unrewritten, never `//etc/passwd`)
- An empty-basename link: [empty basename](.md) (left unrewritten, never `/`)
- A broken internal link: [missing](does-not-exist.md) (-> `/does-not-exist`, lands on 404)

Pass-through links (left exactly as authored):

- A same-page anchor: [jump to lists](#lists)
- An external link: [the site](https://themarkdownweb.com)
- A mailto link: [mail us](mailto:hello@themarkdownweb.com)
- A root-absolute link: [already a route](/already-a-route)
- A non-`.md` asset link: [a pdf](report.pdf)

## Media (AC: 2.4)

These media embeds are authored as raw HTML so the relative `src`/`poster`
values reach the build-time rehype media rewrite as plain HAST `<img>`/`<video>`/
`<audio>`/`<source>` element nodes (the same node set a markdown `![]()` embed
lowers to), and so a missing-asset reference cannot hard-crash Astro's image
pipeline. The rewrite is at the HAST stage, so it is syntax-agnostic.

The epic same-dir embed with alt text (AC1/AC4): <img src="media/powder.jpg" alt="a skier in deep powder">

An empty-alt decorative embed (AC4): <img src="media/powder.jpg" alt="">

A leading `./` embed collapses (AC2): <img src="./media/powder.jpg" alt="dot slash">

An image carrying title/width/height that must survive the rewrite (AC4): <img src="media/powder.jpg" alt="titled" title="Powder Day" width="320" height="240">

Pass-through media, left exactly as authored (AC3):

<img src="https://example.com/a.png" alt="remote https">
<img src="//cdn.example.com/b.png" alt="protocol relative">
<img src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==" alt="data uri">
<img src="/already/served.png" alt="already served">

F1 smuggling guards, left UNREWRITTEN (AC2 / rows 12-14):

<img src="media%2Fpowder.jpg" alt="encoded separator smuggle">
<img src="%2Fetc/passwd.png" alt="encoded leading slash smuggle">
<img src="bad%zz.jpg" alt="malformed percent escape">

A missing-asset embed still rewrites to its would-be served path and 404s, never crashing the build (AC6): <img src="media/missing.jpg" alt="broken">

A raw-HTML video with a relative `poster` and a nested relative `<source>` (AC5):

<video controls width="320" poster="media/powder.jpg"><source src="media/clip.mp4" type="video/mp4"></video>

A raw-HTML audio with a relative `src` (AC5):

<audio controls src="media/clip.mp3"></audio>

The AC1 epic example authored as LITERAL markdown `![]()` (not raw HTML) — proves
the markdown image syntax flows through the same rewrite and bypasses Astro's
`astro:assets`/`_astro` optimisation, emitting a plain served `<img>` (AC1):

![markdown embed](media/powder.jpg)
