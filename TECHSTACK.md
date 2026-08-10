# 技术栈与代码组织

更新日期：2026-08-09
配套文档：[PRD.md](PRD.md)（v4.0 界面重做）、[ROADMAP.md](ROADMAP.md)（开发阶段）、[HANDOFF.md](HANDOFF.md)（构建与验证命令）

## 1. 结论

**留在 WinForms（.NET Framework 4.8）+ csc 命令行编译，界面全部自绘；把 9418 行单文件拆成多个 .cs 文件。不引入 NuGet / MSBuild / 新框架。**

理由：
- 产品核心优势是"单 exe、双击即用、零依赖"，WinForms 恰好满足，且 Windows 11 自带 .NET Framework 4.8，无需安装任何东西。
- 现有验证工作流（`csc` 编译 → `--compare-pack` 回归）完全依赖这个构建方式，HANDOFF 里所有命令都是围绕它写的。
- 最有价值的转换引擎（brushset 解析 / ABR v6/v7 写入 / BinaryPlist / 回归体系）已经过真实样本验证，与 UI 完全解耦，不需要换平台。

## 2. 现状（2026-08-09，Phase 0 拆分后）

| 项 | 值 |
| --- | --- |
| 语言/平台 | C#（csc 编译器，仅支持到 C#5）/ .NET Framework 4.8 |
| 源码组织 | 拆分 8 文件：`Program.cs`/`Tokens.cs`/`Models.cs`/`Core.cs`/`Controls.cs`/`MainForm.cs`/`Mapping.cs`/`Modern.cs`（构建入口 `build.ps1`） |
| 产物 | `PNGtoABR.exe`（`/target:winexe`，GUI 程序，带 CLI 参数；含 `app.manifest` PerMonitorV2 DPI 声明） |
| 构建命令 | `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1`（内部 csc 通配编译 `*.cs`，排除 `.bak-`） |
| 依赖 | 仅 .NET Framework 内置程序集（System.*、System.Windows.Forms、System.Drawing、System.IO.Compression） |
| 测试/回归 | `--compare-pack` 自校验（写入→回读→逐像素/逐参数对比），真实样本 130/130 exact |

已知债务（Phase 0 后剩余）：
- **文档行号已按新文件刷新**（ARCHITECTURE.md 第 6 节已更新；其余以代码为准）。
- **两套并存的设计 token**：`Tokens.cs` 的 `Theme` 与 `Controls.cs` 的 `Ui`，UI v4 Phase 1 需统一为单一 token 体系。

## 3. 约束与目标

- 单 exe、零依赖、双击即用。
- csc 单命令编译不变；CLI 参数与回归命令不变。
- 转换核心不改、黑盒使用（见第 6 节分层）。
- C#5 语言限制：**没有** `out var`、字符串插值、null 条件运算符、表达式体成员、局部函数等；沿用老式 `out` 参数与 `string.Concat`/`+`。
- 界面重做要"现代浅色 + 统一设计系统"，消除系统控件拼盘。

## 4. 选型对比

| 方案 | 构建 | 依赖 | 界面美观度 | 对现有引擎复用 | 风险/代价 |
| --- | --- | --- | --- | --- | --- |
| **WinForms 自绘（本方案）** | csc 不变 | 无（系统自带） | 中等偏高（需自绘投入） | 100% 复用，黑盒 | 自绘控件工作量大，但可控 |
| WinForms + 现成 UI 库（如 SunnyUI/ReaLTaiizor） | 需 NuGet/下载 dll | 多一个 dll 或源码 | 高 | 高 | 引入第三方依赖，与"零依赖"冲突；且这些库基于较新 C# |
| WPF | 需 MSBuild + XAML | 系统自带 WPF | 高 | 需包装引擎成可调用层 | 构建/工作流全变，重写成本大 |
| Avalonia / Electron / WebView2 | 需 NuGet/SDK/Node | 运行时或打包体积大 | 高 | 需重构 | 破坏单 exe 与现有回归，对桌面小工具过重 |

**决定**：WinForms 自绘。美观度靠"设计 token + 统一基础控件"保证，而不是换平台。

## 5. 文件拆分（已实施，Phase 0）

