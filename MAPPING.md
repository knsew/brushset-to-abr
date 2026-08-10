# Procreate → Photoshop ABR 参数对照表 · Procreate → ABR Parameter Mapping

> **English**: This is the authoritative Procreate→ABR parameter mapping reference — the mapping
> formulas, real-PS reference values, descriptor item order, and pitfalls. Formulas are implemented
> in `Mapping.cs`; the writer logic is `BuildBrushPresetItems` in `Core.cs`.
>
> **诚实声明 / Honest note**: formulas are at "writeable" level, not "faithful replication" —
> they prove parameters can be written and imported, but precision is limited by the lack of a large
> reference dataset and tuning time. Pure-shape brushes replicate best. See `IDEAS.md` / `ATTEMPTS.md`.

> 本表基于：① 真实 ABR v6/v7 文件 Action Descriptor 逆向（`brushPreset` + `sampledBrush`）；
> ② 用户提供的手工"全参数"笔刷（颜色动态/结构标尺）；
> ③ 真实 PS 笔刷反代对账（brush/ 84 支 + Legacy Brushes + Default Brushes）；
> ④ 小红书人工复刻指南 + 参考 PDF。
> 所有公式实现在 `Mapping.cs`，调参只改该文件。代码生成逻辑在 `Core.cs` 的 `BuildBrushPresetItems`。

## 0. ABR 描述符 items 顺序（必须与真实 PS 一致，PS 才应用动态）

真实 PS（用户全参数笔刷 1.ABR 逆向）的顺序，**我们的写入已完全对齐**：

```
Nm  , Brsh(sampledBrush), useTipDynamics, flipX, flipY, brushProjection,
minimumDiameter, minimumRoundness, tiltScale, szVr, angleDynamics, roundnessDynamics,
useScatter, Spcn, Cnt , bothAxes, countDynamics, scatterDynamics,
dualBrush, brushGroup,
useTexture, TxtC, interpretation, textureBlendMode, textureDepth, minimumDepth,
textureDepthDynamics, Txtr, textureScale, InvT, protectTexture, textureBrightness, textureContrast,
usePaintDynamics, prVr, opVr, wtVr, mxVr,
useColorDynamics, clVr, H   , Strt, Brgh, purity, colorDynamicsPerTip,
Wtdg, Nose, Rpt , useBrushSize, useBrushPose, [overridePose*/brushPose*],
toolOptions(PbTl/MixB/SmTl)
```

> ⚠️ 历史教训：纹理项曾错写在最末尾（toolOptions 后）导致 PS 忽略动态；
> 现已对齐真实 PS 顺序（纹理紧跟 useTexture）。**改顺序前先对照此表。**

## 1. 直接映射（写入 ABR descriptor）

| Procreate 参数 | ABR 字段 | 类型 | 公式 / 说明 | 真实 PS 参考 |
| --- | --- | --- | --- | --- |
| `plotSpacing` | `Spcn`（顶层 + Brsh 内） | `UntF #Prc` | `20 + plotSpacing*80`，clamp 1-100（用户选定"整体调高"曲线；旧 `plotSpacing*100` 在低端糊片） | PS 范围 1-125，密实笔刷常为 10，SoftRound=25，松散 125 |
| `shapeAngle`（弧度） | `Angl` | `UntF #Ang` | `rad * 180/PI`，归一 0-360 | — |
| `shapeRoundness` | `Rndn` | `UntF #Prc` | `shapeRoundness * 100`（≤0 视为正圆） | 100 = 正圆 |
| 默认 | `Dmtr` | `UntF #Pxl` | 笔尖较长边，上限 2500 | 真实 25~4887 |
| 默认 | `Intr` | `bool` | 始终 true | true |
| `Shape.png` | `samp` 条目位图 | 8 位 alpha | 原分辨率，可 RLE；可 `--max-tip` 压上限 | — |
| 默认 | `Nm  ` / `sampledData` | `TEXT` | 笔刷名 + UUID（无 36 位 UUID 自动生成） | 真实均 36 位 UUID |

## 2. 形状动态 Shape Dynamics

