# Wineclouds Studio · 酒云工作室

**桌面的第二层交互界面。** Wineclouds Studio 是一个 Windows 桌面增强平台，在你现有的桌面之上叠加一层轻量的控制面板，让你更高效地感知和操控正在运行的窗口。

---

## 为什么需要 Wineclouds Studio？

当你同时开着十几个窗口——IDE、浏览器、终端、聊天工具、设计稿——**切换窗口本身就是一种认知负担**。Alt+Tab 列表越来越长，任务栏图标越来越小，你真正关心的那三五个窗口淹没在噪音里。

Wineclouds Studio 提供一种不同的思路：**把你选中的窗口"提"到前面**，以实时缩略图的形式始终可见。看一眼就知道那个编译完了没有、那个聊天有没有新消息、那个监控面板的数字变没变。点一下，直接切过去。

> 灵感来源于 EVE Online 玩家社区广为人知的 EVE-O-Preview 工具。Wineclouds Studio 将同样的理念——多窗口实时预览与一键切换——带到所有 Windows 用户的日常桌面上。

---

## 当前功能

### 窗口管理器（已上线）

| 能力 | 说明 |
| --- | --- |
| **进程发现** | 一键扫描当前所有带窗口的进程，按名称排序展示 |
| **实时预览** | 为选中的窗口创建基于 DWM 的实时缩略图浮窗——不是截图，是真正实时渲染的画面 |
| **始终置顶** | 缩略图浮窗始终悬浮在其他窗口之上，无需来回切换即可监视目标窗口状态 |
| **一键切换** | 左键点击任一缩略图，直接激活对应窗口并聚焦 |
| **自由拖拽** | 右键拖拽缩略图到屏幕任意位置，布局由你决定 |
| **位置记忆** | 停止监控时自动保存每个窗口的缩略图位置，下次启动时恢复原位 |
| **视觉调节** | 可自由调整缩略图尺寸（80–800px 宽，60–600px 高）和透明度（10%–100%） |
| **荧光绿边框** | 可选的醒目标识边框，方便在多屏或深色背景下快速定位 |

### 更多模块（规划中）

Wineclouds Studio 采用模块化架构，当前已预留 B–F 共五个模块插槽，后续将根据需求逐步填充。

---

## 界面一览

```text
┌──────────────────────┬──────────────────────────────────────────────┐
│  Wineclouds Studio   │  窗口管理器                                   │
│  桌面服务平台         │                                               │
│                      │  ┌─────────────────────────────────────────┐ │
│  工作台              │  │  可用进程                         [刷新]  │ │
│  · 首页              │  │  ☑ notepad  —  新建文本文档.txt         │ │
│                      │  │  ☑ chrome   —  GitHub - Wineclouds     │ │
│  服务模块            │  │  ☐ spotify  —  Now Playing              │ │
│  · 窗口管理器        │  │  ...                                     │ │
│  · 模块 B            │  └─────────────────────────────────────────┘ │
│  · 模块 C            │                                               │
│  · 模块 D            │  ┌─────────────────────────────────────────┐ │
│  · 模块 E            │  │  缩略图设置                              │ │
│  · 模块 F            │  │  宽度 [280]  高度 [180]                  │ │
│                      │  │  透明度 [═══════ 0.9]                    │ │
│                      │  │  ☑ 始终置顶  ☐ 荧光绿边框               │ │
│                      │  └─────────────────────────────────────────┘ │
│                      │                                               │
│                      │  [ 开始监控 ]    [ 停止监控 ]                 │
└──────────────────────┴──────────────────────────────────────────────┘
```

缩略图浮窗独立于主窗口之外：

```text
    ┌─ notepad ─ 新建文本文档.txt ─┐
    │                              │   ← 实时 DWM 缩略图，始终置顶
    │     (实时渲染的窗口画面)       │      左键点击 = 切到该窗口
    │                              │      右键拖拽 = 移动浮窗
    └──────────────────────────────┘
```

---

## 安装与使用

### 系统要求

- Windows 10 1809+ 或 Windows 11
- .NET 10 桌面运行时
- 无需账户注册，无需联网授权

### 构建

