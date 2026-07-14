# Wineclouds Studio · 酒云工作室

Wineclouds Studio 是一个面向 Windows 多窗口场景的实时预览与快速切换工具。它可以为指定进程创建始终置顶的小窗口，通过 DWM 实时显示目标窗口画面，并按自定义分组使用单键热键循环切换焦点。

窗口预览、交互方式与循环切换思路参考了 [EVE-O-Preview](https://github.com/Phrynohyas/eve-o-preview)。

## 当前功能

- 扫描并选择需要监控的窗口进程。
- 使用 DWM Thumbnail 显示实时窗口画面。
- 小窗口左键激活对应目标窗口。
- 小窗口右键拖动并自动保存位置。
- 自定义缩略图宽度、高度、透明度、边框、标题和置顶状态。
- 创建多个窗口分组，并设置组内窗口循环顺序。
- 为每个分组分别绑定前进和后退热键。
- 通过二级菜单从128个键盘按键中选择单个按键，不使用按键录制。
- 普通按键采用 `RegisterHotKey` / `WM_HOTKEY`；Alt、Ctrl、Shift 和 Win 单键使用内部中继后进入同一切换路径。
- 按进程归属识别 EVE 等应用的内部前台窗口。
- 自动保存窗口管理器设置、分组配置和缩略图位置。

## 系统要求

- Windows 10 1809 或更高版本
- Windows 11 推荐
- .NET 10
- Windows App SDK 2.2

目标程序以管理员权限运行时，Wineclouds Studio 也必须处于相同完整性级别才能可靠聚焦窗口。程序启动后会请求 UAC 管理员权限。

## 构建与运行

```powershell
git clone https://github.com/wineclouds/WinecloudsStudio.git
cd WinecloudsStudio
dotnet restore WinecloudsStudio.slnx
dotnet build WinecloudsStudio.slnx --configuration Release
dotnet run --project src/WinecloudsStudio
```

运行时请接受 UAC 提示。开发环境使用 Windows App SDK 的调试包身份启动应用，普通权限入口会拉起管理员实例。

## 使用方法

1. 打开左侧的“窗口管理器”。
2. 刷新进程列表并选择需要监控的进程。
3. 新建窗口分组，将目标窗口按所需顺序加入分组。
4. 在二级菜单中分别选择前进键和后退键；每个方向只允许绑定一个按键。
5. 保存设置并开始监控。
6. 使用分组热键循环聚焦窗口，或左键点击小窗口直接聚焦。
7. 按住小窗口右键拖动位置；停止监控时位置会自动保存。

## 热键说明

热键选择器包含以下128个按键：

- A–Z、数字行 0–9
- F1–F24
- 数字小键盘
- 导航键、编辑键和方向键
- Shift、Ctrl、Alt、左右 Win 等系统键
- OEM 符号键
- 浏览器键、音量键和媒体键

绑定仅支持单键。同一个按键不能同时分配给多个分组方向。

## 技术架构

| 模块 | 实现 |
| --- | --- |
| 主界面 | WinUI 3 / Windows App SDK 2.2 |
| 小窗口 | Windows Forms 无边框窗口与独立覆盖层 |
| 实时预览 | DWM Thumbnail API |
| 窗口操作 | User32 P/Invoke |
| 普通全局热键 | `RegisterHotKey` 与 `WM_HOTKEY` |
| 修饰键单键 | `WH_KEYBOARD_LL` 识别与内部注册热键中继 |
| 配置 | 本地 JSON |
| 运行时 | .NET 10 |

### 主要目录

```text
src/WinecloudsStudio/
├── Configuration/       # 窗口管理、分组和持久化配置
├── Pages/               # WinUI 页面及128键菜单
├── Services/
│   ├── Implementation/  # 进程监控、缩略图、窗口和热键服务
│   ├── Interface/       # 服务接口
│   └── Interop/         # User32、DWM 等原生接口
├── Views/               # 小窗口、实时缩略图与覆盖层
├── App.xaml.cs          # 启动与权限提升
└── Package.appxmanifest # MSIX 包清单
```

## 本地数据

程序会在应用本地数据目录中保存：

- `window_manager_config.json`：界面设置、分组、窗口顺序和热键。
- `thumbnail_positions.json`：各目标窗口对应的小窗口位置。
- `logs/`：运行日志。

这些运行时文件不会提交到仓库。

## 操作约定

- 左键点击小窗口：聚焦目标窗口。
- 右键拖动小窗口：调整小窗口位置。
- 分组前进键：按配置顺序切换到下一个窗口。
- 分组后退键：按配置顺序切换到上一个窗口。

## 许可

[MIT License](LICENSE)
