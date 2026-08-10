# 尝试过程 · The Journey

> 按时间顺序记录每一步试了什么、成没成。负结果同样有价值——它们省下了后来者的时间。
> Chronological record of what was tried and whether it worked. Negative results matter too —
> they save later developers' time.

---

## 阶段 1：解包 · Extract (v1，WinForms 时代)

- **目标**：把 `.brushset` 里的笔尖图（Shape.png）和参数（params.json）解出来。
- **结果**：✅ 用 zip 直接解包，能批量提取所有笔尖图、中文名、间距等参数。
- **认识**：很多笔刷用的是 **Procreate 内置材质**（bundledShapePath/GrainPath），zip 里看不到
  物理文件——后来用"内置材质解析"补上了（恢复 130→132 支）。
- Stage 1 goal: extract Shape.png tips and params.json from `.brushset`. ✅ Done via zip unpacking.
  Key finding: many brushes use Procreate *bundled* materials (bundledShapePath/GrainPath) not present
  as physical files — later solved via bundled-material resolution (restores 130→132 brushes).

## 阶段 2：写入 · Write (v2 时代)

- **目标**：把笔尖图写回 ABR。
- **结果**：✅ 写出 ABR v2（只有笔尖位图），PS 能导入。
- **踩坑**：v2 没有描述符 → **PS 只认形状，动态全丢**。这也是后来发现"GUI 默认误写 v2"
  导致"PS 只导入形状"的根因——改成默认 v6 后修复。
- Stage 2 goal: write tips back into ABR. ✅ Wrote ABR v2 (bitmap only). Pitfall: v2 has no
  descriptor, so PS imports only the shape, all dynamics lost — the root cause of the
  "PS imports shape only" bug (GUI defaulted to v2; fixed by defaulting to v6).

## 阶段 3：描述符逆向 · Descriptor Reverse-Engineering (v6/v7)

- **目标**：写 v6/v7，让 PS 应用动态。
- **尝试**：先手写"全量 items"描述符 → **PS 拒绝**。改成**条件性结构**（无参数不写动态项）→ ✅ 导入成功。
- **结果**：✅ 逆向出 brushPreset 描述符顺序（59 项），对齐真实 PS。
- **关键认知**：描述符顺序必须逐字节一致；纹理项位置、顶层 Spcn、prVr/wtVr/mxVr 都验证过。
- Stage 3 goal: write v6/v7 so PS applies dynamics. Hand-written "all-items" descriptor was
  **rejected by PS**; the conditional structure (omit dynamics without parameters) worked.
  Reverse-engineered the 59-item brushPreset order to byte-match real PS.

## 阶段 4：参数映射 · Parameter Mapping

- **目标**：把 Procreate 参数映射到 ABR 字段。
- **尝试**：对照真实 PS 参考值 + 扫描 1262 支 Procreate 笔刷统计分布，逐功能写映射公式。
- **结果**：✅ 形状/间距/双色/分布/散射/湿边/颜色动态等能写入并导入；**但精度不足**——
  公式大多是"能写入"而非"精确复刻"，因为缺数据库参考和训练时间。
- **收获**：颜色动态键名（clVr/H/Strt/Brgh/purity）和**类型**（UntF #Prc 而非 long）都是靠
  真实笔刷字节对账才确认的——这正是 132/132 往返测不出来的那类错。
- Stage 4 goal: map Procreate params to ABR fields. ✅ Shape/spacing/dual-color/distribution/scatter/
  wet-edge/color dynamics write & import; **precision is limited** (formulas prove "writeable", not
  "faithful") due to lack of a large reference database and training time. Color-dynamics key names
  and **types** were confirmed via byte-level diff against a real brush — exactly the class of error
  that round-trip tests can't catch.

## 阶段 5：纹理 · Texture（最大的坑）

