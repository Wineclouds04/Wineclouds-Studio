# Wineclouds Studio

> 面向 Windows 多窗口场景的实时桌面预览与快速切换工具。

Wineclouds Studio 将你选定的应用窗口变成可自由摆放、始终置顶的实时缩略图。无需反复切换窗口，也能在桌面上观察多个客户端、远程桌面或工作程序的状态；需要操作时，点击缩略图或按下分组热键即可回到目标窗口。

窗口预览与循环切换的思路参考 [EVE-O-Preview](https://github.com/Phrynohyas/eve-o-preview)。

## 出发点

多开游戏客户端、管理多台远程桌面或同时处理多个长时间任务时，传统的 Alt+Tab 有两个问题：你需要逐个切换才能确认窗口状态，也很难按固定顺序回到某个窗口。

Wineclouds Studio 的目标不是替代窗口管理器，而是在不打断当前工作的前提下，让关键窗口始终“看得见、点得到、切得快”。它使用 Windows DWM Thumbnail 显示真实窗口画面，而不是定时截取静态图片；窗口内容变化会直接反映到桌面缩略图上。

## 核心亮点

- **实时预览，而非截图**：使用 DWM Thumbnail 显示目标窗口的即时画面，适合观察游戏、远程桌面、下载器和业务程序状态。
- **直接切换**：左键点击任一缩略图即可激活对应窗口，无需先在任务栏或 Alt+Tab 列表中寻找它。
- **为多开而设计**：将窗口整理为分组，为每个分组配置前进、后退单键热键，按固定顺序循环切换。
- **不干扰桌面布局**：缩略图支持置顶、透明度、尺寸和边框调节；可锁定位置避免误拖动，并按 8px 网格对齐。
- **记住你的工作台**：自动保存窗口分组、热键、缩略图显示参数与位置，下次启动无需从头安排。
- **普通安装体验**：提供传统 Windows 安装包，安装后从开始菜单或桌面快捷方式启动，也可通过 Windows 设置卸载。

## 适用场景

| 场景 | 你可以怎样使用 |
| --- | --- |
| 多个游戏客户端 | 将每个角色窗口缩小并固定在屏幕边缘，观察状态后用热键轮流切换。 |
| 远程桌面与服务器监控 | 同时查看多个远程会话或控制台，有变化时点击缩略图直接进入。 |
| 长时间任务 | 监看构建、下载、渲染或自动化程序，不必频繁切回原窗口。 |
| 直播与日常办公 | 将聊天、预览或关键工作窗口保持在可见区域，减少窗口查找。 |

## 工作方式

```text
选择可用进程 → 创建实时缩略图 → 调整并保存布局
                              ├─ 左键：激活目标窗口
                              ├─ 右键拖动：调整缩略图位置
                              └─ 分组热键：按顺序切换窗口
```

1. 在“窗口管理器”中搜索并勾选需要监控的窗口。
2. 设置缩略图的大小、透明度、置顶、边框、位置锁定和网格吸附。
3. 按需将窗口加入分组，并为分组设置前进、后退热键。
4. 点击“开始监控”，通过缩略图或热键在目标窗口之间切换。

## 下载与安装

请从项目发布页或开发者提供的下载地址获取 x64 安装包：

```text
WinecloudsStudio-Setup-<版本号>-win-x64.exe
```

安装步骤：

1. 双击安装包；出现 Windows 权限提示时选择“是”。
2. 按向导选择安装目录。建议保留默认位置。
3. 如需桌面入口，勾选“Create a desktop shortcut”。
4. 安装完成后，从开始菜单或桌面快捷方式启动 Wineclouds Studio。

安装包已携带应用所需的 .NET 和 Windows App SDK 运行时，无需额外安装运行环境。

> Wineclouds Studio 需要激活其他窗口。若目标程序以管理员权限运行，Wineclouds Studio 也需要以管理员权限启动，才能稳定地与其交互。
>
> 当前安装包未使用正式代码签名证书。Windows 首次运行时可能显示“未知发布者”或 SmartScreen 提示；请只从可信发布地址下载。

## 卸载与本地数据

通过 Windows“设置” → “应用” → “已安装的应用”搜索 `Wineclouds Studio`，选择“卸载”即可；也可以从开始菜单的 Wineclouds Studio 文件夹中打开卸载入口。

卸载程序会删除安装目录。应用的布局、热键、缩略图位置和日志保存在 Windows 本地数据目录中，以便升级后继续使用；如需完全清除，请在卸载后手动删除这些本地数据。

## 项目结构

```text
WinecloudsStudio.slnx
├── src/WinecloudsStudio/             # 应用主体
│   ├── Pages/                        # WinUI 页面与交互：窗口选择、分组、热键
│   ├── Services/
│   │   ├── Implementation/           # 进程监控、DWM 缩略图、窗口操作、热键服务
│   │   ├── Interface/                # 服务边界与依赖抽象
│   │   └── Interop/                  # DWM、User32、热键等 Win32 调用
│   ├── Views/                        # 独立缩略图窗体、覆盖层和视图工厂
│   ├── Configuration/                # 布局、窗口分组与持久化配置
│   ├── Assets/                       # 应用图标与图像资源
│   ├── app.manifest                  # 普通 EXE 的权限、DPI 与兼容性配置
│   └── WinecloudsStudio.csproj       # WinUI 3 / .NET 项目定义
├── installer/
│   └── WinecloudsStudio.iss          # Inno Setup 安装器定义
├── scripts/
│   └── New-Installer.ps1             # 发布应用并编译安装包
├── README.md
└── LICENSE
```

## 技术实现

| 领域 | 实现 |
| --- | --- |
| 主界面 | WinUI 3 / Windows App SDK |
| 实时窗口预览 | Desktop Window Manager（DWM）Thumbnail API |
| 缩略图窗口 | Windows Forms 无边框窗体与独立覆盖层 |
| 目标窗口操作 | User32 P/Invoke |
| 全局热键 | `RegisterHotKey`、`WM_HOTKEY` 与低级键盘钩子 |
| 本地配置 | JSON 持久化 |
| 发布方式 | .NET 自包含 x64 发布 + Inno Setup 安装包 |

## 从源码构建

开发环境需要：

- Windows 10 1809 或更高版本，64 位系统。
- [.NET 10 SDK](https://dotnet.microsoft.com/download)。
- [Inno Setup](https://jrsoftware.org/isinfo.php)，仅在生成安装包时需要。

运行应用：

```powershell
dotnet restore WinecloudsStudio.slnx
dotnet build WinecloudsStudio.slnx --configuration Release --property:Platform=x64
dotnet run --project .\src\WinecloudsStudio
```

生成安装包：

```powershell
.\scripts\New-Installer.ps1
```

生成文件位于：

```text
artifacts\installer\output\WinecloudsStudio-Setup-1.0.0-win-x64.exe
```

`artifacts/` 中的发布产物已被 `.gitignore` 忽略，不应提交到仓库。

## 许可

[MIT License](LICENSE)
