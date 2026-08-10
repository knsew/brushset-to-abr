# 第三方参考与版权声明 · Third-Party References & Copyright Notices

本项目参考了以下开源项目与文档。除特殊说明外，**没有复制任何代码**——本项目的实现语言（C#）
与参考项目不同，借鉴的是格式知识、映射思路与启发式**概念**（概念与事实不受版权保护）。
列出的初衷是致谢，并让后续维护者清楚来源。

This project references the following open-source projects and documents. Unless noted otherwise,
**no code was copied** — this project is written in C# while the references are in other languages;
what we drew on is format knowledge, mapping ideas, and heuristic **concepts** (concepts and facts
are not copyrightable expression). This list exists to give credit and to make the provenance clear
to future maintainers.

## 代码/资产参考 · Code / Asset References

| 项目 · Project | 用途 · Use | 协议 · License | 说明 · Notes |
|---|---|---|---|
| [tohsakrat/Brush-Converter](https://github.com/tohsakrat/Brush-Converter) | "内置材质"理念 · bundled-materials concept | **CC BY-NC-SA 4.0** | 只借鉴概念，未复制其 Python 代码（本项目为 C#）。本仓库因此采用 CC BY-NC-SA 4.0 · Concept only, no code copied; this repo thus uses CC BY-NC-SA 4.0 |
| [prct2sai](https://github.com/Xinyisun-creator/prct2sai) | 智能反相启发式、材质库来源 · smart-inversion heuristic, materials source | **无 LICENSE**（默认保留所有权利）· no LICENSE (all rights reserved) | 只借鉴思路，自行实现 `ShouldAutoInvert`；未使用其代码 · Idea only, self-implemented; no code used |
| [Spectator (keimbio/Spectator)](https://github.com/keimbio/Spectator) | 笔刷预览/解析参考 · preview/parse reference | **MIT** | 仅本地研究，未并入本项目代码 · Local study only, not merged |
| [SunnyUI](https://github.com/yhuse/SunnyUI) | 评估过的 UI 库 · evaluated UI library | MIT | **未采用**，仅留档 · **Not used**, archived only |

## 内嵌/生成素材 · Embedded / Generated Assets

| 素材 · Asset | 来源 · Source | 协议 · License | 状态 · Status |
|---|---|---|---|
| `Resources/materials.zip`（Procreate 内置材质 · Procreate built-in materials） | Procreate（Savage Interactive）素材 · Procreate assets | Procreate 版权 · Procreate copyright | **不在仓库内**（csproj 条件包含，本机有才嵌入）· **Not in repo** (conditionally embedded if present locally) |
| `PatternData.cs`（真实 PS pattern 像素+尾部 · real PS pattern pixels + tails） | Adobe 随附 `.pat` / Default Brushes · Adobe-shipped files | Adobe 版权 · Adobe copyright | **仓库内为空数组**。运行 `tools/extract_patterns.py` 在本机生成，生成物仅供个人使用，勿再分发 · **Empty array in repo**; regenerate locally via `tools/extract_patterns.py`, personal use only |

## 文档/格式参考 · Documentation / Format References（非代码，知识致谢 · non-code, knowledge credit）

| 来源 · Source | 用途 · Use |
|---|---|
| Adobe Photoshop File Formats Specification（官方 · official） | PSD/描述符格式依据 · PSD/descriptor format basis |
| fileformats.archiveteam.org/wiki/Photoshop_brush | ABR 格式公开资料 · public ABR format reference |
| 小红书映射文档 / 微博人工复刻指南 · RedNote & Weibo mapping guides | Procreate↔PS 参数映射知识（收录于 MAPPING.md）· parameter-mapping knowledge (see MAPPING.md) |
| Microsoft dotnet-install.ps1 | 构建环境安装脚本（MIT，保留版权头）· build setup script (MIT, copyright header retained) |

## 本项目许可证 · This Project's License

CC BY-NC-SA 4.0（见 `LICENSE`）。由于本项目参考了 CC BY-NC-SA 的 Brush-Converter 概念，
采用相同协议以保持一致；本项目**严禁商用**（与 About 页声明一致）。
CC BY-NC-SA 4.0 (see `LICENSE`). Because this project draws on concepts from the CC BY-NC-SA
Brush-Converter, the same license is used for consistency; **commercial use is prohibited**
(as stated on the About page).
