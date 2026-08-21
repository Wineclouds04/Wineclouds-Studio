<div align="center">

[简体中文](README.md) · **English**

<pre>
██╗    ██╗██╗███╗   ██╗███████╗ ██████╗██╗      ██████╗ ██╗   ██╗██████╗ ███████╗
██║    ██║██║████╗  ██║██╔════╝██╔════╝██║     ██╔═══██╗██║   ██║██╔══██╗██╔════╝
██║ █╗ ██║██║██╔██╗ ██║█████╗  ██║     ██║     ██║   ██║██║   ██║██║  ██║███████╗
██║███╗██║██║██║╚██╗██║██╔══╝  ██║     ██║     ██║   ██║██║   ██║██║  ██║╚════██║
╚███╔███╔╝██║██║ ╚████║███████╗╚██████╗███████╗╚██████╔╝╚██████╔╝██████╔╝███████║
 ╚══╝╚══╝ ╚═╝╚═╝  ╚═══╝╚══════╝ ╚═════╝╚══════╝ ╚═════╝  ╚═════╝ ╚══════╝
                 Windows workspace for multi-window workflows
</pre>

**A Windows desktop workspace for multi-window workflows**

Wineclouds Studio brings window management, hardware overview, status detection, an EVE universe map, and customizable themes into one focused desktop workspace.

<p>
  <a href="https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest"><img src="https://img.shields.io/badge/latest-v0.3.6-c96b52" alt="Latest release v0.3.6"></a>
  <a href="#requirements"><img src="https://img.shields.io/badge/Windows-10_1809%2B-00A4EF?logo=windows&amp;logoColor=white" alt="Windows 10 1809 or later"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-22C55E" alt="MIT License"></a>
</p>

<p>
  <a href="https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest"><strong>Download latest release</strong></a>
  ·
  <a href="#features">Features</a>
  ·
  <a href="#quick-start">Quick start</a>
</p>

</div>

## Product overview

Wineclouds Studio is designed for multi-client workflows, remote sessions, build monitoring, game-related information, and multi-display workspaces. Keep important windows, color states, and EVE system intel visible in one configurable desktop tool.

## Features

| Feature | Description |
| --- | --- |
| Window Manager | Live window previews, always-on-top views, click-to-focus, automatic arrangement, opacity controls, and grouped hotkeys. |
| Multi-window Sync | Forward keyboard, mouse, click, drag, and wheel actions to selected target windows. |
| Hardware Overview | View Windows, processor, graphics, memory, storage, display, audio, and network adapter information. |
| Region Detection | Watch a selected screen area for a target color and play a looping MP3 alert after the state is stable. |
| Region Alerts | Offline EVE map data for 5,202 systems across 68 regions, autocomplete search, routing, avoid lists, jump bridges, danger zones, chat intel, and activity statistics. |
| Themes and Palettes | Daylight, Midnight, or system appearance with Minimal, Ocean Blue, Sakura Pink, and Liquid Glass palettes. |
| Account and Updates | Account sign-in, membership features, and integrity-checked release updates. |

## Region Alerts

Region Alerts is the EVE workspace introduced in version 0.3.6:

- Type a system name, route origin, or destination and choose from matching suggestions.
- Switch between region and universe views, zoom and pan the map, and plan shortest, safer, or low-security-preferred routes.
- Maintain systems to avoid, add bidirectional custom jump bridges, and calculate capital jump ranges.
- Switch between Current, Intel, and Activity tabs in the side panel.
- Paste intel or monitor a selected EVE Chatlogs folder and receive a warning when a system enters the configured danger zone.
- Optionally load public ESI ship-jump and kill statistics. The offline map and route planner remain available without network access.

## Quick start

1. Download and run the installer from the [latest Release](https://github.com/Wineclouds04/Wineclouds-Studio/releases/latest).
2. Choose a workspace feature from the home page after launch.
3. Select windows in Window Manager, configure a color alert in Region Detection, or search systems and plan routes in Region Alerts.
4. Open Settings to choose appearance, palette, language, and whether closing the window exits or minimizes to the tray.

## Requirements

- Windows 10 version 1809 (build 17763) or later.
- 64-bit Windows.
- Administrator approval at startup for reliable window access and global hotkeys.
- A local MP3 file is required for Region Detection audio alerts.
- The installer is self-contained; a separate .NET runtime is not required.

## Data and privacy

- Hardware information, window layouts, detection settings, and appearance preferences are stored locally by default.
- The Region Alerts map and route planner work offline.
- Public ESI activity data is requested only when you choose to refresh it; account and local window data are not uploaded with that request.
- Chat intel monitoring reads only the local EVE Chatlogs folder you select.

## Version updates

The current release is **v0.3.6**. It adds Region Alerts, fixes the Region Detection stop/audio race, and improves map-line visibility in Daylight and Midnight themes.

Download releases and read the full notes on the [Releases page](https://github.com/Wineclouds04/Wineclouds-Studio/releases).

## License and attribution

Wineclouds Studio is released under the MIT License. EVE map data in Region Alerts is adapted from MIT-licensed Slazanger/SMT. This project is not affiliated with or endorsed by CCP hf.

[MIT License](LICENSE)
