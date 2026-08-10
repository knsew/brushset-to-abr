# 技术架构：Procreate .brushset 转 Photoshop .abr

更新时间：2026-08-09（Phase 0 拆分后）

源码：`D:\ps\`（C#，WinForms / .NET Framework 4.8，已拆分为多文件，构建入口 `build.ps1`）

程序：`D:\ps\PNGtoABR.exe`

## 1. 两个格式的本质

### Procreate .brushset

- 本质是 zip 包，扩展名为 `.brushset`。
- 每支笔刷一个目录，关键文件：
  - `Shape.png`：笔尖位图，可能是透明底、白底或灰底。
  - `Grain.png`：纹理位图。
  - `params.json`：笔刷参数，如 `plotSpacing`（间距）、`shapeAngle`（弧度）、`shapeRoundness`（圆度）、`dynamics*`（动态曲线）、`taper*`（锥度）、`wetEdgesAmount`（湿边）等。
- 目录名或 archive 元数据携带 UUID，用于笔刷身份唯一化。

### Photoshop .abr

- 二进制画笔库；v6/v7 由文件头 + 逐支 Brush Section 组成。
- 每支 Brush Section 含 Action Descriptor：
  - `brushPreset`：直径 `Dmtr`、间距 `Spcn`、角度 `Angl`、圆度 `Rndn`、`flipX`/`flipY`、湿边布尔 `Wtdg`、插值 `Intr` 等。
  - `sampledBrush`：位图 `samp`、名称 `Nm`、UUID `sampledData`。

## 2. 为什么能转换

1. 两个软件对“笔刷”的底层定义一致：笔尖位图 + 一组参数。
2. 位图可以直接搬运：按 alpha 规则（透明通道优先；白底/灰底按 luma/alpha 归一化）把 `Shape.png` 转成 8 位 alpha 写入 `samp`，保留原始分辨率，可选 RLE 压缩。
3. 参数语义一致时直接映射（见第 3 节）。
4. 无等价字段的能力无法原样写入，只能暂缓或烘焙，用扣分制评分表达差距（见第 5 节）。

## 3. 参数映射

| Procreate | ABR | 公式 / 说明 |
| --- | --- | --- |
| `plotSpacing` | `Spcn` | `round(plotSpacing * 100)`，限制 1-100 |
| `shapeAngle`（弧度） | `Angl` | `shapeAngle * 180 / PI`，归一到 0-360 |
| `shapeRoundness` | `Rndn` | `round(shapeRoundness * 100)`，1 表示正圆 |
| `shapeFlipXJitter` | `flipX` | 原值 |
| `shapeFlipYJitter` | `flipY` | 原值 |
| `Shape.png` | `samp` | 8 位 alpha，原分辨率，可 RLE |
| 默认 | `Dmtr` | 笔尖较长边，上限 2500px |
| 默认 | `Intr` | true |
| 默认 | `Nm` / `sampledData` | 笔刷名 + 与源一致的 UUID |

未映射能力（扣分项）：

| Procreate | 当前处理 | 扣分 |
| --- | --- | --- |
| `Grain.png` / 纹理参数 | 有 Shape 时暂不合成 | -8 |
| `wetEdgesAmount` / `burntEdgesAmount` | ABR 只有布尔 `Wtdg` | -15 |
| `taper*` / `pencilTaper*` | 压力锥度未预烘焙 | -10 |
| `dynamics*` | 动态 descriptor 未实现 | -10 |
| `shapeScatter` / `shapeCount` | 散射动态未实现 | -6 |
| 双笔刷 / 颜色动态 / 混合模式 | 无等价字段 | -5 |

## 4. 三层保真验证

- 像素级：`Shape.png` 原分辨率写入 `samp`，回读时按同一 alpha 规则还原；真实样本 130/130 逐像素 `diff=0`。
- 参数级：`Spcn`/`Angl`/`Rndn` 按公式换算并校验；spacing 精确，angle/roundness 的 0.01 容差只是 float32 显示精度（ABR 写入完整 double）。
- 身份级：UUID 与源目录一一绑定，重名笔刷不串数据。

评分模型：100 分起步，按未映射能力扣分；>=85 高保真，>=60 近似，否则受限；无 archive 参数按 62 分保守评估。

真实样本（2026-08-09）：“蕾丝花边.brushset”474 支中 130 支可转，写入 ABR v7；回读 130/130 逐像素 exact、参数 130/130；可见保真平均 64.22/100，高保真 0 支、近似 95 支、受限 35 支。

## 5. 架构图（文字版）

转换方向：

```text
.brushset zip -> 解包 -> 解析 params.json
  -> alpha 归一化（透明优先 / 白底 luma 反转）
  -> 映射为 ABR descriptor（brushPreset + sampledBrush）
  -> 写 .abr
