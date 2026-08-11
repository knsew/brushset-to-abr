using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BrushStudio
{
    internal sealed class SourceFile
    {
        public string Path;
        public string DisplayName;
        public string Meta;
        public List<ExtractedBrush> ExtractedBrushes;
        public Bitmap Thumb;

        public static string HumanSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }
            double kb = bytes / 1024.0;
            if (kb < 1024)
            {
                return kb.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            }
            double mb = kb / 1024.0;
            return mb.ToString("0.##", CultureInfo.InvariantCulture) + " MB";
        }
    }
    internal sealed class ExtractedBrush
    {
        public string BrushsetPath;
        public string SourceId;
        public string Name;
        public string ShapePath;
        public string GrainPath;
        public List<string> ExtraTextures = new List<string>();
        public Dictionary<string, object> Params;
        public int SpacingPercent = 25;

        public int? ManualSpacing;
        public double? ManualAngle;
        public double? ManualRoundness;
        public double? ManualDiameter;
        // 笔刷库覆盖：改名 / 单笔刷反转 / 收藏（持久化在 BrushLibrary）
        public string ManualName;
        public bool? ManualInvert;
        public bool Favorite;

        public string EffectiveName
        {
            get
            {
                return string.IsNullOrEmpty(ManualName) ? Name : ManualName;
            }
        }

        public bool HasManualAdjust
        {
            get
            {
                return ManualSpacing.HasValue || ManualAngle.HasValue || ManualRoundness.HasValue || ManualDiameter.HasValue;
            }
        }

        public int EffectiveSpacingPercent
        {
            get
            {
                return Mapping.SpacingClamp(ManualSpacing ?? SpacingPercent);
            }
        }

        public double EffectiveAngleDegrees()
        {
            double angle = ManualAngle.HasValue ? ManualAngle.Value : ParamMapping.AngleDegrees(Params);
            angle = angle % 360.0;
            if (angle < 0)
            {
                angle += 360.0;
            }
            return angle;
        }

        public double EffectiveRoundnessPercent()
        {
            return Math.Max(1, Math.Min(100, ManualRoundness.HasValue ? ManualRoundness.Value : ParamMapping.RoundnessPercent(Params)));
        }

        public double EffectiveDiameter()
        {
            return ManualDiameter.HasValue ? Math.Max(1, ManualDiameter.Value) : 0;
        }
    }
    internal enum FidelityLevel
    {
        High,
        Medium,
        Low
    }
    internal sealed class FidelityReport
    {
        public int Score;
        public FidelityLevel Level;
        public List<string> Notes = new List<string>();
    }

    // 第一层行为模型：按 MAPPING.md 的可映射能力评分，后续升级为完整 BrushBehaviorModel。
    internal sealed class AbrBrush
    {
        public string Name;
        public int Width;
        public int Height;
        public byte[] Alpha;
        public int Spacing = 25;
        public string Uuid;
        public double Angle;
        public double Roundness = 100;
        public double Diameter;
        public Dictionary<string, object> Params;
        public AbrReader.AbrDescriptor Descriptor;
        // 纹理（Grain.png）路径：非空时该笔刷需内嵌 pattern 并写 useTexture/Txtr。
        public string GrainPath;
        // 匹配到的真实 pattern 名/ID（近似纹理方案）；非空时该笔刷带纹理。
        public string PatternName;
        public string PatternId;
        // 内嵌 pattern 条目（VMAL 格式原图纹理）；非空时写入 patt 块并引用。
        public byte[] PatternEntry;
    }
    internal sealed class AbrInput
    {
        public string ImagePath;
        public string DisplayName;
        public string Uuid;
        public int Spacing = 25;
        public double Angle;
        public double Roundness = 100;
        public double Diameter;
        public Dictionary<string, object> Params;
        // 纹理（Grain.png）：仅当笔刷同时有 Shape 与 Grain 时非空，作为内嵌 pattern 的源图。
        public string GrainPath;
        // 单笔刷反转 alpha（null = 跟随全局）。
        public bool? Invert;
    }
}
