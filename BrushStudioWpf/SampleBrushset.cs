using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;

namespace BrushStudio
{
    /// <summary>内置演示笔刷包：动态生成一个含 3 支笔尖的 .brushset（供"试试示例笔刷"与 --test-sample 使用）。</summary>
    internal static class SampleBrushset
    {
        public static string CreateSample()
        {
            string temp = Path.Combine(Path.GetTempPath(), "BrushStudioSample-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            string zipPath = Path.Combine(temp, "内置示例.brushset");
            try
            {
                using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    AddBrush(zip, "示例 · 圆点", MakeDot(128));
                    AddBrush(zip, "示例 · 星形", MakeStar(128));
                    AddBrush(zip, "示例 · 花瓣", MakePetal(128));
                }
                return zipPath;
            }
            catch
            {
                try
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                }
                catch
                {
                }
                return null;
            }
        }

        private static void AddBrush(ZipArchive zip, string name, Bitmap bmp)
        {
            ZipArchiveEntry shape = zip.CreateEntry(name + "/Shape.png");
            using (Stream stream = shape.Open())
            {
                bmp.Save(stream, ImageFormat.Png);
            }
            bmp.Dispose();
        }

        private static Bitmap MakeDot(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    g.FillEllipse(brush, size * 0.20f, size * 0.20f, size * 0.60f, size * 0.60f);
                }
            }
            return bmp;
        }

        private static Bitmap MakeStar(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                PointF[] star = new PointF[10];
                double cx = size / 2.0;
                double cy = size / 2.0;
                double outer = size * 0.44;
                double inner = size * 0.18;
                for (int i = 0; i < 10; i++)
                {
                    double r = i % 2 == 0 ? outer : inner;
                    double a = Math.PI / 2.0 + i * Math.PI / 5.0;
                    star[i] = new PointF((float)(cx + r * Math.Cos(a)), (float)(cy - r * Math.Sin(a)));
                }
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    g.FillPolygon(brush, star);
                }
            }
            return bmp;
        }

        private static Bitmap MakePetal(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    g.FillEllipse(brush, size * 0.35f, size * 0.35f, size * 0.30f, size * 0.30f);
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * Math.PI / 3.0;
                        float cx = (float)(size / 2.0 + Math.Cos(a) * size * 0.22);
                        float cy = (float)(size / 2.0 + Math.Sin(a) * size * 0.22);
                        g.FillEllipse(brush, cx - size * 0.12f, cy - size * 0.20f, size * 0.24f, size * 0.40f);
                    }
                }
            }
            return bmp;
        }
    }
}
