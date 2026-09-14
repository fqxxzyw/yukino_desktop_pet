# Yukino Pet 独立桌宠

以《我的青春恋爱物语果然有问题。》中的雪之下雪乃为灵感制作的 Windows Q 版桌面宠物。

下载后双击 `YukinoPet.exe` 即可运行。

> **仓库首页不再存放 EXE 或压缩包。请前往 [Releases 下载页面](https://github.com/fqxxzyw/yukino_desktop_pet/releases/tag/v1.0.0) 下载可运行版本。** GitHub 自动生成的 `Source code` 压缩包只是源代码，不是桌宠安装包。

## 下载

- [下载 YukinoPet v2.1.2 单文件 EXE](https://github.com/fqxxzyw/yukino_desktop_pet/releases/download/v1.0.0/YukinoPet-v2.1.2.exe)：角色素材、2391 条语音和音频解码组件均已嵌入。
- [下载 YukinoPet v2.1.2 完整压缩包](https://github.com/fqxxzyw/yukino_desktop_pet/releases/download/v1.0.0/YukinoPet-Standalone-v2.1.2.zip)：包含 EXE、可编辑语录、使用说明和第三方许可说明。
- [下载 VPet Simulator 适配包](https://github.com/fqxxzyw/yukino_desktop_pet/releases/download/v1.0.0/YukinoVPet-v2.1.2.zip)：用于 Steam《虚拟桌宠模拟器》。

当前版本：**2.1.2**

需要回退时，可前往单独的 [“旧版本” Release](https://github.com/fqxxzyw/yukino_desktop_pet/releases/tag/old-versions)。其中的文件仅供存档，首次下载请使用上面的 v2.1.2。

## 源代码

打包前的完整可编译工程位于 [`src/YukinoPet`](src/YukinoPet)。源码、最终动画素材、依赖库、可编辑语录和内嵌语音资源均保存在仓库中；编译方法见该目录的说明。源代码不会放入 Release。

## 主要功能

- 支持左右慢走和自由散步，动作自然流畅。
- 待机时会眨眼、转动视线、歪头等。
- 可以在桌面自由移动，也会在任务栏附近休息。
- 鼠标靠近时会看向鼠标，并支持点击、拖拽和摸头互动。
- 会根据互动表现出不同情绪，并记住近期的互动情况。
- 不同时段会说不同的话，偶尔还会偷偷看你。
- 对话气泡会跟随角色移动，并自动避开屏幕边缘。
- 内置 25/50 分钟专注计时和深夜休息提醒。
- 支持安静模式，可暂停动画和互动。
- 支持多显示器、位置记忆、大小缩放、透明度和窗口置顶。
- 支持系统托盘和开机自动启动。

## 语音

- 2391 条语音已经直接嵌入 EXE，发送单独的 `YukinoPet.exe` 也能播放。
- 点击角色会随机播放一句；再次点击会停止上一句并随机换一句，不会叠音。
- 启动约 20～40 秒后会尝试首次空闲语音，之后约每 45～90 秒随机触发一次。
- 右键菜单一级提供“随机试听一条语音”。
- “语音设置”中可以静音、试听，并选择 25%、50% 或 75% 音量。
- 音频解码或播放设备出现错误时会安全跳过，不会导致桌宠退出。

## 操作

- **单击角色**：互动并随机播放语音；短时间连续点击会逐渐触发疑惑或不耐烦反应。
- **按住左键拖动**：提起并移动角色，松开后播放落地缓冲。
- **摸头**：在角色头顶部位按住鼠标，缓慢左右来回移动。
- **右键角色**：打开动作、语音、专注计时、缩放、透明度、开机启动等菜单。
- **双击托盘图标**：重新显示或隐藏桌宠。

## 设置与语录

设置保存在：

```text
%LOCALAPPDATA%\YukinoPet\settings.ini
```

删除该文件即可恢复默认位置和设置。

压缩包中的 `quotes.json` 可以扩充语录文字、权重、冷却、亲密度和触发条件。文件缺失或损坏时，程序仍会使用内置基础语录运行。

## 当前素材限制

屏幕底边停留已经具有独立状态、碰撞和计时逻辑，但现有素材没有真正坐姿，因此暂时使用安静等待姿势。看书、喝茶、抱膝等完整连续动画也需要后续补充专用素材。

## VPet Simulator 导入与切换

1. 完全退出 VPet。
2. 解压 `YukinoVPet-v2.1.2.zip`，把整个 `YukinoVPet` 文件夹复制到 VPet 安装目录的 `mod` 文件夹。
3. 启动 VPet，在“设置 → MOD 管理”中启用 `Yukino Pet`，然后完全退出并重启。
4. 重新打开设置，点击左侧的“宠物动画”。这个名称是 VPet 官方对角色选择项的称呼，并不表示雪乃只是附加动画。
5. 在下拉框选择“雪乃”（英文界面为 `Yukino`），按提示重启后角色才会真正切换。

只启用 MOD 不会自动替换当前角色；必须额外完成第 4、5 步。

## Windows 提示

程序由本机直接编译，没有商业代码签名证书。若 Windows SmartScreen 显示“未知发布者”，请在确认文件来自本仓库后选择“更多信息 → 仍要运行”。

本项目仅供个人桌面使用。角色、原作及语音相关权利归各自权利人所有，请勿用于商业发布。

<img width="192" height="208" alt="Yukino Pet" src="https://github.com/user-attachments/assets/88241ae2-22d1-4534-ab2a-1655037c68cb" />