| Procreate 参数 | ABR 字段 | 公式 | 真实 PS 参考 | 状态 |
| --- | --- | --- | --- | --- |
| `dynamicsPressureSize` | `szVr` | bVTy=2（压力）；Mnm=`max(minSize, (1-pressureSize)*100)`；jitter=`dynamicsJitterSize*100` | bVTy 2/3，jitter 0-70，Mnm 0 | ✅ 映射 |
| `dynamicsTiltSize` | `szVr` | bVTy=3（倾斜） | bVTy 3 | ✅ 映射 |
| `dynamicsTiltAngle` | `angleDynamics` | bVTy=3（倾斜），jitter=`TiltPercent(tilt)` | bVTy 0/2/3/5/6 | ✅ 映射 |
| `shapeOrientation≥2` 且无倾斜 | `angleDynamics` | **bVTy=6（初始方向）** | bVTy 6 真实存在（Kyle 3/5 支） | ✅ 2026-08-10 校准 |
| `dynamicsPressureShapeRoundnessMinimum` | `roundnessDynamics` | bVTy=2，Mnm=`×100` | bVTy 0/2，jitter 4-39 | ✅ 映射 |
| `dynamicsJitterSize` | `szVr.jitter` | `×100` | 0-70 | ✅ |
| `dynamicsTiltAngle`→`tiltScale` | `tiltScale` | `clamp(60,200, 60+tilt*1.4)` | 真实 200 | ✅ |
| `dynamics*Curve` 非恒等 | （无） | ABR 动态只有线性斜坡，非线性曲线无法表达 | — | ⚠️ 近似丢失 |

## 3. 透明度 / 传递 Transfer

| Procreate 参数 | ABR 字段 | 公式 | 真实 PS 参考 | 状态 |
| --- | --- | --- | --- | --- |
| `paintOpacity`（静态半透明） | `toolOptions.flow`（PbTl） | `×100`（仅无压力透明度时） | flow 56-100 | ✅ |
| `dynamicsPressureOpacity` | `opVr` | bVTy=2，Mnm=`max(minOpacity, (1-pressureOpacity)*100)`，jitter=`×100` | Mnm 0/39/78/100，jitter 0/30/50 | ✅ |
| `dynamicsJitterOpacity` | `opVr.jitter` | `×100` | — | ✅ |
| `minOpacity` | `opVr.Mnm` | `×100`（静态下限） | — | ✅ |
| 流量/湿度/混合占位 | `prVr`/`wtVr`/`mxVr` | prVr bVTy=2（压力流量）；wtVr/mxVr bVTy=0 | 真实 prVr Mnm=22 | ✅ 结构对齐 |

## 4. 散射 / 数量 Scatter / Count

| Procreate 参数 | ABR 字段 | 公式 | 真实 PS 参考 | 状态 |
| --- | --- | --- | --- | --- |
| `shapeScatter` + `plotJitter` | `scatterDynamics.jitter` | `clamp(0,450, scatter*200 + plotJitter*25)` | jitter 30-450（喷溅450/高级203/刮痕127/印象派122） | ✅ 已校准 |
| `shapeCount` | `Cnt ` | `clamp(1,5, 1+round(count*5))` | Cnt 1-5 | ✅ |
| 两轴 | `bothAxes` | 恒 true | 真实 bothAxes=true | ✅ |

## 5. 纹理 Texture（近似方案）

| Procreate 参数 | ABR 字段 | 公式 | 真实 PS 参考 | 状态 |
| --- | --- | --- | --- | --- |
| `grainDepth` | `textureDepth` | `UntF #Prc`，`×100`，下限 12（防空白） | 12/21/61/100 | ✅ |
| （无） | `minimumDepth` | `UntF #Prc`，0 | 0 | ✅ |
| `textureScale` | `textureScale` | `UntF #Prc`，`×100`，clamp 1-1000 | 1/24/80/90 | ✅ |
| `textureBrightness` | `textureBrightness` | `×100`，0 为中性（去掉旧 +30 下限） | **-150~+30**（Kyle 负亮度正常） | ✅ 2026-08-10 校准，待 PS 实测不空白 |
| `textureContrast` | `textureContrast` | `×100`，默认 -10，clamp -50~100 | -10~90 | ✅ 2026-08-10 校准 |
| `textureInverted` | `InvT` | 原值 | 墨水盒 InvT=true | ✅ |
| `Grain.png` | `Txtr`（Ptrn 引用）+ `patt` 块 | **近似方案**：Grain 缩 8×8 亮度特征 → 匹配 11 个内置真实 PS pattern 中最像的（Towel/Stone/…），按真实名字/ID 引用内嵌 | 真实 pattern 有专有尾部校验和，自定义像素会被 PS 拒收 | ⚠️ **近似：不是原 Grain 图** |
| `textureBlendMode` | `textureBlendMode` | 恒 `Hght`（Procreate 原模式未保留） | 真实 hght/sbtr 等多模式 | ⚠️ 固定 Hght |
| Offset Jitter（纹理随机化） | （无） | ABR 描述符无此字段 | 真实无 textureJitter | ❌ 无落点 |

