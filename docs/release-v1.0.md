## PreviewMark 1.0

Windows Insider 桌面预览版水印的轻量开关，适用于 Windows x64。此版本更新了图形界面，并为不同显示缩放加入 DPI 感知。

![PreviewMark 1.0 图形界面](https://raw.githubusercontent.com/wzs0512/PreviewMark/v1.0.0/docs/previewmark-1.0.png)

### 下载

| 文件 | 用途 |
| --- | --- |
| [PreviewMark.exe](https://github.com/wzs0512/PreviewMark/releases/download/v1.0.0/PreviewMark.exe) | 图形界面版，推荐大多数用户使用 |
| [PreviewMark.Cli.exe](https://github.com/wzs0512/PreviewMark/releases/download/v1.0.0/PreviewMark.Cli.exe) | 命令行版，支持检查、隐藏和恢复 |
| [SHA256SUMS.txt](https://github.com/wzs0512/PreviewMark/releases/download/v1.0.0/SHA256SUMS.txt) | 两个程序的 SHA-256 校验值 |

### 快速开始

1. 下载并打开 `PreviewMark.exe`。
2. 程序启动时会只读检查当前 Windows 构建。
3. 点击“隐藏本次水印”；需要恢复时点击“恢复原状”。

### 版本说明

- 重新设计主窗口，清楚显示系统构建、检查结果和操作状态。
- 添加版本信息与项目主页入口，并针对高 DPI 显示器调整缩放。
- 隐藏仅持续到当前 Explorer 会话结束；重启 Explorer、注销或重启 Windows 后自动恢复。
- 不改写系统文件、注册表或启动项；不移除“激活 Windows”提示。
- 已在 Windows 11 Home Insider Preview build 26220.9587 x64 上验证。

源码和构建方法见 [README](https://github.com/wzs0512/PreviewMark/blob/v1.0.0/README.md)。

