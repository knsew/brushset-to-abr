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
    internal static class AppSettings
    {
        private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrushsetToABR");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.ini");

        public static string SettingsFilePath
        {
            get
            {
                return FilePath;
            }
        }

        public static string LastOutput = "";
        public static string LastFolder = "";
        public static int Spacing = 25;
        public static int VersionIndex;
        public static bool Invert;
        public static bool Autocrop;
        public static bool UseRle = true;
        public static bool Pure;   // 纯笔刷模式：只导图案/间距/大小，不映射其他参数
        public static bool AutoOpen = true;
        public static bool ShowGuide = true;
        public static int WindowX = -1;
        public static int WindowY = -1;
        public static int WindowWidth = -1;
        public static int WindowHeight = -1;
        public static bool WindowMaximized;

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = raw.Substring(0, eq).Trim();
                    string value = raw.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "LastOutput": LastOutput = value; break;
                        case "LastFolder": LastFolder = value; break;
                        case "Spacing": Spacing = ParseInt(value, 25); break;
                        case "VersionIndex": VersionIndex = ParseInt(value, 0); break;
                        case "Invert": Invert = ParseBool(value); break;
                        case "Autocrop": Autocrop = ParseBool(value, true); break;
                        case "UseRle": UseRle = ParseBool(value, true); break;
                        case "Pure": Pure = ParseBool(value); break;
                        case "AutoOpen": AutoOpen = ParseBool(value, true); break;
                        case "ShowGuide": ShowGuide = ParseBool(value, true); break;
                        case "WindowX": WindowX = ParseInt(value, -1); break;
                        case "WindowY": WindowY = ParseInt(value, -1); break;
                        case "WindowWidth": WindowWidth = ParseInt(value, -1); break;
                        case "WindowHeight": WindowHeight = ParseInt(value, -1); break;
                        case "WindowMaximized": WindowMaximized = ParseBool(value); break;
                    }
                }
            }
            catch
            {
            }
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                {
                    Directory.CreateDirectory(DirectoryPath);
                }
                StringBuilder builder = new StringBuilder();
                Append(builder, "LastOutput", LastOutput);
                Append(builder, "LastFolder", LastFolder);
                Append(builder, "Spacing", Spacing.ToString(CultureInfo.InvariantCulture));
                Append(builder, "VersionIndex", VersionIndex.ToString(CultureInfo.InvariantCulture));
                Append(builder, "Invert", Invert ? "1" : "0");
                Append(builder, "Autocrop", Autocrop ? "1" : "0");
                Append(builder, "UseRle", UseRle ? "1" : "0");
                Append(builder, "Pure", Pure ? "1" : "0");
                Append(builder, "AutoOpen", AutoOpen ? "1" : "0");
                Append(builder, "ShowGuide", ShowGuide ? "1" : "0");
                Append(builder, "WindowX", WindowX.ToString(CultureInfo.InvariantCulture));
                Append(builder, "WindowY", WindowY.ToString(CultureInfo.InvariantCulture));
                Append(builder, "WindowWidth", WindowWidth.ToString(CultureInfo.InvariantCulture));
                Append(builder, "WindowHeight", WindowHeight.ToString(CultureInfo.InvariantCulture));
                Append(builder, "WindowMaximized", WindowMaximized ? "1" : "0");
                File.WriteAllText(FilePath, builder.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').Append(value.Replace("\r", "").Replace("\n", "")).AppendLine();
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string value, bool fallback = false)
        {
            if (value == "1" || value == "true")
            {
                return true;
            }
            if (value == "0" || value == "false")
            {
                return false;
            }
            return fallback;
        }
    }
    internal static class AppLog
    {
        private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrushsetToABR");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "error.log");

        public static string LogFilePath
        {
            get
            {
                return FilePath;
            }
        }

        public static void Write(string message)
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                {
                    Directory.CreateDirectory(DirectoryPath);
                }
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine, Encoding.UTF8);
                FileInfo info = new FileInfo(FilePath);
                if (info.Length > 512 * 1024)
                {
                    string trimmed = FilePath + ".1";
                    File.Copy(FilePath, trimmed, true);
                    File.WriteAllText(FilePath, "", Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
    internal static class BrushFidelity
    {
        public static FidelityReport Evaluate(ExtractedBrush brush)
        {
            FidelityReport report = new FidelityReport { Score = 100, Level = FidelityLevel.High };
            if (brush == null || brush.Params == null || brush.Params.Count == 0)
            {
                report.Score = 62;
                report.Level = FidelityLevel.Medium;
                report.Notes.Add("没有解析到 archive 参数，按可烘焙笔尖保守评估");
                return report;
            }

            Dictionary<string, object> p = brush.Params;
            bool hasDirect = HasKey(p, "plotSpacing") ||
                             HasKey(p, "shapeRoundness") ||
                             HasKey(p, "shapeAngle") ||
                             HasKey(p, "shapeFlipXJitter") ||
                             HasKey(p, "shapeFlipYJitter");
            if (!hasDirect)
            {
                report.Score -= 4;
                report.Notes.Add("缺少直接映射参数（间距/圆度/角度/翻转）");
            }

            // 湿边已映射（Wtdg 布尔 + MixB 湿度/混合）；烧边程度仍无连续字段
            double wet = Num(p, "wetEdgesAmount");
            double burnt = Num(p, "burntEdgesAmount");
            if (wet > 0)
            {
                report.Notes.Add("湿边已映射：Wtdg 布尔 + MixB（湿度/混合）");
            }
            if (burnt > 0)
            {
                report.Score -= 3;
                report.Notes.Add("烧边程度无连续字段，ABR 仅布尔 Wtdg 近似表达");
            }

            double taper = Math.Max(
                Math.Max(Math.Max(Num(p, "taperSize"), Num(p, "taperOpacity")), Num(p, "taperPressure")),
                Math.Max(Math.Max(Num(p, "pencilTaperSize"), Num(p, "pencilTaperOpacity")),
                         Math.Max(Num(p, "pencilTaperStartLength"), Num(p, "pencilTaperEndLength"))));
            if (taper > 0)
            {
                report.Score -= 10;
                report.Notes.Add("锥度需压力笔尖预烘焙，尚未实现");
            }

            // 压力/倾斜动态已写入 szVr/opVr（bVTy=2/3）；速度动态 ABR 描述符无字段
            bool hasPressureDyn = Num(p, "dynamicsPressureSize") > 0 ||
                                  Num(p, "dynamicsPressureOpacity") > 0 ||
                                  Num(p, "dynamicsTiltSize") > 0;
            if (hasPressureDyn)
            {
                report.Notes.Add("压力/倾斜动态已映射（szVr/opVr bVTy）");
            }
            bool hasSpeed = Num(p, "dynamicsSpeedSize") != 0 || Num(p, "dynamicsSpeedOpacity") != 0;
            if (hasSpeed)
            {
                report.Score -= 5;
                report.Notes.Add("速度动态在 ABR 描述符中无字段，暂未映射");
            }

            // 纹理：用内置真实 pattern 近似匹配，非原始 grain 像素
            bool hasTexture = HasKey(p, "textureScale") ||
                              HasKey(p, "textureContrast") ||
                              HasKey(p, "textureBrightness") ||
                              HasKey(p, "textureRotation") ||
                              HasKey(p, "grainDepth") ||
                              HasKey(p, "bundledGrainPath") ||
                              HasKey(p, "textureApplication") ||
                              HasKey(p, "textureMovement");
            if (hasTexture)
            {
                report.Score -= 3;
                report.Notes.Add("纹理用内置真实 pattern 近似匹配，非原始 grain 像素");
            }

            object blendValue;
            if (p.TryGetValue("grainBlendMode", out blendValue))
            {
                report.Score -= 3;
                report.Notes.Add("grain 混合模式固定写 Hght（Procreate 原模式未保留）");
            }

            // 颜色动态（双色）已映射：hue/sat/brt jitter + colorJitter；双笔刷未支持
            bool hasColor = Num(p, "dynamicsJitterHue") > 0 ||
                            Num(p, "dynamicsJitterSaturation") > 0 ||
                            Num(p, "dynamicsJitterLightness") > 0 ||
                            Num(p, "dynamicsPressureSecondaryColor") > 0;
            if (hasColor)
            {
                report.Notes.Add("颜色动态（双色）已映射：hue/sat/brt jitter + colorJitter");
            }
            bool hasDual = HasKey(p, "dualBlendMode") || HasKey(p, "blendMode");
            if (hasDual)
            {
                report.Score -= 3;
                report.Notes.Add("双笔刷尚未支持（ABR dualBrush 需专门逆向）");
            }

            if (report.Score < 5)
            {
                report.Score = 5;
            }
            if (report.Score >= 85)
            {
                report.Level = FidelityLevel.High;
            }
            else if (report.Score >= 60)
            {
                report.Level = FidelityLevel.Medium;
            }
            else
            {
                report.Level = FidelityLevel.Low;
            }
            return report;
        }

        public static string ShortLabel(FidelityLevel level)
        {
            if (level == FidelityLevel.High)
            {
                return "高";
            }
            return level == FidelityLevel.Medium ? "近" : "限";
        }

        public static string LevelLabel(FidelityLevel level)
        {
            if (level == FidelityLevel.High)
            {
                return "高保真";
            }
            return level == FidelityLevel.Medium ? "近似" : "受限";
        }

        public static List<string> DescribeParams(ExtractedBrush brush)
        {
            List<string> lines = new List<string>();
            if (brush == null)
            {
                return lines;
            }
            lines.Add("间距  " + brush.EffectiveSpacingPercent + "%" + (brush.ManualSpacing.HasValue ? "（手动）" : ""));
            if (brush.Params == null || brush.Params.Count == 0)
            {
                lines.Add("无 archive 参数");
                return lines;
            }

            Dictionary<string, object> p = brush.Params;
            double size = brush.ManualDiameter.HasValue
                ? brush.ManualDiameter.Value
                : Math.Max(Num(p, "maxSize"), Math.Max(Num(p, "minSize"), Num(p, "paintSize")));
            if (size > 0)
            {
                lines.Add("尺寸  " + size.ToString("0.#", CultureInfo.InvariantCulture) + " px" + (brush.ManualDiameter.HasValue ? "（手动）" : ""));
            }
            double wet = Num(p, "wetEdgesAmount");
            double burnt = Num(p, "burntEdgesAmount");
            if (wet > 0 || burnt > 0)
            {
                lines.Add("湿边/烧边  " + Math.Max(wet, burnt).ToString("0.##", CultureInfo.InvariantCulture));
            }
            double taper = Math.Max(Math.Max(Num(p, "taperSize"), Num(p, "taperOpacity")), Num(p, "taperPressure"));
            if (taper > 0)
            {
                lines.Add("锥度  " + taper.ToString("0.##", CultureInfo.InvariantCulture));
            }
            double roundness = brush.EffectiveRoundnessPercent();
            if (brush.ManualRoundness.HasValue || HasKey(p, "shapeRoundness"))
            {
                lines.Add("圆度  " + roundness.ToString("0.##", CultureInfo.InvariantCulture) + "%" + (brush.ManualRoundness.HasValue ? "（手动）" : ""));
            }
            if (HasKey(p, "textureScale") || HasKey(p, "grainDepth") || HasKey(p, "bundledGrainPath"))
            {
                lines.Add("纹理  grain");
            }
            double paintOpacity = Num(p, "paintOpacity");
            double minOpacity = Num(p, "minOpacity");
            if (paintOpacity > 0)
            {
                string minText = minOpacity > 0 ? "（最小 " + (minOpacity * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%）" : "";
                lines.Add("透明度  " + (paintOpacity * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%" + minText);
            }
            else if (minOpacity > 0)
            {
                lines.Add("透明度  最小 " + (minOpacity * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%");
            }
            double scatter = Num(p, "shapeScatter");
            double plotJitter = Num(p, "plotJitter");
            double plotJitterLong = Num(p, "plotJitterLongitudinal");
            if (scatter > 0 || plotJitter > 0 || plotJitterLong > 0)
            {
                lines.Add("散射  " + Mapping.ScatterJitter(plotJitter, plotJitterLong).ToString("0.#", CultureInfo.InvariantCulture) + "% / 角度抖动 " + Mapping.AngleJitter(scatter).ToString("0.#", CultureInfo.InvariantCulture) + "%");
            }
            double shapeCount = Num(p, "shapeCount");
            if (shapeCount > 0)
            {
                lines.Add("数量  ×" + Mapping.Count(shapeCount).ToString("0.#", CultureInfo.InvariantCulture));
            }
            double pressureSize = Num(p, "dynamicsPressureSize");
            double pressureOpacity = Num(p, "dynamicsPressureOpacity");
            if (pressureSize > 0 || pressureOpacity > 0)
            {
                lines.Add("压力  尺寸 " + (pressureSize * 100).ToString("0.#", CultureInfo.InvariantCulture)
                          + "% / 透明度 " + (pressureOpacity * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%");
            }
            if (lines.Count < 2)
            {
                lines.Add("基础形状笔刷");
            }
            return lines;
        }

        public static string Summary(List<ExtractedBrush> brushes)
        {
            int high = 0;
            int medium = 0;
            int low = 0;
            if (brushes != null)
            {
                foreach (ExtractedBrush brush in brushes)
                {
                    FidelityLevel level = Evaluate(brush).Level;
                    if (level == FidelityLevel.High)
                    {
                        high++;
                    }
                    else if (level == FidelityLevel.Medium)
                    {
                        medium++;
                    }
                    else
                    {
                        low++;
                    }
                }
            }
            return "高保真 " + high + " · 近似 " + medium + " · 受限 " + low;
        }

        private static double Num(Dictionary<string, object> dict, string key)
        {
            object value;
            if (dict == null || !dict.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static bool HasKey(Dictionary<string, object> dict, string key)
        {
            return dict != null && dict.ContainsKey(key);
        }
    }
    internal static class ParamMapping
    {
        public static double Num(Dictionary<string, object> dict, string key)
        {
            object value;
            if (dict == null || !dict.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        public static bool HasKey(Dictionary<string, object> dict, string key)
        {
            return dict != null && dict.ContainsKey(key);
        }

        public static bool Bool(Dictionary<string, object> dict, string key)
        {
            object value;
            if (dict == null || !dict.TryGetValue(key, out value) || value == null)
            {
                return false;
            }
            try
            {
                if (value is bool)
                {
                    return (bool)value;
                }
                return Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>取字符串参数；"$null" / null / 非字符串都返回 null。</summary>
        public static string Str(Dictionary<string, object> dict, string key)
        {
            object value;
            if (dict == null || !dict.TryGetValue(key, out value) || value == null)
            {
                return null;
            }
            string s = value as string;
            if (s == null)
            {
                return null;
            }
            s = s.Trim();
            if (s.Length == 0 || s == "$null")
            {
                return null;
            }
            return s;
        }

        public static double AngleDegrees(Dictionary<string, object> p)
        {
            return Mapping.AngleFrom(Num(p, "shapeAngle"));
        }

        public static double RoundnessPercent(Dictionary<string, object> p)
        {
            return Mapping.RoundnessFrom(Num(p, "shapeRoundness"));
        }

        public static double DefaultDiameter(AbrBrush brush)
        {
            if (brush == null)
            {
                return Mapping.DiameterDefault;
            }
            return Mapping.DiameterFrom(brush.Width, brush.Height);
        }
    }
    internal static class Cli
    {
        public static int MakeAbr(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("用法: BrushStudio.exe --cli 输出.abr 图片1 [图片2 ...] [--spacing 25] [--version 2] [--invert|--invert-alpha] [--no-crop] [--raw] [--pure]");
                return 2;
            }
            string output = args[1];
            List<string> inputs = new List<string>();
            int version = 2;
            int spacing = 25;
            bool raw = false;
            bool invert = false;
            bool noCrop = false;
            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--version" && i + 1 < args.Length)
                {
                    version = Convert.ToInt32(args[++i]);
                }
                else if (arg == "--spacing" && i + 1 < args.Length)
                {
                    spacing = Convert.ToInt32(args[++i]);
                }
                else if (arg == "--raw")
                {
                    raw = true;
                }
                else if (arg == "--invert" || arg == "--invert-alpha")
                {
                    invert = true;
                }
                else if (arg == "--no-crop")
                {
                    noCrop = true;
                }
                else if (arg == "--pure")
                {
                    AppSettings.Pure = true;
                }
                else
                {
                    inputs.Add(arg);
                }
            }
            if (inputs.Count == 0)
            {
                return 2;
            }
            int count = AbrWriter.ConvertImagesToAbr(output, inputs, version, !raw, spacing, invert, !noCrop);
            Console.WriteLine("已生成 " + count + " 支笔刷: " + output);
            return 0;
        }

        public static int ConvertBrushset(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("用法: BrushStudio.exe --convert-brushset 输出.abr 文件.brushset [--version 7] [--spacing 25] [--invert|--invert-alpha] [--no-crop] [--pure]");
                return 2;
            }
            string output = args[1];
            List<string> inputs = new List<string>();
            int version = 7;
            int? spacing = null;
            bool invert = false;
            bool noCrop = false;
            int? maxTip = null;
            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--version" && i + 1 < args.Length)
                {
                    version = Convert.ToInt32(args[++i]);
                }
                else if (arg == "--spacing" && i + 1 < args.Length)
                {
                    spacing = Convert.ToInt32(args[++i]);
                }
                else if (arg == "--max-tip" && i + 1 < args.Length)
                {
                    maxTip = Convert.ToInt32(args[++i]);
                }
                else if (arg == "--invert" || arg == "--invert-alpha")
                {
                    invert = true;
                }
                else if (arg == "--no-crop")
                {
                    noCrop = true;
                }
                else if (arg == "--pure")
                {
                    AppSettings.Pure = true;
                }
                else
                {
                    inputs.Add(arg);
                }
            }
            if (inputs.Count == 0)
            {
                Console.Error.WriteLine("没有指定 .brushset 文件。");
                return 2;
            }
            int brushCount;
            int generated = BrushsetConverter.ConvertToAbr(output, inputs, version, true, invert, !noCrop, out brushCount, null, spacing, null, maxTip);
            Console.WriteLine("已生成 " + generated + " 支 ABR 画笔: " + output);
            Console.WriteLine("brushCount=" + brushCount);
            return 0;
        }

        public static int Extract(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("用法: BrushStudio.exe --cli-extract 输出目录 文件.brushset|文件.abr [--invert|--invert-alpha] [--no-crop] [--params] [--dump-descriptor]");
                return 2;
            }
            string outputDir = args[1];
            string input = args[2];
            bool invert = false;
            bool noCrop = false;
            bool paramsJson = false;
            bool dumpDescriptor = false;
            for (int i = 3; i < args.Length; i++)
            {
                if (args[i] == "--invert" || args[i] == "--invert-alpha")
                {
                    invert = true;
                }
                else if (args[i] == "--no-crop")
                {
                    noCrop = true;
                }
                else if (args[i] == "--params")
                {
                    paramsJson = true;
                }
                else if (args[i] == "--dump-descriptor")
                {
                    dumpDescriptor = true;
                }
            }
            Directory.CreateDirectory(outputDir);
            string ext = Path.GetExtension(input).ToLowerInvariant();
            if (dumpDescriptor && ext != ".abr")
            {
                Console.Error.WriteLine("--dump-descriptor 仅支持 .abr 文件。");
                return 2;
            }
            if (ext == ".brushset")
            {
                List<ExtractedBrush> brushes = BrushsetExtractor.Extract(input, outputDir, paramsJson);
                Console.WriteLine("已提取 " + brushes.Count + " 支 Procreate 笔刷: " + outputDir);
                return 0;
            }
            if (ext == ".abr")
            {
                int count = AbrReader.ExtractAbr(input, outputDir, invert, !noCrop, paramsJson);
                if (dumpDescriptor)
                {
                    AbrReader.DumpDescriptors(input, Path.Combine(outputDir, "abr-descriptor.json"));
                }
                Console.WriteLine("已提取 " + count + " 支 Photoshop 笔刷: " + outputDir);
                return 0;
            }
            Console.Error.WriteLine("不支持的文件: " + input);
            return 2;
        }

        public static int MakeComparePack(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("用法: BrushStudio.exe --compare-pack 输出目录 文件.brushset [--version 7] [--invert|--invert-alpha] [--no-crop] [--no-rle] [--force]");
                return 2;
            }
            string outputDir = args[1];
            string input = args[2];
            int version = 7;
            bool invert = false;
            bool noCrop = false;
            bool useRle = true;
            bool force = false;
            for (int i = 3; i < args.Length; i++)
            {
                if (args[i] == "--version" && i + 1 < args.Length)
                {
                    version = Convert.ToInt32(args[++i]);
                }
                else if (args[i] == "--invert" || args[i] == "--invert-alpha")
                {
                    invert = true;
                }
                else if (args[i] == "--no-crop")
                {
                    noCrop = true;
                }
                else if (args[i] == "--no-rle")
                {
                    useRle = false;
                }
                else if (args[i] == "--force")
                {
                    force = true;
                }
                else
                {
                    Console.Error.WriteLine("未知参数: " + args[i]);
                    return 2;
                }
            }
            if (!File.Exists(input))
            {
                Console.Error.WriteLine("找不到输入文件: " + input);
                return 2;
            }
            if (!string.Equals(Path.GetExtension(input), ".brushset", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("对照包只支持 .brushset 输入: " + input);
                return 2;
            }

            string root = Path.GetFullPath(outputDir);
            Directory.CreateDirectory(root);
            string sourceDir = Path.Combine(root, "source");
            string abrDir = Path.Combine(root, "abr-extracted");
            string abrPath = Path.Combine(root, "compare.abr");
            string checklistPath = Path.Combine(root, "compare-checklist.md");
            string summaryPath = Path.Combine(root, "compare-summary.json");
            string[] generated =
            {
                sourceDir,
                abrDir,
                abrPath,
                checklistPath,
                summaryPath
            };
            bool hasExisting = false;
            foreach (string path in generated)
            {
                if (Directory.Exists(path) || File.Exists(path))
                {
                    hasExisting = true;
                    break;
                }
            }
            if (hasExisting && !force)
            {
                Console.Error.WriteLine("输出目录已有生成文件，请加 --force 覆盖，或换一个空目录。");
                return 2;
            }
            if (force)
            {
                ComparePack.ClearOutput(root, generated);
            }

            try
            {
                List<ExtractedBrush> sourceBrushes = BrushsetExtractor.Extract(input, sourceDir, true);
                List<AbrInput> inputs = new List<AbrInput>(sourceBrushes.Count);
                foreach (ExtractedBrush brush in sourceBrushes)
                {
                    string image = brush.ShapePath ?? brush.GrainPath;
                    if (image == null || !File.Exists(image))
                    {
                        continue;
                    }
                    inputs.Add(new AbrInput
                    {
                        ImagePath = image,
                        DisplayName = string.IsNullOrEmpty(brush.Name) ? "Brush" : brush.Name,
                        Uuid = brush.SourceId,
                        Spacing = brush.SpacingPercent,
                        Angle = ParamMapping.AngleDegrees(brush.Params),
                        Roundness = ParamMapping.RoundnessPercent(brush.Params),
                        Params = brush.Params
                    });
                }
                int written = AbrWriter.ConvertImagesToAbr(abrPath, inputs, version, useRle, invert, !noCrop);
                int read = AbrReader.ExtractAbr(abrPath, abrDir, invert, !noCrop, true);
                ComparePack.Report report = ComparePack.Build(
                    sourceBrushes,
                    AbrReader.ReadBrushes(abrPath),
                    input,
                    abrPath,
                    root,
                    abrDir,
                    version,
                    invert,
                    noCrop,
                    written,
                    read);
                File.WriteAllText(summaryPath, JsonUtil.Format(report.ToJson()), new UTF8Encoding(false));
                File.WriteAllText(checklistPath, report.ToMarkdown(), new UTF8Encoding(false));
                Console.WriteLine("对照包已生成: " + root);
                Console.WriteLine("源笔刷=" + report.SourceUsable + " ABR写入=" + written + " 回读=" + read + " 精确=" + report.Exact + "/" + report.Matched);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("生成对照包失败: " + ex.Message);
                AppLog.Write("compare-pack: " + ex);
                return 1;
            }
        }
    }
    internal static class ComparePack
    {
        internal sealed class Report
        {
            public string Input;
            public string AbrPath;
            public int Version;
            public int SourceTotal;
            public int SourceUsable;
            public int WrittenAbr;
            public int ReadAbr;
            public int Matched;
            public int Exact;
            public int Close;
            public int Diff;
            public int Missing;
            public int MaxDiff;
            public int ParamOk;
            public int ParamBad;
            public int HighFidelity;
            public int MediumFidelity;
            public int LowFidelity;
            public readonly List<Dictionary<string, object>> Details = new List<Dictionary<string, object>>();
            public readonly List<Dictionary<string, object>> MissingRows = new List<Dictionary<string, object>>();

            public Dictionary<string, object> ToJson()
            {
                Dictionary<string, object> root = new Dictionary<string, object>();
                root["input"] = Input;
                root["abr"] = AbrPath;
                root["version"] = Version;
                root["source_total"] = SourceTotal;
                root["source_usable"] = SourceUsable;
                root["written_abr"] = WrittenAbr;
                root["read_abr"] = ReadAbr;
                root["matched"] = Matched;
                root["exact"] = Exact;
                root["close"] = Close;
                root["diff"] = Diff;
                root["missing"] = Missing;
                root["max_diff"] = MaxDiff;
                root["param_ok"] = ParamOk;
                root["param_bad"] = ParamBad;
                root["high_fidelity"] = HighFidelity;
                root["medium_fidelity"] = MediumFidelity;
                root["low_fidelity"] = LowFidelity;
                root["details"] = Details;
                root["missing_rows"] = MissingRows;
                return root;
            }

            public string ToMarkdown()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Photoshop 肉眼对照清单");
                sb.AppendLine();
                sb.AppendLine("- 输入: `" + Input + "`");
                sb.AppendLine("- ABR: `" + AbrPath + "`");
                sb.AppendLine("- ABR 版本: " + Version);
                sb.AppendLine("- 源笔刷(可用): " + SourceUsable + " / " + SourceTotal + "，ABR 写入: " + WrittenAbr + "，回读: " + ReadAbr);
                sb.AppendLine("- 匹配: " + Matched + "，逐像素 exact: " + Exact + "，close: " + Close + "，diff: " + Diff + "，缺失: " + Missing);
                sb.AppendLine("- 参数核对: " + ParamOk + "/" + (ParamOk + ParamBad));
                sb.AppendLine("- 保真分布: 高 " + HighFidelity + " / 近 " + MediumFidelity + " / 限 " + LowFidelity);
                sb.AppendLine();
                sb.AppendLine("## 操作步骤");
                sb.AppendLine();
                sb.AppendLine("1. 用 Photoshop 的“画笔”面板导入 `compare.abr`。");
                sb.AppendLine("2. 对照 `source/` 下同名目录的 `Shape.png`（或 `Grain.png`）与 `abr-extracted/` 下的回读 PNG。");
                sb.AppendLine("3. 在 Photoshop 里用同一支笔刷画一笔，逐项勾选下面的观察项；差异明显的在“备注”里记录。");
                sb.AppendLine("4. 把反馈写回 MAPPING.md，按真实差异迭代映射表。");
                sb.AppendLine();
                sb.AppendLine("## 逐项清单");
                sb.AppendLine();
                int index = 1;
                foreach (Dictionary<string, object> row in Details)
                {
                    string name = Safe(row, "name");
                    string uuid = Safe(row, "uuid");
                    string sourceFile = Safe(row, "source_file");
                    string abrFile = Safe(row, "abr_file");
                    string status = Safe(row, "status");
                    string maxDiff = Safe(row, "max_diff");
                    string spacing = Safe(row, "spacing");
                    string angle = Safe(row, "angle");
                    string roundness = Safe(row, "roundness");
                    string score = Safe(row, "score");
                    string level = Safe(row, "level");
                    sb.Append("### ").Append(index++).Append(". ").Append(name);
                    if (!string.IsNullOrEmpty(uuid))
                    {
                        sb.Append(" (").Append(uuid).Append(")");
                    }
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.Append("- 源: `").Append(sourceFile).AppendLine("`");
                    sb.Append("- ABR: `").Append(abrFile).AppendLine("`");
                    sb.Append("- 逐像素: ").Append(status);
                    if (status == "diff" || status == "size-diff")
                    {
                        sb.Append(" (max diff ").Append(maxDiff).Append(")");
                    }
                    sb.AppendLine();
                    sb.Append("- 参数: spacing ").Append(spacing).Append(", angle ").Append(angle).Append(", roundness ").Append(roundness).AppendLine();
                    sb.Append("- 保真: ").Append(score).Append("/100 ").Append(level).AppendLine();
                    sb.AppendLine("- 观察项: [ ] 笔尖形状  [ ] 方向角度  [ ] 间距  [ ] 压力动态  [ ] 纹理  [ ] 湿边/锥度  [ ] 备注");
                    sb.AppendLine();
                }
                foreach (Dictionary<string, object> row in MissingRows)
                {
                    string name = Safe(row, "name");
                    string uuid = Safe(row, "uuid");
                    sb.Append("### ").Append(index++).Append(". ").Append(name);
                    if (!string.IsNullOrEmpty(uuid))
                    {
                        sb.Append(" (").Append(uuid).Append(")");
                    }
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("- 未匹配到 ABR 回读笔刷，跳过。");
                    sb.AppendLine();
                }
                return sb.ToString();
            }

            private static string Safe(Dictionary<string, object> row, string key)
            {
                object value;
                return row.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
            }
        }

        public static void ClearOutput(string root, params string[] paths)
        {
            string rootFull = Path.GetFullPath(root);
            foreach (string path in paths)
            {
                string full = Path.GetFullPath(path);
                if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("拒绝删除输出目录之外的路径: " + full);
                }
                if (Directory.Exists(full))
                {
                    Directory.Delete(full, true);
                }
                else if (File.Exists(full))
                {
                    File.Delete(full);
                }
            }
        }

        internal static Report Build(
            List<ExtractedBrush> sourceBrushes,
            List<AbrBrush> abrBrushes,
            string input,
            string abrPath,
            string root,
            string abrDir,
            int version,
            bool invert,
            bool noCrop,
            int written,
            int read)
        {
            Report report = new Report
            {
                Input = input,
                AbrPath = abrPath,
                Version = version,
                WrittenAbr = written,
                ReadAbr = read
            };
            int usableIndex = 0;
            foreach (ExtractedBrush source in sourceBrushes)
            {
                report.SourceTotal++;
                string image = source.ShapePath ?? source.GrainPath;
                if (image == null || !File.Exists(image))
                {
                    continue;
                }
                report.SourceUsable++;
                AbrBrush abr = FindByUuid(abrBrushes, source.SourceId);
                if (abr == null && usableIndex >= 0 && usableIndex < abrBrushes.Count)
                {
                    abr = abrBrushes[usableIndex];
                }
                usableIndex++;
                if (abr == null)
                {
                    report.Missing++;
                    Dictionary<string, object> missing = new Dictionary<string, object>();
                    missing["name"] = source.Name ?? "";
                    missing["uuid"] = source.SourceId ?? "";
                    report.MissingRows.Add(missing);
                    continue;
                }

                AbrBrush sourceBrush;
                string status = "missing";
                int maxDiff = 0;
                if (!AbrWriter.TryLoadAlpha(new AbrInput
                {
                    ImagePath = image,
                    DisplayName = source.Name,
                    Spacing = source.SpacingPercent
                }, invert, !noCrop, out sourceBrush))
                {
                    report.Missing++;
                    continue;
                }
                byte[] srcAlpha = sourceBrush.Alpha;
                byte[] abrAlpha = NormalizeAlpha(abr, invert, !noCrop);
                if (srcAlpha != null && abrAlpha != null)
                {
                    status = CompareAlphas(srcAlpha, abrAlpha, out maxDiff);
                }
                report.Matched++;
                if (maxDiff > report.MaxDiff)
                {
                    report.MaxDiff = maxDiff;
                }
                if (status == "exact")
                {
                    report.Exact++;
                }
                else if (status == "close")
                {
                    report.Close++;
                }
                else
                {
                    report.Diff++;
                }

                bool paramOk = ParamsOk(source, abr);
                if (paramOk)
                {
                    report.ParamOk++;
                }
                else
                {
                    report.ParamBad++;
                }

                FidelityReport fidelity = BrushFidelity.Evaluate(source);
                if (fidelity.Level == FidelityLevel.High)
                {
                    report.HighFidelity++;
                }
                else if (fidelity.Level == FidelityLevel.Medium)
                {
                    report.MediumFidelity++;
                }
                else
                {
                    report.LowFidelity++;
                }

                string abrFile = PathUtil.UniquePath(abrDir, PathUtil.Sanitize(abr.Name), ".png");
                Dictionary<string, object> row = new Dictionary<string, object>();
                row["name"] = source.Name ?? "";
                row["uuid"] = source.SourceId ?? "";
                row["source_file"] = RelativePath(root, image);
                row["abr_file"] = RelativePath(root, abrFile);
                row["status"] = status;
                row["max_diff"] = maxDiff;
                row["spacing"] = abr.Spacing;
                row["angle"] = Math.Round(abr.Angle, 4);
                row["roundness"] = Math.Round(abr.Roundness, 4);
                row["param_ok"] = paramOk;
                row["score"] = fidelity.Score;
                row["level"] = BrushFidelity.LevelLabel(fidelity.Level);
                report.Details.Add(row);
            }
            return report;
        }

        private static AbrBrush FindByUuid(List<AbrBrush> brushes, string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return null;
            }
            foreach (AbrBrush brush in brushes)
            {
                if (string.Equals(brush.Uuid, uuid, StringComparison.OrdinalIgnoreCase))
                {
                    return brush;
                }
            }
            return null;
        }

        private static string CompareAlphas(byte[] a, byte[] b, out int maxDiff)
        {
            maxDiff = 0;
            if (a.Length != b.Length)
            {
                maxDiff = Math.Max(a.Length, b.Length);
                return "size-diff";
            }
            for (int i = 0; i < a.Length; i++)
            {
                int delta = Math.Abs(a[i] - b[i]);
                if (delta > maxDiff)
                {
                    maxDiff = delta;
                }
            }
            if (maxDiff == 0)
            {
                return "exact";
            }
            if (maxDiff <= 2)
            {
                return "close";
            }
            return "diff";
        }

        private static byte[] NormalizeAlpha(AbrBrush brush, bool invert, bool autocrop)
        {
            byte[] alpha = brush.Alpha;
            int width = brush.Width;
            int height = brush.Height;
            if (invert)
            {
                for (int i = 0; i < alpha.Length; i++)
                {
                    alpha[i] = (byte)(255 - alpha[i]);
                }
            }
            if (autocrop)
            {
                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (alpha[y * width + x] > 0)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
                if (maxX >= 0 && (minX > 0 || minY > 0 || maxX < width - 1 || maxY < height - 1))
                {
                    int newWidth = maxX - minX + 1;
                    int newHeight = maxY - minY + 1;
                    byte[] cropped = new byte[newWidth * newHeight];
                    for (int y = 0; y < newHeight; y++)
                    {
                        Array.Copy(alpha, (minY + y) * width + minX, cropped, y * newWidth, newWidth);
                    }
                    alpha = cropped;
                }
            }
            return alpha;
        }

        private static bool ParamsOk(ExtractedBrush source, AbrBrush abr)
        {
            if (source.Params == null)
            {
                return true;
            }
            bool spacing = source.SpacingPercent == abr.Spacing;
            bool angle = Math.Abs(ParamMapping.AngleDegrees(source.Params) - abr.Angle) < 0.01;
            bool roundness = Math.Abs(ParamMapping.RoundnessPercent(source.Params) - abr.Roundness) < 0.01;
            if (!spacing || !angle || !roundness || abr.Descriptor == null)
            {
                return spacing && angle && roundness;
            }

            Dictionary<string, object> preset = abr.Descriptor.Items;
            double tilt = ParamMapping.Num(source.Params, "dynamicsTiltAngle");
            double pressureSize = ParamMapping.Num(source.Params, "dynamicsPressureSize");
            double jitterSize = ParamMapping.Num(source.Params, "dynamicsJitterSize");
            double pressureOpacity = ParamMapping.Num(source.Params, "dynamicsPressureOpacity");
            double jitterOpacity = ParamMapping.Num(source.Params, "dynamicsJitterOpacity");
            double minSize = ParamMapping.Num(source.Params, "minSize");
            double minOpacity = ParamMapping.Num(source.Params, "minOpacity");
            double scatter = ParamMapping.Num(source.Params, "shapeScatter");
            double shapeCount = ParamMapping.Num(source.Params, "shapeCount");
            double wet = ParamMapping.Num(source.Params, "wetEdgesAmount");

            if ((tilt > 0.12 || pressureSize > 0 || jitterSize > 0 || minSize > 0) &&
                (!GetBool(preset, "useTipDynamics") || GetDescriptor(preset, "szVr") == null))
            {
                return false;
            }
            if ((scatter > 0 || shapeCount > 0) &&
                (!GetBool(preset, "useScatter") || !preset.ContainsKey("Cnt ") || GetDescriptor(preset, "scatterDynamics") == null))
            {
                return false;
            }
            if (wet > 0 &&
                (!GetBool(preset, "Wtdg") || GetDescriptor(preset, "toolOptions") == null))
            {
                return false;
            }
            if ((jitterOpacity > 0 || pressureOpacity > 0 || minOpacity > 0) &&
                (!GetBool(preset, "usePaintDynamics") || GetDescriptor(preset, "opVr") == null))
            {
                return false;
            }
            return true;
        }

        private static bool GetBool(Dictionary<string, object> items, string key)
        {
            object value;
            if (items != null && items.TryGetValue(key, out value) && value is bool)
            {
                return (bool)value;
            }
            return false;
        }

        private static AbrReader.AbrDescriptor GetDescriptor(Dictionary<string, object> items, string key)
        {
            object value;
            if (items != null && items.TryGetValue(key, out value))
            {
                return value as AbrReader.AbrDescriptor;
            }
            return null;
        }

        private static string RelativePath(string root, string path)
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return full.Substring(root.Length).TrimStart('\\', '/');
            }
            return full;
        }
    }
    internal static class PathUtil
    {
        public static string Sanitize(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            string result = sb.ToString().Trim();
            if (result.Length == 0)
            {
                result = "Brush";
            }
            if (result.Length > 80)
            {
                result = result.Substring(0, 80);
            }
            return result;
        }

        public static string UniquePath(string directory, string baseName, string extension)
        {
            string candidate = Path.Combine(directory, baseName + extension);
            int i = 2;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, baseName + " " + i.ToString() + extension);
                i++;
            }
            return candidate;
        }
    }
    internal sealed class BrushsetExtractor
    {
        public static List<ExtractedBrush> Extract(string brushsetPath, string outputRoot, bool writeParamsJson)
        {
            Directory.CreateDirectory(outputRoot);
            Dictionary<string, List<ZipArchiveEntry>> byDirectory = new Dictionary<string, List<ZipArchiveEntry>>(StringComparer.OrdinalIgnoreCase);
            List<string> order = new List<string>();
            HashSet<string> usedTargetDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (ZipArchive zip = ZipFile.OpenRead(brushsetPath))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                    string dir = name.Contains("/") ? name.Substring(0, name.LastIndexOf('/')) : "";
                    string file = name.Substring(name.LastIndexOf('/') + 1);
                    if (dir.Length == 0 || file.Length == 0)
                    {
                        continue;
                    }
                    bool relevant = file.Equals("Shape.png", StringComparison.OrdinalIgnoreCase) ||
                                    file.Equals("Grain.png", StringComparison.OrdinalIgnoreCase) ||
                                    file.EndsWith(".archive", StringComparison.OrdinalIgnoreCase) ||
                                    file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
                    if (!relevant)
                    {
                        continue;
                    }
                    List<ZipArchiveEntry> entries;
                    if (!byDirectory.TryGetValue(dir, out entries))
                    {
                        entries = new List<ZipArchiveEntry>();
                        byDirectory[dir] = entries;
                        order.Add(dir);
                    }
                    entries.Add(entry);
                }

                List<ExtractedBrush> result = new List<ExtractedBrush>();
                foreach (string dir in order)
                {
                    List<ZipArchiveEntry> entries = byDirectory[dir];
                    ExtractedBrush brush = new ExtractedBrush { BrushsetPath = brushsetPath, SourceId = dir, Name = dir };

                    byte[] archiveBytes = null;
                    foreach (ZipArchiveEntry entry in entries)
                    {
                        if (entry.Name.EndsWith(".archive", StringComparison.OrdinalIgnoreCase))
                        {
                            archiveBytes = ReadEntryBytes(entry);
                            break;
                        }
                    }
                    if (archiveBytes != null)
                    {
                        try
                        {
                            Dictionary<string, object> resolved = BinaryPlist.ParseArchive(archiveBytes);
                            brush.Params = resolved;
                            if (resolved != null)
                            {
                                object nameValue;
                                if (resolved.TryGetValue("name", out nameValue) && nameValue is string)
                                {
                                    brush.Name = (string)nameValue;
                                }
                                brush.SpacingPercent = ReadSpacingPercent(resolved);
                            }
                        }
                        catch
                        {
                        }
                    }

                    string baseDirName = PathUtil.Sanitize(brush.Name);
                    string targetDirName = baseDirName;
                    int dirCounter = 2;
                    while (usedTargetDirs.Contains(targetDirName))
                    {
                        targetDirName = baseDirName + " " + dirCounter.ToString();
                        dirCounter++;
                    }
                    usedTargetDirs.Add(targetDirName);
                    string targetDir = Path.Combine(outputRoot, targetDirName);
                    Directory.CreateDirectory(targetDir);
                    if (writeParamsJson && brush.Params != null)
                    {
                        File.WriteAllText(Path.Combine(targetDir, "params.json"), JsonUtil.Format(brush.Params), new UTF8Encoding(false));
                    }
                    if (writeParamsJson)
                    {
                        Dictionary<string, object> meta = new Dictionary<string, object>();
                        meta["sourceId"] = brush.SourceId;
                        meta["name"] = brush.Name;
                        File.WriteAllText(Path.Combine(targetDir, "meta.json"), JsonUtil.Format(meta), new UTF8Encoding(false));
                    }

                    foreach (ZipArchiveEntry entry in entries)
                    {
                        string file = entry.Name;
                        if (file.Equals("Shape.png", StringComparison.OrdinalIgnoreCase))
                        {
                            brush.ShapePath = ExtractEntry(entry, targetDir, file);
                        }
                        else if (file.Equals("Grain.png", StringComparison.OrdinalIgnoreCase))
                        {
                            brush.GrainPath = ExtractEntry(entry, targetDir, file);
                        }
                        else if (!file.EndsWith(".archive", StringComparison.OrdinalIgnoreCase) &&
                                 (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                  file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                  file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)))
                        {
                            brush.ExtraTextures.Add(ExtractEntry(entry, targetDir, file));
                        }
                    }

                    // 内置材质解析：包内无物理 Shape/Grain，但引用 Procreate 内置材质（bundledShapePath/GrainPath）时，
                    // 从内置材质库解析出材质文件，恢复这类被跳过的笔刷。
                    if (brush.ShapePath == null)
                    {
                        string bundled = ParamMapping.Str(brush.Params, "bundledShapePath");
                        string resolved;
                        if (bundled != null && BundledMaterials.TryExtract(bundled, targetDir, out resolved))
                        {
                            brush.ShapePath = resolved;
                        }
                    }
                    if (brush.GrainPath == null)
                    {
                        string bundled = ParamMapping.Str(brush.Params, "bundledGrainPath");
                        string resolved;
                        if (bundled != null && BundledMaterials.TryExtract(bundled, targetDir, out resolved))
                        {
                            brush.GrainPath = resolved;
                        }
                    }
                    result.Add(brush);
                }
                return result;
            }
        }

        private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (Stream src = entry.Open())
                {
                    src.CopyTo(ms);
                }
                return ms.ToArray();
            }
        }

        private static string ExtractEntry(ZipArchiveEntry entry, string targetDir, string file)
        {
            string outPath = Path.Combine(targetDir, PathUtil.Sanitize(file));
            using (Stream src = entry.Open())
            using (FileStream dst = File.Create(outPath))
            {
                src.CopyTo(dst);
            }
            return outPath;
        }

        private static int ReadSpacingPercent(Dictionary<string, object> resolved)
        {
            object value;
            if (resolved == null || !resolved.TryGetValue("plotSpacing", out value))
            {
                return Mapping.SpacingDefault;
            }
            try
            {
                return Mapping.SpacingFrom(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }
            catch
            {
                return Mapping.SpacingDefault;
            }
        }
    }
    internal static class JsonUtil
    {
        public static string Format(object value)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, value, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int depth)
        {
            string indent = new string(' ', depth * 2);
            string indentInner = new string(' ', (depth + 1) * 2);
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            if (value is string)
            {
                sb.Append('"').Append(Escape((string)value)).Append('"');
                return;
            }
            if (value is bool)
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }
            if (value is byte[])
            {
                byte[] bytes = (byte[])value;
                sb.Append('"').Append(BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant()).Append('"');
                return;
            }
            if (value is int || value is long || value is short || value is sbyte || value is byte || value is ulong || value is uint || value is ushort)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is float || value is double || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is DateTime)
            {
                sb.Append('"').Append(((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('"');
                return;
            }
            if (value is AbrReader.AbrDescriptor)
            {
                AbrReader.AbrDescriptor descriptor = (AbrReader.AbrDescriptor)value;
                Dictionary<string, object> obj = new Dictionary<string, object>();
                obj["classId"] = descriptor.ClassId;
                obj["items"] = descriptor.Items;
                WriteValue(sb, obj, depth);
                return;
            }
            if (value is IDictionary<string, object>)
            {
                IDictionary<string, object> dict = (IDictionary<string, object>)value;
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> pair in dict)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    sb.Append('\n').Append(indentInner).Append('"').Append(Escape(pair.Key)).Append("\": ");
                    WriteValue(sb, pair.Value, depth + 1);
                }
                if (!first)
                {
                    sb.Append('\n').Append(indent);
                }
                sb.Append('}');
                return;
            }
            if (value is System.Collections.IList)
            {
                System.Collections.IList list = (System.Collections.IList)value;
                sb.Append('[');
                bool first = true;
                foreach (object item in list)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    sb.Append('\n').Append(indentInner);
                    WriteValue(sb, item, depth + 1);
                }
                if (!first)
                {
                    sb.Append('\n').Append(indent);
                }
                sb.Append(']');
                return;
            }
            sb.Append('"').Append(Escape(value.ToString())).Append('"');
        }

        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
    internal static class BinaryPlist
    {
        public static Dictionary<string, object> ParseArchive(byte[] data)
        {
            PlistParser parser;
            int topObject = PrepareParser(data, out parser);
            object rawTop = parser.ReadObject(topObject);
            if (rawTop == null)
            {
                return null;
            }
            object resolvedRoot = null;
            List<object> objectsTable = null;
            IDictionary<string, object> archive = rawTop as IDictionary<string, object>;
            if (archive != null)
            {
                object objectsEntry;
                if (archive.TryGetValue("$objects", out objectsEntry) && objectsEntry is Ref)
                {
                    objectsTable = parser.ReadObject(((Ref)objectsEntry).Index) as List<object>;
                }
                object topEntry;
                if (objectsTable != null && archive.TryGetValue("$top", out topEntry))
                {
                    object topResolved = ResolveValue(topEntry, parser, objectsTable, new HashSet<int>());
                    IDictionary<string, object> topDict = topResolved as IDictionary<string, object>;
                    object rootEntry;
                    if (topDict != null && topDict.TryGetValue("root", out rootEntry))
                    {
                        resolvedRoot = ResolveValue(rootEntry, parser, objectsTable, new HashSet<int>());
                        return resolvedRoot as Dictionary<string, object>;
                    }
                }
            }
            resolvedRoot = ResolveValue(rawTop, parser, objectsTable, new HashSet<int>());
            if (resolvedRoot is string)
            {
                return new Dictionary<string, object> { { "name", resolvedRoot } };
            }
            return resolvedRoot as Dictionary<string, object>;
        }

        private sealed class PlistParser
        {
            private readonly byte[] data;
            private readonly int offsetSize;
            private readonly int refSize;
            private readonly int[] offsets;

            public PlistParser(byte[] data, int offsetSize, int refSize, int numObjects, int offsetTableOffset)
            {
                this.data = data;
                this.offsetSize = offsetSize;
                this.refSize = refSize;
                offsets = new int[numObjects];
                for (int i = 0; i < numObjects; i++)
                {
                    offsets[i] = ReadInt(offsetTableOffset + i * offsetSize, offsetSize);
                }
            }

            public object ReadObject(int index)
            {
                if (index < 0 || index >= offsets.Length)
                {
                    return null;
                }
                int pos = offsets[index];
                if (pos < 0 || pos + 1 > data.Length)
                {
                    return null;
                }
                int marker = data[pos];
                int type = marker >> 4;
                int info = marker & 0x0f;
                pos++;
                if (type == 0x0)
                {
                    if (info == 0x08)
                    {
                        return false;
                    }
                    if (info == 0x09)
                    {
                        return true;
                    }
                    return null;
                }
                if (type == 0x1)
                {
                    int length = info == 0x0f ? ReadLength(ref pos) : (1 << info);
                    return ReadBigInt(pos, length);
                }
                if (type == 0x2)
                {
                    int length = info == 0x0f ? ReadLength(ref pos) : (1 << info);
                    byte[] raw = new byte[length];
                    Array.Copy(data, pos, raw, 0, length);
                    if (length == 8)
                    {
                        long bits = BitConverter.ToInt64(Reverse(raw), 0);
                        return BitConverter.Int64BitsToDouble(bits);
                    }
                    if (length == 4)
                    {
                        int bits = BitConverter.ToInt32(Reverse(raw), 0);
                        return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
                    }
                    return 0.0;
                }
                if (type == 0x3)
                {
                    int length = info == 0x0f ? ReadLength(ref pos) : (1 << info);
                    byte[] raw = new byte[length];
                    Array.Copy(data, pos, raw, 0, length);
                    if (length == 8)
                    {
                        long bits = BitConverter.ToInt64(Reverse(raw), 0);
                        return new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(BitConverter.Int64BitsToDouble(bits));
                    }
                    return null;
                }
                if (type == 0x4)
                {
                    int length = info == 0x0f ? ReadLength(ref pos) : info;
                    byte[] raw = new byte[length];
                    Array.Copy(data, pos, raw, 0, length);
                    return raw;
                }
                if (type == 0x5)
                {
                    int length = info == 0x0f ? ReadLength(ref pos) : info;
                    byte[] raw = new byte[length];
                    Array.Copy(data, pos, raw, 0, length);
                    return Encoding.UTF8.GetString(raw);
                }
                if (type == 0x6)
                {
                    int length = info == 0x0f ? ReadLength(ref pos) : info;
                    byte[] raw = new byte[length * 2];
                    Array.Copy(data, pos, raw, 0, raw.Length);
                    return Encoding.BigEndianUnicode.GetString(raw);
                }
                if (type == 0x8)
                {
                    int length = info + 1;
                    return new UidRef(ReadInt(pos, length));
                }
                if (type == 0xa)
                {
                    int count = info == 0x0f ? ReadLength(ref pos) : info;
                    List<object> list = new List<object>(count);
                    for (int i = 0; i < count; i++)
                    {
                        list.Add(new Ref(ReadInt(pos + i * refSize, refSize)));
                    }
                    return list;
                }
                if (type == 0xc || type == 0xd)
                {
                    int count = info == 0x0f ? ReadLength(ref pos) : info;
                    int keyBase = pos;
                    int valueBase = pos + count * refSize;
                    Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.Ordinal);
                    for (int i = 0; i < count; i++)
                    {
                        int keyRef = ReadInt(keyBase + i * refSize, refSize);
                        int valueRef = ReadInt(valueBase + i * refSize, refSize);
                        object key = ResolveRef(keyRef, false);
                        dict[key == null ? "#" + i.ToString() : key.ToString()] = new Ref(valueRef);
                    }
                    return dict;
                }
                return null;
            }

            private object ResolveRef(int index, bool resolveUid)
            {
                object obj = ReadObject(index);
                if (obj is UidRef)
                {
                    UidRef uid = (UidRef)obj;
                    if (resolveUid)
                    {
                        return ResolveRef(uid.Index, true);
                    }
                    return uid;
                }
                if (obj is Ref)
                {
                    return ResolveRef(((Ref)obj).Index, resolveUid);
                }
                return obj;
            }

            private int ReadLength(ref int pos)
            {
                int token = ReadInt(pos, 1);
                pos += 1;
                int sizeBytes = 1 << (token & 0x03);
                int length = ReadInt(pos, sizeBytes);
                pos += sizeBytes;
                return length;
            }

            private int ReadInt(int pos, int length)
            {
                int value = 0;
                for (int i = 0; i < length; i++)
                {
                    if (pos + i >= data.Length)
                    {
                        return 0;
                    }
                    value = (value << 8) | data[pos + i];
                }
                return value;
            }

            private object ReadBigInt(int pos, int length)
            {
                if (length <= 0)
                {
                    return 0L;
                }
                ulong value = 0;
                int take = Math.Min(length, 8);
                for (int i = 0; i < take; i++)
                {
                    if (pos + i >= data.Length)
                    {
                        break;
                    }
                    value = (value << 8) | data[pos + i];
                }
                if (length <= 8)
                {
                    return (long)value;
                }
                return value;
            }

            private static byte[] Reverse(byte[] raw)
            {
                Array.Reverse(raw);
                return raw;
            }
        }

        private sealed class UidRef
        {
            public readonly int Index;
            public UidRef(int index) { Index = index; }
        }

        private sealed class Ref
        {
            public readonly int Index;
            public Ref(int index) { Index = index; }
        }

        public static object Parse(byte[] data)
        {
            PlistParser parser;
            int topObject = PrepareParser(data, out parser);
            return ResolveRoot(parser, topObject);
        }

        private static int PrepareParser(byte[] data, out PlistParser parser)
        {
            if (data == null || data.Length < 40 || data[0] != 'b' || data[1] != 'p' || data[2] != 'l' || data[3] != 'i' || data[4] != 's' || data[5] != 't')
            {
                throw new InvalidDataException("不是 binary plist 文件");
            }
            int trailerOffset = data.Length - 32;
            int offsetSize = data[trailerOffset + 6];
            int refSize = data[trailerOffset + 7];
            long numObjects = ReadLong(data, trailerOffset + 8);
            long topObject = ReadLong(data, trailerOffset + 16);
            long offsetTableOffset = ReadLong(data, trailerOffset + 24);
            parser = new PlistParser(data, offsetSize, refSize, (int)numObjects, (int)offsetTableOffset);
            return (int)topObject;
        }

        private static object ResolveRoot(PlistParser parser, int rootIndex)
        {
            object obj = parser.ReadObject(rootIndex);
            Dictionary<string, object> dict = obj as Dictionary<string, object>;
            if (dict != null && dict.ContainsKey("$archiver"))
            {
                List<object> objectsTable = null;
                object objectsEntry;
                if (dict.TryGetValue("$objects", out objectsEntry) && objectsEntry is Ref)
                {
                    objectsTable = parser.ReadObject(((Ref)objectsEntry).Index) as List<object>;
                }
                object archiver = ResolveValue(dict["$archiver"], parser, objectsTable, new HashSet<int>());
                if (archiver is string && (string)archiver == "NSKeyedArchiver")
                {
                    if (dict.ContainsKey("$top"))
                    {
                        object top = ResolveValue(dict["$top"], parser, objectsTable, new HashSet<int>());
                        if (top is Dictionary<string, object>)
                        {
                            return top;
                        }
                    }
                    if (objectsTable != null)
                    {
                        return objectsTable;
                    }
                }
            }
            return ResolveValue(obj, parser, null, new HashSet<int>());
        }

        private static object ResolveValue(object value, PlistParser parser, List<object> objectsTable, HashSet<int> visiting)
        {
            if (value is UidRef)
            {
                UidRef uid = (UidRef)value;
                if (objectsTable == null || uid.Index < 0 || uid.Index >= objectsTable.Count)
                {
                    return null;
                }
                object target = objectsTable[uid.Index];
                Ref reference = target as Ref;
                if (reference == null)
                {
                    return ResolveValue(target, parser, objectsTable, visiting);
                }
                if (!visiting.Add(uid.Index))
                {
                    return "<cycle>";
                }
                object resolved = parser.ReadObject(reference.Index);
                object result = ResolveValue(resolved, parser, objectsTable, visiting);
                visiting.Remove(uid.Index);
                return result;
            }
            if (value is Ref)
            {
                Ref reference = (Ref)value;
                return ResolveValue(parser.ReadObject(reference.Index), parser, objectsTable, visiting);
            }
            if (value is Dictionary<string, object>)
            {
                Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)value)
                {
                    dict[pair.Key] = ResolveValue(pair.Value, parser, objectsTable, visiting);
                }
                return dict;
            }
            if (value is List<object>)
            {
                List<object> list = (List<object>)value;
                List<object> result = new List<object>(list.Count);
                foreach (object item in list)
                {
                    result.Add(ResolveValue(item, parser, objectsTable, visiting));
                }
                return result;
            }
            return value;
        }

        private static long ReadLong(byte[] data, int pos)
        {
            long value = 0;
            for (int i = 0; i < 8; i++)
            {
                value = (value << 8) | data[pos + i];
            }
            return value;
        }
    }
    internal static class BrushsetConverter
    {
        public static int ConvertToAbr(
            string outputAbr,
            List<string> brushsetPaths,
            int version,
            bool useRle,
            bool invert,
            bool autocrop,
            out int brushCount,
            Action<int> progress = null,
            int? spacingOverride = null,
            HashSet<string> includeIds = null,
            int? maxTipSide = null)
        {
            brushCount = 0;
            string temp = Path.Combine(Path.GetTempPath(), "BrushStudio-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                List<AbrInput> convertInputs = new List<AbrInput>();
                for (int i = 0; i < brushsetPaths.Count; i++)
                {
                    List<ExtractedBrush> brushes = BrushsetExtractor.Extract(brushsetPaths[i], temp, false);
                    // 应用笔刷库覆盖（改名/单笔刷反转），否则新解包的笔刷没有个性化设置
                    foreach (ExtractedBrush brush in brushes)
                    {
                        LibraryEntry entry = BrushLibrary.Get(brushsetPaths[i], brush.SourceId);
                        if (entry != null)
                        {
                            brush.ManualName = entry.Name;
                            brush.ManualInvert = entry.Invert;
                        }
                    }
                    foreach (ExtractedBrush brush in brushes)
                    {
                        if (includeIds != null && !includeIds.Contains(brush.SourceId))
                        {
                            continue;
                        }
                        string image = brush.ShapePath ?? brush.GrainPath;
                        if (image == null || !File.Exists(image))
                        {
                            continue;
                        }
                        convertInputs.Add(new AbrInput
                        {
                            ImagePath = image,
                            DisplayName = string.IsNullOrEmpty(brush.EffectiveName) ? "Brush" : brush.EffectiveName,
                            Uuid = AbrWriter.ValidUuid(brush.SourceId),
                            Spacing = spacingOverride ?? brush.EffectiveSpacingPercent,
                            Angle = brush.EffectiveAngleDegrees(),
                            Roundness = brush.EffectiveRoundnessPercent(),
                            Diameter = brush.ManualDiameter ?? 0,
                            Params = brush.Params,
                            Invert = brush.ManualInvert,
                            // 仅当笔刷同时有 Shape 与 Grain、且 grainDepth>0（Procreate 里纹理可见）时，Grain 作为纹理；
                            // grainDepth=0 的笔刷纹理本就不可见，加了反而失真；无 Shape 时 Grain 就是笔尖（沿用旧行为）。
                            GrainPath = brush.ShapePath != null && brush.GrainPath != null && File.Exists(brush.GrainPath) && ParamMapping.Num(brush.Params, "grainDepth") > 0 ? brush.GrainPath : null
                        });
                    }
                    if (progress != null)
                    {
                        progress((i + 1) * 100 / brushsetPaths.Count);
                    }
                }
                brushCount = convertInputs.Count;
                if (convertInputs.Count == 0)
                {
                    throw new InvalidOperationException("笔刷包里没有找到可用的 Shape / Grain 图片。");
                }
                return AbrWriter.ConvertImagesToAbr(outputAbr, convertInputs, version, useRle, invert, autocrop, maxTipSide ?? 0);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                    {
                        Directory.Delete(temp, true);
                    }
                }
                catch
                {
                }
            }
        }
    }
    /// <summary>反向打包：把选中的笔刷导出为 Procreate .brushset（Shape.png + Grain.png）。</summary>
    internal static class BrushsetBuilder
    {
        public static int Build(string outputPath, List<ExtractedBrush> brushes)
        {
            int count = 0;
            if (brushes == null || brushes.Count == 0)
            {
                return 0;
            }
            using (ZipArchive zip = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ExtractedBrush b in brushes)
                {
                    string image = b.ShapePath ?? b.GrainPath;
                    if (image == null || !File.Exists(image))
                    {
                        continue;
                    }
                    string name = PathUtil.Sanitize(b.EffectiveName ?? "Brush");
                    if (name.Length == 0)
                    {
                        name = "Brush";
                    }
                    string dirName = name;
                    int n = 2;
                    while (!used.Add(dirName))
                    {
                        dirName = name + " " + n.ToString();
                        n++;
                    }
                    ZipArchiveEntry shape = zip.CreateEntry(dirName + "/Shape.png");
                    using (FileStream fs = File.OpenRead(image))
                    {
                        using (Stream s = shape.Open())
                        {
                            fs.CopyTo(s);
                        }
                    }
                    if (b.ShapePath != null && b.GrainPath != null && File.Exists(b.GrainPath))
                    {
                        ZipArchiveEntry grain = zip.CreateEntry(dirName + "/Grain.png");
                        using (FileStream fs = File.OpenRead(b.GrainPath))
                        {
                            using (Stream s = grain.Open())
                            {
                                fs.CopyTo(s);
                            }
                        }
                    }
                    count++;
                }
            }
            return count;
        }
    }
    internal static class AbrReader
    {
        private const int MaxRowsPerChunk = 16384;
        internal sealed class AbrDescriptor
        {
            public string Name = "";
            public string ClassId = "";
            public readonly Dictionary<string, object> Items = new Dictionary<string, object>();
        }

        public static List<AbrBrush> ReadBrushes(string input)
        {
            byte[] data = File.ReadAllBytes(input);
            int index = 0;
            int version = ReadBE16(data, ref index);
            int second = ReadBE16(data, ref index);
            // ABR v6/v7/v10/v11 都是 8BIM 块结构（v10 起为较新的 Photoshop 导出）；
            // 旧格式 v1/v2 才是 type+length 块。按版本号 >=6 分流。
            return version >= 6
                ? ReadV6Plus(data, version, second)
                : ReadV12(data, version, second);
        }

        public static int ExtractAbr(string input, string outputDir, bool invert, bool autocrop, bool printParams = false)
        {
            Directory.CreateDirectory(outputDir);
            List<AbrBrush> brushes = ReadBrushes(input);

            int saved = 0;
            List<Dictionary<string, object>> paramRows = null;
            if (printParams)
            {
                paramRows = new List<Dictionary<string, object>>(brushes.Count);
            }
            for (int i = 0; i < brushes.Count; i++)
            {
                AbrBrush brush = brushes[i];
                string name = PathUtil.Sanitize(string.IsNullOrEmpty(brush.Name) ? "Brush" : brush.Name);
                if (string.IsNullOrEmpty(name))
                {
                    name = "Brush " + (i + 1).ToString();
                }
                string path = PathUtil.UniquePath(outputDir, name, ".png");
                if (paramRows != null)
                {
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    row["uuid"] = brush.Uuid;
                    row["name"] = brush.Name;
                    row["spacing"] = brush.Spacing;
                    row["angle"] = brush.Angle;
                    row["roundness"] = brush.Roundness;
                    row["diameter"] = brush.Diameter;
                    row["file"] = path;
                    paramRows.Add(row);
                }
                byte[] alpha = brush.Alpha;
                int w = brush.Width;
                int h = brush.Height;
                if (invert)
                {
                    for (int p = 0; p < alpha.Length; p++)
                    {
                        alpha[p] = (byte)(255 - alpha[p]);
                    }
                }
                if (autocrop)
                {
                    int minX = w, minY = h, maxX = -1, maxY = -1;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            if (alpha[y * w + x] > 0)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                        }
                    }
                    if (maxX >= 0 && (minX > 0 || minY > 0 || maxX < w - 1 || maxY < h - 1))
                    {
                        int nw = maxX - minX + 1;
                        int nh = maxY - minY + 1;
                        byte[] cropped = new byte[nw * nh];
                        for (int y = 0; y < nh; y++)
                        {
                            Array.Copy(alpha, (minY + y) * w + minX, cropped, y * nw, nw);
                        }
                        alpha = cropped;
                        w = nw;
                        h = nh;
                    }
                }

                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            byte a = alpha[y * w + x];
                            byte value = a;
                            Marshal.WriteByte(bd.Scan0, y * bd.Stride + x * 4, value);
                            Marshal.WriteByte(bd.Scan0, y * bd.Stride + x * 4 + 1, value);
                            Marshal.WriteByte(bd.Scan0, y * bd.Stride + x * 4 + 2, value);
                            Marshal.WriteByte(bd.Scan0, y * bd.Stride + x * 4 + 3, a);
                        }
                    }
                    bmp.UnlockBits(bd);
                    bmp.Save(path, ImageFormat.Png);
                    saved++;
                }
            }
            if (paramRows != null)
            {
                File.WriteAllText(Path.Combine(outputDir, "abr-params.json"), JsonUtil.Format(paramRows), new UTF8Encoding(false));
            }
            return saved;
        }

        public static void DumpDescriptors(string input, string outputPath)
        {
            List<AbrBrush> brushes = ReadBrushes(input);
            List<object> rows = new List<object>(brushes.Count);
            foreach (AbrBrush brush in brushes)
            {
                Dictionary<string, object> row = new Dictionary<string, object>();
                row["name"] = brush.Name;
                row["uuid"] = brush.Uuid;
                row["descriptor"] = brush.Descriptor;
                rows.Add(row);
            }
            File.WriteAllText(outputPath, JsonUtil.Format(rows), new UTF8Encoding(false));
        }

        private static List<AbrBrush> ReadV12(byte[] data, int version, int count)
        {
            List<AbrBrush> brushes = new List<AbrBrush>();
            int cursor = 4;
            for (int i = 0; i < count && cursor + 6 <= data.Length; i++)
            {
                int type = ReadBE16(data, ref cursor);
                int length = ReadBE32(data, ref cursor);
                if (length < 0 || cursor + length > data.Length)
                {
                    break;
                }
                AbrBrush brush = null;
                if (type == 2)
                {
                    brush = TryReadSampledBrush(data, cursor, length, version, i);
                }
                else if (type == 3)
                {
                    brush = TryReadPattern(data, cursor, length, i);
                }
                if (brush != null)
                {
                    brushes.Add(brush);
                }
                cursor += length;
            }
            return brushes;
        }

        private static List<AbrBrush> ReadV6Plus(byte[] data, int version, int subversion)
        {
            List<AbrBrush> brushes = new List<AbrBrush>();
            int pos = 4;
            int sampleStart = -1;
            int sampleLength = -1;
            int descStart = -1;
            int descLength = -1;
            while (pos + 12 <= data.Length)
            {
                string signature = Encoding.ASCII.GetString(data, pos, 4);
                if (signature != "8BIM" && signature != "8bim")
                {
                    break;
                }
                string key = Encoding.ASCII.GetString(data, pos + 4, 4);
                int length = ReadBE32At(data, pos + 8);
                if (length < 0 || pos + 12 + length > data.Length)
                {
                    break;
                }
                if (key == "samp")
                {
                    sampleStart = pos + 12;
                    sampleLength = length;
                }
                else if (key == "desc")
                {
                    descStart = pos + 12;
                    descLength = length;
                }
                pos += 12 + length;
            }

            if (sampleStart >= 0 && sampleLength > 0)
            {
                brushes = ReadSampSection(data, sampleStart, sampleLength, subversion);
            }
            if (descStart >= 0 && descLength > 0 && brushes.Count > 0)
            {
                ApplyBrushDescriptor(data, descStart, descLength, brushes);
            }
            return brushes;
        }

        private static List<AbrBrush> ReadSampSection(byte[] data, int start, int length, int subversion)
        {
            List<AbrBrush> brushes = new List<AbrBrush>();
            int end = start + length;
            int pos = start;
            int index = 0;
            while (pos + 4 <= end)
            {
                int entryLen = ReadBE32(data, ref pos);
                if (entryLen < 0 || pos + entryLen > end)
                {
                    break;
                }
                int entryData = pos;
                int next = entryData + ((entryLen + 3) & ~3);
                int skip = subversion == 1 ? 47 : 301;
                if (entryData + skip + 20 <= entryData + entryLen)
                {
                    int cursor = entryData;
                    string uuid = "";
                    if (cursor < end)
                    {
                        int uuidLen = data[cursor];
                        cursor++;
                        if (uuidLen > 0 && cursor + uuidLen <= entryData + entryLen)
                        {
                            uuid = Encoding.ASCII.GetString(data, cursor, uuidLen);
                            cursor += uuidLen;
                        }
                    }
                    cursor += skip - 1 - uuid.Length;
                    if (cursor < entryData + 1 + uuid.Length)
                    {
                        cursor = entryData + 1 + uuid.Length;
                    }
                    int top = ReadBE32(data, ref cursor);
                    int left = ReadBE32(data, ref cursor);
                    int bottom = ReadBE32(data, ref cursor);
                    int right = ReadBE32(data, ref cursor);
                    int depth = ReadBE16(data, ref cursor);
                    int compression = data[cursor];
                    cursor++;
                    int w = right - left;
                    int h = bottom - top;
                    if (depth == 8 && w > 0 && h > 0 && w <= 8192 && h <= 8192 && (long)w * h <= 32L * 1024 * 1024)
                    {
                        byte[] alpha = new byte[w * h];
                        bool ok = false;
                        if (compression == 0)
                        {
                            if (cursor + alpha.Length <= entryData + entryLen)
                            {
                                Array.Copy(data, cursor, alpha, 0, alpha.Length);
                                ok = true;
                            }
                        }
                        else if (compression == 1)
                        {
                            ok = RleDecode(data, cursor, entryData + entryLen, alpha, w, h, 0);
                        }
                        if (ok)
                        {
                            brushes.Add(new AbrBrush
                            {
                                Name = uuid,
                                Width = w,
                                Height = h,
                                Alpha = alpha,
                                Uuid = uuid
                            });
                        }
                    }
                }
                index++;
                pos = next;
            }
            return brushes;
        }

        private static void ApplyBrushDescriptor(byte[] data, int start, int length, List<AbrBrush> brushes)
        {
            try
            {
                int pos = start;
                int end = start + length;
                if (pos + 4 > end)
                {
                    return;
                }
                int version = ReadBE32(data, ref pos);
                if (version != 16)
                {
                    return;
                }
                AbrDescriptor root = ReadDescriptor(data, ref pos, end);
                if (root == null)
                {
                    return;
                }
                object listValue;
                if (!root.Items.TryGetValue("Brsh", out listValue))
                {
                    return;
                }
                List<object> list = listValue as List<object>;
                if (list == null)
                {
                    return;
                }
                int brushIndex = 0;
                foreach (object item in list)
                {
                    AbrDescriptor preset = item as AbrDescriptor;
                    if (preset == null || (preset.ClassId != "brushPreset" && preset.ClassId != "computedBrush"))
                    {
                        continue;
                    }
                    AbrBrush brush = brushIndex < brushes.Count ? brushes[brushIndex] : null;
                    AbrDescriptor sampled = null;
                    object nested;
                    if (preset.Items.TryGetValue("Brsh", out nested))
                    {
                        sampled = nested as AbrDescriptor;
                        if (sampled == null && nested is List<object>)
                        {
                            List<object> nestedList = (List<object>)nested;
                            if (nestedList.Count > 0)
                            {
                                sampled = nestedList[0] as AbrDescriptor;
                            }
                        }
                    }
                    string name = GetText(preset.Items, "Nm  ");
                    if (string.IsNullOrEmpty(name) && sampled != null)
                    {
                        name = GetText(sampled.Items, "Nm  ");
                    }
                    string uuid = sampled != null ? GetText(sampled.Items, "sampledData") : "";
                    if (string.IsNullOrEmpty(uuid))
                    {
                        uuid = sampled != null ? GetText(sampled.Items, "Uuid") : "";
                    }
                    if (!string.IsNullOrEmpty(uuid))
                    {
                        AbrBrush match = null;
                        foreach (AbrBrush candidate in brushes)
                        {
                            if (candidate.Uuid == uuid)
                            {
                                match = candidate;
                                break;
                            }
                        }
                        if (match != null)
                        {
                            brush = match;
                        }
                    }
                    if (brush == null)
                    {
                        continue;
                    }
                    brush.Descriptor = preset;
                    if (!string.IsNullOrEmpty(name))
                    {
                        brush.Name = name;
                    }
                    if (sampled != null)
                    {
                        double diameter = GetNumber(sampled.Items, "Dmtr");
                        if (diameter > 0)
                        {
                            brush.Diameter = diameter;
                        }
                        double angle = GetNumber(sampled.Items, "Angl");
                        if (angle != 0 || sampled.Items.ContainsKey("Angl"))
                        {
                            brush.Angle = angle;
                        }
                        double roundness = GetNumber(sampled.Items, "Rndn");
                        if (roundness > 0)
                        {
                            brush.Roundness = roundness;
                        }
                        double spacing = GetNumber(sampled.Items, "Spcn");
                        if (spacing > 0)
                        {
                            brush.Spacing = (int)Math.Round(spacing);
                        }
                    }
                    brushIndex++;
                }
            }
            catch
            {
            }
        }

        private static string GetText(Dictionary<string, object> items, string key)
        {
            object value;
            if (items != null && items.TryGetValue(key, out value))
            {
                string text = value as string;
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
            return "";
        }

        private static double GetNumber(Dictionary<string, object> items, string key)
        {
            object value;
            if (items != null && items.TryGetValue(key, out value))
            {
                if (value is double)
                {
                    return (double)value;
                }
                if (value is long)
                {
                    return (long)value;
                }
                if (value is bool)
                {
                    return (bool)value ? 1 : 0;
                }
            }
            return 0;
        }

        private static AbrDescriptor ReadDescriptor(byte[] data, ref int pos, int end)
        {
            if (pos + 4 > end)
            {
                return null;
            }
            AbrDescriptor descriptor = new AbrDescriptor();
            descriptor.Name = ReadDescriptorString(data, ref pos, end);
            descriptor.ClassId = ReadClassId(data, ref pos, end);
            int count = ReadBE32(data, ref pos);
            if (count < 0 || count > 100000)
            {
                return null;
            }
            for (int i = 0; i < count; i++)
            {
                string key = ReadClassId(data, ref pos, end);
                if (pos + 4 > end)
                {
                    return null;
                }
                string type = Encoding.ASCII.GetString(data, pos, 4);
                pos += 4;
                object value = ReadDescriptorValue(data, ref pos, end, type);
                if (!descriptor.Items.ContainsKey(key))
                {
                    descriptor.Items.Add(key, value);
                }
                else
                {
                    descriptor.Items[key] = value;
                }
            }
            return descriptor;
        }

        private static object ReadDescriptorValue(byte[] data, ref int pos, int end, string type)
        {
            if (type == "TEXT")
            {
                return ReadDescriptorString(data, ref pos, end);
            }
            if (type == "UntF")
            {
                if (pos + 12 > end)
                {
                    return 0d;
                }
                pos += 4; // unit
                return ReadBEDouble(data, ref pos);
            }
            if (type == "doub")
            {
                return ReadBEDouble(data, ref pos);
            }
            if (type == "long")
            {
                if (pos + 4 > end)
                {
                    return 0L;
                }
                return (long)ReadBE32(data, ref pos);
            }
            if (type == "comp")
            {
                if (pos + 8 > end)
                {
                    return 0L;
                }
                int high = ReadBE32(data, ref pos);
                int low = ReadBE32(data, ref pos);
                return ((long)high << 32) | (uint)low;
            }
            if (type == "bool")
            {
                if (pos >= end)
                {
                    return false;
                }
                return data[pos++] != 0;
            }
            if (type == "Objc" || type == "GlbC")
            {
                return ReadDescriptor(data, ref pos, end);
            }
            if (type == "VlLs")
            {
                int count = ReadBE32(data, ref pos);
                List<object> list = new List<object>();
                if (count < 0 || count > 100000)
                {
                    return list;
                }
                for (int i = 0; i < count; i++)
                {
                    if (pos + 4 > end)
                    {
                        break;
                    }
                    string itemType = Encoding.ASCII.GetString(data, pos, 4);
                    pos += 4;
                    list.Add(ReadDescriptorValue(data, ref pos, end, itemType));
                }
                return list;
            }
            if (type == "ObAr")
            {
                int itemsCount = ReadBE32(data, ref pos);
                AbrDescriptor descriptor = ReadDescriptor(data, ref pos, end);
                if (descriptor == null)
                {
                    return new AbrDescriptor();
                }
                return descriptor;
            }
            if (type == "enum")
            {
                string enumType = ReadClassId(data, ref pos, end);
                string enumValue = ReadClassId(data, ref pos, end);
                return enumType + "/" + enumValue;
            }
            if (type == "type")
            {
                return ReadClassId(data, ref pos, end);
            }
            if (type == "tdta" || type == "alias" || type == "path")
            {
                int length = ReadBE32(data, ref pos);
                if (length < 0 || length > 64 * 1024 * 1024 || pos + length > end)
                {
                    return new byte[0];
                }
                byte[] bytes = new byte[length];
                Array.Copy(data, pos, bytes, 0, length);
                pos += length;
                return bytes;
            }
            return null;
        }

        private static string ReadDescriptorString(byte[] data, ref int pos, int end)
        {
            if (pos + 4 > end)
            {
                return "";
            }
            int charCount = ReadBE32(data, ref pos);
            if (charCount < 0 || charCount > 16384 || pos + charCount * 2 > end)
            {
                return "";
            }
            string value = Encoding.BigEndianUnicode.GetString(data, pos, charCount * 2).TrimEnd('\0');
            pos += charCount * 2;
            return value;
        }

        private static string ReadClassId(byte[] data, ref int pos, int end)
        {
            if (pos + 4 > end)
            {
                return "";
            }
            int length = ReadBE32(data, ref pos);
            if (length == 0)
            {
                if (pos + 4 > end)
                {
                    return "";
                }
                string code = Encoding.ASCII.GetString(data, pos, 4);
                pos += 4;
                return code;
            }
            if (length < 0 || length > 1024 || pos + length > end)
            {
                return "";
            }
            string value = Encoding.ASCII.GetString(data, pos, length);
            pos += length;
            return value;
        }

        private static double ReadBEDouble(byte[] data, ref int pos)
        {
            byte[] bytes = new byte[8];
            Array.Copy(data, pos, bytes, 0, 8);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            pos += 8;
            return BitConverter.ToDouble(bytes, 0);
        }

        private static AbrBrush TryReadSampledBrush(byte[] data, int start, int length, int version, int itemIndex)
        {
            try
            {
                int end = start + length;
                if (end - start < 20)
                {
                    return null;
                }
                int cursor = start;
                ReadBE32(data, ref cursor); // obsolete miscellaneous field
                int spacing = ReadBE16(data, ref cursor);
                string name = "";
                if (version >= 2)
                {
                    if (cursor + 4 > end)
                    {
                        return null;
                    }
                    int nameLen = ReadBE32(data, ref cursor);
                    if (nameLen < 0 || nameLen > 1024 || cursor + nameLen * 2 > end)
                    {
                        return null;
                    }
                    name = ReadUtf16BE(data, ref cursor, nameLen);
                }

                if (cursor + 9 > end)
                {
                    return null;
                }
                cursor++; // anti-alias flag
                int bounds16Top = ReadBE16(data, ref cursor);
                int bounds16Left = ReadBE16(data, ref cursor);
                int bounds16Bottom = ReadBE16(data, ref cursor);
                int bounds16Right = ReadBE16(data, ref cursor);

                int rect32Pos = FindRect32(data, cursor, end, bounds16Top, bounds16Left, bounds16Bottom, bounds16Right);
                if (rect32Pos < 0)
                {
                    return null;
                }

                int top = ReadBE32(data, ref rect32Pos);
                int left = ReadBE32(data, ref rect32Pos);
                int bottom = ReadBE32(data, ref rect32Pos);
                int right = ReadBE32(data, ref rect32Pos);
                int depth = ReadBE16(data, ref rect32Pos);
                if (depth != 8)
                {
                    return null;
                }
                int w = right - left;
                int h = bottom - top;
                if (w <= 0 || h <= 0 || w > 8192 || h > 8192 || (long)w * h > 32L * 1024 * 1024)
                {
                    return null;
                }
                byte[] alpha = new byte[w * h];
                int rowsRemaining = h;
                int pixelOffset = 0;
                while (rowsRemaining > 0)
                {
                    if (rect32Pos >= end)
                    {
                        return null;
                    }
                    int compression = data[rect32Pos];
                    rect32Pos++;
                    int chunkHeight = Math.Min(rowsRemaining, MaxRowsPerChunk);
                    if (compression == 0)
                    {
                        int bytes = chunkHeight * w;
                        if (rect32Pos + bytes > end)
                        {
                            return null;
                        }
                        Array.Copy(data, rect32Pos, alpha, pixelOffset, bytes);
                        rect32Pos += bytes;
                    }
                    else if (compression == 1)
                    {
                        if (!RleDecode(data, rect32Pos, end, alpha, w, chunkHeight, pixelOffset))
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                    pixelOffset += chunkHeight * w;
                    rowsRemaining -= chunkHeight;
                }
                return new AbrBrush { Name = name, Width = w, Height = h, Alpha = alpha, Spacing = spacing };
            }
            catch
            {
                return null;
            }
        }

        private static int FindRect32(byte[] data, int from, int end, int bounds16Top, int bounds16Left, int bounds16Bottom, int bounds16Right)
        {
            int searchEnd = Math.Min(end - 23, from + 600);
            if (searchEnd < from)
            {
                return -1;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = from; i <= searchEnd; i++)
                {
                    int top = ReadBE32At(data, i);
                    int left = ReadBE32At(data, i + 4);
                    int bottom = ReadBE32At(data, i + 8);
                    int right = ReadBE32At(data, i + 12);
                    int w = right - left;
                    int h = bottom - top;
                    if (w <= 0 || h <= 0 || w > 8192 || h > 8192)
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        int expectedH = bounds16Bottom - bounds16Top;
                        int expectedW = bounds16Right - bounds16Left;
                        if (expectedH != h || expectedW != w)
                        {
                            continue;
                        }
                    }
                    if (data[i + 16] != 0 || data[i + 17] != 8)
                    {
                        continue;
                    }
                    if (data[i + 18] != 0 && data[i + 18] != 1)
                    {
                        continue;
                    }
                    return i;
                }
            }
            return -1;
        }

        private static int ReadBE32At(byte[] data, int pos)
        {
            return (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
        }

        private static AbrBrush TryReadPattern(byte[] data, int start, int length, int itemIndex)
        {
            try
            {
                if (length < 40)
                {
                    return null;
                }
                int cursor = start;
                int version = ReadBE32(data, ref cursor);
                int mode = ReadBE32(data, ref cursor);
                int h = ReadBE16(data, ref cursor);
                int w = ReadBE16(data, ref cursor);
                int nameLen = ReadBE32(data, ref cursor);
                string name = nameLen > 0 && nameLen < 512 ? ReadUtf16BE(data, ref cursor, nameLen) : "";
                int idLen = data[cursor];
                cursor++;
                if (idLen > 0 && cursor + idLen <= start + length)
                {
                    cursor += idLen;
                }
                if (mode == 2)
                {
                    cursor += 768;
                }
                if (w <= 0 || h <= 0 || w > 8192 || h > 8192 || cursor >= start + length)
                {
                    return null;
                }

                byte[] signature = new byte[] { 0, 0, (byte)(h >> 8), (byte)(h & 0xff), 0, 0, (byte)(w >> 8), (byte)(w & 0xff) };
                int searchEnd = Math.Min(start + length, cursor + 4096);
                int sigPos = -1;
                for (int i = cursor; i + 8 <= searchEnd; i++)
                {
                    bool match = true;
                    for (int j = 0; j < 8; j++)
                    {
                        if (data[i + j] != signature[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        sigPos = i;
                        break;
                    }
                }
                if (sigPos < 0)
                {
                    return null;
                }
                cursor = sigPos;
                cursor += 8;
                if (cursor + 23 > start + length)
                {
                    return null;
                }
                cursor += 8;  // version, length
                cursor += 16; // rect + max
                cursor += 8;  // unknowns
                cursor += 8;  // actual rect
                cursor += 2;  // depth
                int compression = data[cursor];
                cursor++;
                int channels = mode == 1 || mode == 2 ? 1 : mode == 4 ? 4 : 3;
                byte[] plane = new byte[w * h];
                if (compression == 1)
                {
                    if (!RleDecode(data, cursor, start + length, plane, w, h, 0))
                    {
                        return null;
                    }
                }
                else if (compression == 0)
                {
                    if (cursor + w * h > start + length)
                    {
                        return null;
                    }
                    Array.Copy(data, cursor, plane, 0, w * h);
                }
                else
                {
                    return null;
                }
                return new AbrBrush { Name = name, Width = w, Height = h, Alpha = plane };
            }
            catch
            {
                return null;
            }
        }

        private static bool RleDecode(byte[] data, int start, int end, byte[] target, int w, int h, int targetOffset)
        {
            int rowCount = h;
            if (start + rowCount * 2 > end)
            {
                return false;
            }
            int cursor = start;
            int[] rowSizes = new int[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                rowSizes[i] = ReadBE16(data, ref cursor);
            }
            for (int y = 0; y < rowCount; y++)
            {
                int rowEnd = Math.Min(end, cursor + rowSizes[y]);
                int x = 0;
                while (cursor < rowEnd && x < w)
                {
                    int n = data[cursor];
                    cursor++;
                    if (n < 128)
                    {
                        int count = n + 1;
                        for (int k = 0; k < count && x < w && cursor < rowEnd; k++)
                        {
                            target[targetOffset + x++] = data[cursor++];
                        }
                    }
                    else if (n > 128)
                    {
                        int count = 257 - n;
                        byte value = cursor < rowEnd ? data[cursor] : (byte)0;
                        cursor++;
                        for (int k = 0; k < count && x < w; k++)
                        {
                            target[targetOffset + x++] = value;
                        }
                    }
                }
                targetOffset += w;
            }
            return true;
        }

        private static int ReadBE16(byte[] data, ref int pos)
        {
            int value = (data[pos] << 8) | data[pos + 1];
            pos += 2;
            return value;
        }

        private static int ReadBE32(byte[] data, ref int pos)
        {
            int value = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            pos += 4;
            return value;
        }

        private static string ReadUtf16BE(byte[] data, ref int pos, int charCount)
        {
            if (charCount <= 0 || charCount > 1024 || pos + charCount * 2 > data.Length)
            {
                return "";
            }
            string value = Encoding.BigEndianUnicode.GetString(data, pos, charCount * 2);
            pos += charCount * 2;
            return value.TrimEnd('\0');
        }
    }
    internal static class AbrWriter
    {
        private const int SampledBrushType = 2;
        private const int MaxRowsPerChunk = 16384;

        public static int ConvertImagesToAbr(
            string output,
            List<string> inputs,
            int version,
            bool useRle,
            int spacing,
            bool invert,
            bool autocrop)
        {
            List<AbrInput> converted = new List<AbrInput>(inputs.Count);
            foreach (string input in inputs)
            {
                converted.Add(new AbrInput
                {
                    ImagePath = input,
                    DisplayName = Path.GetFileNameWithoutExtension(input),
                    Spacing = spacing
                });
            }
            return ConvertImagesToAbr(output, converted, version, useRle, invert, autocrop, 0);
        }

        public static int ConvertImagesToAbr(
            string output,
            List<AbrInput> inputs,
            int version,
            bool useRle,
            bool invert,
            bool autocrop,
            int maxTipSide = 0)
        {
            if (version != 1 && version != 2 && version != 6 && version != 7)
            {
                version = 2;
            }
            List<AbrBrush> brushes = new List<AbrBrush>();
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AbrInput input in inputs)
            {
                AbrBrush brush;
                if (!TryLoadAlpha(input, invert, autocrop, out brush, maxTipSide))
                {
                    continue;
                }
                if (brush.Width > 32767 || brush.Height > 32767)
                {
                    throw new InvalidOperationException(input.ImagePath + ": " + brush.Width + "x" + brush.Height + " 超过 ABR 尺寸上限。");
                }
                string baseName = brush.Name;
                string name = baseName;
                int counter = 2;
                while (usedNames.Contains(name))
                {
                    name = baseName + " " + counter.ToString();
                    counter++;
                }
                usedNames.Add(name);
                brush.Name = name;
                if (!string.IsNullOrEmpty(brush.GrainPath))
                {
                    // 原图纹理：把 Grain 图以 VMAL 格式内嵌为自定义 pattern（2026-08 实测 PS 接受并渲染，
                    // 参考 brushset2abr 的 build_patt_item 结构）。pattern 名用 $null（与 brushset2abr 一致，
                    // PS 能解析；自定义名曾被实测渲染为实心）。
                    try
                    {
                        brush.PatternName = "$null";
                        brush.PatternId = Guid.NewGuid().ToString();
                        brush.PatternEntry = BuildVmAlPatternEntryFromFile(brush.GrainPath, brush.PatternName, brush.PatternId);
                    }
                    catch
                    {
                        brush.PatternName = null;
                        brush.PatternId = null;
                        brush.PatternEntry = null;
                        brush.GrainPath = null;
                    }
                }
                brush.Uuid = string.IsNullOrEmpty(input.Uuid) ? Guid.NewGuid().ToString() : input.Uuid;
                brushes.Add(brush);
            }
            if (brushes.Count == 0)
            {
                throw new InvalidOperationException("没有可用的笔刷图片（所有图片都是空白）。");
            }
            using (FileStream stream = File.Create(output))
            {
                if (version == 6 || version == 7)
                {
                    WriteAbrV6Plus(stream, brushes, version, useRle);
                }
                else
                {
                    WriteInt16(stream, (short)version);
                    WriteInt16(stream, (short)brushes.Count);
                    foreach (AbrBrush brush in brushes)
                    {
                        byte[] body = BuildBrush(brush, version, useRle);
                        WriteInt16(stream, SampledBrushType);
                        WriteInt32(stream, body.Length);
                        stream.Write(body, 0, body.Length);
                    }
                }
            }
            return brushes.Count;
        }

        internal static bool TryLoadAlpha(AbrInput input, bool invert, bool autocrop, out AbrBrush brush, int maxTipSide = 0)
        {
            brush = null;
            try
            {
                string path = input.ImagePath;
                string displayName = input.DisplayName;
                int spacing = input.Spacing;
                using (Bitmap original = new Bitmap(path))
                {
                    int width = original.Width;
                    int height = original.Height;
                    bool hasAlpha = (original.Flags & (int)ImageFlags.HasAlpha) != 0;
                    using (Bitmap argb = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics graphics = Graphics.FromImage(argb))
                        {
                            graphics.DrawImage(original, 0, 0, width, height);
                        }
                        BitmapData data = argb.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        byte[] pixels;
                        try
                        {
                            int stride = data.Stride;
                            pixels = new byte[stride * height];
                            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                        }
                        finally
                        {
                            argb.UnlockBits(data);
                        }
                        bool useAlpha = hasAlpha && HasTransparentPixels(pixels, data.Stride, width, height);
                        byte[] luma = new byte[width * height];
                        int nearBlack = 0;
                        int nearWhite = 0;
                        for (int y = 0; y < height; y++)
                        {
                            int row = y * data.Stride;
                            for (int x = 0; x < width; x++)
                            {
                                int pixel = row + x * 4;
                                int red = pixels[pixel + 2];
                                int green = pixels[pixel + 1];
                                int blue = pixels[pixel];
                                byte value = (byte)((red * 299 + green * 587 + blue * 114 + 500) / 1000);
                                luma[y * width + x] = value;
                                if (value <= 32) nearBlack++;
                                else if (value >= 224) nearWhite++;
                            }
                        }
                        bool whiteBackground = false;
                        if (!useAlpha)
                        {
                            int total = width * height;
                            whiteBackground = nearWhite * 100 / total >= 60
                                && nearBlack > 0
                                && nearWhite > nearBlack * 2;
                        }
                        byte[] alpha = new byte[width * height];
                        for (int i = 0; i < alpha.Length; i++)
                        {
                            if (useAlpha)
                            {
                                alpha[i] = pixels[(i / width) * data.Stride + (i % width) * 4 + 3];
                            }
                            else if (whiteBackground)
                            {
                                alpha[i] = (byte)(255 - luma[i]);
                            }
                            else
                            {
                                alpha[i] = luma[i];
                            }
                        }
                        // 单笔刷反转 alpha 优先，未设置则跟随全局。
                        bool effectiveInvert = input.Invert.HasValue ? input.Invert.Value : invert;
                        if (effectiveInvert)
                        {
                            for (int i = 0; i < alpha.Length; i++)
                            {
                                alpha[i] = (byte)(255 - alpha[i]);
                            }
                        }
                        if (autocrop)
                        {
                            int minX = width, minY = height, maxX = -1, maxY = -1;
                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    if (alpha[y * width + x] > 0)
                                    {
                                        if (x < minX) minX = x;
                                        if (x > maxX) maxX = x;
                                        if (y < minY) minY = y;
                                        if (y > maxY) maxY = y;
                                    }
                                }
                            }
                            if (maxX < 0)
                            {
                                return false;
                            }
                            int newWidth = maxX - minX + 1;
                            int newHeight = maxY - minY + 1;
                            byte[] cropped = new byte[newWidth * newHeight];
                            for (int y = 0; y < newHeight; y++)
                            {
                                Array.Copy(alpha, (minY + y) * width + minX, cropped, y * newWidth, newWidth);
                            }
                            brush = new AbrBrush { Name = displayName, Alpha = cropped, Width = newWidth, Height = newHeight, Spacing = spacing, Angle = input.Angle, Roundness = input.Roundness, Diameter = input.Diameter, Params = input.Params, GrainPath = input.GrainPath };
                        }
                        else
                        {
                            brush = new AbrBrush { Name = displayName, Alpha = alpha, Width = width, Height = height, Spacing = spacing, Angle = input.Angle, Roundness = input.Roundness, Diameter = input.Diameter, Params = input.Params, GrainPath = input.GrainPath };
                        }
                        // 笔尖尺寸上限：超出上限时按最长边等比缩放 alpha，压缩 ABR 体积
                        if (maxTipSide > 0 && (brush.Width > maxTipSide || brush.Height > maxTipSide))
                        {
                            double scale = (double)maxTipSide / Math.Max(brush.Width, brush.Height);
                            int nw = Math.Max(1, (int)Math.Round(brush.Width * scale));
                            int nh = Math.Max(1, (int)Math.Round(brush.Height * scale));
                            brush.Alpha = ResampleAlpha(brush.Alpha, brush.Width, brush.Height, nw, nh);
                            brush.Width = nw;
                            brush.Height = nh;
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>双线性重采样 alpha 通道（缩小笔尖用）。</summary>
        private static byte[] ResampleAlpha(byte[] src, int sw, int sh, int dw, int dh)
        {
            byte[] dst = new byte[dw * dh];
            double xRatio = (double)sw / dw;
            double yRatio = (double)sh / dh;
            for (int y = 0; y < dh; y++)
            {
                double sy = (y + 0.5) * yRatio - 0.5;
                int y0 = (int)Math.Floor(sy);
                int y1 = y0 + 1;
                double fy = sy - y0;
                if (y0 < 0)
                {
                    y0 = 0;
                }
                if (y1 >= sh)
                {
                    y1 = sh - 1;
                }
                for (int x = 0; x < dw; x++)
                {
                    double sx = (x + 0.5) * xRatio - 0.5;
                    int x0 = (int)Math.Floor(sx);
                    int x1 = x0 + 1;
                    double fx = sx - x0;
                    if (x0 < 0)
                    {
                        x0 = 0;
                    }
                    if (x1 >= sw)
                    {
                        x1 = sw - 1;
                    }
                    double v = src[y0 * sw + x0] * (1 - fx) * (1 - fy)
                             + src[y0 * sw + x1] * fx * (1 - fy)
                             + src[y1 * sw + x0] * (1 - fx) * fy
                             + src[y1 * sw + x1] * fx * fy;
                    dst[y * dw + x] = (byte)Math.Round(Math.Max(0, Math.Min(255, v)));
                }
            }
            return dst;
        }

        private static bool HasTransparentPixels(byte[] pixels, int stride, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x * 4 + 3] != 255)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 智能反相检测：采样图像边缘一圈，若白色像素占比 ≥60% 则判定该形状图是"白底"风格，
        /// 需要反转 alpha 才正确（prct2sai 同类工具的同款启发式）。
        /// </summary>
        internal static bool ShouldAutoInvert(string imagePath)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(imagePath))
                {
                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    byte[] pixels;
                    int stride;
                    try
                    {
                        stride = data.Stride;
                        pixels = new byte[stride * bmp.Height];
                        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    }
                    finally
                    {
                        bmp.UnlockBits(data);
                    }

                    // 1) 带透明通道：alpha 即形状，无需反色（brushset2abr 分档，2026-08 采纳）。
                    bool hasAlpha = false;
                    long[] hist = new long[256];
                    long sum = 0;
                    long count = 0;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            int pixel = y * stride + x * 4;
                            if (pixels[pixel + 3] < 200)
                            {
                                hasAlpha = true;
                            }
                            int gray = (pixels[pixel + 2] + pixels[pixel + 1] + pixels[pixel]) / 3;
                            hist[gray]++;
                            sum += gray;
                            count++;
                        }
                    }
                    if (hasAlpha)
                    {
                        return false;
                    }
                    if (count == 0)
                    {
                        return false;
                    }

                    // 2) 近纯色（标准差<6）：偏暗→反成纯白，偏亮保持。
                    double mean = (double)sum / count;
                    double variance = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        variance += hist[i] * (i - mean) * (i - mean);
                    }
                    variance /= count;
                    if (Math.Sqrt(variance) < 6)
                    {
                        return mean < 128;
                    }

                    // 3) 有图案：看边缘，边缘白→黑形白底→反色；边缘黑→白形黑底→保持。
                    int border = Math.Max(2, Math.Min(bmp.Width, bmp.Height) / 10);
                    int white = 0;
                    int total = 0;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            bool onBorder = x < border || y < border || x >= bmp.Width - border || y >= bmp.Height - border;
                            if (!onBorder)
                            {
                                continue;
                            }
                            int pixel = y * stride + x * 4;
                            int red = pixels[pixel + 2];
                            int green = pixels[pixel + 1];
                            int blue = pixels[pixel];
                            int value = (red + green + blue) / 3;
                            total++;
                            if (value >= 224)
                            {
                                white++;
                            }
                        }
                    }
                    return total > 0 && white * 100 / total >= 60;
                }
            }
            catch
            {
                return false;
            }
        }

        internal static Bitmap BuildAlphaPreviewBitmap(string path, bool invert, bool autocrop)
        {
            AbrInput input = new AbrInput { ImagePath = path, DisplayName = "preview", Spacing = 25 };
            AbrBrush brush;
            if (!TryLoadAlpha(input, invert, autocrop, out brush))
            {
                return null;
            }
            Bitmap bmp = new Bitmap(brush.Width, brush.Height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, brush.Width, brush.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] pixels = new byte[stride * brush.Height];
                for (int y = 0; y < brush.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < brush.Width; x++)
                    {
                        int offset = row + x * 4;
                        pixels[offset] = 255;
                        pixels[offset + 1] = 255;
                        pixels[offset + 2] = 255;
                        pixels[offset + 3] = brush.Alpha[y * brush.Width + x];
                    }
                }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        private static byte[] BuildBrush(AbrBrush brush, int version, bool useRle)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                WriteInt32(stream, 0);
                WriteInt16(stream, (short)brush.Spacing);
                if (version == 2)
                {
                    WriteUnicodeString(stream, brush.Name);
                }
                bool antiAlias = brush.Width < 32 && brush.Height < 32;
                stream.WriteByte((byte)(antiAlias ? 1 : 0));
                WriteInt16(stream, 0);
                WriteInt16(stream, 0);
                WriteInt16(stream, (short)brush.Height);
                WriteInt16(stream, (short)brush.Width);
                WriteInt32(stream, 0);
                WriteInt32(stream, 0);
                WriteInt32(stream, brush.Height);
                WriteInt32(stream, brush.Width);
                WriteInt16(stream, 8);

                int rowsRemaining = brush.Height;
                int pixelOffset = 0;
                while (rowsRemaining > 0)
                {
                    int chunkHeight = Math.Min(rowsRemaining, MaxRowsPerChunk);
                    if (useRle)
                    {
                        stream.WriteByte(1);
                        List<short> rowSizes = new List<short>(chunkHeight);
                        List<byte[]> encodedRows = new List<byte[]>(chunkHeight);
                        for (int y = 0; y < chunkHeight; y++)
                        {
                            byte[] row = new byte[brush.Width];
                            Array.Copy(brush.Alpha, pixelOffset + y * brush.Width, row, 0, brush.Width);
                            byte[] encoded = EncodeRow(row);
                            rowSizes.Add((short)encoded.Length);
                            encodedRows.Add(encoded);
                        }
                        foreach (short rowSize in rowSizes)
                        {
                            WriteInt16(stream, rowSize);
                        }
                        foreach (byte[] encoded in encodedRows)
                        {
                            stream.Write(encoded, 0, encoded.Length);
                        }
                    }
                    else
                    {
                        stream.WriteByte(0);
                        stream.Write(brush.Alpha, pixelOffset, chunkHeight * brush.Width);
                    }
                    pixelOffset += chunkHeight * brush.Width;
                    rowsRemaining -= chunkHeight;
                }
                return stream.ToArray();
            }
        }

        private static byte[] EncodeRow(byte[] row)
        {
            List<byte> output = new List<byte>();
            int index = 0;
            int length = row.Length;
            while (index < length)
            {
                int run = 1;
                while (index + run < length && run < 128 && row[index + run] == row[index])
                {
                    run++;
                }
                if (run >= 3)
                {
                    output.Add((byte)(257 - run));
                    output.Add(row[index]);
                    index += run;
                    continue;
                }
                List<byte> literal = new List<byte>();
                while (index < length && literal.Count < 128)
                {
                    int run2 = 1;
                    while (index + run2 < length && run2 < 128 && row[index + run2] == row[index])
                    {
                        run2++;
                    }
                    if (run2 >= 3 && literal.Count > 0)
                    {
                        break;
                    }
                    literal.Add(row[index]);
                    index++;
                }
                output.Add((byte)(literal.Count - 1));
                output.AddRange(literal);
            }
            return output.ToArray();
        }

        /// <summary>
        /// 原图纹理：把 Grain 图以 VMAL 格式构建成内嵌 pattern 条目（2026-08 实测 PS 接受并渲染，
        /// 参考 brushset2abr 的 build_patt_item 结构）。缩到最长边 ≤512 控制体积。
        /// </summary>
        private static byte[] BuildVmAlPatternEntryFromFile(string grainPath, string name, string id)
        {
            using (Bitmap src = new Bitmap(grainPath))
            {
                Bitmap rgb = (src.PixelFormat == PixelFormat.Format24bppRgb && src.Width <= 512 && src.Height <= 512)
                    ? new Bitmap(src)
                    : DownscaleRgb(src, 512);
                try
                {
                    return BuildVmAlPatternEntry(rgb, name, id);
                }
                finally
                {
                    rgb.Dispose();
                }
            }
        }

        private static Bitmap DownscaleRgb(Bitmap src, int maxSide)
        {
            double scale = Math.Min(1.0, (double)maxSide / Math.Max(src.Width, src.Height));
            int nw = Math.Max(1, (int)(src.Width * scale));
            int nh = Math.Max(1, (int)(src.Height * scale));
            Bitmap outBmp = new Bitmap(nw, nh, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(outBmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, nw, nh);
            }
            return outBmp;
        }

        private static byte[] BuildVmAlPatternEntry(Bitmap rgb, string name, string id)
        {
            int w = rgb.Width, h = rgb.Height;
            byte[][] channels = new byte[3][];
            for (int c = 0; c < 3; c++)
            {
                channels[c] = new byte[w * h];
            }
            BitmapData bd = rgb.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = bd.Stride;
                byte[] row = new byte[stride];
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(bd.Scan0 + y * stride, row, 0, stride);
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        channels[0][idx] = row[x * 3 + 2]; // R
                        channels[1][idx] = row[x * 3 + 1]; // G
                        channels[2][idx] = row[x * 3];     // B
                    }
                }
            }
            finally
            {
                rgb.UnlockBits(bd);
            }

            using (MemoryStream vmal = new MemoryStream())
            {
                WriteInt32(vmal, 0); // top
                WriteInt32(vmal, 0); // left
                WriteInt32(vmal, h); // bottom
                WriteInt32(vmal, w); // right
                WriteInt32(vmal, 24); // depth
                for (int c = 0; c < 26; c++)
                {
                    if (c < 3)
                    {
                        byte[] data = channels[c];
                        WriteInt32(vmal, 1);
                        WriteInt32(vmal, data.Length + 23);
                        WriteInt32(vmal, 8);
                        WriteInt32(vmal, 0);
                        WriteInt32(vmal, 0);
                        WriteInt32(vmal, h);
                        WriteInt32(vmal, w);
                        WriteInt16(vmal, 8);
                        vmal.WriteByte(0);
                        vmal.Write(data, 0, data.Length);
                    }
                    else
                    {
                        WriteInt32(vmal, 0);
                    }
                }
                byte[] vmalBody = vmal.ToArray();

                using (MemoryStream body = new MemoryStream())
                {
                    WriteInt32(body, 1); // pattern version
                    WriteInt32(body, 3); // RGB
                    WriteInt16(body, 0);
                    WriteInt16(body, 0);
                    byte[] nb = Encoding.BigEndianUnicode.GetBytes(name);
                    WriteInt32(body, nb.Length / 2 + 1);
                    body.Write(nb, 0, nb.Length);
                    WriteInt16(body, 0); // name NUL
                    body.WriteByte((byte)id.Length);
                    body.Write(Encoding.ASCII.GetBytes(id), 0, id.Length);
                    WriteInt32(body, 3); // VMAL version
                    WriteInt32(body, vmalBody.Length);
                    body.Write(vmalBody, 0, vmalBody.Length);
                    return body.ToArray();
                }
            }
        }

        private static void WriteAbrV6Plus(Stream stream, List<AbrBrush> brushes, int version, bool useRle)
        {
            WriteInt16(stream, (short)version);
            WriteInt16(stream, 2);

            using (MemoryStream samp = new MemoryStream())
            {
                foreach (AbrBrush brush in brushes)
                {
                    byte[] entry = BuildSampEntry(brush, useRle);
                    samp.Write(entry, 0, entry.Length);
                }
                Write8Bim(stream, "samp", samp.ToArray());
            }

            using (MemoryStream patt = new MemoryStream())
            {
                // 原图纹理：每个引用的 pattern 条目（VMAL）内嵌一次（按条目字节去重——同名 $null 的
                // 不同 grain 靠独立 ID 区分），按名字/ID 引用。
                HashSet<string> embedded = new HashSet<string>(StringComparer.Ordinal);
                foreach (AbrBrush brush in brushes)
                {
                    if (string.IsNullOrEmpty(brush.PatternName) || brush.PatternEntry == null)
                    {
                        continue;
                    }
                    // 去重键用条目字节的哈希（所有 pattern 名都是 $null，不能按名去重）。
                    string key = Convert.ToBase64String(brush.PatternEntry);
                    if (!embedded.Add(key))
                    {
                        continue;
                    }
                    WriteInt32(patt, brush.PatternEntry.Length);
                    patt.Write(brush.PatternEntry, 0, brush.PatternEntry.Length);
                    // pattern 条目间按 4 字节对齐（与真实 patt 块一致）。
                    while ((patt.Length % 4) != 0)
                    {
                        patt.WriteByte(0);
                    }
                }
                Write8Bim(stream, "patt", patt.ToArray());
            }

            using (MemoryStream desc = new MemoryStream())
            {
                WriteInt32(desc, 16);
                WriteBrushDescriptor(desc, brushes);
                Write8Bim(stream, "desc", desc.ToArray());
            }
        }

        // ===== 纹理写入（原图纹理，2026-08 采纳 brushset2abr 的 VMAL 格式）=====
        // Procreate 原 Grain 图以 VMAL 格式内嵌为自定义 pattern 条目（无尾部校验，PS 接受并渲染），
        // 由 BuildVmAlPatternEntryFromFile 生成，替换旧的"匹配真实 pattern 近似"方案。
        // 自定义名/ID 仅作引用键，PS 不校验（2026-08 实测）。

        private static void Write8Bim(Stream stream, string key, byte[] data)
        {
            stream.Write(Encoding.ASCII.GetBytes("8BIM"), 0, 4);
            stream.Write(Encoding.ASCII.GetBytes(key), 0, 4);
            WriteInt32(stream, data.Length);
            stream.Write(data, 0, data.Length);
        }

        /// <summary>保证 ABR 里的笔刷 UUID 是标准 36 位格式；无 UUID 的笔刷（如手工构造的包）自动生成。</summary>
        public static string ValidUuid(string sourceId)
        {
            if (!string.IsNullOrEmpty(sourceId) && sourceId.Length == 36 && sourceId[8] == '-')
            {
                return sourceId;
            }
            return Guid.NewGuid().ToString();
        }

        private static byte[] BuildSampEntry(AbrBrush brush, bool useRle)
        {
            byte[] pixels = EncodePixelsV6(brush, useRle);
            int entryLen = 328 + pixels.Length;
            using (MemoryStream ms = new MemoryStream())
            {
                WriteInt32(ms, entryLen);
                byte[] uuid = Encoding.ASCII.GetBytes(brush.Uuid);
                ms.WriteByte((byte)uuid.Length);
                ms.Write(uuid, 0, uuid.Length);

                ms.WriteByte(0);
                ms.WriteByte(1);
                ms.WriteByte(0);
                ms.WriteByte(0);
                WriteInt32(ms, 3);
                WriteInt32(ms, entryLen - 49);
                WriteInt32(ms, 0);
                WriteInt32(ms, 0);
                WriteInt32(ms, brush.Height);
                WriteInt32(ms, brush.Width);
                WriteInt32(ms, 56);
                for (int i = 0; i < 220; i++)
                {
                    ms.WriteByte(0);
                }
                WriteInt32(ms, 1);
                WriteInt32(ms, entryLen - 305);
                WriteInt32(ms, 8);

                WriteInt32(ms, 0);
                WriteInt32(ms, 0);
                WriteInt32(ms, brush.Height);
                WriteInt32(ms, brush.Width);
                WriteInt16(ms, 8);
                ms.WriteByte((byte)(useRle ? 1 : 0));
                ms.Write(pixels, 0, pixels.Length);

                for (int i = 0; i < 8; i++)
                {
                    ms.WriteByte(0);
                }
                while ((ms.Length % 4) != 0)
                {
                    ms.WriteByte(0);
                }
                return ms.ToArray();
            }
        }

        private static byte[] EncodePixelsV6(AbrBrush brush, bool useRle)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                if (useRle)
                {
                    List<short> rowSizes = new List<short>(brush.Height);
                    List<byte[]> encodedRows = new List<byte[]>(brush.Height);
                    for (int y = 0; y < brush.Height; y++)
                    {
                        byte[] row = new byte[brush.Width];
                        Array.Copy(brush.Alpha, y * brush.Width, row, 0, brush.Width);
                        byte[] encoded = EncodeRow(row);
                        rowSizes.Add((short)encoded.Length);
                        encodedRows.Add(encoded);
                    }
                    foreach (short rowSize in rowSizes)
                    {
                        WriteInt16(ms, rowSize);
                    }
                    foreach (byte[] encoded in encodedRows)
                    {
                        ms.Write(encoded, 0, encoded.Length);
                    }
                }
                else
                {
                    ms.Write(brush.Alpha, 0, brush.Alpha.Length);
                }
                return ms.ToArray();
            }
        }

        private static void WriteBrushDescriptor(Stream stream, List<AbrBrush> brushes)
        {
            WriteUnicodeString(stream, "");
            WriteClassId(stream, "null");
            WriteInt32(stream, 1);
            WriteKey(stream, "Brsh");
            WriteTag(stream, "VlLs");
            WriteInt32(stream, brushes.Count);
            foreach (AbrBrush brush in brushes)
            {
                WriteTag(stream, "Objc");
                WriteUnicodeString(stream, "");
                WriteClassId(stream, "brushPreset");
                int itemCount;
                byte[] items = BuildBrushPresetItems(brush, out itemCount);
                WriteInt32(stream, itemCount);
                stream.Write(items, 0, items.Length);
            }
        }

        private static byte[] BuildBrushPresetItems(AbrBrush brush, out int itemCount)
        {
            using (MemoryStream items = new MemoryStream())
            {
                int count = 0;
                Action<Action<MemoryStream>> add = delegate(Action<MemoryStream> writer)
                {
                    writer(items);
                    count++;
                };

                // 反向跟随描边（shapeRotation<=-0.5 且无倾斜）：方向动态 bVTy=6，角度加 180° 近似
                // （brushset2abr 思路，2026-08 采纳；须在 Brsh 写入前调整 brush.Angle）。
                if (ParamMapping.Num(brush.Params, "shapeRotation") <= -0.5 &&
                    ParamMapping.Num(brush.Params, "dynamicsTiltAngle") <= Mapping.TiltZeroThreshold)
                {
                    brush.Angle += 180;
                }

                add(delegate(MemoryStream s)
                {
                    WriteKey(s, "Nm  ");
                    WriteTag(s, "TEXT");
                    WriteUnicodeString(s, brush.Name);
                });
                add(delegate(MemoryStream s)
                {
                    WriteKey(s, "Brsh");
                    WriteTag(s, "Objc");
                    WriteSampledBrushDescriptor(s, brush);
                });

                double tilt = ParamMapping.Num(brush.Params, "dynamicsTiltAngle");
                double pressureSize = ParamMapping.Num(brush.Params, "dynamicsPressureSize");
                double jitterSize = ParamMapping.Num(brush.Params, "dynamicsJitterSize");
                double pressureOpacity = ParamMapping.Num(brush.Params, "dynamicsPressureOpacity");
                double jitterOpacity = ParamMapping.Num(brush.Params, "dynamicsJitterOpacity");
                double minSize = ParamMapping.Num(brush.Params, "minSize");
                double minOpacity = ParamMapping.Num(brush.Params, "minOpacity");
                double paintOpacity = ParamMapping.Num(brush.Params, "paintOpacity");
                double scatter = ParamMapping.Num(brush.Params, "shapeScatter");
                double shapeCount = ParamMapping.Num(brush.Params, "shapeCount");
                double shapeCountJitter = ParamMapping.Num(brush.Params, "shapeCountJitter");
                double plotJitter = ParamMapping.Num(brush.Params, "plotJitter");
                double plotJitterLongitudinal = ParamMapping.Num(brush.Params, "plotJitterLongitudinal");
                double wet = ParamMapping.Num(brush.Params, "wetEdgesAmount");
                double roundness = ParamMapping.Num(brush.Params, "shapeRoundness");
                double pressureRoundnessMin = ParamMapping.Num(brush.Params, "dynamicsPressureShapeRoundnessMinimum");

                // 颜色动态（双色笔刷）：Procreate hue/sat/lightness/darkness 抖动 + 压力副色
                // → PS useColorDynamics（hueJitter/satJitter/brtJitter/colorJitter + perStroke）。
                double hueJitter = ParamMapping.Num(brush.Params, "dynamicsJitterHue");
                double satJitter = ParamMapping.Num(brush.Params, "dynamicsJitterSaturation");
                double lightnessJitter = ParamMapping.Num(brush.Params, "dynamicsJitterLightness");
                double darknessJitter = ParamMapping.Num(brush.Params, "dynamicsJitterDarkness");
                double strokeHueJitter = ParamMapping.Num(brush.Params, "dynamicsJitterStrokeHue");
                double strokeSatJitter = ParamMapping.Num(brush.Params, "dynamicsJitterStrokeSaturation");
                double strokeLightJitter = ParamMapping.Num(brush.Params, "dynamicsJitterStrokeLightness");
                double strokeDarkJitter = ParamMapping.Num(brush.Params, "dynamicsJitterStrokeDarkness");
                double pressureSecondary = ParamMapping.Num(brush.Params, "dynamicsPressureSecondaryColor");
                double colorHue = ParamMapping.Num(brush.Params, "dynamicsPressureHue");
                double colorSat = ParamMapping.Num(brush.Params, "dynamicsPressureSaturation");
                double colorLight = ParamMapping.Num(brush.Params, "dynamicsPressureBrightness");

                double maxBrightness = Math.Max(lightnessJitter, darknessJitter);
                int hueJitterPercent = Mapping.PercentFrom01(hueJitter);
                int satJitterPercent = Mapping.PercentFrom01(satJitter);
                int brtJitterPercent = Mapping.PercentFrom01(maxBrightness);
                int colorJitterPercent = (int)Math.Round(Math.Max(0, Math.Min(1, pressureSecondary)) * 100.0);
                bool useColorDynamics = hueJitterPercent > 0 || satJitterPercent > 0 || brtJitterPercent > 0 || colorJitterPercent > 0;

                // 纹理：仅匹配到真实 pattern 的笔刷写入（近似方案；无图/grainDepth=0 不加）。
                bool hasTexture = !string.IsNullOrEmpty(brush.PatternName);

                int tiltPercent = Mapping.TiltPercent(tilt);
                int jitterSizePercent = Mapping.PercentFrom01(jitterSize);
                int jitterOpacityPercent = Mapping.PercentFrom01(jitterOpacity);
                int countJitterPercent = Mapping.CountJitter(shapeCountJitter);
                int minSizePercent = Mapping.PercentFrom01(minSize);
                int minOpacityPercent = Mapping.PercentFrom01(minOpacity);

                bool sizePressure = pressureSize > 0;
                bool sizeTilt = !sizePressure && tiltPercent > 0 && ParamMapping.Num(brush.Params, "dynamicsTiltSize") > 0;
                int sizeControl = sizePressure ? Mapping.SizeControlPressure : (sizeTilt ? Mapping.SizeControlTilt : Mapping.SizeControlNone);
                double sizeCurveMin = Mapping.CurveMin(brush.Params, "dynamicsPressureSizeCurve");
                int sizeMinimum = Mapping.SizeMinimum(minSizePercent, pressureSize, sizeCurveMin);
                int sizeJitter = sizePressure || jitterSizePercent > 0 ? jitterSizePercent : 0;

                bool opacityPressure = pressureOpacity > 0;
                int opacityControl = opacityPressure ? Mapping.OpacityControlPressure : Mapping.OpacityControlNone;
                double opacityCurveMin = Mapping.CurveMin(brush.Params, "dynamicsPressureOpacityCurve");
                int opacityMinimum = Mapping.OpacityMinimum(minOpacityPercent, pressureOpacity, opacityCurveMin);
                int opacityJitter = opacityPressure || jitterOpacityPercent > 0 ? jitterOpacityPercent : 0;

                int roundnessControl = 0;
                int roundnessMinimum = Mapping.RoundnessMinimumDefault;
                if (roundness > 0 && roundness < 1 && pressureRoundnessMin > 0 && pressureRoundnessMin < 1)
                {
                    roundnessControl = Mapping.RoundnessControlEnable;
                    roundnessMinimum = Mapping.RoundnessMinimum(pressureRoundnessMin);
                }

                bool useTip = tiltPercent > 0 || pressureSize > 0 || jitterSizePercent > 0 || minSizePercent > 0;
                bool useScatter = scatter > 0 || shapeCount > 0 || plotJitter > 0 || plotJitterLongitudinal > 0;
                bool usePaint = jitterOpacityPercent > 0 || pressureOpacity > 0 || minOpacityPercent > 0;
                bool wetEdge = wet > 0;
                // 不透明度/流量语义（brushset2abr，2026-08）：paintOpacity → PbTl Opct；
                // dynamicsGlazedFlow → PbTl flow + prVr 最小流量。
                double glazedFlow = ParamMapping.Num(brush.Params, "dynamicsGlazedFlow");
                if (glazedFlow <= 0)
                {
                    glazedFlow = 1.0;
                }
                int opacityPercent = paintOpacity > 0 ? (int)Math.Round(Math.Max(0, Math.Min(1, paintOpacity)) * 100.0) : 100;
                int flowPct = (int)Math.Round(Math.Max(0, Math.Min(1, glazedFlow)) * 100.0);
                bool needPbTl = !wetEdge && (opacityPercent < 100 || flowPct < 100);
                int flowControl = (flowPct < 100 && (sizePressure || opacityPressure))
                    ? Mapping.OpacityControlPressure : Mapping.DynamicsNone;
                int tiltScale = Mapping.TiltScale(tiltPercent);
                // 散射语义分离（brushset2abr，2026-08）：shapeScatter（旋转散）→ 角度抖动；
                // plotJitter（横向/纵向位置散）→ 散射抖动；shapeRandomise 时角度抖动 +25。
                int scatterJitter = Mapping.ScatterJitter(plotJitter, plotJitterLongitudinal);
                int angleJitter = Mapping.AngleJitter(scatter);
                if (ParamMapping.Bool(brush.Params, "shapeRandomise"))
                {
                    angleJitter = Math.Min(100, angleJitter + 25);
                }
                double scatterCount = Mapping.Count(shapeCount);
                bool bothAxes = plotJitterLongitudinal > 0.05;

                // 纯笔刷模式：只导图案/间距/大小，不映射任何其他参数（角度/圆度/动态/纹理/湿边/颜色/flow 全关）。
                if (AppSettings.Pure)
                {
                    useTip = false;
                    useScatter = false;
                    usePaint = false;
                    wetEdge = false;
                    hasTexture = false;
                    useColorDynamics = false;
                    needPbTl = false;
                    brush.Angle = 0;
                    brush.Roundness = 100;
                }

                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "useTipDynamics", useTip);
                });
                if (useTip)
                {
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "flipX", false);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "flipY", false);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "brushProjection", false);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "minimumDiameter", sizeMinimum);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "minimumRoundness", roundnessMinimum);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "tiltScale", tiltScale);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "szVr", sizeControl, Mapping.DynamicsSteps, sizeJitter, sizeMinimum);
                    });
                    // 角度动态：完整输入样式映射（倾斜→3，方位/侧旋→3，跟随→6，旋转→8，反向跟随→6+180°）。
                    bool angleHalfTurn;
                    int angleControl = Mapping.AngleControl(brush.Params, tiltPercent, out angleHalfTurn);
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "angleDynamics", angleControl, Mapping.DynamicsSteps, angleJitter, 0);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "roundnessDynamics", roundnessControl, Mapping.DynamicsSteps, 0, 0);
                    });
                }
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "useScatter", useScatter);
                });
                // 真实 PS：useScatter 后紧跟顶层 Spcn（间距），再是散射子项
                add(delegate(MemoryStream s)
                {
                    WriteUnitPercent(s, "Spcn", brush.Spacing > 0 ? brush.Spacing : Mapping.SpacingDefault);
                });
                if (useScatter)
                {
                    add(delegate(MemoryStream s)
                    {
                        WriteDouble(s, "Cnt ", scatterCount);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "bothAxes", bothAxes);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "countDynamics", Mapping.DynamicsNone, Mapping.DynamicsSteps, countJitterPercent, 0);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "scatterDynamics", Mapping.DynamicsNone, Mapping.DynamicsSteps, scatterJitter, 0);
                    });
                }
                add(delegate(MemoryStream s)
                {
                    WriteObjcBool(s, "dualBrush", "dualBrush", "useDualBrush", false);
                });
                add(delegate(MemoryStream s)
                {
                    WriteObjcBool(s, "brushGroup", "brushGroup", "useBrushGroup", false);
                });
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "useTexture", hasTexture);
                });
                if (hasTexture)
                {
                    // 真实 PS：useTexture 后紧跟全部纹理项（顺序对齐真实样本，Kyle 墨水盒/油画）。
                    double texScale = ParamMapping.Num(brush.Params, "textureScale");
                    double grainDepth = ParamMapping.Num(brush.Params, "grainDepth");
                    double texBrightness = ParamMapping.Num(brush.Params, "textureBrightness");
                    double texContrast = ParamMapping.Num(brush.Params, "textureContrast");
                    bool texInverted = ParamMapping.Bool(brush.Params, "textureInverted");
                    string patternName = brush.PatternName;
                    string patternId = brush.PatternId;
                    string grainBlend = Mapping.GrainBlendAbr(ParamMapping.Num(brush.Params, "grainBlendMode")); // 纹理可见；Hght 实测变实色透明，弃用
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "TxtC", true);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "interpretation", true);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteEnum(s, "textureBlendMode", "BlnM", grainBlend);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "textureDepth", Mapping.TextureDepth(grainDepth));
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "minimumDepth", 0);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "textureDepthDynamics", Mapping.DynamicsNone, Mapping.DynamicsSteps, 0, 0);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteObjcPatternReference(s, patternName, patternId);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "textureScale", Mapping.TextureScale(texScale));
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "InvT", texInverted);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBoolKey(s, "protectTexture", false);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteLong(s, "textureBrightness", Mapping.TextureBrightness(texBrightness));
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteLong(s, "textureContrast", Mapping.TextureContrast(texContrast));
                    });
                }
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "usePaintDynamics", usePaint);
                });
                if (usePaint)
                {
                    // 真实 PS 油漆动态顺序：prVr(流量) → opVr(透明度) → wtVr(湿度) → mxVr(混合)
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "prVr", flowControl, Mapping.DynamicsSteps, 0, flowPct);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "opVr", opacityControl, Mapping.DynamicsSteps, opacityJitter, opacityMinimum);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "wtVr", Mapping.DynamicsNone, Mapping.DynamicsSteps, 0, 0);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "mxVr", Mapping.DynamicsNone, Mapping.DynamicsSteps, 0, 0);
                    });
                }
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "useColorDynamics", useColorDynamics);
                });
                if (useColorDynamics)
                {
                    // 真实 PS 颜色动态子项（用户手工全参数笔刷逆向，2026-08-10）：
                    // clVr(brVr 前景/背景) → H(色相) → Strt(饱和度) → Brgh(亮度) → purity → colorDynamicsPerTip
                    add(delegate(MemoryStream s)
                    {
                        WriteBrVr(s, "clVr", pressureSecondary > 0 ? Mapping.OpacityControlPressure : Mapping.DynamicsNone, Mapping.DynamicsSteps, colorJitterPercent, 0);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "H   ", hueJitterPercent);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "Strt", satJitterPercent);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "Brgh", brtJitterPercent);
                    });
                    add(delegate(MemoryStream s)
                    {
                        WriteUnitPercent(s, "purity", 0);
                    });
                    add(delegate(MemoryStream s)
                    {
                        // 真实 PS 双色笔刷 colorDynamicsPerTip=true（应用到每个笔尖）；false 会把颜色动态
                        // 锁定成整笔一个颜色，压力副色就切不动。2026-08 修正。
                        WriteBoolKey(s, "colorDynamicsPerTip", true);
                    });
                }
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "Wtdg", wetEdge);
                });
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "Nose", false);
                });
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "Rpt ", false);
                });
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "useBrushSize", false);
                });
                add(delegate(MemoryStream s)
                {
                    WriteBoolKey(s, "useBrushPose", false);
                });
                if (wetEdge)
                {
                    add(delegate(MemoryStream s)
                    {
                        WriteToolOptionsMixB(s, sizeJitter);
                    });
                }
                else if (needPbTl)
                {
                    add(delegate(MemoryStream s)
                    {
                        WriteToolOptionsPbTl(s, flowPct, opacityPercent);
                    });
                }

                itemCount = count;
                return items.ToArray();
            }
        }

        private static void WriteLong(Stream stream, string key, long value)
        {
            WriteKey(stream, key);
            WriteTag(stream, "long");
            WriteInt32(stream, (int)value);
        }

        private static void WriteDouble(Stream stream, string key, double value)
        {
            WriteKey(stream, key);
            WriteTag(stream, "doub");
            byte[] bits = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bits);
            }
            stream.Write(bits, 0, 8);
        }

        private static void WriteUnitPercent(Stream stream, string key, double value)
        {
            WriteKey(stream, key);
            WriteTag(stream, "UntF");
            WriteUnit(stream, "#Prc", value);
        }

        private static void WriteEnum(Stream stream, string key, string enumType, string enumValue)
        {
            WriteKey(stream, key);
            WriteTag(stream, "enum");
            WriteClassId(stream, enumType);
            WriteClassId(stream, enumValue);
        }

        private static void WriteBrVr(Stream stream, string key, long bVTy, long fStp, double jitter, double minimum)
        {
            WriteKey(stream, key);
            WriteTag(stream, "Objc");
            WriteUnicodeString(stream, "");
            WriteClassId(stream, "brVr");
            WriteInt32(stream, 4);
            WriteLong(stream, "bVTy", bVTy);
            WriteLong(stream, "fStp", fStp);
            WriteUnitPercent(stream, "jitter", jitter);
            WriteUnitPercent(stream, "Mnm ", minimum);
        }

        private static void WriteToolOptionsMixB(Stream stream, double sizeJitter)
        {
            WriteKey(stream, "toolOptions");
            WriteTag(stream, "Objc");
            WriteUnicodeString(stream, "");
            WriteClassId(stream, "MixB");
            WriteInt32(stream, 19);
            WriteLong(stream, "flow", (long)Mapping.WetFlow);
            WriteDouble(stream, "wetness", Mapping.WetWetness);
            WriteDouble(stream, "dryness", Mapping.WetDryness);
            WriteDouble(stream, "mix", Mapping.WetMix);
            WriteBrVr(stream, "prVr", Mapping.DynamicsNone, Mapping.DynamicsSteps, 0, 0);
            WriteBrVr(stream, "wtVr", Mapping.DynamicsNone, Mapping.DynamicsSteps, 0, 0);
            WriteBrVr(stream, "mxVr", Mapping.DynamicsNone, Mapping.DynamicsSteps, 0, 0);
            WriteEnum(stream, "Md  ", "BlnM", "Nrml");
            WriteLong(stream, "Opct", (long)Mapping.MixBOpacity);
            WriteBrVr(stream, "szVr", Mapping.DynamicsNone, Mapping.DynamicsSteps, sizeJitter, 0);
            WriteBoolKey(stream, "smoothing", true);
            WriteBoolKey(stream, "usePressureOverridesSize", false);
            WriteBoolKey(stream, "usePressureOverridesOpacity", false);
            WriteBoolKey(stream, "useLegacy", false);
            WriteBoolKey(stream, "autoFill", false);
            WriteBoolKey(stream, "autoClean", false);
            WriteBoolKey(stream, "loadSolidColorOnly", true);
            WriteBoolKey(stream, "sampleAllLayers", false);
            WriteBoolKey(stream, "brushPreset", true);
        }

        private static void WriteToolOptionsPbTl(Stream stream, double flowValue, int opacityPercent)
        {
            WriteKey(stream, "toolOptions");
            WriteTag(stream, "Objc");
            WriteUnicodeString(stream, "");
            WriteClassId(stream, "PbTl");
            WriteInt32(stream, 15);
            WriteBoolKey(stream, "brushPreset", true);
            WriteLong(stream, "flow", (long)flowValue);
            WriteLong(stream, "Smoo", 0);
            WriteEnum(stream, "Md  ", "BlnM", "Nrml");
            WriteLong(stream, "Opct", opacityPercent);
            WriteBoolKey(stream, "smoothing", true);
            WriteLong(stream, "smoothingValue", 0);
            WriteBoolKey(stream, "smoothingRadiusMode", false);
            WriteBoolKey(stream, "smoothingCatchup", true);
            WriteBoolKey(stream, "smoothingCatchupAtEnd", false);
            WriteBoolKey(stream, "smoothingZoomCompensation", true);
            WriteBoolKey(stream, "pressureSmoothing", false);
            WriteBoolKey(stream, "usePressureOverridesSize", false);
            WriteBoolKey(stream, "usePressureOverridesOpacity", false);
            WriteBoolKey(stream, "useLegacy", false);
        }

        private static void WriteSampledBrushDescriptor(Stream stream, AbrBrush brush)
        {
            WriteUnicodeString(stream, "");
            WriteClassId(stream, "sampledBrush");
            WriteInt32(stream, 9);

            double diameter = brush.Diameter > 0 ? brush.Diameter : ParamMapping.DefaultDiameter(brush);
            WriteKey(stream, "Dmtr");
            WriteTag(stream, "UntF");
            WriteUnit(stream, "#Pxl", diameter);

            WriteKey(stream, "Angl");
            WriteTag(stream, "UntF");
            WriteUnit(stream, "#Ang", brush.Angle);

            double roundness = brush.Roundness <= 0 ? Mapping.RoundnessDefault : brush.Roundness;
            WriteKey(stream, "Rndn");
            WriteTag(stream, "UntF");
            WriteUnit(stream, "#Prc", roundness);

            WriteKey(stream, "Nm  ");
            WriteTag(stream, "TEXT");
            WriteUnicodeString(stream, brush.Name);

            WriteKey(stream, "Spcn");
            WriteTag(stream, "UntF");
            WriteUnit(stream, "#Prc", brush.Spacing);

            WriteKey(stream, "Intr");
            WriteTag(stream, "bool");
            WriteBool(stream, true);

            WriteKey(stream, "flipX");
            WriteTag(stream, "bool");
            WriteBool(stream, false);

            WriteKey(stream, "flipY");
            WriteTag(stream, "bool");
            WriteBool(stream, false);

            WriteKey(stream, "sampledData");
            WriteTag(stream, "TEXT");
            WriteUnicodeString(stream, brush.Uuid);
        }

        private static void WriteBoolKey(Stream stream, string key, bool value)
        {
            WriteKey(stream, key);
            WriteTag(stream, "bool");
            WriteBool(stream, value);
        }

        private static void WriteObjcBool(Stream stream, string key, string classId, string innerKey, bool value)
        {
            WriteKey(stream, key);
            WriteTag(stream, "Objc");
            WriteUnicodeString(stream, "");
            WriteClassId(stream, classId);
            WriteInt32(stream, 1);
            WriteKey(stream, innerKey);
            WriteTag(stream, "bool");
            WriteBool(stream, value);
        }

        private static void WriteObjcPatternReference(Stream stream, string patternName, string patternId)
        {
            // Txtr = Ptrn{Nm, Idnt}：引用 patt 块内嵌 pattern，名称/ID 与 pattern 条目一致。
            WriteKey(stream, "Txtr");
            WriteTag(stream, "Objc");
            WriteUnicodeString(stream, "");
            WriteClassId(stream, "Ptrn");
            WriteInt32(stream, 2);
            WriteKey(stream, "Nm  ");
            WriteTag(stream, "TEXT");
            WriteUnicodeString(stream, patternName);
            WriteKey(stream, "Idnt");
            WriteTag(stream, "TEXT");
            WriteUnicodeString(stream, patternId);
        }

        private static void WriteKey(Stream stream, string key)
        {
            WriteClassId(stream, key);
        }

        private static void WriteClassId(Stream stream, string id)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(id);
            if (id.Length == 4)
            {
                WriteInt32(stream, 0);
                stream.Write(bytes, 0, 4);
            }
            else
            {
                WriteInt32(stream, id.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private static void WriteTag(Stream stream, string tag)
        {
            stream.Write(Encoding.ASCII.GetBytes(tag), 0, 4);
        }

        private static void WriteBool(Stream stream, bool value)
        {
            stream.WriteByte((byte)(value ? 1 : 0));
        }

        private static void WriteUnit(Stream stream, string unit, double value)
        {
            stream.Write(Encoding.ASCII.GetBytes(unit), 0, 4);
            byte[] bits = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bits);
            }
            stream.Write(bits, 0, 8);
        }

        private static void WriteUnicodeString(Stream stream, string value)
        {
            WriteInt32(stream, value.Length + 1);
            foreach (char character in value)
            {
                WriteInt16(stream, (short)character);
            }
            WriteInt16(stream, 0);
        }

        private static void WriteInt16(Stream stream, short value)
        {
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)(value & 0xff));
        }

        private static void WriteInt32(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)(value & 0xff));
        }
    }
}
