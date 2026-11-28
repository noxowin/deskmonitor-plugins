# 外设电量插件的第三方组件

`peripheral-battery.plugin` 内置 BatteryHub 的官方 Windows 便携包、对应的 BatteryHub 源码和 HeadsetControl 源码。插件将 `BatteryHub.exe` 与 `headsetcontrol.exe` 写入当前用户的版本化缓存，逐个验证 SHA-256。便携包内含许可文件和第三方声明。

随包版本、源码提交和各文件的 SHA-256 记录在 [upstream.json](upstream.json)。同步脚本从 [BatteryHub 作者的发布页](https://git.krisgfx.com/krisgfx/Wireless-Device-Battery-Monitor/releases) 获取便携包和 `SHA256SUMS.txt`，并按源码提交下载对应源码。HeadsetControl 的版本和可执行文件校验值来自该提交的 `installer/build-installer.ps1`，对应源码取自 [HeadsetControl 项目](https://github.com/Sapd/HeadsetControl)。

如需从已分发的插件提取便携包及对应源码，将该文件复制为 `peripheral-battery.exe`，再运行 `peripheral-battery.exe --export-third-party <空目录>`。插件项目的 `bin/Release/net48/` 也会生成可执行同一命令的 `.exe`。
