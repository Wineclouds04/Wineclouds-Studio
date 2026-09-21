<div align="center">

简体中文 · [English](README.en.md)

<pre>
██╗    ██╗██╗███╗   ██╗███████╗ ██████╗██╗      ██████╗ ██╗   ██╗██████╗ ███████╗
██║    ██║██║████╗  ██║██╔════╝██╔════╝██║     ██╔═══██╗██║   ██║██╔══██╗██╔════╝
██║ █╗ ██║██║██╔██╗ ██║█████╗  ██║     ██║     ██║   ██║██║   ██║██║  ██║███████╗
██║███╗██║██║██║╚██╗██║██╔══╝  ██║     ██║     ██║   ██║██║   ██║██║  ██║╚════██║
╚███╔███╔╝██║██║ ╚████║███████╗╚██████╗███████╗╚██████╔╝╚██████╔╝██████╔╝███████║
 ╚══╝╚══╝ ╚═╝╚═╝  ╚═══╝╚══════╝ ╚═════╝╚══════╝ ╚═════╝  ╚═════╝ ╚══════╝
                 Windows 多窗口工作台
</pre>

**面向 Windows 多窗口工作流的桌面效率工具**

Wineclouds Studio 将窗口管理、硬件概览、状态检测、EVE 星图和主题设置集中在一个清爽的桌面工作台中。

<p>
  <a href="https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest"><img src="https://img.shields.io/badge/最新版本-v0.4.0-c96b52" alt="Latest release v0.4.0"></a>
  <a href="#运行要求"><img src="https://img.shields.io/badge/Windows-10_1809%2B-00A4EF?logo=windows&amp;logoColor=white" alt="Windows 10 1809 or later"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-22C55E" alt="MIT License"></a>
</p>

<p>
  <a href="https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest"><strong>下载最新版</strong></a>
  ·
  <a href="#功能概览">功能概览</a>
  ·
  <a href="#快速开始">快速开始</a>
</p>

</div>

## 产品简介

Wineclouds Studio 适合多开客户端、远程会话、构建任务、游戏辅助信息和多显示器工作台。它可以把需要持续关注的窗口、颜色状态和 EVE 星系情报放在同一个可自定义的界面中。

## v0.4.0 更新

- 新增 EVE 战斗日志面板：累计伤害、滚动 DPS、平均 DPS，支持按角色重置和本地持久化。
- 支持日志目录、统计窗口、保留天数与缩略图统计显示设置。
- DWM 实时预览按需恢复，注册或更新失败后退避重试。
- 配套账号服务修复永久会员续期、过期高级会员兑换和活跃订阅统计。

## 功能概览

| 功能 | 说明 |
| --- | --- |
| 窗口管理 | 实时预览窗口、置顶显示、点击切换、自动排列、透明度调整和分组热键。 |
| 多窗口同步 | 将键盘、鼠标、点击、拖动和滚轮操作同步到多个目标窗口。 |
| 硬件信息 | 查看 Windows、处理器、显卡、内存、磁盘、显示器、音频和网络适配器信息。 |
| 区域检测 | 框选屏幕区域，按颜色、像素数量和稳定帧数检测状态，并循环播放 MP3 提醒。 |
| 区域预警 | 内置 5,202 个星系和 68 个区域的离线星图，支持搜索匹配、路线规划、避让、跳桥、危险区、聊天情报和活动统计。 |
| 主题与调色盘 | 支持日光、夜间和跟随系统主题，并提供极简、海蓝、樱粉和液态玻璃配色。 |
| 账号与更新 | 支持账号登录、会员权益和安全的版本更新检查；更新包安装前会校验完整性。 |

## 区域预警

区域预警是 0.3.6 版本新增的 EVE 工作台：

- 搜索星系、路线起点和终点时，输入内容会即时匹配并显示建议。
- 支持区域视图与宇宙视图、缩放、拖拽、最短路线、更安全路线和偏好低安路线。
- 可维护避让星系和双向自定义跳桥，并计算旗舰跳跃范围。
- 当前星系、情报和活动通过侧栏选项卡快速切换。
- 可粘贴情报或监控 EVE 聊天日志，命中危险区时显示提醒。
- 活动页可选加载公共 ESI 的舰船跳跃和击杀统计；网络不可用时离线地图和路线仍可使用。

## 快速开始

1. 从[最新版 Release](https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest)下载并运行安装包。
2. 启动后在首页选择需要使用的工作区功能。
3. 在“窗口管理器”中选择要关注的窗口，在“区域检测”中设置颜色提醒，在“区域预警”中搜索星系和规划路线。
4. 在“设置”中选择主题、配色、语言以及关闭窗口时退出或最小化到托盘。

## 运行要求

- Windows 10 1809（17763）或更高版本，64 位系统。
- 首次启动需要管理员权限，以便稳定访问目标窗口并注册全局热键。
- 区域检测的声音提醒需要本地 MP3 文件。
- 安装包为自包含版本，不需要另外安装 .NET 运行环境。

## 数据与隐私

- 硬件信息、窗口配置、检测参数和主题设置默认保存在本机。
- 区域预警的星图和路线计算可以完全离线使用。
- 公共 ESI 活动统计只有在用户主动刷新时请求，且不上传账号或本地窗口数据。
- 聊天情报监控只读取用户选择的本地 EVE Chatlogs 目录。
- 战斗日志读取用户配置的本地 EVE 日志目录，统计保存在本机。

## 版本更新

当前版本为 **v0.4.0**（Build 50）。新增战斗日志统计并改进窗口预览恢复；完整更新内容见上方说明。

请通过 [Releases](https://github.com/Wineclouds04/Wineclouds-Studio/releases) 下载正式版本和查看更新说明。

## 许可与声明

Wineclouds Studio 使用 MIT License 发布。区域预警中的 EVE 星图数据改编自 MIT 许可的 Slazanger/SMT；战斗日志模块包含 GPL v3 许可的 EveOPlus/eve-o-preview 改编代码，请参阅随包许可证。本项目与 CCP hf. 无隶属或背书关系。

## English

Wineclouds Studio is a Windows desktop workspace for multi-window workflows. It combines live window management, hardware overview, region color detection, an offline EVE universe map, routing, intel alerts, activity statistics, and customizable themes.

The latest release is [v0.4.0](https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest). It adds persistent EVE combat statistics and thumbnail overlays, improves live-preview recovery, and fixes membership handling in the companion API.

Wineclouds Studio supports Windows 10 version 1809 or later on 64-bit systems. The installer is self-contained and does not require a separate .NET runtime. See the [latest Release](https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest) for downloads and release notes.

[MIT License](LICENSE)
