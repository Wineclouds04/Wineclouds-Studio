# Wineclouds Studio

> Windows 桌面窗口预览、快速切换与屏幕区域颜色提醒工具。

Wineclouds Studio 面向需要同时关注多个窗口或屏幕状态的 Windows 用户。它将目标应用窗口显示为可自由摆放的实时缩略图，并提供分组热键切换；也可以持续监测指定屏幕区域，当目标颜色稳定出现时循环播放 MP3 提醒。

窗口实时预览与循环切换的设计参考了 [EVE-O-Preview](https://github.com/Phrynohyas/eve-o-preview)。

## 功能概览

### 窗口管理器

- 通过 DWM Thumbnail 显示目标窗口的实时画面，无需反复切换窗口确认状态。
- 单击缩略图即可激活对应窗口。
- 按窗口分组配置前进、后退热键，按固定顺序循环切换。
- 可调整缩略图尺寸、透明度、边框与置顶状态；支持锁定位置和网格吸附。
- 自动保存窗口布局、分组、热键和显示设置。

### 屏幕区域检测

- 在单屏或多屏虚拟桌面上框选待检测区域，并实时预览画面。
- 按指定 RGB 目标色检测像素，支持色相、饱和度和亮度容差。
- 使用最少目标像素数与最小连通面积过滤孤立噪点。
- 通过出现/消失确认帧数减少画面闪动引发的误报。
- 目标色稳定出现后循环播放本地 MP3 文件，目标色消失后停止并重新布防。
- 自动保存检测区域、检测参数和声音文件路径。

## 适用场景

| 场景 | 使用方式 |
| --- | --- |
| 多开客户端 | 将关键窗口固定为实时缩略图，观察状态后通过点击或热键进入对应窗口。 |
| 远程桌面与任务监看 | 同时查看多个远程会话、构建、下载或渲染任务，避免频繁切换。 |
| 状态提示监测 | 框选界面中的状态灯、告警条或进度提示颜色，出现目标色时获得声音提醒。 |
| 多显示器工作台 | 在跨屏区域内选择检测范围，监看副屏上的应用状态。 |

## 快速开始

### 使用窗口管理器

1. 打开“窗口管理器”，搜索并勾选要关注的窗口。
2. 按需设置缩略图尺寸、透明度、置顶、位置锁定和网格吸附。
3. 将窗口加入分组，并为分组设置前进、后退热键。
4. 点击“开始监控”；之后可点击缩略图，或使用分组热键切换窗口。

### 使用屏幕区域检测

1. 打开“屏幕区域检测”，点击“框选区域”并拖动选择要监测的位置。
2. 设置目标颜色，或使用“选择颜色”从屏幕画面中取色。
3. 根据画面情况调整容差、最少目标像素数、最小连通面积与确认帧数。
4. 选择本地 MP3 提醒声音，点击“开始检测”。
5. 当目标颜色连续出现到设定帧数时，应用开始循环播放声音；颜色连续消失到设定帧数后，提醒停止并等待下次触发。

## 运行要求

- Windows 10 1809 或更高版本，64 位系统。
- 若目标应用以管理员权限运行，Wineclouds Studio 也需要以管理员权限启动，才能稳定地激活与操作该窗口。
- 屏幕区域检测的声音提醒仅接受本地 MP3 文件。

## 从源码构建

开发环境需要：

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows App SDK（由 NuGet 还原）
- [Inno Setup](https://jrsoftware.org/isinfo.php)，仅在创建安装包时需要

```powershell
dotnet restore WinecloudsStudio.slnx
dotnet build WinecloudsStudio.slnx --configuration Release --property:Platform=x64
dotnet run --project .\src\WinecloudsStudio
```

运行检测模块的独立测试程序：

```powershell
dotnet run --project .\tests\WinecloudsStudio.Detection.Tests
```

创建安装包：

```powershell
.\scripts\New-Installer.ps1
```

安装文件输出到 `artifacts\installer\output\`。该目录已被忽略，不应提交到仓库。

## 项目结构

```text
WinecloudsStudio.slnx
├── src/
│   └── WinecloudsStudio/              # WinUI 3 桌面应用
│       ├── Modules/
│       │   ├── Home/                  # 首页
│       │   ├── WindowManager/         # 页面、模型、配置、服务与缩略图视图
│       │   ├── ScreenDetection/       # 页面、选区、截图、提醒与检测核心
│       │   ├── Reserved/              # 预留模块 C–F 的独立目录
│       │   └── Navigation/            # 导航与兜底页面
│       └── Shared/                    # 跨模块共享能力（如日志）
├── tests/
│   └── WinecloudsStudio.Detection.Tests/ # 检测核心的独立测试程序
├── installer/                         # Inno Setup 安装器定义
├── scripts/                           # 发布与打包脚本
└── README.md
```

## 安装与卸载

请从项目发布页或开发者提供的可信下载地址获取 x64 安装包。安装后可从开始菜单或桌面快捷方式启动应用；卸载请前往 Windows“设置”→“应用”→“已安装的应用”，搜索 `Wineclouds Studio`。

安装包未使用正式代码签名证书时，Windows 可能显示“未知发布者”或 SmartScreen 提示。请仅从可信发布地址下载安装文件。

## 许可

[MIT License](LICENSE)
