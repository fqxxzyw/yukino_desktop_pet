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

## 回归检查

从仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
powershell -ExecutionPolicy Bypass -File .\src\YukinoPet\build.ps1
```

测试直接编译生产代码中的动画控制器、位置计算和 WPF 气泡窗口，覆盖延迟/暂停/回调、重复拖动坐标、边缘翻转、负坐标显示器、素材透明帧，以及长中文语录的固定窗口尺寸。测试窗口不激活且透明，结束后关闭。

`.github/workflows/windows-validation.yml` 在 Windows runner 上执行以上步骤并保留 7 天的验证构建，只有仓库读取权限，不访问 secrets、不发布 Release。真正的鼠标捕获、混合 DPI 多显示器和菜单键盘交互仍须按 [`tests/WINDOWS-QA.md`](../../tests/WINDOWS-QA.md) 手动确认；数学/布局断言不能替代这些检查。
