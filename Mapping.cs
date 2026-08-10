using System;
using System.Collections.Generic;
using System.Globalization;

namespace BrushStudio
{
    /// <summary>
    /// Procreate → ABR 参数映射公式集中配置。
    /// 校准相似度（"像不像"）时只需调整本类中的常量与方法，无需改动转换引擎代码。
    /// 每项注释标注真实 ABR 参考值（来源 FidelityTest/refs/abr-desc.json，PS 默认笔刷）。
    /// 注意：修改本类后请保持 --compare-pack 130/130 回归不倒退。
    /// </summary>
    internal static class Mapping
    {
        // ===== 间距 Spacing（ABR Spcn, #Prc）=====
        // 校准（2026-08-10，用户反馈"转换后糊成一堆、要手动调"）：
        //   旧公式 plotSpacing*100 在低端无兜底，G压暗包中位数 0.042 → 4% 间距 → 图章连续糊片。
        //   用户选择"整体调高"曲线：20 + plotSpacing*80（plotSpacing=0→20%, 0.5→60%, 1→100%, clamp 1-100）。
        //   效果：G压暗 1-4%→21-23%、酱鱼 39%→51%、蕾丝 40%→52%。
        // 缺失时默认 25。
        public const int SpacingDefault = 25;
        public const int SpacingMin = 1;
        public const int SpacingMax = 100;
        public const double SpacingCurveBase = 20.0;
        public const double SpacingCurvePer = 80.0;

        public static int SpacingFrom(double plotSpacing)
        {
            if (double.IsNaN(plotSpacing) || double.IsInfinity(plotSpacing))
            {
                return SpacingDefault;
            }
            return SpacingClamp((int)Math.Round(SpacingCurveBase + plotSpacing * SpacingCurvePer, MidpointRounding.AwayFromZero));
        }

        public static int SpacingClamp(int value)
        {
            return Math.Max(SpacingMin, Math.Min(SpacingMax, value));
        }

        // ===== 角度 Angle（ABR Angl, #Ang）=====
        // Procreate shapeAngle 是弧度；ABR Angl 是度。rad * 180 / PI，归一到 0-360。
        public static double AngleFrom(double radians)
        {
            double degrees = radians * 180.0 / Math.PI;
            degrees = degrees % 360.0;
            if (degrees < 0)
            {
                degrees += 360.0;
            }
            return degrees;
        }

        // ===== 圆度 Roundness（ABR Rndn, #Prc）=====
        // 公式: round(shapeRoundness * 100)，1 表示正圆（=100%）；<=0 视为正圆。
        public const double RoundnessDefault = 100;
        public const double RoundnessMin = 1;
        public const double RoundnessMax = 100;

        public static double RoundnessFrom(double shapeRoundness)
        {
            if (shapeRoundness <= 0)
            {
                return RoundnessDefault;
            }
            return Math.Max(RoundnessMin, Math.Min(RoundnessMax, shapeRoundness * 100.0));
        }

        // ===== 直径 Diameter（ABR Dmtr, #Pxl）=====
        // 笔尖较长边，上限 2500px。
        public const double DiameterMax = 2500;
        public const double DiameterDefault = 100;

        public static double DiameterFrom(double width, double height)
        {
            double size = Math.Max(width, height);
            return Math.Max(1, Math.Min(DiameterMax, size));
        }

        // ===== 0-1 值 → 0-100 百分比 =====
        // <=0 → 0；>=1 → 100；否则 round(x*100)。
        public static int PercentFrom01(double value)
        {
            if (value <= 0)
            {
                return 0;
            }
            if (value >= 1)
            {
                return 100;
            }
            return (int)Math.Round(value * 100.0);
        }

        // ===== 倾斜 Tilt =====
        // 样本中 dynamicsTiltAngle 多数恰为 0.1，视为无倾斜（阈值 0.12）。
        // (tilt - 0.1) / 0.4 → 0-1，再 ×100。
        public const double TiltZeroThreshold = 0.12;
        public const double TiltNormalizeMin = 0.1;
        public const double TiltNormalizeRange = 0.4;

        public static int TiltPercent(double tilt)
        {
            if (tilt <= TiltZeroThreshold)
            {
                return 0;
            }
            double percent = (tilt - TiltNormalizeMin) / TiltNormalizeRange;
            if (percent <= 0)
            {
                return 0;
            }
            if (percent >= 1)
            {
                return 100;
            }
            return (int)Math.Round(percent * 100.0);
        }

        // ===== 动态步数 fStp / 默认 jitter =====
        // 真实 ABR brVr 的 fStp 通常为 25；jitter 范围参考：szVr 0-70、angleDynamics 0-100。
        public const int DynamicsSteps = 25;
        public const int DynamicsNone = 0;

