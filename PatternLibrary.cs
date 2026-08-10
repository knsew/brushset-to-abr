using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BrushStudio
{
    /// <summary>
    /// 内嵌真实 PS pattern 库（PatternData.cs，条目含合法尾部，PS 能导入）。
    /// 近似纹理方案：每支带可见纹理（grainDepth&gt;0）的笔刷匹配一个最近似的真实 pattern，
    /// 用真实名字/ID 引用并内嵌其条目（逐字节原样，尾部合法）。
    /// </summary>
    internal static class PatternLibrary
    {
        private sealed class PatternInfo
        {
            public string Name;
            public string Id;
            public byte[] Entry;
            public float[] Feature;
        }

        private static readonly PatternInfo[] Patterns;

        static PatternLibrary()
        {
            int n = PatternData.Names.Length;
            Patterns = new PatternInfo[n];
            for (int i = 0; i < n; i++)
            {
                Patterns[i] = new PatternInfo
                {
                    Name = PatternData.Names[i],
                    Id = PatternData.Ids[i],
                    Entry = PatternData.Entries[i],
                    Feature = ComputeEntryFeature(PatternData.Entries[i])
                };
            }
        }

        /// <summary>把 grain 位图缩到 256×256 后计算 8×8 亮度均值池化特征。</summary>
        public static float[] ComputeGrainFeature(string grainPath)
        {
            byte[] planar = LoadGrainPlanarRgb(grainPath);
            return PoolLuma(planar, 256, 256);
        }

        private static byte[] LoadGrainPlanarRgb(string grainPath)
        {
            const int size = 256;
            byte[] planar = new byte[size * size * 3];
            using (Bitmap original = new Bitmap(grainPath))
            {
                using (Bitmap resized = new Bitmap(size, size, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(original, 0, 0, size, size);
                    }
                    BitmapData data = resized.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        int stride = data.Stride;
                        byte[] pixels = new byte[stride * size];
                        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                        int plane = size * size;
                        for (int y = 0; y < size; y++)
                        {
                            int row = y * stride;
                            for (int x = 0; x < size; x++)
                            {
                                int idx = row + x * 3;
                                byte luma = (byte)((pixels[idx + 2] * 299 + pixels[idx + 1] * 587 + pixels[idx] * 114 + 500) / 1000);
                                planar[y * size + x] = luma;
                                planar[plane + y * size + x] = luma;
                                planar[plane * 2 + y * size + x] = luma;
                            }
                        }
                    }
                    finally
                    {
                        resized.UnlockBits(data);
                    }
                }
            }
            return planar;
        }

        /// <summary>解析 pattern 条目（version/mode/h/w/name/id/pre60/pixels[+tail]）并计算 8×8 亮度特征。</summary>
        private static float[] ComputeEntryFeature(byte[] entry)
        {
            int mode = ReadBE32(entry, 4);
            int h = ReadBE16(entry, 8);
            int w = ReadBE16(entry, 10);
            int namec = ReadBE32(entry, 12);
            int aid = 16 + namec * 2 + 1 + entry[16 + namec * 2];
            int ch = mode == 3 ? 3 : 1;
            int pxStart = aid + 60;
            byte[] luma = new byte[h * w];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (ch == 3)
                    {
                        int r = entry[pxStart + idx];
                        int g = entry[pxStart + h * w + idx];
                        int b = entry[pxStart + 2 * h * w + idx];
                        luma[idx] = (byte)((r * 299 + g * 587 + b * 114 + 500) / 1000);
                    }
                    else
                    {
                        luma[idx] = entry[pxStart + idx];
                    }
                }
            }
            return PoolLuma(luma, h, w);
        }

        private static float[] PoolLuma(byte[] luma, int h, int w)
        {
            float[] feature = new float[64];
            for (int y = 0; y < 8; y++)
            {
                int y0 = y * h / 8, y1 = Math.Max(y0 + 1, (y + 1) * h / 8);
                for (int x = 0; x < 8; x++)
                {
                    int x0 = x * w / 8, x1 = Math.Max(x0 + 1, (x + 1) * w / 8);
                    long sum = 0;
                    long count = 0;
                    for (int py = y0; py < y1; py++)
                    {
                        for (int px = x0; px < x1; px++)
                        {
                            sum += luma[py * w + px];
                            count++;
                        }
                    }
                    feature[y * 8 + x] = (float)(sum / (double)count);
                }
            }
            return feature;
        }

        /// <summary>返回与 grain 特征最接近的 pattern 名字。</summary>
        public static string MatchName(float[] grainFeature)
        {
            int best = -1;
            double bestMse = double.MaxValue;
            for (int i = 0; i < Patterns.Length; i++)
            {
                double mse = 0;
                float[] f = Patterns[i].Feature;
                for (int j = 0; j < 64; j++)
                {
                    double d = grainFeature[j] - f[j];
                    mse += d * d;
                }
                if (mse < bestMse)
                {
                    bestMse = mse;
                    best = i;
                }
            }
            // 空库（开源版无内嵌 pattern）时返回 null，调用方降级为不写纹理。
            return best < 0 ? null : Patterns[best].Name;
        }

        public static string GetId(string name)
        {
            for (int i = 0; i < Patterns.Length; i++)
            {
                if (Patterns[i].Name == name)
                {
                    return Patterns[i].Id;
                }
            }
            return null;
        }

        public static byte[] GetEntry(string name)
        {
            for (int i = 0; i < Patterns.Length; i++)
            {
                if (Patterns[i].Name == name)
                {
                    return Patterns[i].Entry;
                }
            }
            return null;
        }

        private static int ReadBE32(byte[] b, int o)
        {
            return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        }

        private static int ReadBE16(byte[] b, int o)
        {
            return (b[o] << 8) | b[o + 1];
        }
    }
}
