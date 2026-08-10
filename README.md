# Brush Studio（Procreate .brushset → Photoshop .abr）

把 **Procreate `.brushset` 笔刷包转换成 Photoshop `.abr`** 的 Windows 桌面工具（WPF / .NET 9，零第三方依赖）。
A Windows desktop tool that converts **Procreate `.brushset` brush packs into Photoshop `.abr`** brushes (WPF / .NET 9, zero third-party dependencies).

> 作者 · Author：小红书 **@此恨绵绵无绝期**
> 本软件完全免费，欢迎自由分享与转载，但请勿用于任何商业用途。
> This software is completely free. Feel free to share, but **no commercial use**.
> 许可证 · License：CC BY-NC-SA 4.0（见 [LICENSE](LICENSE)）

---

## 作者的话 · Author's Note

这个工具源于一个笨办法：以前想把 Procreate 笔刷弄进 Photoshop，得先解压 `.brushset`、逐个找出每支笔刷的 shape 图片、再一张张手动导入。做这个软件就是为了省掉这堆重复劳动——它能批量提取、批量编译成 ABR，内置**智能反相**（一键判断是否需要反转）和**收藏夹**（单独导出你想要的那几支）。

This tool was born out of a tedious manual process: to bring Procreate brushes into Photoshop, you used to have to unzip the `.brushset`, find each brush's shape image, and import them one by one. I wrote this software to eliminate that repetitive work — it extracts and compiles brushes in batch, with **smart inversion** (one-click detection of whether a shape needs inverting) and a **favorites** system (export only the brushes you want).

坦白说，这是一个纯 **vibecoding + AI 生成代码**的产物：所有笔刷都留在本地，单 exe 开箱即用，不需要配置任何环境。它最硬的部分——把 `.brushset` 编译回 Photoshop 认的 ABR——已经做到了：形状、间距、双色混合、形状分布、散射等都能写入并导入。但**参数转换的精度不够**：包括间距在内的参数都只能算"能写入"，谈不上"精确复刻"。原因一是缺少大量笔刷数据库当参考，二是我没有时间、精力和金钱去逐一对比、调整、训练。网上几乎找不到这种映射对照表，我是自己反复编译、填入不同数值、一次次验证，才把写入方法试出来。

To be honest, this is a pure **vibecoding + AI-generated** project: all brushes stay local, it's a single self-contained exe with no environment setup. The hardest part — compiling `.brushset` back into an ABR Photoshop recognizes — is done: shape, spacing, dual-color blending, shape distribution, and scatter can all be written and imported. But **the precision of parameter conversion is lacking**: parameters including spacing can only be said to "write correctly", not to "replicate precisely". The reasons are, first, the lack of a large brush database to reference, and second, I don't have the time, energy, or money to compare, adjust, and train each one. There are almost no such mapping tables online — I figured out the write method by repeatedly compiling, filling in different values, and verifying over and over.

纹理是我最没辙的部分：现在只能把 Procreate 的纹理**提取出来单独导出**，或者用**降级方案**——带纹理的笔刷按特征匹配我预设库里最接近的笔刷编入。想直接把纹理图写进笔刷，始终绕不过 Photoshop 的"定义图案"这一环，动作和脚本我都试过，没成功。这个功能的更新就"随缘"了。

Texture is where I'm most stuck: for now I can only **extract and export Procreate textures separately**, or use the **fallback** — brushes with texture are matched to the closest pattern in my preset library by feature. Writing a texture image directly into a brush keeps hitting Photoshop's "Define Pattern" wall; I tried actions and scripts, neither worked. Updates to this feature are "whenever it happens".

这个项目消耗了我大量 Token；我同时还在做一个前端项目，也想把更多精力放在美工上，所以它暂时保持现状。我本来就想把它**免费开源、为爱发电**——等闲下来，我会把编译用的映射表、编译方法、我的思路和尝试过程、以及各类参考一并分享。

This project consumed a lot of my tokens. I'm also working on a frontend project and want to spend more energy on art, so this brush tool will stay as it is for now. I always intended to **open-source it for free — passion over profit**. When I get the time, I'll share the mapping table, the compile methods, my thoughts and attempts, and the various references.

如果你对这个方向感兴趣，非常欢迎**基于它二次开发**。如果以后能实现更多参数的准确对照、用更多数据去锻炼，效果应该会更好。也谢谢 tohsakrat、prct2sai 这些前辈的开源与分享，让这条路有迹可循。

If you're interested in this direction, you're very welcome to **build on it**. If more parameters can be accurately mapped with more data to train on, the results should only get better. Thanks also to the open-source pioneers like tohsakrat and prct2sai, whose work made this path traceable.

---

## 📥 获取成品 exe · Download

**GitHub 仓库只放源码。** 成品 exe 请到作者**小红书主页**获取与关注更新（每次发布/更新都会在那里通知）。也可自行构建（见下）。
**This GitHub repo contains source code only.** Get the ready-to-use exe from the author's **Xiaohongshu (RedNote) page**, where releases and updates are announced. You can also build it yourself (see below).

## ✨ 功能 · Features

- **Procreate → Photoshop**：读 `.brushset`，按 Procreate 语义映射成 PS 认识的完整动态 ABR（v6/v7 推荐，v2 旧版可选）。
  Read `.brushset`, map Procreate semantics into full-dynamics ABR that Photoshop understands (v6/v7 recommended, v2 legacy optional).
- **全动态映射 · Full dynamics**：形状（压力/倾斜/角度/圆度）、传递（静态半透明 + 压力透明度）、散射/数量、纹理（近似）、湿边/混合、颜色动态（双色）。
  Shape (pressure/tilt/angle/roundness), transfer (static opacity + pressure opacity), scatter/count, texture (approximate), wet edge/blend, color dynamics (dual-color).
