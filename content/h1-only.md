# Distinct Heading Title

This page has a first-level heading but NO front-matter `title`, so the
destination page's `<title>` comes from the H1 ("Distinct Heading Title").

The browsable index uses the cheap Decision-D precedence
(`data.title || slugToTitle(entry.id)`), so its link label is the slug-derived
Title Case of `h1-only` -> "H1 Only", which PROVABLY differs from this page's
`<title>`. This fixture exists to genuinely exercise the documented
label/title divergence (unlike `/no-h1`, whose label and title coincide).
