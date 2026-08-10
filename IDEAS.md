# 思路 · How We Thought About It

> 这篇文章讲的是"为什么这么做、怎么想出来的"，不是代码本身。
> 配套：`MAPPING.md`（映射表）、`ATTEMPTS.md`（尝试过程）。
> This article explains the *reasoning* behind the approach, not the code itself.
> Companion docs: `MAPPING.md` (mapping table), `ATTEMPTS.md` (the journey).

---

## 1. 为什么是 Procreate → PS · Why Procreate → Photoshop

Procreate 的 `.brushset` 在 Photoshop 里**没有任何原生导入途径**。反过来，PS 的 `.abr`
可以导入 Procreate（但质量取决于 Procreate 自身的实现）。所以"正向"（Procreate → PS）
才是真正稀缺的方向——这正是这个工具存在的理由。

Procreate's `.brushset` has **no native import path** in Photoshop. The reverse (PS `.abr`)
can be imported into Procreate, but the quality depends on Procreate's own implementation.
So the "forward" direction (Procreate → Photoshop) is the genuinely scarce one — that's why
this tool exists.

## 2. 核心难题：怎么"写"出 PS 认的 ABR · The Core Problem: Writing an ABR Photoshop Accepts

- **ABR v2** 只有笔尖位图，没有描述符 → PS 只认形状，动态全丢。
- **ABR v6/v7** 是 `brushPreset` 描述符 → **顺序和字段类型都必须对齐真实 PS**，PS 才会应用动态。
- 关键发现：**手写"全量 items"描述符会被 PS 拒绝**，只能用**条件性结构**（没有参数就不写对应的动态项）。
  A hand-written "all-items" descriptor gets rejected by PS; only the conditional structure
  (omit dynamic items that have no parameters) is accepted.
- 描述符顺序必须和真实 PS 逐字节一致：纹理项紧跟 `useTexture`（曾错放在末尾导致 PS 忽略）、
  顶层 `Spcn`、`prVr/wtVr/mxVr` 的位置都不能错。
  The descriptor order must byte-match real PS: texture items right after `useTexture` (they were
  once wrongly placed at the end, so PS ignored them), top-level `Spcn`, the positions of
  `prVr/wtVr/mxVr`, etc.

## 3. 对账方法论：真实 PS 笔刷当"二进制标尺" · Byte-Level Reconciliation

用一个真实的全参数 PS 笔刷 + Default Brushes 当**二进制标尺**，逐字段核对：
- **顺序**：描述符每一项的位置。
- **类型**：`long` / `UntF #Prc`（double）/ `doub` / `bool` / `Objc` / `TEXT` / `enum`。
- **值**：真实参考值范围。

靠这个抓出过 **4 处类型错误**：颜色动态 `H/Strt/Brgh/purity`、顶层 `Spcn`、`textureDepth`、
`minimumDepth` 都应是 `UntF #Prc`（double）而不是 `long`。

**这类错误往返自洽测不出来**——读取端兼容两种类型，写入→读回永远一致；只有真实样本的
字节级 diff 才能暴露。这是本项目最重要的方法论：**不要只测"写没写对"，要拿真东西逐字节对**。

## 4. 行为尺子：自洽 ≠ 相似 · The Behavior Ruler: Consistency ≠ Similarity

- `--compare-pack` 只测**往返自洽**（写入→读回一致）。公式再怎么错，往返照样 132/132。
- `--render-compare` 用**标准笔划**（固定压力曲线）并排渲染 Procreate 语义 vs ABR 行为，
  把"像不像"变成可以对比的图。
- 校准**按功能做**（透明度/压力/散射/纹理…），不按笔刷类型。
- 核心认知：**自洽 ≠ 相似**。相似度要靠真实笔划对比去校准，不是改完公式看往返就完事。

## 5. 数据驱动校准 · Data-Driven Calibration

全盘扫描 1262 支 Procreate 笔刷，统计参数分布（间距中位数、散射范围、count 范围…），
再用真实 PS 参考值范围校准公式（散射上限 450、间距曲线 `20+plot*80`、textureBrightness 0 中性、
count 1–5 等）。公式都有数据依据，不是拍脑袋。

## 6. 纹理为什么是死胡同 · Why Direct Texture Embedding Is a Dead End

完整推理链（每一步都实测过，详见 `ATTEMPTS.md`）：
1. ABR 内嵌自定义 pattern 时，pattern 尾部是**像素专有校验和**——非加密，但无法静态反推。
2. PS 27 在脚本层**禁了 `Mk Ptrn`**（error 1220），连 PS 自己录制的 descriptor 原样重放都失败。
3. 手工构造 `.pat` 全被拒（连逐字节复制的真实条目都报"版本不兼容"）。
4. 动作/批处理能定义图案，但图案名是录制时写死的，无法自动化到几百支笔刷。
5. → 结论：**原图纹理直进 PS 暂时不可行**，只能降级：匹配真实内置 pattern（近似）或单独导出纹理。

## 7. 诚实声明 · Honest Disclaimer

转换公式大多处于"**能写入**"而不是"**精确复刻**"的水平：包括间距在内，所有参数都验证过
能真正写进 ABR 并被导入，但精度有限。原因是：① 缺少大量笔刷数据库当参考；② 没有足够
时间、精力和金钱逐一对比、调整、训练。**纯形状笔刷（只是形状平铺）复刻度最高**，微调间距
就能做到几乎一致。锥度、速度动态等需要烘焙和更多公式映照的部分，目前没有做。

欢迎后来者用更多数据和精力把精度提上去。
