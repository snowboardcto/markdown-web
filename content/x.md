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
- A broken internal link: [missing](does-not-exist.md) (-> `/does-not-exist`, lands on 404)

Pass-through links (left exactly as authored):

- A same-page anchor: [jump to lists](#lists)
- An external link: [the site](https://themarkdownweb.com)
- A mailto link: [mail us](mailto:hello@themarkdownweb.com)
- A root-absolute link: [already a route](/already-a-route)
- A non-`.md` asset link: [a pdf](report.pdf)
