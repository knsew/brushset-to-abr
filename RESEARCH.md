# 技术突破与研究路线 · Technical Highlights & Research Roadmap

> 说明：本文只讲本项目做了什么、为什么难、后续值得研究什么，不与其他项目做优劣对比。
> 参考过的开源项目与致谢见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
> Note: This document describes what this project does, why it is hard, and what is worth researching next.
> It makes no comparative judgments against other projects. See THIRD-PARTY-NOTICES.md for references & credits.

---

## 一、我们做到了什么 · What We Achieved

### 1. 一条完整的"反向编译"链路
Procreate 的 `.brushset` 在 Photoshop 里**没有任何原生导入途径**，本项目打通了
"读 `.brushset` → 解析参数 → 按 Procreate 语义映射 → 写成 Photoshop 认识的完整动态 ABR"。
brushPreset 描述符 **59 项顺序逐字节对齐真实 PS**；写入的类型（`long` / `UntF #Prc` / `double` / `bool` 等）
也是对照真实 PS 笔刷逐字段核对过的。往返回归 **132/132**。

### 2. "写入类型逐字节对账"的方法论
用一个真实 PS 全参数笔刷 + Default Brushes 当二进制标尺，逐字段核对写入类型，
抓出过 4 处类型错误（颜色动态 `H/Strt/Brgh/purity`、顶层 `Spcn`、`textureDepth`、`minimumDepth`
都应是 `UntF #Prc` 而非 `long`）。这类错误**往返自洽测不出来**（读取端兼容两种类型），
只有真实样本的字节级 diff 才能暴露。这是一个可复用的"对账 → 修正 → 字节验证"闭环。

### 3. "行为尺子"——把"像不像"变成可度量
- `--compare-pack` 只测**往返自洽**（写入→读回一致，公式错照样过）
- `--render-compare` 用标准笔划并排渲染 Procreate 语义 vs ABR 行为，让"像不像"有可对比的图
- 核心认知：**自洽 ≠ 相似**。相似度要靠真实笔划对比来校准

### 4. 数据驱动的公式校准
全盘扫描 1262 支 Procreate 笔刷统计参数分布，用真实 PS 参考值范围校准：
散射上限 450、间距曲线 `20+plot*80`、textureBrightness 0 中性、count 1-5 等，都有数据依据。

### 5. 一组"为什么行不通"的负结果 + 一次"推倒重来"（省下后来者的时间）
- ~~**ABR 内嵌自定义 pattern 被 PS 拒**~~ **已反转（2026-08）**：planar 格式的 pattern 尾部校验确实拒收，但 **VMAL 格式没有校验**——原图纹理能进 PS（借鉴 brushset2abr 的 `build_patt_item` 结构，`$null` pattern 名）。这是本项目纹理方案复活的突破。
- **PS 27 禁了 `Mk Ptrn`**：AM 命令报 error 1220，连 PS 自己录制的 descriptor 原样重放都失败；但动作播放可行（批处理能定义图案）
- **手工构造 .pat 全被拒**：连逐字节复制的真实条目都报"版本不兼容"，根因未明
- 结论：**planar 格式自定义 pattern 不行，但 VMAL 格式可以**——纹理走 VMAL 原图嵌入（近似方案已被替代）

### 6. 其他可复用成果
AbrReader v10+ 兼容（能读原生蕾丝 60 支）、智能反相启发式、内置材质解析
（bundledShapePath/GrainPath）、笔尖尺寸上限（67MB→11MB）、还原度评分体系、
WPF + CLI 双形态零依赖、纯笔刷模式（只导图案/间距/大小）。

---

## 二、值得继续研究的方向 · Research Directions

| 优先级 | 方向 | 为什么值得 | 卡点 / 切入点 |
|---|---|---|---|
| ★★★ | **纹理"水感"校准** | VMAL 原图纹理已能渲染（2026-08），但亮度/对比/混合/湿边的组合还没调到像 Procreate | 需要用户在 Procreate/PS 同款笔刷画一笔当靶子，调 `Mapping.cs` 纹理参数 |
| ★★☆ | **双色压力副色验证** | colorDynamicsPerTip 修复后待实测 | 用户 PS 实测压力副色是否随压力切换 |
| ★★☆ | **双笔刷 dualBrush 完整映射** | Procreate 双笔刷在 PS 语义不同，目前未支持 | **真实样本已拿到**（全参数笔刷里的嵌套 sampledBrush + 子尖图已导出），逆向嵌套结构即可 |
| ★★☆ | **jitter 相似度校准** | 用户反馈"抖动不像真的" | 需要用户同款笔刷各画一笔当靶子，调 `Mapping.cs` 系数 |
| ★☆☆ | **UXP 插件** | 新架构可能绕过被禁的 `Mk Ptrn` | 查 UXP 是否有图案库/预设 API |
| ★☆☆ | **速度动态 / 锥度** | 目前 ABR 无对应字段 | 速度：查新 PS 版本有无字段；锥度：尝试烘焙进 alpha |
| ★☆☆ | **相似度闭环自动化** | 现在是"用户画靶子→人工调参" | 把评分→调公式做成半自动循环 |
| ★☆☆ | **材质合规化** | 开源前置条件 | 把内置素材换成合规开源纹理，或让用户自备（`materials/` 文件夹覆盖已支持） |

---

## 三、作者的技术坦白 · Author's Technical Notes

> 这部分是作者对现状最诚实的一段话，也是了解本项目"能到什么程度"的最好入口。

1. **ABR 是二进制编码**。ABR 笔刷通常能导入到其他软件，但反过来，其他软件很难**制作**出 ABR 格式的笔刷——本项目绕过了这个不对称。
2. **成功实现**从 Procreate `.brushset` 批量提取形状（shape），并批量编译回 ABR。
3. 尝试提取了 Procreate 笔刷的参数，并通过编译**找到了写入方法**：双色混合、形状分布、散射等都能写入并导入。但**所有参数的转换都不够精确**，包括间距在内。虽然找到了变量对照表和写入方式，但无法做到精确调整——原因一是缺乏大量笔刷数据库作为参考，二是没有足够时间逐一对比、调整和训练。**目前翻译出来的参数谈不上精确，只有形状是一致的。**
4. 视频里展示的笔刷是**导入后再手动调整参数**的；另外单独生成了一个版本，用于输出形状、尺寸和间距信息。
5. **纹理是最难的部分**：可以提取 Procreate 笔刷的纹理并批量命名，但**始终无法绕过 Photoshop 的"定义图案"这一环**——直接写入、动作、脚本都试过，没成功。降级方案：把纹理单独导出、需要时手动导入；或带纹理的笔刷按特征匹配预设库里最接近的 pattern 编入。这个功能的更新"随缘"。
6. **纯形状笔刷**（只是形状平铺的那种）复刻度最高，微调一下间距就能做到几乎一模一样。锥度等需要烘焙和公式映照的部分，目前完全没做。

---

## 四、FAQ

### Q: PS 的 .abr 不是能直接导入 Procreate 吗？为什么要做反向转换？
**Procreate → PS 没有原生路径**（PS 完全不能导入 .brushset），这是本项目存在的根本原因。
反向（PS → Procreate）有原生导入，但对复杂 .abr 的导入质量取决于 Procreate 自身实现，
与本项目无关。**结论：本项目专注正向方向（Procreate → PS），这是价值所在；反向是"锦上添花"，优先级低于上表。**
