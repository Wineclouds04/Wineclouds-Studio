# Wineclouds Studio · 酒云工作室

Wineclouds Studio 是一款面向 Windows 多窗口场景的实时预览与快速切换工具。它可为选定进程创建始终置顶的实时缩略图，通过 DWM Thumbnail 显示目标窗口内容，并支持按分组使用单键热键切换焦点。

窗口预览与循环切换的思路参考 [EVE-O-Preview](https://github.com/Phrynohyas/eve-o-preview)。

## 功能概览

- 扫描窗口进程，并按进程名或窗口标题实时搜索、筛选。
- 使用 DWM Thumbnail 显示目标窗口的实时画面。
- 左键点击缩略图，直接激活对应目标窗口。
- 右键拖动缩略图并自动保存位置。
- 可锁死缩略图，防止误拖动。
- 可将缩略图位置吸附到固定的 8px 网格。
- 调整缩略图宽度、高度、透明度、置顶状态与荧光绿边框。
- 创建多个窗口分组，并分别配置组内窗口顺序、前进键和后退键。
- 自动保存显示设置、分组配置、热键和缩略图位置。

## 系统要求

- Windows 10 1809 或更高版本（推荐 Windows 11）
- .NET 10 SDK
- Windows App SDK 2.2

若目标程序以管理员权限运行，Wineclouds Studio 也必须处于相同完整性级别，才能稳定激活目标窗口。应用启动时会请求 UAC 管理员权限。

## 快速开始

```powershell
git clone https://github.com/Wineclouds04/Wineclouds-Studio.git
cd Wineclouds-Studio
dotnet restore WinecloudsStudio.slnx
dotnet build WinecloudsStudio.slnx --configuration Release
dotnet run --project src/WinecloudsStudio
```

## 使用流程

1. 打开“窗口管理器”。
2. 在“可用进程”中搜索或刷新列表，勾选要监控的窗口。
3. 按需设置缩略图尺寸、透明度、置顶、边框、锁死或网格吸附。
4. 创建窗口分组，并按需要配置分组内窗口顺序与单键热键。
5. 点击“开始监控”。
6. 使用热键、或左键点击缩略图切换到目标窗口。

## 缩略图交互

| 操作 | 行为 |
| --- | --- |
| 左键点击 | 激活对应目标窗口 |
| 右键拖动 | 调整缩略图位置 |
| 锁死缩略图 | 禁用右键拖动，不影响左键激活 |
| 缩略图吸附网格 | 将位置对齐到最近的 8px 网格；拖动时保持对齐 |

缩略图位置会在停止监控或关闭页面时保存，并在对应窗口再次出现时恢复。

## 热键规则

- 每个分组可配置一个前进键和一个后退键。
- 热键仅支持单键；同一个按键不能分配给多个分组方向。
- 支持字母、数字、功能键、导航键、修饰键、媒体键及常见 OEM 键。

## 技术架构

| 模块 | 实现 |
| --- | --- |
| 主界面 | WinUI 3 / Windows App SDK 2.2 |
| 缩略图窗口 | Windows Forms 无边框窗口与独立覆盖层 |
| 实时预览 | DWM Thumbnail API |
| 窗口操作 | User32 P/Invoke |
| 全局热键 | `RegisterHotKey`、`WM_HOTKEY` 与低级键盘钩子 |
| 配置 | 本地 JSON |
| 运行时 | .NET 10 |

## 项目结构

```text
src/WinecloudsStudio/
├── Configuration/       # 设置、分组和位置持久化
├── Pages/               # WinUI 页面与交互逻辑
├── Services/            # 进程、缩略图、窗口和热键服务
├── Views/               # 缩略图窗口与覆盖层
├── App.xaml.cs          # 启动与权限提升
└── Package.appxmanifest # MSIX 包清单
```

## 本地数据

应用会将运行时设置、缩略图位置和日志保存在 Windows 应用本地数据目录。这些文件不属于源码，也不应提交到仓库。

## 许可

[MIT License](LICENSE)
