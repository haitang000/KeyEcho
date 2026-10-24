# KeyEcho

Windows 全局键鼠音效应用。中文浅色界面，支持后台打字和鼠标点击发声、托盘运行和本地 WAV 导入。

源码仓库：[haitang000/KeyEcho](https://github.com/haitang000/KeyEcho)。项目代码采用 [Apache License 2.0](LICENSE)，第三方组件和音效素材的许可见文末说明。

## 使用

按下方步骤从源码构建后，打开生成的安装程序 `artifacts/installer/KeyEcho-Setup-1.2.6-x64.exe`，按提示安装。更新前先从托盘退出旧版本。安装包包含 .NET 运行环境，安装和运行应用不需要开发工具。

- **机械键盘**：9 款真实录音，Cherry MX 红／茶／青／黑轴各有 ABS、PBT 两个版本，另有 Topre 静电容。选择下拉列表中的轴体即可切换；空格、回车、字母等使用对应键位的采样。
- **枪械**：手枪、步枪、霰弹枪、AWM 狙击枪。AWM 预设从用户提供的视频中截取单次枪响，去掉后续拉栓段，提升响度并限幅防止削波。
- **热门梗音效**：FAHHHHHH、冰冰冰、bruh、AUGHHHHH、钢管落地、Shocked、私人笑声、wow，使用用户提供的原声，保留原速和双声道。选中后每次按键播放一次，支持暂停时试听，重启后保留选择。列表可向下滚动查看更多音效。
- **自定义**：导入单／双声道 PCM 或 IEEE Float WAV，最长 30 秒、最大 32 MB。导入后原文件可移动或删除。
- **鼠标**：在“鼠标音效”页选择清脆点击、柔和点击，或“跟随键盘音效”（包括枪械和全部热门梗音效）。清脆／柔和点击使用四段真实按键采样，相邻点击不重复同一段，中键使用独立录音；柔和版轻微收敛高频并降低响度，保留按键瞬态。左键、右键、中键和前进／后退侧键按下时发声，移动、滚轮和松开不发声。
- **独立控制**：鼠标有独立开关。右侧音量滑块控制当前页对应的键盘或鼠标音量，两者互不影响。
- **试听**：暂停时也能试听，不会重新开启全局音效。
- **暂停／恢复**：点击窗口开关、托盘菜单，或按 `Ctrl+Alt+F12`，同时暂停／恢复键盘与鼠标。快捷键冲突时会在窗口提示；该暂停快捷键本身也有一次按键声反馈。
- **关闭窗口**：继续在托盘运行。右键托盘图标选择“退出”可完全结束程序。
- **开机启动**：默认关闭，在窗口勾选后以托盘方式启动。

首次运行默认红轴 PBT、清脆鼠标点击，键盘与鼠标音量均为 35%，启用音效。长按只响一次，快速输入可叠加最多 32 路声音。设置和导入文件保存在 `%LOCALAPPDATA%\KeyEcho`。卸载保留这些个人设置和音效，移除程序、快捷方式及开机启动项。

1.2.0 为 Alt+Tab 和第三方注册的全局快捷键增加低层键盘钩子补偿。普通输入继续由 Raw Input 提供设备信息；两路事件关联去重，缺少 Raw Input 的组合键约 12 毫秒后补播。钩子始终传递原始事件，不拦截快捷键功能；只在内存中短暂维护物理按键状态，不保存输入文本。

为降低内存占用，键盘音效按需加载，仅保留最近使用的两套解码缓存。首次切换未缓存的音效时解码一次，实际敲键时只使用内存采样。

1.2.2 移除了鼠标 Raw Input 注册，避免为点击音效接收高频移动消息。独立后台线程以目标 4 ms 间隔读取五个按钮的当前状态；不使用鼠标全局钩子，也不处理坐标或滚轮。鼠标关闭、音量为零或全局暂停时，采样计时器停止。恢复时先读取当前按钮作为基线，避免将已经按住的按钮误认为新点击。按钮按 Windows 合并状态处理，不区分多只鼠标；极短的合成点击可能跨过采样间隔。

适用于 Windows 10 2004 及以上、Windows 11 x64。后台按键监听只针对普通桌面会话，安全桌面、远程或部分特殊输入设备不作为保证支持的场景。蓝牙输出延迟取决于音频设备。

## 开发与构建

技术栈：C# / .NET 10 / WPF / NAudio 3.3 / Inno Setup 6。

在 Windows x64 上安装 Git，然后运行 PowerShell：

```powershell
git clone https://github.com/haitang000/KeyEcho.git
cd KeyEcho
./scripts/bootstrap.ps1 -WithInstaller
./scripts/build.ps1 -Installer
```

工具安装到项目 `.tools` 目录。SDK 下载通过 SHA-512 校验，Inno Setup 安装程序通过 Authenticode 验证。构建脚本使用项目内 SDK，依次执行编译、自动测试、自包含发布及安装包生成。应用运行时不联网、不保存输入内容。

首次准备工具和还原 NuGet 依赖需要联网。仓库包含编译所需的处理后音效；`.tools/`、`artifacts/`、`bin/`、`obj/` 和 `assets/originals/` 不纳入版本控制。只编译和运行自动测试时，执行 `./scripts/bootstrap.ps1` 和 `./scripts/build.ps1` 即可。

开发运行：

```powershell
$env:DOTNET_ROOT = (Resolve-Path './.tools/dotnet').Path
./.tools/dotnet/dotnet.exe run --project src/KeyEcho.App
```

命令行支持 `--background`（隐藏主窗口启动）和 `--exit`（请求已运行的实例正常退出）。重复启动默认打开已有实例。

Windows 集成验证（会试听一个音效，仅分析验证进程自身的音频峰值，不保存音频）：

```powershell
$env:DOTNET_ROOT = (Resolve-Path './.tools/dotnet').Path
./.tools/dotnet/dotnet.exe run --project tests/KeyEcho.WindowsSmoke -c Release -- artifacts/qa
```

## 音效素材

处理后的 WAV 已随源码提供，正常构建不需要重新下载素材。原始素材和处理参数记录于 `assets/*-manifest.json`。

- Mechvibes / Hai Nguyen：9 款键盘录音，MIT，固定上游提交。
- Michel Baradari：枪声素材，CC BY 3.0。
- gokiburi：mouse_clicks.wav，CC0，通过 ESC-50 固定版本取得其中单条录音，截取四段独立点击；1j01：Middle Mouse Click，CC0，用于独立中键音效。来源、逐条许可证据及处理参数见 `assets/mouse-manifest.json`。
- AWM：从用户提供的 `微信视频2026-10-24_012643_888.mp4` 提取，48 kHz 单声道 16-bit PCM，约 0.768 秒；来源及处理参数见 `assets/sniper-manifest.json`。
- FAHHHHHH：从用户提供并确认的 [Fahhhh - Sound effect (HD)](https://www.youtube.com/watch?v=-vH2b55TSHI) 视频提取，48 kHz 双声道 16-bit PCM，2.155 秒。裁掉首尾空白，保留原速、音调和声道，统一增益并添加短淡入淡出；来源与校验值见 `assets/meme-manifest.json`。
- 冰冰冰、bruh、AUGHHHHH：来自用户提供的 MP3，分别约 0.989、0.586、2.872 秒。裁掉首尾静音，保留原速、音调与双声道，统一峰值并添加短淡入淡出；来源与校验值见 `assets/bing-manifest.json`、`assets/bruh-manifest.json`、`assets/augh-manifest.json`。
- 钢管落地、Shocked、私人笑声：来自用户提供的 MP3，分别约 2.615、1.151、6.057 秒。保留原速、音调与双声道，裁掉首尾静音并统一峰值；私人笑声跳过开头静音中的短杂音，保留笑声内部的自然停顿。来源与参数见 `assets/metal-pipe-manifest.json`、`assets/shocked-manifest.json`、`assets/private-laugh-manifest.json`。
- wow：来自用户提供的 `anime-wow-sound-effect.mp3`，保留原速、音调和双声道，裁掉首尾静音并统一峰值；来源与参数见 `assets/wow-manifest.json`。

完整署名、来源、修改说明及组件许可见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

可重新下载的素材来源和校验值记录在清单中；用户提供的原始视频与 MP3 未包含在仓库内。重新处理素材需要先备齐对应原始文件，以及 Python、NumPy、Pillow、FFmpeg、7-Zip：

```powershell
python scripts/prepare-assets.py
python scripts/prepare-keyboards.py
python scripts/prepare-sniper.py
python scripts/prepare-mouse.py
python scripts/prepare-memes.py
python scripts/create-icon.py
```

`prepare-assets.py` 同时保留了早期机械键盘素材的转换流程；当前应用使用 Mechvibes 的 9 款录音。

`prepare-sniper.py` 使用本地保留的 `assets/originals/sniper/awm-video-shot.wav` 重新处理 AWM；该原始提取文件由 `assets/awm-video-source.json` 校验。日常编译直接使用已生成的 `awm.wav`。

`prepare-memes.py` 校验 `assets/originals/memes/` 下保留的视频和 MP3 后重新处理全部梗音效。日常编译与播放直接使用已生成的 WAV，不需要联网。

## 验证范围

自动测试覆盖按键去重、组合键、多键盘、断开清理、混音叠加和峰值限制、暂停清音、WAV 解码及设置恢复，并逐个检查 9 个键盘包的键位和采样。Windows 集成程序检查实际 WASAPI 输出、全部键盘预设、AWM、自定义文件复制及恢复、导入失败回退，以及 100%／150%／200% 离屏布局渲染。

真实硬件插拔、锁屏／休眠恢复、蓝牙切换及不同实体显示器 DPI 仍需要相应硬件手动验收。

## 许可证

Copyright 2026 haitang000。KeyEcho 原创代码、构建脚本和文档采用 [Apache License 2.0](LICENSE)。

第三方组件和音效素材遵循各自的许可与来源说明，不因本项目开源而改为 Apache-2.0。完整信息见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 及 [组件许可证](src/KeyEcho.App/Assets/Licenses/)。其中 AWM 和热门梗音效的原始文件未附带独立再分发许可证，不能据本项目的代码许可证推定获得这些素材的使用或再分发授权。
