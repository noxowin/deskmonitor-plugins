# 外设电量插件的第三方组件

`peripheral-battery.plugin` 在程序集资源中携带以下原始分发文件。插件将便携包内的 `WirelessBattery.exe` 与 `headsetcontrol.exe` 写入当前用户的版本化缓存，逐个验证 SHA-256。便携包内含授权文件和第三方声明。

| 资源 | SHA-256 |
| --- | --- |
| [WirelessBattery-1.0.8-portable.zip](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor/releases/download/v1.0.8/WirelessBattery-1.0.8-portable.zip) | `86e6de44d00f7d9846797a48fa74af73938bb4441c48a8163eebcdfebf2ada1c` |
| [WirelessBattery-1.0.8-source.zip](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor/archive/refs/tags/v1.0.8.zip) | `acbab10a41d070cc58ee78374e24d709785d9d078a0c74e0ecb2751f270e7f41` |
| [headsetcontrol-4.3.0.tar.gz](https://github.com/krisgfx9/Wireless-Device-Battery-Monitor/releases/download/v1.0.8/headsetcontrol-4.3.0.tar.gz) | `918dcacf8aef347744b36b2d259d76880fa64b3869f0f8ef26b6576b873d28a7` |

如需从已分发的插件提取便携包及对应源码，将该文件复制为 `peripheral-battery.exe`，再运行 `peripheral-battery.exe --export-third-party <空目录>`。插件项目的 `bin/Release/net48/` 也会生成可执行同一命令的 `.exe`。
