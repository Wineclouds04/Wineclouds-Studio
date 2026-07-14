# Light Navigation Theme Implementation Plan

> **For agentic workers:** Execute in the current workspace. The user explicitly owns testing and code review; do not create tests or dispatch review agents.

**Goal:** Change the WinUI 3 application shell to an all-light theme with a pale-blue navigation pane and a distinct pale-blue selected state.

**Architecture:** Keep the existing single-project navigation shell and page flow unchanged. Centralize the new light palette in application resources, then consume those resources from the existing `NavigationView` so later pages keep the same content surface.

**Tech Stack:** C#, .NET 10, WinUI 3, Windows App SDK, XAML.

---

## Chunk 1: Light theme update

### Task 1: Apply the approved pale-blue light palette

**Files:**
- Modify: `docs/superpowers/specs/2026-07-14-winui3-desktop-shell-design.md`
- Modify: `src/WinecloudsStudio/App.xaml`
- Modify: `src/WinecloudsStudio/MainWindow.xaml`

- [ ] Replace the dark navigation resources with a pale-blue background, dark text, muted group-label text, and pale-blue selected-item resources.
- [ ] Apply the selected-item resource to the 首页 navigation item; keep all navigation labels dark and all content surfaces light.
- [ ] Update the design document to describe the approved shallow-blue, all-light visual direction.
- [ ] Run `dotnet build WinecloudsStudio.slnx --configuration Debug` and resolve compilation errors only.
- [ ] Do not add or run tests; the developer performs testing and visual review.
