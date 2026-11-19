# DeskMonitor 插件 API v1

每个插件是一个可以独立构建和安装的 Windows 命令行程序。把 `.plugin` 放到 `DeskMonitor.exe` 同目录的 `plugins/`，或在 **设置 → 插件** 中导入。新增单文件默认启用，停用状态按插件 ID 保存。

## 安装包

将示例复制为新目录，修改 `manifest.json`，然后执行 `dotnet build extensions/<插件目录>/plugin.csproj -c Release`。共享 MSBuild 目标会自动生成可直接上传的 `bin/Release/net48/<插件目录>.plugin`。

单文件布局为 Windows EXE 字节、UTF-8 清单、四字节小端清单长度、ASCII 标记 `DeskMonitorPlugin1`。宿主读取末尾的清单后再按需启动该程序。插件发布时可使用任意文件名，显示名称、排序和停用状态以清单内的 ID 为准。同一个 ID 同时存在多个文件时，宿主优先选择单文件插件，再按版本选择。

旧 ZIP 及已解压的 `plugin-<id>/` 安装仍可使用。

```json
{
  "apiVersion": 1,
  "id": "example.clock",
  "name": "时钟",
  "version": "0.1.0",
  "description": "显示本机时间。",
  "intervalSeconds": 60
}
```

- `id` 是稳定的小写点分 ID，使用字母、数字和连字符，总长最多 64 字符。
- `intervalSeconds` 的范围为 5 至 3600 秒。宿主按此间隔调度，插件每次采集完成后退出。
- 可选 `inputFileName` 指定由用户选择的文件名，`inputFileLabel` 指定设置页标签。宿主将所选绝对路径传给环境变量 `MONITOR_PLUGIN_INPUT_FILE`，也可能省略该变量。
- 清单最大 16 KiB，程序最大 100 MiB。可使用任何能产出独立 Windows EXE 的语言。

## 读取结果

宿主以普通用户身份启动插件并传入 `--poll`，等待进程退出并解析标准输出中的一个 UTF-8 JSON 对象。每次最多返回 64 条记录和 64 KiB 输出，进程采集限时 20 秒。出错可返回非零退出码。插件进程拥有普通桌面应用的用户权限。

```json
{
  "readings": [
    {
      "id": "local-clock",
      "label": "当前时间",
      "value": "09:42",
      "detail": "本地时间",
      "icon": "time",
      "showOnTaskbar": true
    }
  ]
}
```

- `label` 和 `value` 必填。任务栏建议使用不超过五个窄字符的数值；较长信息放入 `detail`。
- 可选 `id` 使用户能够在详情页控制该条读数的任务栏可见性。ID 在同一个插件内应当唯一，最长 240 字符，匹配时忽略大小写。对同一设备的 ID 应在多轮采集间保持稳定。
- `icon` 支持 `battery`、`keyboard`、`mouse`、`headphones`、`gamepad`、`time`、`power`。其他值使用通用图标。
- `showOnTaskbar` 默认启用，设为 `false` 可仅在详情页显示该条。用户的可见性偏好作用于允许显示在任务栏且有 ID 的条目。
- 暂无数据时返回 `{"readings":[],"message":"暂无数据"}`。宿主在管理界面显示消息，有可见数据时才分配该插件的任务栏空间。

三个示例均位于 `extensions/`。C# 示例共用 `extensions/contract/PluginContract.cs`。
