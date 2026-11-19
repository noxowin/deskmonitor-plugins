# DeskMonitor Plugins

[DeskMonitor](https://github.com/noxowin/deskmonitor) 的官方插件仓库。各插件可独立安装和构建，使用 [插件 API v1](docs/plugin-api.md)。

## 插件

| 插件 | 功能 | 下载 |
| --- | --- | --- |
| [外设电量](extensions/peripheral-battery) | 显示蓝牙和受支持的 2.4 GHz 外设电量，内置 Wireless Battery 采集引擎 | [0.2.1](https://github.com/noxowin/deskmonitor/releases/download/v0.3.0/peripheral-battery.plugin) |
| [电脑电源](extensions/device-power) | 显示本机电池电量、充电状态和系统提供的剩余续航 | [0.1.0](https://github.com/noxowin/deskmonitor/releases/download/v0.3.0/device-power.plugin) |
| [今日进度](extensions/day-progress) | 显示今天已经过去的比例和距离午夜的时间 | [0.1.0](https://github.com/noxowin/deskmonitor/releases/download/v0.3.0/day-progress.plugin) |

在 DeskMonitor 的 **设置 → 插件 → 导入插件** 选择下载的 `.plugin`，也可放到 `DeskMonitor.exe` 同目录的 `plugins/`。表中的下载对应 DeskMonitor 首发版本。

## 构建

在 Windows 上使用 .NET SDK 构建，插件目标框架为 .NET Framework 4.8。可以构建整个仓库，也可以定位到单个插件项目。

```powershell
dotnet build DeskMonitor.Plugins.sln -c Release
dotnet build extensions/peripheral-battery/plugin.csproj -c Release
```

每个插件自己的 `bin/Release/net48/` 目录会生成可直接分发的 `.plugin`。构建脚本和协议类型分别位于 `extensions/Directory.Build.targets` 和 `extensions/contract/`。

## 版本与许可

插件版本以各自的 `manifest.json` 为准。插件独立发布时使用 `<插件名>-v<版本>` 标签，例如 `peripheral-battery-v0.2.1`。同一插件的清单 ID 应在升级时保持稳定。

本仓库以 [DeskMonitor v0.3.0 的插件源码](https://github.com/noxowin/deskmonitor/tree/v0.3.0/extensions) 为起点。插件代码见 [Apache-2.0 许可证](LICENSE)。外设电量插件随包的 Wireless Battery 和 HeadsetControl 源码、许可与校验信息见[第三方组件说明](extensions/peripheral-battery/vendor/README.md)。
