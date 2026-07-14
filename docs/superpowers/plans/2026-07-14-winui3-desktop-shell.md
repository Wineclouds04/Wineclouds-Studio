# WinUI 3 Desktop Shell Implementation Plan

> **For agentic workers:** Execute this plan in the current workspace. The user explicitly retains testing and code-review responsibility; do not add or run automated tests, and do not dispatch review agents.

**Goal:** Build a Windows-only WinUI 3 desktop application shell with a dark grouped left navigation pane and three switchable placeholder pages.

**Architecture:** Use a single WinUI 3 project. `MainWindow` owns a `NavigationView` and content `Frame`; the selection event maps a small set of static navigation keys directly to page types. Pages contain only static placeholder content, with no business services, data access, dependency injection, module registration, or runtime isolation.

**Tech Stack:** C#, .NET 10, WinUI 3, Windows App SDK, XAML.

---

## Chunk 1: Project bootstrap

### Task 1: Create the WinUI 3 project

**Files:**
- Create: `WinecloudsStudio.slnx`
- Create: `src/WinecloudsStudio/` (WinUI 3 template output)

- [ ] Install the official WinUI C# template package with `dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` if it is not already installed.
- [ ] Create the project using `dotnet new winui --name WinecloudsStudio --output src/WinecloudsStudio`.
- [ ] Create the solution with `dotnet new sln --name WinecloudsStudio`, then add `src/WinecloudsStudio/WinecloudsStudio.csproj` to `WinecloudsStudio.slnx` (the .NET 10 default solution format).
- [ ] Restore packages using `dotnet restore WinecloudsStudio.slnx`.

## Chunk 2: Application shell

### Task 2: Create the navigation window structure

**Files:**
- Modify: `src/WinecloudsStudio/MainWindow.xaml`
- Modify: `src/WinecloudsStudio/MainWindow.xaml.cs`
- Modify: `src/WinecloudsStudio/App.xaml`

- [ ] Define the application color resources for the dark navigation pane, selected navigation item, and light content surface.
- [ ] Replace the template window content with a `NavigationView` in left-pane mode, a header containing `Wineclouds Studio`, grouped navigation entries for “工作台” and “服务模块”, and a nested content `Frame`.
- [ ] Add static navigation keys for 首页, 模块 A, and 模块 B.
- [ ] Handle navigation selection in `MainWindow.xaml.cs`: map each key to its page type and navigate the frame; load 首页 on startup.
- [ ] Display a non-fatal fallback view in the content frame if a key does not resolve.

### Task 3: Create the three placeholder pages

**Files:**
- Create: `src/WinecloudsStudio/Pages/HomePage.xaml`
- Create: `src/WinecloudsStudio/Pages/HomePage.xaml.cs`
- Create: `src/WinecloudsStudio/Pages/ModuleAPage.xaml`
- Create: `src/WinecloudsStudio/Pages/ModuleAPage.xaml.cs`
- Create: `src/WinecloudsStudio/Pages/ModuleBPage.xaml`
- Create: `src/WinecloudsStudio/Pages/ModuleBPage.xaml.cs`

- [ ] Create a page for 首页, 模块 A, and 模块 B.
- [ ] Give every page a Chinese title, module label, and the static message “功能建设中”.
- [ ] Use a common visual pattern: heading, short explanatory text, and a bordered placeholder card.

## Chunk 3: Build handoff

### Task 4: Compile and prepare for developer validation

**Files:**
- Modify as needed: files from Tasks 1–3 only, to resolve compilation errors.

- [ ] Run `dotnet build WinecloudsStudio.slnx --configuration Debug` and resolve compilation errors only.
- [ ] Do not create or execute test projects or test commands; testing and code review are explicitly assigned to the developer.
- [ ] Hand off the build output and the developer validation checklist: application starts, 首页 is the initial page, all three navigation entries switch the right-side content, selected state is visible, and Chinese text renders correctly.
