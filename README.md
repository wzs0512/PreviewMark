# PreviewMark

PreviewMark 是一个面向 Windows Insider 预览版的轻量水印开关。它只修改当前 Explorer 进程中 `shell32.dll` 的一条内存指令，不改写系统文件、注册表或启动项。退出并重新启动 Explorer、注销或重启 Windows 后，内存修改会自动消失。

本工具不处理“激活 Windows”水印，也不更改 Windows 授权状态。

## 使用

运行 `release/PreviewMark.exe`，先点“检查”，确认当前构建能被唯一识别，再点“隐藏本次水印”。同一窗口中的“恢复”会还原本次会话保存的原始字节。

命令行版本提供相同功能：

```powershell
.\release\PreviewMark.Cli.exe --scan
.\release\PreviewMark.Cli.exe --self-check
.\release\PreviewMark.Cli.exe --apply
.\release\PreviewMark.Cli.exe --restore
```

`--scan` 只读取系统 DLL；`--self-check` 只测试临时分配内存中的写入和恢复，不触碰 Explorer。工具只支持 x64 Windows。扫描不唯一、构建不匹配或 Explorer 中的 DLL 与磁盘版本不一致时，工具会停止。

## 构建

在 x64 Windows PowerShell 中运行：

```powershell
.\Build.ps1
```

构建依赖 Windows 自带的 .NET Framework C# 编译器和 WinForms，不需要下载包。

## 限制

- 水印隐藏仅持续到当前 Explorer 进程退出。Explorer 更新或重启后需要重新运行。
- 仅在 Windows Insider 预览水印上验证。Windows 更新可能改变水印绘制实现，使扫描失败；失败时工具会停止，不会尝试猜测地址。
- 隐藏后若桌面没有立即重绘，可按 `F5` 刷新桌面。
- 程序会在 `%LOCALAPPDATA%\PreviewMark\session.ini` 保存当前进程 ID、目标 RVA 和一个原始字节，供“恢复”使用；恢复成功或确认 Explorer 已重启后会删除该记录。

## 参考

目标识别思路参考了 [UWD3](https://github.com/jcnnik/uwd3)：在 `shell32.dll` 中定位预览水印绘制函数，并只对 Explorer 当前进程应用可逆的内存补丁。PreviewMark 采用独立的 C# 实现；UWD3 项目以 MIT License 发布，出处和许可信息见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
