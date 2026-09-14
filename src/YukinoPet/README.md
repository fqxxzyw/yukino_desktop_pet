# Yukino Pet 源代码

这里是独立桌宠 v2.1.2 打包前的可编译源工程，不是 Release 成品。

## 目录

- `*.cs`：桌宠窗口、动画、行为、语录、气泡、设置与语音逻辑。
- `assets/`：最终精灵图、左右散步图和程序图标。
- `lib/`：NAudio、NAudio.Vorbis、NVorbis 编译依赖。
- `voice/YukinoVoices.zip`：EXE 内嵌的离线语音资源。
- `quotes.json`：可编辑的语录数据。
- `build.ps1`：Windows 一键编译脚本。

## 编译

在 PowerShell 中进入本目录后执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

生成结果位于 `dist/YukinoPet.exe`，同时会复制 `quotes.json` 和第三方许可说明。编译使用 Windows 自带的 .NET Framework 4.x C# 编译器，不要求安装 Visual Studio；如果系统精简掉了该组件，需要先启用 .NET Framework 4.x。

语音包体积较大，是因为 2391 条语音会直接嵌入最终 EXE，以保证软件发给别人后可以完全离线播放。
