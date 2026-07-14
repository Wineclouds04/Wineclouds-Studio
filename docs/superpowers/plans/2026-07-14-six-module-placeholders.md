# Six Module Placeholders Implementation Plan

> **For agentic workers:** Execute in the current workspace. The user owns testing and code review; do not add tests or dispatch review agents.

**Goal:** Reserve six service-module slots, A through F, in the WinUI 3 navigation shell and expose a placeholder page for each.

**Architecture:** Keep the existing static `NavigationView` tag-to-page mapping. Add four explicit placeholder pages (C–F) matching the existing A and B pages; no dynamic module system, service layer, or module isolation is introduced.

**Tech Stack:** C#, .NET 10, WinUI 3, Windows App SDK, XAML.

---

## Chunk 1: Static module capacity

### Task 1: Add module C–F navigation and pages

**Files:**
- Modify: `src/WinecloudsStudio/MainWindow.xaml`
- Modify: `src/WinecloudsStudio/MainWindow.xaml.cs`
- Create: `src/WinecloudsStudio/Pages/ModuleCPage.xaml`
- Create: `src/WinecloudsStudio/Pages/ModuleCPage.xaml.cs`
- Create: `src/WinecloudsStudio/Pages/ModuleDPage.xaml`
- Create: `src/WinecloudsStudio/Pages/ModuleDPage.xaml.cs`
- Create: `src/WinecloudsStudio/Pages/ModuleEPage.xaml`
- Create: `src/WinecloudsStudio/Pages/ModuleEPage.xaml.cs`
- Create: `src/WinecloudsStudio/Pages/ModuleFPage.xaml`
- Create: `src/WinecloudsStudio/Pages/ModuleFPage.xaml.cs`
- Modify: `docs/superpowers/specs/2026-07-14-winui3-desktop-shell-design.md`

- [ ] Add navigation entries for 模块 C, 模块 D, 模块 E, and 模块 F under 服务模块.
- [ ] Extend the static tag-to-page mapping for the four new entries.
- [ ] Create four static pages matching the existing module placeholder layout, with the corresponding title and “功能建设中” label.
- [ ] Update the design document to specify six reserved service modules, A–F.
- [ ] Run `dotnet build WinecloudsStudio.slnx --configuration Debug` and resolve compilation errors only.
- [ ] Do not add or execute tests; developer testing and review remain out of scope.