- **目标**：把 Procreate 原 Grain 纹理直接写进 ABR。
- **尝试**（按顺序，全部实测）：
  1. 内嵌自定义 pattern（像素 + 尾部）→ **PS 拒**。分析：尾部是像素专有校验和，非加密但无法静态反推。
  2. 用真实 pattern 名字引用（像素不匹配）→ ✅ 能导入，但 PS 用的是它自己的内置图，不是我们的。
  3. 脚本 `Mk Ptrn`（AM 命令）→ **PS 27 报 error 1220 被禁**，连 PS 自己录制的 descriptor 原样重放都失败。
  4. 动作/批处理定义图案 → ✅ 能成功（批处理 5 张图→5 个独立图案），但**图案名是录制时写死的**，无法自动化到几百支。
  5. 手工构造 `.pat` → **全被拒**（"版本不兼容"），连逐字节复制的真实条目都拒，根因未明。
- **结论**：**原图纹理直进 PS 暂时不可行**。降级方案：① 带纹理的笔刷按 8×8 亮度特征匹配预设库
  里最接近的真实内置 pattern 编入（近似）；② 纹理单独导出，需要时手动导入。
- 若未来 PS 出新脚本接口（UXP 图案 API 或 `Mk Ptrn` 解禁），此路可重启（详见 `RESEARCH.md`）。
- Stage 5 goal: embed original Procreate grain as texture. Attempts (all tested):
  1. Embed custom pattern (pixels+tail) → **rejected**; the tail is a pixel-derived checksum.
  2. Reference a real pattern name (mismatched pixels) → ✅ imports, but PS uses its own built-in image.
  3. Scripted `Mk Ptrn` (AM) → **blocked in PS 27 (error 1220)**, even replaying PS's own recorded descriptor.
  4. Actions/batch to define patterns → ✅ works, but the pattern name is baked into the action.
  5. Hand-built `.pat` → **all rejected** ("version incompatible"), even verbatim real entries.
  Conclusion: direct texture embedding is currently infeasible; fallback = match closest real pattern
  or export textures separately. Could restart if PS exposes a new scripting path.

## 阶段 6：类型修正 · Type Fixes（2026-08）

- **对账发现**：颜色动态 `H/Strt/Brgh/purity`、顶层 `Spcn`、`textureDepth`、`minimumDepth`
  写成了 `long`，真实 PS 是 `UntF #Prc`（double）。
- **修复**：改成 `WriteUnitPercent`；reader 兼容两种类型，所以往返没暴露，只有真实字节 diff 抓出来。
- **验证**：回归 132/132 不倒退；用带颜色动态的笔刷包重转，字节级确认类型已对齐。
- Stage 6: byte-level audit found 4 write-type errors (long → UntF #Prc). Fixed, verified 132/132,
  confirmed at the byte level with a color-dynamics brush pack.

## 阶段 7：产品化 · Productization（2026-08）

- WPF (.NET 9) 全面重构：三栏可拖拽、Acubi 风格、启动弹「关于」。
- 笔刷库（改名/反转/收藏/多选）、笔尖尺寸上限、导出 `.brushset`、还原度报告、行为尺子。
- 纯笔刷模式（只导图案/间距/大小，CLI `--pure`）——给"只要形状"的用户。
- 开源准备：移除内嵌的 Procreate 素材与 Adobe pattern 数据（版权不入仓），提供本机重建脚本。
- Stage 7: WPF rewrite, brush library, tip-size cap, fidelity report, behavior ruler, pure-brush mode,
  and open-source prep (removed copyrighted assets, added local regeneration script).

---

## 给后来者的总结 · TL;DR for Future Developers

- **最容易踩的坑**：描述符**类型**错（round-trip 测不出来）+ 描述符**顺序**错（PS 忽略动态）。
  解法只有一个：**拿真实 PS 笔刷逐字节对**。
- **纹理**：别花时间在"自定义 pattern 内嵌"上，等 PS 出新脚本接口。
- **精度**：公式能"写入"≠"精确"。要提升必须靠大量真实笔刷数据 + 逐支对比训练。
- Biggest pitfalls: descriptor *types* (round-trip can't catch) and descriptor *order* (PS ignores
  dynamics). Only fix: byte-compare against a real PS brush.
- Texture: don't spend time on custom-pattern embedding until PS exposes a new scripting path.
- Precision: "writeable" ≠ "faithful"; improving requires a large real-brush dataset + per-brush tuning.
