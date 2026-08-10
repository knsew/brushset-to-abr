using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BrushStudio
{
    /// <summary>标准笔划渲染参数（行为尺子）。</summary>
    internal sealed class StrokeParams
    {
        public double SpacingPercent = 25;
        public double Size = 60;
        public double SizePressure;   // 0-1：压力对大小的调制强度
        public double SizeMin;        // 0-1：最小大小（压力 0 时）
        public double OpacityPressure;// 0-1：压力对透明度的调制强度
        public double OpacityMin;     // 0-1：最小透明度
        public double Angle;
        public double Roundness = 1;  // 1 = 正圆
        public double ScatterPx;      // 散射幅度（像素），0 = 不散射
        public double Count = 1;      // 每站盖章数（1-5）
    }

    /// <summary>盖章模拟器：用固定压力曲线渲染标准笔划，作为 Procreate vs ABR 的行为对比尺。
    /// 两端用同一引擎，仅参数来源不同（Procreate 原始参数 vs 转换后 ABR 参数）。</summary>
    internal static class StrokeSimulator
    {
        public const int StrokeLength = 640;
        public const int StrokeHeight = 360;

        public static Bitmap Render(Bitmap shape, StrokeParams p)
        {
            if (shape != null)
            {
                // BuildAlphaPreviewBitmap 输出白色像素+alpha，在白底对比图上不可见；统一黑化为深色笔划
                Blacken(shape);
            }
            Bitmap bmp = new Bitmap(StrokeLength, StrokeHeight, PixelFormat.Format32bppArgb);
            double spacing = 0;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                spacing = Math.Max(4, p.Size * p.SpacingPercent / 100.0);
                double x = p.Size * 0.6;
                // 固定种子保证两端同参数渲染完全一致，可复现对比
                Random rng = new Random(20260810);
                int countStamps = Math.Max(1, (int)Math.Round(p.Count));
                while (x < StrokeLength - p.Size * 0.6)
                {
                    double pressure = PressureAt(x / StrokeLength);
                    double sizeFactor = p.SizePressure > 0
                        ? (p.SizeMin + (1 - p.SizeMin) * pressure)
                        : 1;
                    double size = Math.Max(2, p.Size * sizeFactor);
                    double alpha = 1;
                    if (p.OpacityPressure > 0)
                    {
                        alpha = p.OpacityMin + (1 - p.OpacityMin) * pressure;
                    }
                    else if (p.OpacityMin > 0)
                    {
                        alpha = p.OpacityMin;   // 静态最小透明度（无压力调制时整支半透明）
                    }
                    for (int c = 0; c < countStamps; c++)
                    {
                        double jx = 0, jy = 0;
                        if (p.ScatterPx > 0)
                        {
                            // 圆形均匀散射：幅度为 ScatterPx 像素，但钳制在画布内保证可见
                            double maxOff = Math.Max(0, StrokeHeight / 2f - p.Size * 0.55);
                            double ang = rng.NextDouble() * 2 * Math.PI;
                            double rad = Math.Min(p.ScatterPx, maxOff) * Math.Sqrt(rng.NextDouble());
                            jx = rad * Math.Cos(ang);
                            jy = rad * Math.Sin(ang);
                        }
                        double sx = x + jx;
                        double sy = StrokeHeight / 2f + jy;
                        if (sx < -p.Size || sx > StrokeLength + p.Size)
                        {
                            continue;
                        }
                        GraphicsState state = g.Save();
                        g.TranslateTransform((float)sx, (float)sy);
                        g.RotateTransform((float)p.Angle);
                        g.ScaleTransform((float)p.Roundness, 1f);
                        Rectangle dest = new Rectangle((int)(-size / 2), (int)(-size / 2), (int)size, (int)size);
                        if (alpha >= 1)
                        {
                            g.DrawImage(shape, dest, 0, 0, shape.Width, shape.Height, GraphicsUnit.Pixel);
                        }
                        else
                        {
                            ColorMatrix cm = new ColorMatrix();
                            cm.Matrix33 = (float)Math.Max(0, Math.Min(1, alpha));
                            using (ImageAttributes ia = new ImageAttributes())
                            {
                                ia.SetColorMatrix(cm);
                                g.DrawImage(shape, dest, 0, 0, shape.Width, shape.Height, GraphicsUnit.Pixel, ia);
                            }
                        }
                        g.Restore(state);
                    }
                    x += spacing;
                }
            }
            return bmp;
        }

        /// <summary>把位图像素 RGB 置零、保留 alpha，使笔划在白底对比图上可见。</summary>
        private static void Blacken(Bitmap shape)
        {
            System.Drawing.Imaging.BitmapData bd = shape.LockBits(new Rectangle(0, 0, shape.Width, shape.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] px = new byte[bd.Stride * shape.Height];
                System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, px, 0, px.Length);
                for (int y = 0; y < shape.Height; y++)
                {
                    int row = y * bd.Stride;
                    for (int x = 0; x < shape.Width; x++)
                    {
                        int off = row + x * 4;
                        px[off] = 0;
                        px[off + 1] = 0;
                        px[off + 2] = 0;
                    }
                }
                System.Runtime.InteropServices.Marshal.Copy(px, 0, bd.Scan0, px.Length);
            }
            finally
            {
                shape.UnlockBits(bd);
            }
        }

        /// <summary>由 Procreate 笔刷参数生成行为参数（UI 预览与行为尺子共用）。Size 由调用方指定。</summary>
        public static StrokeParams FromProcreate(ExtractedBrush brush)
        {
            StrokeParams sp = new StrokeParams();
            Dictionary<string, object> p = brush.Params;
            sp.SpacingPercent = brush.EffectiveSpacingPercent;
            sp.Angle = brush.EffectiveAngleDegrees();
            sp.Roundness = brush.EffectiveRoundnessPercent() / 100.0;
            double pressureSize = ParamMapping.Num(p, "dynamicsPressureSize");
            double pressureOpacity = ParamMapping.Num(p, "dynamicsPressureOpacity");
            double minSize = ParamMapping.Num(p, "minSize");
            double minOpacity = ParamMapping.Num(p, "minOpacity");
            sp.SizePressure = pressureSize > 0 ? 1 : 0;
            sp.SizeMin = Math.Max(minSize, pressureSize > 0 ? (1 - pressureSize) : 0);
            sp.OpacityPressure = pressureOpacity > 0 ? 1 : 0;
            double paintOpacity = ParamMapping.Num(p, "paintOpacity");
            if (sp.OpacityPressure > 0)
            {
                // 压力透明度动态：下限取 minOpacity 与压力范围下限的较大者
                sp.OpacityMin = Math.Max(minOpacity, 1 - pressureOpacity);
            }
            else
            {
                // 静态不透明度：paintOpacity（笔刷整体不透明度）优先，否则静态 minOpacity
                sp.OpacityMin = paintOpacity > 0 ? Math.Max(0, Math.Min(1, paintOpacity)) : minOpacity;
            }
            double scatter = ParamMapping.Num(p, "shapeScatter");
            double plotJitter = ParamMapping.Num(p, "plotJitter");
            double shapeCount = ParamMapping.Num(p, "shapeCount");
            sp.ScatterPx = Mapping.ScatterJitter(scatter, plotJitter);
            sp.Count = Mapping.ScatterCount(shapeCount);
            return sp;
        }

        /// <summary>渲染一支笔刷的标准笔划预览（透明背景黑笔划）。无笔尖图返回 null。</summary>
        public static Bitmap RenderBrushPreview(ExtractedBrush brush, double baseSize, bool invert = false)
        {
            string path = brush.ShapePath ?? brush.GrainPath;
            if (path == null || !File.Exists(path))
            {
                return null;
            }
            using (Bitmap full = AbrWriter.BuildAlphaPreviewBitmap(path, invert, false) ?? new Bitmap(path))
            {
                // 大图（可能 2048×2048）先缩到预览尺寸，盖章才快；预览/对比两端用同一缩图，公平
                using (Bitmap shape = DownscaleTo(full, PreviewShapeMaxSide))
                {
                    StrokeParams sp = FromProcreate(brush);
                    sp.Size = baseSize;
                    return Render(shape, sp);
                }
            }
        }

        private const int PreviewShapeMaxSide = 256;

        private static Bitmap DownscaleTo(Bitmap source, int maxSide)
        {
            int w = source.Width;
            int h = source.Height;
            if (w <= maxSide && h <= maxSide)
            {
                return new Bitmap(source);
            }
            double scale = Math.Min(maxSide / (double)w, maxSide / (double)h);
            int nw = Math.Max(1, (int)Math.Round(w * scale));
            int nh = Math.Max(1, (int)Math.Round(h * scale));
            Bitmap result = new Bitmap(nw, nh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, nw, nh);
            }
            return result;
        }

        /// <summary>标准压力曲线：从 0.15 线性升到 1.0，再降到 0.15。</summary>
        private static double PressureAt(double t)
        {
            if (t < 0.5)
            {
                return 0.15 + 0.85 * (t * 2.0);
            }
            return 1.0 - 0.85 * ((t - 0.5) * 2.0);
        }
    }

    /// <summary>并排对比工具：对同一支笔刷，用 Procreate 语义与 ABR 行为分别渲染标准笔划并排输出。
    /// CLI：--render-compare 输出目录 文件.brushset [--brush 名字子串]</summary>
    internal static class RenderCompareTool
    {
        // 标准笔划统一笔尖基准尺寸，隔离"绝对大小"，只对比动态行为（压力/透明度调制）
        private const double BaseSize = 80;

        public static int Run(string[] args)
        {
            string outputDir = args.Length > 1 ? args[1] : "render-compare";
            string brushsetPath = args.Length > 2 ? args[2] : null;
            string brushFilter = "";
            for (int i = 3; i < args.Length - 1; i++)
            {
                if (args[i] == "--brush" && i + 1 < args.Length)
                {
                    brushFilter = args[i + 1];
                }
            }
            if (brushsetPath == null || !File.Exists(brushsetPath))
            {
                Console.WriteLine("render-compare: missing brushset file");
                return 1;
            }

            string temp = Path.Combine(Path.GetTempPath(), "RenderCompare-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                List<ExtractedBrush> brushes = BrushsetExtractor.Extract(brushsetPath, temp, false);
                if (brushes.Count == 0)
                {
                    Console.WriteLine("render-compare: no brushes extracted");
                    return 1;
                }
                // 转换到临时 ABR 并读回，获得 ABR 侧实际动态参数
                string abrPath = Path.Combine(temp, "compare.abr");
                int brushCount;
                BrushsetConverter.ConvertToAbr(abrPath, new List<string> { brushsetPath }, 7, true, false, true, out brushCount, p => { });
                List<AbrBrush> abrBrushes = AbrReader.ReadBrushes(abrPath);
                // 转换端与读回端用同一过滤规则（无 Shape/Grain 图跳过），顺序一一对应。
                // 预计算每个笔刷在 ABR 全量里的索引：--brush 名字过滤不能破坏该对应关系，
                // 也不能用名字匹配（包内存在多支同名笔刷）。
                Dictionary<ExtractedBrush, int> abrIndexMap = new Dictionary<ExtractedBrush, int>();
                int abrIndex = 0;
                foreach (ExtractedBrush b in brushes)
                {
                    string bShape = b.ShapePath ?? b.GrainPath;
                    if (bShape == null || !File.Exists(bShape))
                    {
                        continue;
                    }
                    abrIndexMap[b] = abrIndex++;
                }

                Directory.CreateDirectory(outputDir);
                List<string> indexLines = new List<string>();
                indexLines.Add("# 行为对比（标准笔划：Procreate 语义 vs ABR 行为）");
                indexLines.Add("");
                indexLines.Add("来源：" + brushsetPath);
                indexLines.Add("");

                int rendered = 0;
                foreach (ExtractedBrush brush in brushes)
                {
                    string name = brush.Name ?? "";
                    if (brushFilter.Length > 0 && name.IndexOf(brushFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    string shapePath = brush.ShapePath ?? brush.GrainPath;
                    if (shapePath == null || !File.Exists(shapePath))
                    {
                        continue;
                    }
                    AbrBrush abr = null;
                    int brushAbrIndex;
                    if (abrIndexMap.TryGetValue(brush, out brushAbrIndex) && brushAbrIndex < abrBrushes.Count)
                    {
                        abr = abrBrushes[brushAbrIndex];
                    }
                    StrokeParams pro = ProcreateParams(brush);
                    StrokeParams abrP = AbrParams(abr, brush);
                    using (Bitmap shape = AbrWriter.BuildAlphaPreviewBitmap(shapePath, false, false) ?? new Bitmap(shapePath))
                    {
                        Bitmap left = StrokeSimulator.Render(shape, pro);
                        Bitmap right = StrokeSimulator.Render(shape, abrP);
                        Bitmap sheet = Compose(left, right, name, pro, abrP);
                        string file = PathUtil.Sanitize(name) + ".png";
                        sheet.Save(Path.Combine(outputDir, file), ImageFormat.Png);
                        left.Dispose();
                        right.Dispose();
                        sheet.Dispose();
                    }
                    indexLines.Add("- [" + name + "](" + PathUtil.Sanitize(name) + ".png)  `" + FormatParams(pro) + "` vs `" + FormatParams(abrP) + "`");
                    rendered++;
                }

                indexLines.Add("");
                indexLines.Add("共渲染 " + rendered + " 支。");
                File.WriteAllText(Path.Combine(outputDir, "index.md"), string.Join(Environment.NewLine, indexLines.ToArray()), new UTF8Encoding(false));
                Console.WriteLine("render-compare: rendered " + rendered + " sheets -> " + outputDir);
                return rendered > 0 ? 0 : 1;
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

        /// <summary>UI 用：渲染单支笔刷的"Procreate 语义 vs ABR 行为"对比图（临时转换→读回→并排渲染）。</summary>
        public static Bitmap RenderSingleCompare(ExtractedBrush brush, string brushsetPath)
        {
            string temp = Path.Combine(Path.GetTempPath(), "RenderCompare-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                string abrPath = Path.Combine(temp, "compare.abr");
                int count;
                int gen = BrushsetConverter.ConvertToAbr(abrPath, new List<string> { brushsetPath }, 7, true, false, true, out count, null, null, new HashSet<string> { brush.SourceId });
                if (gen <= 0)
                {
                    return null;
                }
                List<AbrBrush> abrBrushes = AbrReader.ReadBrushes(abrPath);
                if (abrBrushes == null || abrBrushes.Count == 0)
                {
                    return null;
                }
                AbrBrush abr = abrBrushes[0];
                StrokeParams pro = ProcreateParams(brush);
                StrokeParams abrP = AbrParams(abr, brush);
                string shapePath = brush.ShapePath ?? brush.GrainPath;
                if (shapePath == null || !File.Exists(shapePath))
                {
                    return null;
                }
                using (Bitmap shape = AbrWriter.BuildAlphaPreviewBitmap(shapePath, false, false) ?? new Bitmap(shapePath))
                {
                    using (Bitmap left = StrokeSimulator.Render(shape, pro))
                    using (Bitmap right = StrokeSimulator.Render(shape, abrP))
                    {
                        return Compose(left, right, brush.EffectiveName ?? brush.Name, pro, abrP);
                    }
                }
            }
            catch
            {
                return null;
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

        private static StrokeParams ProcreateParams(ExtractedBrush brush)
        {
            StrokeParams sp = StrokeSimulator.FromProcreate(brush);
            sp.Size = BaseSize;
            return sp;
        }

        private static StrokeParams AbrParams(AbrBrush abr, ExtractedBrush brush)
        {
            StrokeParams sp = new StrokeParams();
            if (abr == null)
            {
                // ABR 读回失败时退化为 Procreate 侧参数，便于对照
                return ProcreateParams(brush);
            }
            sp.SpacingPercent = abr.Spacing > 0 ? abr.Spacing : 25;
            sp.Size = BaseSize;
            sp.Angle = abr.Angle;
            sp.Roundness = abr.Roundness > 0 ? abr.Roundness / 100.0 : 1;
            if (abr.Descriptor != null)
            {
                Dictionary<string, object> items = abr.Descriptor.Items;
                AbrReader.AbrDescriptor szVr = GetDescriptor(items, "szVr");
                if (szVr != null)
                {
                    double bVTy = GetNumber(szVr.Items, "bVTy");
                    double mnm = GetNumber(szVr.Items, "Mnm ");
                    if (bVTy == 2)
                    {
                        sp.SizePressure = 1;
                        sp.SizeMin = Math.Max(0, Math.Min(1, mnm / 100.0));
                    }
                    else if (mnm > 0)
                    {
                        // 静态最小尺寸（未被压力调制）：随转换写入了 Mnm，表达出来供对照
                        sp.SizeMin = Math.Max(0, Math.Min(1, mnm / 100.0));
                    }
                }
                AbrReader.AbrDescriptor opVr = GetDescriptor(items, "opVr");
                if (opVr != null)
                {
                    double bVTy = GetNumber(opVr.Items, "bVTy");
                    double mnm = GetNumber(opVr.Items, "Mnm ");
                    if (bVTy == 2)
                    {
                        sp.OpacityPressure = 1;
                        sp.OpacityMin = Math.Max(0, Math.Min(1, mnm / 100.0));
                    }
                }
                if (sp.OpacityPressure <= 0)
                {
                    // 静态半透明：真实 PS 用 toolOptions PbTl 的 flow 表达（Opct 恒 100 不进 ABR）
                    // flow=100（或未写 flow）→ 全不透明，读回按 100% 对齐 Procreate 侧 paintOpacity=1。
                    AbrReader.AbrDescriptor tool = GetDescriptor(items, "toolOptions");
                    double flow = tool != null ? GetNumber(tool.Items, "flow") : 0;
                    if (flow > 0 && flow < 100)
                    {
                        sp.OpacityMin = Math.Max(0, Math.Min(1, flow / 100.0));
                    }
                    else
                    {
                        sp.OpacityMin = 1.0;
                    }
                }
                AbrReader.AbrDescriptor scatterDyn = GetDescriptor(items, "scatterDynamics");
                if (scatterDyn != null)
                {
                    sp.ScatterPx = GetNumber(scatterDyn.Items, "jitter");
                    sp.Count = Math.Max(1, GetNumber(items, "Cnt "));
                }
            }
            return sp;
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

        private static Bitmap Compose(Bitmap left, Bitmap right, string name, StrokeParams lp, StrokeParams rp)
        {
            int pad = 12;
            int titleH = 34;
            int metaH = 24;
            int cellW = left.Width;
            int cellH = left.Height;
            int w = pad * 2 + cellW * 2 + 20;
            int h = pad * 2 + titleH + cellH + metaH * 2;
            Bitmap sheet = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(sheet))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                Font nameFont = Theme.UiFont(10f, FontStyle.Bold);
                Font captionFont = Theme.UiFont(8.5f);
                Rectangle nameRect = new Rectangle(pad, pad, w - pad * 2, titleH);
                TextRenderer.DrawText(g, name, nameFont, nameRect, Color.FromArgb(29, 29, 31), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                int y = pad + titleH;
                TextRenderer.DrawText(g, "Procreate 语义", captionFont, new Rectangle(pad, y, cellW, 18), Color.FromArgb(10, 132, 255), TextFormatFlags.Left);
                TextRenderer.DrawText(g, "ABR 行为", captionFont, new Rectangle(pad + cellW + 20, y, cellW, 18), Color.FromArgb(48, 209, 88), TextFormatFlags.Left);
                y += 18;
                g.DrawImage(left, pad, y, cellW, cellH);
                g.DrawImage(right, pad + cellW + 20, y, cellW, cellH);
                y += cellH;
                TextRenderer.DrawText(g, "P: " + FormatParams(lp), captionFont, new Rectangle(pad, y, w - pad * 2, metaH), Color.FromArgb(110, 110, 115), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                y += metaH;
                TextRenderer.DrawText(g, "A: " + FormatParams(rp), captionFont, new Rectangle(pad, y, w - pad * 2, metaH), Color.FromArgb(110, 110, 115), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            return sheet;
        }

        private static string FormatParams(StrokeParams sp)
        {
            return "spacing " + sp.SpacingPercent.ToString("0") +
                   " · size " + sp.Size.ToString("0") +
                   " · sizeP " + sp.SizePressure.ToString("0.0") + " / min " + (sp.SizeMin * 100).ToString("0") + "%" +
                   " · opP " + sp.OpacityPressure.ToString("0.0") + " / min " + (sp.OpacityMin * 100).ToString("0") + "%" +
                   " · scat " + sp.ScatterPx.ToString("0") + "px / cnt " + sp.Count.ToString("0");
        }
    }
}