- **真实 PS 字节级对账 · Byte-level verification against real PS**：描述符顺序与字段类型逐字节对齐真实 PS 笔刷（见 [RESEARCH.md](RESEARCH.md)）。
- **纯笔刷模式 · Pure-brush mode**：只要图案/间距/大小，不映射任何其它参数（GUI 勾选或 CLI `--pure`）。
- 笔刷库（改名/单笔刷反转/收藏/多选）、笔尖尺寸上限（67MB→11MB）、导出 `.brushset`、还原度评分报告、行为尺子 `--render-compare`、启动弹「关于」。
  Brush library (rename/per-brush invert/favorites/multi-select), tip size cap (67MB→11MB), export `.brushset`, fidelity report, behavior ruler `--render-compare`, About dialog on launch.
- **WPF 界面**（三栏可拖拽、Acubi 风格）+ 全功能 CLI。

## 🚀 快速开始 · Quick Start

**图形界面 · GUI**：双击 `PNGtoABR.exe` → 添加 `.brushset` 或拖进窗口 → 选输出位置 → 点「转成 ABR」。
Double-click `PNGtoABR.exe` → add a `.brushset` or drag it in → pick an output path → click "Convert to ABR".

**命令行 · CLI**（程序是 GUI 子系统，PowerShell 用 `Start-Process -Wait` / it's a GUI-subsystem app, use `Start-Process -Wait` in PowerShell）：

```powershell
# 转换笔刷包 · Convert a brush pack
Start-Process -Wait PNGtoABR.exe -ArgumentList '--convert-brushset','输出.abr','输入.brushset','--version','7'

# 纯笔刷模式（只导图案/间距/大小）· Pure mode (shape/spacing/size only)
Start-Process -Wait PNGtoABR.exe -ArgumentList '--convert-brushset','输出.abr','输入.brushset','--pure'

# 反代 ABR（读回描述符）· Reverse-parse an ABR (dump descriptor)
Start-Process -Wait PNGtoABR.exe -ArgumentList '--cli-extract','输出目录','文件.abr','--dump-descriptor'

# 回归对比包（往返自洽）· Regression pack (round-trip)
Start-Process -Wait PNGtoABR.exe -ArgumentList '--compare-pack','输出目录','文件.brushset','--version','7'
```

完整教程见 [使用教程.md](使用教程.md) · Full tutorial: [使用教程.md](使用教程.md) (Chinese).

## 🛠 从源码构建 · Build from Source

需要 .NET 9 SDK（免管理员安装：运行 `dotnet-install.ps1`）。
Requires .NET 9 SDK (install without admin via `dotnet-install.ps1`).

```powershell
# 调试构建 · Debug build → BrushStudioWpf\bin\Debug\...\PNGtoABR.exe
powershell -NoProfile -ExecutionPolicy Bypass -File build-wpf.ps1

# 发布自包含单文件 · Publish self-contained single-file → dist\PNGtoABR.exe
powershell -NoProfile -ExecutionPolicy Bypass -File build-wpf.ps1 -Publish
```

结构 · Layout：核心引擎（`Core.cs` / `Mapping.cs` / `Models.cs` / `RenderSim.cs` / `PatternLibrary.cs` /
`PatternData.cs` / `BrushLibrary.cs`）在仓库根，WPF 工程 `BrushStudioWpf/` 通过 Link 原样引用（勿复制改）。
Engine files live at repo root; the WPF project `BrushStudioWpf/` references them via Link (don't copy-and-modify).

## 📦 版权与素材说明 · License & Assets

- 本项目源码按 **CC BY-NC-SA 4.0** 开源，**严禁商用**。
  Licensed under **CC BY-NC-SA 4.0**. **No commercial use.**
- **不包含 Procreate 素材库**（`materials.zip`）与 **Adobe 真实 pattern 数据**（`PatternData.cs` 为空数组）——
  这两者是第三方版权素材，不能随仓库分发。本机构建时若本地有 `materials.zip` 会自动嵌入；仓库克隆缺失则自动跳过。
  想恢复近似纹理：运行 `tools/extract_patterns.py`（在装有 Photoshop 的本机，从 Adobe 随附文件提取，生成物仅供个人使用）。
  This repo does **not** include the Procreate materials library (`materials.zip`) or Adobe's real pattern data
  (`PatternData.cs` is an empty array) — both are third-party copyrighted assets. The build embeds `materials.zip`
  only if present locally; clones build without it. To restore approximate textures, run `tools/extract_patterns.py`
  on a machine with Photoshop (extracted data is for personal use only).
- 参考项目清单与协议见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) · References & licenses: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## 📚 文档 · Docs

- [IDEAS.md](IDEAS.md)：**思路**——为什么这么做、对账方法论、自洽≠相似（中英）· The *thinking* behind the approach (bilingual).
- [ATTEMPTS.md](ATTEMPTS.md)：**尝试过程**——每一步试了什么、成没成、坑在哪（中英）· The *journey* — what was tried, what worked, where the pitfalls are (bilingual).
- [MAPPING.md](MAPPING.md)：**映射表**——Procreate↔PS 参数对照（公式 + 真实 PS 参考值 + 坑）· Parameter mapping reference.
- [RESEARCH.md](RESEARCH.md)：技术突破点 + 值得继续研究的方向 + FAQ（中英）· Technical highlights, research roadmap, FAQ (bilingual).
- [使用教程.md](使用教程.md)：用户教程（中文）· User tutorial (Chinese).
- [ARCHITECTURE.md](ARCHITECTURE.md)：转换原理与技术架构（中文）· Architecture (Chinese).
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)：参考项目与版权声明 · References & copyright notices.
- [CHANGELOG.md](CHANGELOG.md)：更新记录 · Changelog.