已按下面的目录结构把单文件拆为 6 个文件（**纯搬运、不改逻辑**），构建入口统一为 `build.ps1`：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\ps\build.ps1
```

| 文件 | 内容 |
| --- | --- |
| `Program.cs` | 入口、CLI 分发、错误钩子 |
| `Tokens.cs` | 设计 token：`Theme`（颜色/字号/间距/圆角） |
| `Controls.cs` | 自绘控件与画布：`PreviewBackground`/`BrushCanvas`/`StrokeCanvas`/`Ui`/`FlatGroupBox` |
| `Core.cs` | 转换引擎（黑盒）：`AppSettings`/`AppLog`/`BrushFidelity`/`ParamMapping`/`Cli`/`ComparePack`/`PathUtil`/`BrushsetExtractor`/`JsonUtil`/`BinaryPlist`/`BrushsetConverter`/`AbrReader`/`AbrWriter` |
| `Models.cs` | 数据模型：`SourceFile`/`ExtractedBrush`/`FidelityLevel`/`FidelityReport`/`AbrBrush`/`AbrInput` |
| `Mapping.cs` | Procreate→ABR 映射公式集中配置（校准相似度的唯一调参入口，含真实 ABR 参考注释） |
| `Modern.cs` | UI v4 设计系统控件：`DpiHelper`/`ModernButton`/`ModernCheckbox`/`ModernSwitch`/`ModernInput`/`ModernSelect`/`ModernProgress`/`ModernCard`/`Tile`/`SearchBox`/`PreviewGalleryForm` |
| `MainForm.cs` | 窗口：`MainForm`/`AboutDialog`/`NewBrushPreviewForm` |

## 6. 分层原则

- **转换核心（Core.cs）**：黑盒，对外只暴露稳定方法（`BrushsetExtractor.Extract`、`BrushsetConverter.ConvertToAbr`、`AbrReader.*`、`BrushFidelity.Evaluate`、`ComparePack.Build`）。
- **UI（Views/Controls）**：只调核心接口，不碰二进制格式细节。
- **CLI（Program.cs + Cli）**：保留现有参数，供开发/回归使用。

## 7. 设计系统定义（v4.0）

- **配色（浅色）**：背景 `#F5F5F7`、卡片 `#FFFFFF`、主色 `#0A84FF`、文字主 `#1D1D1F`、次要 `#86868B`、成功 `#30D158`、警告 `#FF9F0A`、危险 `#FF453A`（具体以最终 token 为准，统一放 `Tokens.cs`）。
- **字号**：10（辅助）/ 12（正文）/ 14（标题）/ 20（大数字）四级，全部用同一字体族（Microsoft YaHei UI 或 Segoe UI，中文环境回退）。
- **间距**：4/8/12/16/24 五档；**圆角**：8（控件）/ 12（卡片）。
- **控件清单**（自绘）：`Button`（primary/secondary/ghost/danger）、`Checkbox`/`Switch`、`Input`（TextBox 重绘）、`Select`（ComboBox 重绘）、`Progress`、`Card`、`Tile`（笔刷磁贴）、`SearchBox`、`Badge`（保真徽章）、`Tooltip`。
- 支持高 DPI：`app.manifest` 声明 `dpiAwareness`，绘制统一按 `DpiHelper` 缩放。

## 8. 构建与验证

```powershell
# 构建（每阶段改动后必跑）
powershell -NoProfile -ExecutionPolicy Bypass -File D:\ps\build.ps1

# 回归闸门（界面/引擎改动后都必须 130/130 不倒退）
Start-Process -Wait D:\ps\PNGtoABR.exe -ArgumentList '--compare-pack','D:\ps\FidelityTest\compare-pack-v2','D:\ps\FidelityTest\src-brushset.brushset','--version','7','--force'

# 界面冒烟
Start-Process -Wait D:\ps\PNGtoABR.exe -ArgumentList '--smoke-gui'
Start-Process -Wait D:\ps\PNGtoABR.exe -ArgumentList '--screenshot','ui-shot.png'
```

## 9. 注意事项

- **C#5 语法边界**：不要写插值字符串、`out var`、null 条件；写老式写法，否则 csc 报错。
- 文件统一 UTF-8（带签名可选）；控制台对中文显示乱码是 cp950 问题，不是文件问题，不要因此重写文件。
- 目录没有 git：改动前备份，或用 ROADMAP 每阶段"编译+回归"做闸门；不要动用户未要求改的文件。
- `--compare-pack` 输出目录已有文件时需要 `--force`。

## 10. 可选演进（暂不实施，记录备选）

- **.NET 8 Windows Forms**：用 `dotnet publish -r win-x64 /p:PublishSingleFile=true` 也能产出单 exe，且 C# 现代语法 + 更好自绘体验；但需安装 .NET SDK，且现有 csc 工作流全部迁移，风险高。当界面工作受 C#5 限制明显受阻时再评估。
- **MSBuild csproj**：为 IDE 调试提供一个 `.csproj`（引用同样源码），但保持 csc 为正式构建，避免双套构建漂移。