        // ===== 尺寸动态 Size dynamics（szVr）=====
        // 真实 ABR: pressure 用 bVTy=2，tilt 用 bVTy=3；jitter 0-70。
        public const int SizeControlPressure = 2;
        public const int SizeControlTilt = 3;
        public const int SizeControlNone = 0;

        // sizeMinimum = max(minSizePercent, (1 - pressureSize) * 100)；仅 pressureSize>0 时生效。
        public static int SizeMinimum(int minSizePercent, double pressureSize)
        {
            if (pressureSize <= 0)
            {
                return minSizePercent;
            }
            int pressureRange = (int)Math.Round(Math.Max(0, 1.0 - pressureSize) * 100.0);
            return Math.Max(minSizePercent, pressureRange);
        }

        // ===== 透明度动态 Opacity dynamics（opVr）=====
        // 真实 ABR: pressure 用 bVTy=2；Mnm 20/39/49/78；jitter 0/30/50。
        public const int OpacityControlPressure = 2;
        public const int OpacityControlNone = 0;

        // opacityMinimum = max(minOpacityPercent, (1 - pressureOpacity) * 100)；仅 pressureOpacity>0 时生效。
        public static int OpacityMinimum(int minOpacityPercent, double pressureOpacity)
        {
            if (pressureOpacity <= 0)
            {
                return minOpacityPercent;
            }
            int opacityRange = (int)Math.Round(Math.Max(0, 1.0 - pressureOpacity) * 100.0);
            return Math.Max(minOpacityPercent, opacityRange);
        }

        // ===== 圆度动态 Roundness dynamics（roundnessDynamics）=====
        // 真实 ABR: bVTy 0/2；jitter 4-39。
        public const int RoundnessControlEnable = 2;
        public const int RoundnessMinimumDefault = 50;

        public static int RoundnessMinimum(double pressureRoundnessMin)
        {
            return (int)Math.Round(pressureRoundnessMin * 100.0);
        }

        // ===== 角度动态 angleDynamics =====
        // 真实 ABR: bVTy 0/3/5/6；jitter 0-100。校准（2026-08-10 真实 PS 样本）：
        //   bVTy=3 倾斜（Kyle 上墨、概念画笔）、bVTy=6 初始方向（印象派、真实油画、油画 Flex）。
        //   Procreate shapeOrientation>=2 且无倾斜 → 方向跟随型 → bVTy=6。
        public const int AngleDynamicsTiltControl = 3;
        public const int AngleDynamicsDirectionControl = 6;
        public const int AngleDynamicsNone = 0;

        // ===== 倾斜缩放 tiltScale =====
        // clamp(60, 200, 60 + tiltPercent * 1.4)
        public const int TiltScaleBase = 60;
        public const int TiltScaleMax = 200;
        public const double TiltScalePerPercent = 1.4;

        public static int TiltScale(int tiltPercent)
        {
            return Math.Max(TiltScaleBase, Math.Min(TiltScaleMax, (int)Math.Round(TiltScaleBase + tiltPercent * TiltScalePerPercent)));
        }

        // ===== 散射 Scatter（scatterDynamics + Cnt）=====
        // 真实 ABR 靶心（FidelityTest/refs/abr-desc.json，PS 默认笔刷）：
        //   scatterDynamics.jitter = 30 / 122 / 208（观测值），范围上限 450；countDynamics jitter = 0/15/39/100；Cnt = 1/3/5。
        // 校准（2026-08-10，行为尺子实测）：旧公式 scatter*250+plotJitter*35 把代表散射笔刷推到
        //   452-501px，超出真实上限；新公式按真实 jitter 30-450 重标定：
        //   编织勾线(scatter=2.0)→400、散落系列(1.58+1.648)→357、🌈散(plotJitter=8.44)→211、
        //   斜方2(0.799)→160、Snow Gum(0.25)→50、微散射(0.013)→3。
        // shapeScatter 与 plotJitter 是两条独立散射轴，Procreate 不同包用不同参数表达同一效果，
        // 两条都计入幅度（系数比 ~8:1，与 Procreate 原始量纲近似）。
        public const double ScatterJitterScatterCoef = 200.0;
        public const double ScatterJitterPlotCoef = 25.0;
        public const double ScatterJitterMax = 450.0;
        public const double ScatterCountBase = 1.0;
        public const double ScatterCountPer = 5.0;
        public const double ScatterCountMax = 5.0;

