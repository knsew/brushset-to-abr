using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace BrushStudio
{
    /// <summary>
    /// System.Drawing（核心引擎使用的位图）与 WPF ImageSource 之间的互操作助手。
    /// </summary>
    internal static class ImageUtil
    {
        public static ImageSource ToImageSource(Bitmap bmp)
        {
            if (bmp == null)
            {
                return null;
            }
            IntPtr hbmp = IntPtr.Zero;
            try
            {
                hbmp = bmp.GetHbitmap();
                return Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                if (hbmp != IntPtr.Zero)
                {
                    DeleteObject(hbmp);
                }
            }
        }

        /// <summary>把位图所有非透明像素统一着成指定颜色（保留 alpha）。</summary>
        public static Bitmap TintToColor(Bitmap source, Color color)
        {
            Bitmap tinted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(tinted))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix();
                cm.Matrix00 = 0;
                cm.Matrix01 = 0;
                cm.Matrix02 = 0;
                cm.Matrix10 = 0;
                cm.Matrix11 = 0;
                cm.Matrix12 = 0;
                cm.Matrix20 = 0;
                cm.Matrix21 = 0;
                cm.Matrix22 = 0;
                cm.Matrix40 = color.R / 255f;
                cm.Matrix41 = color.G / 255f;
                cm.Matrix42 = color.B / 255f;
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, ia);
                }
            }
            return tinted;
        }

        /// <summary>由 alpha 掩码构建指定颜色的 32bppARGB 位图（笔尖形状）。</summary>
        public static Bitmap TintedFromAlpha(byte[] alpha, int width, int height, Color color)
        {
            Bitmap shape = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = shape.LockBits(new Rectangle(0, 0, shape.Width, shape.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] pixels = new byte[stride * shape.Height];
                for (int y = 0; y < shape.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < shape.Width; x++)
                    {
                        int offset = row + x * 4;
                        pixels[offset] = color.B;
                        pixels[offset + 1] = color.G;
                        pixels[offset + 2] = color.R;
                        pixels[offset + 3] = alpha[y * width + x];
                    }
                }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                shape.UnlockBits(data);
            }
            return shape;
        }

        /// <summary>按最长边等比缩小位图（缩略图用）。</summary>
        public static Bitmap MakeThumbnail(Bitmap source, int size = 96)
        {
            Bitmap thumb = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(thumb))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(System.Drawing.Color.Transparent);
                int side = Math.Min(source.Width, source.Height);
                Rectangle srcRect = new Rectangle((source.Width - side) / 2, (source.Height - side) / 2, side, side);
                int pad = 6;
                g.DrawImage(source, new Rectangle(pad, pad, size - pad * 2, size - pad * 2), srcRect, GraphicsUnit.Pixel);
            }
            return thumb;
        }

        /// <summary>按最长边等比缩放位图，超长边大于 maxSide 时缩小（保留比例，透明底）。</summary>
        public static Bitmap ScaleTo(Bitmap source, int maxSide)
        {
            int longest = Math.Max(source.Width, source.Height);
            if (longest <= maxSide)
            {
                return new Bitmap(source);
            }
            double scale = (double)maxSide / longest;
            int w = Math.Max(1, (int)Math.Round(source.Width * scale));
            int h = Math.Max(1, (int)Math.Round(source.Height * scale));
            Bitmap scaled = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(System.Drawing.Color.Transparent);
                g.DrawImage(source, new Rectangle(0, 0, w, h));
            }
            return scaled;
        }

        public static Bitmap MakeFallbackThumb(int size = 96)
        {
            Bitmap thumb = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(thumb))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                int cx = size / 2;
                int r = size * 3 / 10;
                using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(168, 100, 124)))
                {
                    g.FillEllipse(brush, cx - r, cx - r, r * 2, r * 2);
                }
                using (SolidBrush dot = new SolidBrush(System.Drawing.Color.FromArgb(251, 244, 248)))
                {
                    g.FillEllipse(dot, cx - r / 2, cx - r / 2, r, r);
                }
            }
            return thumb;
        }

        /// <summary>星形占位图标（收藏夹卡片用，主色描边星）。</summary>
        public static Bitmap MakeStarIcon(int size = 40)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                using (Font f = new Font("Segoe UI Symbol", size * 0.5f, System.Drawing.FontStyle.Regular))
                using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(184, 134, 145)))
                {
                    g.DrawString("★", f, b, new RectangleF(0, -2, size, size),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            }
            return bmp;
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
