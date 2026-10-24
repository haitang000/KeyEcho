# KeyEcho 第三方素材与组件

## 九款键盘录音
- 作品：Mechvibes 原始内置键盘录音。Cherry MX Red/Brown/Blue/Black × ABS/PBT，以及 Topre Purple Hybrid PBT。
- 作者／项目：Hai Nguyen / Mechvibes
- 来源：https://github.com/hainguyents13/mechvibes/tree/326252a13e7bef4f1c35d08ef0189b5af6f8ba02/src/audio
- 许可：MIT，Copyright (c) 2021 Hai Nguyen
- 完整许可：Assets/Licenses/MECHVIBES-LICENSE.txt
- 修改：依原项目键位映射提取 936 个键位采样，移除起始静音，统一包内增益、添加短淡出，转换为 48 kHz 单声道 16-bit PCM WAV。保留自然余音，不另行触发松键声。Topre 原始配置中 F8 偏移指向静音，使用相邻功能键的录音作为替代。
- 下载地址、固定提交和校验值见源码 assets/keyboard-manifest.json。

## 早期机械键盘录音（1.0.0 使用，1.1.0 已替换）
- 作品：Mechanical keyboard sound，作者 bluszcz。
- 来源：https://opengameart.org/content/mechanical-keyboard-sound
- 许可：CC BY 3.0，https://creativecommons.org/licenses/by/3.0/
- 源码中保留其处理脚本和来源记录，当前发布包不再使用该录音。

## 枪声
- 作品：Chaingun, pistol, rifle, shotgun shots（shots.7z 中的 pistol.wav、rifle.wav、shotgun.wav）
- 作者：Michel Baradari，由 qubodup 上传
- 来源：https://opengameart.org/content/chaingun-pistol-rifle-shotgun-shots
- 许可：Creative Commons Attribution 3.0 Unported（CC BY 3.0）
- 许可链接：https://creativecommons.org/licenses/by/3.0/
- 法律文本：https://creativecommons.org/licenses/by/3.0/legalcode
- 修改：截取单次枪响，移除换弹段及尾部静音，归一化峰值、添加短淡出，转换为 48 kHz 单声道 16-bit PCM WAV。

以上作者并未为 KeyEcho 背书。素材原始下载地址、校验值和处理参数见源码 assets/sound-manifest.json。
素材许可仅适用于各自的音效，不代表整个应用的许可。

## AWM 狙击枪预设
- 来源：用户提供的 `微信视频2026-10-24_012643_888.mp4`。
- 修改：截取视频约 1.147–1.915 秒的单次枪响，移除后续拉栓声；提高增益、限幅至约 -1 dBFS、添加短淡入淡出，转换为 48 kHz 单声道 16-bit PCM WAV。
- 处理后长度：0.768 秒，平均电平较相同截取片段提高约 5 dB。
- 许可：原视频未附带独立许可证；原始作品权利归其相应权利人。
- 此素材已替换此前使用 CC0 Tikka 录音制作的 AWM 风格预设。来源校验值和处理参数见 assets/awm-video-source.json 和 assets/sniper-manifest.json。

## 鼠标点击音效
- 作品：Middle Mouse Click，middle-click-press.wav。
- 作者：1j01。
- 来源：https://opengameart.org/content/middle-mouse-click
- 许可：CC0 1.0，https://creativecommons.org/publicdomain/zero/1.0/
- 修改：截取按下声、去掉起始静音、调整峰值、添加短淡出，转换为 48 kHz 单声道 PCM WAV；柔和版本额外削弱高频并降低增益。
- 鼠标各按键共用这段按下录音，不触发松开声；“跟随键盘音效”模式使用当前键盘或枪声预设。
- 来源、校验值和产物记录见 assets/mouse-manifest.json。

## FAHHHHHH 梗音效
- 作品：Fahhhh - Sound effect (HD)，由 HighQualitySFX 上传。
- 来源：用户提供并确认的 `Fahhhh - Sound effect (HD).mp4`，对应 https://www.youtube.com/watch?v=-vH2b55TSHI 。
- 许可：用户提供的文件未附带独立再分发许可证；原始作品权利归其相应权利人。
- 修改：截取解码音轨的 0.850–3.005 秒，裁掉首尾空白；保留原速、音调、双声道及动态，仅统一增益至峰值 0.80、添加 0.5 毫秒淡入和 10 毫秒淡出，转换为 48 kHz 双声道 16-bit PCM WAV，2.155 秒。
- 原始参考视频、来源校验值、处理参数与产物校验值见 assets/originals/memes/fah-user-reference.mp4 和 assets/meme-manifest.json。
- 此素材已替换此前使用的 UseSounds 版本。
- “跟随键盘音效”模式同样支持该音效；作者未为 KeyEcho 背书。

## 冰冰冰、bruh 与 AUGHHHHH 梗音效
- 来源：用户提供的 `bing-bing-bing-ding-ding-ding-zhe-ni-bing-wo-wo-liao-bing.mp3`（冰冰冰）、`movie_1.mp3`（bruh）、`aughhhhh-aughhhhh.mp3`（AUGHHHHH）。
- 许可：文件未附带独立许可证；原始作品权利归其相应权利人。
- 修改：裁掉首尾静音，保留原速、音调与双声道，统一峰值至 0.80，添加 0.5 毫秒淡入与 10 毫秒淡出，转换为 48 kHz 双声道 16-bit PCM WAV。处理后长度分别约 0.989、0.586、2.872 秒。
- 原始文件保留在 assets/originals/memes/，来源与处理后产物的 SHA-256 及参数分别见 assets/bing-manifest.json、assets/bruh-manifest.json、assets/augh-manifest.json。
- 键盘按键与鼠标“跟随键盘音效”模式均支持这三项；原作者未为 KeyEcho 背书。

## 钢管落地、Shocked、私人笑声与 wow
- 来源：用户提供的 `metal-pipe-clang.mp3`（钢管落地）、`shocked-sound-effect.mp3`（Shocked）、`baby-laughing-meme.mp3`（私人笑声）、`anime-wow-sound-effect.mp3`（wow）。
- 许可：文件未附带独立许可证；原始作品权利归其相应权利人。
- 修改：裁掉首尾静音，保留原速、音调、双声道及音效内部停顿，统一峰值至 0.80，添加 0.5 毫秒淡入与 10 毫秒淡出，转换为 48 kHz 双声道 16-bit PCM WAV。长度分别约 2.615、1.151、6.057 秒。私人笑声另行跳过开头静音中的短杂音。
- wow 同样保留原速和音调，仅移除首尾静音、统一峰值并施加短淡入淡出。
- 原始文件保留在 assets/originals/memes/，来源校验值、处理参数与产物校验值分别见 assets/metal-pipe-manifest.json、assets/shocked-manifest.json、assets/private-laugh-manifest.json、assets/wow-manifest.json。
- 键盘按键与鼠标“跟随键盘音效”模式均支持这四项；原作者未为 KeyEcho 背书。

## NAudio 3.3.0
Copyright 2008-2026 Mark Heath
Source: https://github.com/naudio/NAudio
License: MIT

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.

## .NET / WPF
Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.
The self-contained distribution includes Assets/Licenses/DOTNET-LICENSE.txt
and Assets/Licenses/DOTNET-THIRD-PARTY-NOTICES.txt, which contain full notices
and license texts from the bundled Microsoft .NET SDK distribution.

## 应用图标
KeyEcho 原创程序化绘制图标，未使用第三方图像。