## 6. 湿边 / 混合 Wet Edge / Blend

| Procreate 参数 | ABR 字段 | 公式 | 真实 PS 参考 | 状态 |
| --- | --- | --- | --- | --- |
| `wetEdgesAmount` | `Wtdg` + `toolOptions`（MixB） | Wtdg=true；MixB wet=90, dryness=100, mix=35 | wet 90-100, mix 7-35 | ✅ 映射 |
| `burntEdgesAmount` | `Wtdg` | ABR 只有布尔，烧边程度无连续字段 | — | ⚠️ 近似 |
| `blendMode` | `toolOptions` class | 湿边→MixB；静态透明→PbTl | PbTl/MixB/SmTl 三类 | ✅ 湿边/PbTl 已映射，SmTl 未用 |

## 7. 颜色动态 / 双色 Color Dynamics（2026-08-10 实现 + 校准）

| Procreate 参数 | ABR 字段 | 类型 | 公式 | 状态 |
| --- | --- | --- | --- | --- |
| 开关 | `useColorDynamics` | bool | 任一颜色参数非零则 true | ✅ |
| `dynamicsPressureSecondaryColor` | `clVr` | brVr | bVTy=2（压力），jitter=100 | ✅ 双色 |
| `dynamicsJitterHue` | `H   ` | **UntF #Prc** | `×100`（9→9%） | ✅ |
| `dynamicsJitterSaturation` | `Strt` | **UntF #Prc** | `×100` | ✅ |
| `dynamicsJitterLightness`/`Darkness` | `Brgh` | **UntF #Prc** | max(light,dark)×100 | ✅ |
| （无） | `purity` | UntF #Prc | 0 | ✅ 占位 |
| `dynamicsJitterStroke*`（每笔随机） | `colorDynamicsPerTip` | bool | true | ✅ |

> ⚠️ 关键教训：颜色动态的 H/Strt/Brgh/purity **是 `UntF #Prc`（double）不是 brVr，也不是 long**；
> clVr 才是 brVr。键名必须是 `clVr`/`H   `/`Strt`/`Brgh`/`purity`/`colorDynamicsPerTip`，
> 写错键名或类型（曾用 long）PS 只认开关、数值空白。2026-08-10 已按真实 1.ABR 字节修正为 UntF #Prc。
> 真实参考（全参数笔刷 1.ABR）：H=13 / Strt=18 / Brgh=9 / purity=0；clVr=bVTy=0、jitter=50（静态前景/背景抖动，
> 无压力控制）；textureBrightness=0 有效（long 类型）。

## 8. 未映射（ABR 无等价字段 / 待研究）

| Procreate 参数 | 原因 | 状态 |
| --- | --- | --- |
| `taper*` / `pencilTaper*` | ABR 无锥度字段，需烘焙进 alpha | ❌ 待靶子校准 |
| `dynamicsSpeedSize`/`Opacity` | ABR 描述符无速度动态字段 | ❌ 无落点 |
| `dualBlendMode` / Sub01 子笔刷 | PS dualBrush 为嵌套 brushPreset，语义不同 | ❌ 待逆向 |
| `dynamics*Curve` 非线性 | ABR 动态仅线性斜坡 | ⚠️ 近似 |
| Grain 原图作为纹理 | pattern 尾部专有校验和 | ❌ 需破解校验算法 |
| `grainBlendMode` 原模式 | 固定写 Hght | ⚠️ 近似 |

## 9. 保真评分（BrushFidelity.Evaluate，已 2026-08-10 重写）

- 满分 100；已映射能力不扣分。
- 扣分项：烧边程度 -3、锥度 -10、速度动态 -5、纹理近似 -3、grain 混合模式 -3、双笔刷 -3。
- 无 archive 参数按 62 分保守评估。
- 阈值：≥85 高保真，≥60 近似，否则受限。

## 10. 回归 / 验证

- `--compare-pack` 基线：src-brushset **132/132**（内置材质恢复铺纱网/铺纱网 1 后 130→132）。
- 往返自洽 ≠ 像不像：公式错 132/132 照样过。**"像不像"需用户同款 Procreate/PS 实画对比**（行为尺子 + PS 面板核对）。
- 真实 PS 结构验证：szVr/opVr/描述符顺序与真实 Kyle 笔刷逐字节一致。
- 待测：纹理亮度中性 0 是否空白；双色 clVr/H/Strt/Brgh 数值在 PS 面板是否正确显示。
