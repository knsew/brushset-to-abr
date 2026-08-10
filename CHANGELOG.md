# 更新记录 · Changelog

## v3.0.0（2026-08-11 · 开源准备 · open-source prep）

- **4 处写入类型修正 · 4 write-type fixes**：颜色动态 `H/Strt/Brgh/purity`、顶层 `Spcn`、`textureDepth`、`minimumDepth`
  从 `long` 改为真实 PS 的 `UntF #Prc`（修复 PS 颜色动态数值空白的疑似元凶）。回归 132/132。
  Color dynamics `H/Strt/Brgh/purity`, top-level `Spcn`, `textureDepth`, `minimumDepth` changed from `long`
  to the real PS `UntF #Prc` (likely fix for blank color-dynamics values in PS). Regression 132/132.
- **纯笔刷模式 · Pure-brush mode**：只导图案/间距/大小，不映射其它参数（GUI 复选框 + CLI `--pure`）。
  Shape/spacing/size only (GUI checkbox + CLI `--pure`).
- **启动弹「关于」页 · About dialog on launch**；CLI 自动化模式抑制弹窗。
- **开源整理 · Open-source prep**：移除内嵌的 Procreate 素材库与 Adobe pattern 数据（版权素材不进仓库），
  提供 `tools/extract_patterns.py` 本机重建；补 LICENSE / README / RESEARCH / THIRD-PARTY-NOTICES / CHANGELOG。
  Removed embedded Procreate materials & Adobe pattern data; added `tools/extract_patterns.py`; added docs.

## v2.x（2026-08-10 · WPF 重构与迭代 · WPF rewrite & iterations）

- WPF (.NET 9) 全面重构：三栏可拖拽、Acubi 风格、底部栏、画布性能优化。
  WPF (.NET 9) rewrite: draggable three-pane layout, Acubi style, bottom bar, canvas performance.
- 全动态映射对齐真实 PS：形状/传递/散射/数量/纹理/湿边/颜色动态。
  Full-dynamics mapping aligned to real PS: shape/transfer/scatter/count/texture/wet-edge/color dynamics.
- 内置材质解析（130→132 支）、笔尖尺寸上限、笔刷库、行为尺子 `--render-compare`。
  Bundled-material parsing (130→132), tip size cap, brush library, behavior ruler.
- 纹理近似方案（匹配 11 个真实 pattern）；原图直进 PS 判定不可行（详见 RESEARCH.md）。
  Approximate-texture scheme (match 11 real patterns); direct texture embedding judged infeasible (see RESEARCH.md).

## v1.x（WinForms 时代 · WinForms era）

- 基础转换：`.brushset → .abr`，中文名/间距/角度/圆度/笔尖 alpha。
  Basic conversion: `.brushset → .abr`, names/spacing/angle/roundness/tip alpha.
- AbrReader v10 兼容修复；UI v1–v4 迭代；还原度评分体系。
  AbrReader v10 compat fix; UI v1–v4 iterations; fidelity scoring.
