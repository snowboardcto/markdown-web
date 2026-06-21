# clients/windows/ — native Windows client (.NET 10 + WPF)

The native reader (FR-9–13). Three projects, with a strict dependency direction:

- **`Rendering/`** — the **bedrock**: Markdig AST → WPF `FlowDocument`. Pure and isolated:
  **no networking, no AI**. Independently testable.
- **`App/`** — shell, window, navigation, fetches raw `.md`. Depends on `Rendering/`.
- **`Agent/`** — AI-personality transform (later epic). Depends on `Rendering/`.

`App/` and `Agent/` depend on `Rendering/` — **never the reverse**.

**Hard constraint (NFR-1): no Chromium / no embedded browser / no webview.** The client renders
native WPF UI only. Folder homes only at this stage; the .NET solution/projects land in Epic 3.