        // scatterJitter = clamp(0, 450, scatter*200 + plotJitter*25)
        public static int ScatterJitter(double scatter, double plotJitter)
        {
            return (int)Math.Round(Math.Max(0, Math.Min(ScatterJitterMax, scatter * ScatterJitterScatterCoef + plotJitter * ScatterJitterPlotCoef)));
        }

        // scatterCount = clamp(1, 5, 1 + round(shapeCount*5))
        // shapeCount 分布 0.0625~0.8718 → Cnt 1~5，与真实 ABR Cnt=1/3/5 同量纲（同包内 count 是"每站盖章数"）。
        public static double ScatterCount(double shapeCount)
        {
            return Math.Max(ScatterCountBase, Math.Min(ScatterCountMax, ScatterCountBase + Math.Round(shapeCount * ScatterCountPer)));
        }

        // ===== 湿边 Wet Edge（MixB toolOptions）=====
        // 真实 ABR: flow 38/55/56/90/100；MixB wetness 90/100、mix 35/7/8。
        public const double WetFlow = 100;
        public const double WetWetness = 90;
        public const double WetDryness = 100;
        public const double WetMix = 35;
        public const double MixBOpacity = 100;

        // ===== 静态不透明度（paintOpacity → ABR toolOptions flow，PbTl）=====
        // 真实 ABR: Kyle Inkbox flow=56 用 flow 表达静态半透明；Opct 恒 100 不进 ABR。
        public const int FlowMin = 1;
        public const int FlowMax = 100;

        // paintOpacity 0-1 → 1-100；<=0 返回 100（无需静态流量，不写 flow）。
        public static int FlowFrom(double paintOpacity)
        {
            if (paintOpacity <= 0)
            {
                return FlowMax;
            }
            return Math.Max(FlowMin, Math.Min(FlowMax, (int)Math.Round(paintOpacity * 100.0)));
        }

        // ===== 纹理 Texture =====
        // 机制已验证可行（2026-08-10）：useTexture + Txtr(Ptrn 引用) + patt 块内嵌，PS 2026 能导入显示纹理。
        // 格式基准：真实 PS Towel 条目 textureScale #Prc=100、textureDepth=12、textureBrightness=30、textureContrast=-10。
        // 校准（2026-08-10 用户实测）：textureBrightness=0 + textureDepth≈0 会让 PS 在 Hght 混合下画笔刷全空白！
        //   → 三个参数都必须有安全下限（brightness≥30、depth≥12、contrast 默认 -10），否则笔刷变透明。
        // Procreate 值域：textureScale 0~15.56（倍率，1=基准）、grainDepth 0~1、textureBrightness/Contrast 多 0。
        // 以下映射为初值，待 PS 实画校准（相似度循环）。

        public const int TextureScaleMin = 1;
        public const int TextureScaleMax = 1000;

        // textureScale 倍率 → ABR #Prc：×100，clamp 1-1000；<=0（未设置）用 100 基准。
        public static int TextureScale(double textureScale)
        {
            if (double.IsNaN(textureScale) || double.IsInfinity(textureScale) || textureScale <= 0)
            {
                return 100;
            }
            return Math.Max(TextureScaleMin, Math.Min(TextureScaleMax, (int)Math.Round(textureScale * 100.0)));
        }

        // grainDepth 0-1 → ABR textureDepth；下限 12（低于此 PS 在 Hght 下会空白）。
        public static int TextureDepth(double grainDepth)
        {
            if (double.IsNaN(grainDepth) || grainDepth <= 0)
            {
                return 12;
            }
            return Math.Max(12, Math.Min(100, (int)Math.Round(grainDepth * 100.0)));
        }

        // textureBrightness 0-1 → ABR textureBrightness；0 为中性。
        // 校准（2026-08-10 真实 PS 样本）：Towel=+30 亮、Kyle 真实油画=-33、油画Flex=-36、墨水盒=-150，
        //   负亮度在 hght 混合下只要 depth 足够（>=12）就安全。去掉旧的 +30 强制下限（那是低 depth 才需要的 hack）。
        public static int TextureBrightness(double brightness)
        {
            if (double.IsNaN(brightness) || brightness <= 0)
            {
                return 0;
            }
            return Math.Max(-100, Math.Min(100, (int)Math.Round(brightness * 100.0)));
        }

        // textureContrast 0-1 → ABR textureContrast；默认 -10（真实 KYLE 终极上墨的实测值）。
        // 校准（2026-08-10）：真实 PS 范围 -10~90（上墨=-10、油画=57、墨水盒=90），放宽上限。
        public static int TextureContrast(double contrast)
        {
            if (double.IsNaN(contrast) || contrast <= 0)
            {
                return -10;
            }
            return Math.Max(-50, Math.Min(100, (int)Math.Round(contrast * 100.0)));
        }
    }
}