```

验证方向：

```text
.abr -> 解析 descriptor -> 回读 PNG / 参数
  -> 与源 Shape.png / params.json 逐像素、逐参数对比
  -> compare-summary.json + compare-checklist.md
```

## 6. 关键代码位置（2026-08-09 Phase 4 更新）

源码拆为 8 个文件，构建入口 `build.ps1`（`csc *.cs` 通配，排除 `.bak-` 备份）：

- `Program.cs`：`Program`（26）入口、CLI 分发；`SampleBrushset`（270）内置演示包生成。
- `Tokens.cs`：`Theme`（13）统一设计 token（配色/字号/间距/圆角）。
- `Models.cs`：数据模型 `SourceFile`（19）/`ExtractedBrush`（42）/`FidelityLevel`（95）/`FidelityReport`（101）/`AbrBrush`（109）/`AbrInput`（123）。
- `Mapping.cs`：Procreate→ABR 映射公式集中配置（校准相似度的唯一调参入口）。
- `Modern.cs`：UI v4 设计系统控件：`DpiHelper`（10）/`ModernButton`（53）/`ModernCheckbox`（188）/`ModernSwitch`（299）/`ModernInput`（369）/`ModernSelect`（452）/`ModernProgress`（482）/`ModernCard`（546）/`Tile`（574）/`PackageCard`（688）/`SearchBox`（802）/`PreviewGalleryForm`（956）。
- `Core.cs`（转换引擎，黑盒）：
  - `AppSettings`（19）、`AppLog`（143）：设置与日志。
  - `BrushFidelity`（178）：`Evaluate` 评分、`LevelLabel`、`Summary`。
  - `ParamMapping`（404）：Procreate→ABR 参数读取。
  - `Cli`（447）：`--cli`、`--convert-brushset`、`--cli-extract`、`--compare-pack`。
  - `ComparePack`（745）：回归对照包。
  - `PathUtil`（1189）、`BrushsetExtractor`（1223）解包 brushset、`JsonUtil`（1391）、`BinaryPlist`（1501）、`BrushsetConverter`（1878）。
  - `AbrReader`（1946）：ABR 读取与回读 PNG。
  - `AbrWriter`（2905）：`ConvertImagesToAbr`、`BuildBrush`、`BuildSampEntry`、RLE 编码。
- `Controls.cs`：`PreviewBackground`（19）、`BrushCanvas`（25）、`StrokeCanvas`（127）笔画预览画布、`Ui`（516）、`FlatGroupBox`（551）。
- `MainForm.cs`：`AboutDialog`（19）、`NewBrushPreviewForm`（147）、`MainForm`（269）三栏 UI（包卡片 + 磁贴网格 + 搜索/筛选/排序 + 画布 + 输出设置/参数/报告）与转换流程。

## 7. 文档地图

- [MAPPING.md](MAPPING.md)：参数映射与扣分依据。
- [使用教程.md](使用教程.md)：面向用户的使用教程。
- [HANDOFF.md](HANDOFF.md)：交接、构建与验证命令。
- [PRD.md](PRD.md)：产品需求文档。
