# api/ — content negotiation (Azure Functions)

The only job of this component is **content negotiation** (FR-14):

- `Accept: text/html` → HTML representation
- `Accept: text/markdown` (or client requests raw) → the raw `.md`
- always sets `Vary: Accept`

It negotiates **only** — it holds no content of its own. The negotiate function lives under
`api/negotiate/`. Templates/implementation arrive in a later epic.