```bash
git clone https://github.com/wineclouds/WinecloudsStudio.git
cd WinecloudsStudio
dotnet restore WinecloudsStudio.slnx
dotnet build WinecloudsStudio.slnx --configuration Release
```

### 运行

```bash
dotnet run --project src/WinecloudsStudio
```

或直接双击生成的 `WinecloudsStudio.exe`。

### 快速上手

1. 启动后，左侧导航点击 **「窗口管理器」**
2. 点击 **「刷新列表」** 扫描当前所有窗口
3. 勾选你想监控的进程（可以多选）
4. 调整缩略图尺寸和透明度，或保持默认
5. 点击 **「开始监控」** ——缩略图浮窗立即出现
6. 右键拖拽浮窗到你顺手的位置，左键点击浮窗即可切到对应窗口
7. 点击 **「停止监控」** 关闭所有浮窗，位置自动记忆

---

## 技术架构

Wineclouds Studio 基于 Windows 原生技术栈构建，追求轻量、高效和零依赖。

| 层面 | 技术选型 |
| --- | --- |
| **运行时** | .NET 10 |
| **UI 框架** | WinUI 3（Windows App SDK 2.2） |
| **窗口管理** | P/Invoke → User32.dll / DWM API |
| **实时缩略图** | DWM Thumbnail（`DwmRegisterThumbnail`） |
| **缩略图容器** | 原生 Win32 窗口（`CreateWindowEx`），零 WinForms 依赖 |
| **应用打包** | MSIX（Windows 原生应用包） |
| **语言** | C# 12，XAML |

### 架构原则

- **无第三方 UI 依赖**：缩略图浮窗使用纯 Win32 API 创建，不走 WinUI/WinForms，体积小、启动快
- **模块化设计**：主窗口为壳（Shell），各功能模块独立为 Page，新增模块只需添加页面和导航映射
- **配置持久化**：缩略图位置按 `<进程名>::<窗口标题>` 作为键值存储到本地 JSON 文件
- **安全边界**：仅读取窗口标题和缩略图，不注入、不 Hook、不截获输入

### 项目结构

```text
WinecloudsStudio/
├── src/WinecloudsStudio/
│   ├── App.xaml                  # 应用入口与启动配置
│   ├── MainWindow.xaml           # 主窗口壳（Mica 背景 + NavigationView）
│   ├── Configuration/            # 配置模型与持久化
│   │   ├── WindowManagerConfig.cs
│   │   └── ThumbnailWindowPositionStore.cs
│   ├── Pages/                    # 各模块页面
│   │   ├── HomePage.xaml          # 首页（工作台概览）
│   │   ├── ModuleAPage.xaml       # 窗口管理器（核心功能）
│   │   ├── ModuleBPage ~ ModuleFPage.xaml  # 预留模块占位
│   │   └── UnavailablePage.xaml   # 导航失败时的兜底页面
│   ├── Services/
│   │   ├── Interface/            # 服务接口（IWindowManager, IProcessMonitor, IDwmThumbnail 等）
│   │   ├── Implementation/       # 服务实现（WindowManager, ProcessMonitor, ThumbnailManager）
│   │   └── Interop/              # Win32 P/Invoke 声明（User32, DWM, GDI）
│   ├── Views/                    # 视图组件
│   │   └── ThumbnailForm.cs      # 原生 Win32 缩略图浮窗（WndProc 消息循环）
│   └── Assets/                   # 应用图标与图片资源
└── docs/                         # 设计文档与规范
```

---

## 路线图

- [x] WinUI 3 应用壳（导航框架 + 模块占位）
- [x] 窗口管理器（进程发现 + DWM 实时缩略图 + 基础控制面板）
- [ ] 缩略图缩放与吸附对齐
- [ ] 全局快捷键（快速显示/隐藏所有缩略图）
- [ ] 主题切换（深色模式）
- [ ] 模块 B–F 实际功能

---

## 贡献

本项目处于早期开发阶段。如果你有兴趣参与，欢迎提 Issue 或 PR。建议在动手前先开 Issue 讨论你的想法，以确保方向一致。

---

## 许可

[MIT License](LICENSE)

---

<p align="center">
  <sub>Made with ❤️ by Wineclouds · 2026</sub>
</p>
