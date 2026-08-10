using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace BrushStudio
{
    internal enum PreviewBackground
    {
        Transparent,
        White,
        Gray,
        Black
    }

    /// <summary>
    /// 绘画测试画布（WPF 版）：棋盘/白/黑背景 + 标准笔划自动预览 + 盖章手绘 + 缩放平移。
    /// 行为与 WinForms 版 StrokeCanvas 一致，使用 WPF 保留式 Drawing 渲染。
    /// </summary>
    internal sealed class StrokeCanvas : FrameworkElement
    {
        private const double MinZoom = 0.10;
        private const double MaxZoom = 8.0;
        private const int ShapeMaxSide = 768; // 笔尖图预缩放上限：足够清晰，又避免每笔过滤大图卡顿

        // 盖章图层：每笔一个 DrawingVisual 增量渲染（只画新笔划，不重绘旧笔划 → 消除卡顿）。
        private readonly ContainerVisual layer = new ContainerVisual();
        private static readonly SolidColorBrush WhiteBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xDE));
        private static readonly SolidColorBrush DarkBrush = new SolidColorBrush(Color.FromRgb(20, 20, 22));
        private ImageSource shapeImage;
        private ImageSource previewImage;
        private ExtractedBrush brush;
        private bool hasBrush;
        private bool drawing;
        private Point last;
        private int stampSize = 96;
        private double stampStep = 24;
        private double brushAngle;
        private double brushRoundness = 100;
        private Color brushColor = Colors.Black;
        private PreviewBackground background;
        private bool invert;
        private double zoom = 1;
        private Point pan;
        private bool panning;
        private Point panLast;
        private bool autoPreview = true;
        private string previewCacheKey = "";

        public StrokeCanvas()
        {
            Focusable = false;
            Cursor = Cursors.Cross;
            // 盖章位图用 Linear 快速缩放（默认 Fant 高保真在连续盖章时明显拖慢渲染线程）
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            return layer;
        }

        private void UpdateLayerTransform()
        {
            layer.Transform = new MatrixTransform(zoom, 0, 0, zoom, pan.X, pan.Y);
        }

        // ============ 公开状态 ============

        public void SetBrush(ExtractedBrush value)
        {
            brush = value;
            hasBrush = brush != null;
            RebuildShape();
            ApplyTuning();
            RebuildPreview();
            autoPreview = true;
            ClearStrokes();
            // 切笔刷回到适配视图：避免上次平移/缩放后，新笔刷画到视口外导致"画不出来"
            FitToWindow();
            InvalidateVisual();
        }

        public Color BrushColor => brushColor;

        public void SetBrushColor(Color value)
        {
            brushColor = value;
            RebuildShape();
            RebuildPreview();
            InvalidateVisual();
        }

        public void SetBackgroundMode(PreviewBackground value)
        {
            background = value;
            InvalidateVisual();
        }

        public void SetInvert(bool value)
        {
            invert = value;
            RebuildShape();
            InvalidateVisual();
        }

        public double Zoom => zoom;

        public void FitToWindow()
        {
            zoom = 1;
            pan = new Point();
            UpdateLayerTransform();
            InvalidateVisual();
        }

        public void ZoomBy(double factor)
        {
            Point anchor = ScreenToLayer(Mouse.GetPosition(this));
            double oldZoom = zoom;
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, oldZoom * factor));
            if (oldZoom <= 0)
            {
                oldZoom = 1;
            }
            double ratio = zoom / oldZoom;
            pan = new Point(anchor.X - (anchor.X - pan.X) * ratio, anchor.Y - (anchor.Y - pan.Y) * ratio);
            UpdateLayerTransform();
            InvalidateVisual();
        }

        public void ApplyTuning()
        {
            if (brush == null)
            {
                return;
            }
            if (brush.ManualDiameter.HasValue)
            {
                stampSize = (int)Math.Max(24, Math.Min(400, Math.Round(brush.ManualDiameter.Value * 0.5)));
            }
            else if (brush.Params != null)
            {
                object maxSize;
                if (brush.Params.TryGetValue("maxSize", out maxSize))
                {
                    try
                    {
                        double size = Convert.ToDouble(maxSize, CultureInfo.InvariantCulture);
                        stampSize = (int)Math.Max(24, Math.Min(400, Math.Round(size * 0.5)));
                    }
                    catch
                    {
                        stampSize = 96;
                    }
                }
                else
                {
                    stampSize = 96;
                }
            }
            else
            {
                stampSize = 96;
            }
            stampStep = Math.Max(4, stampSize * brush.EffectiveSpacingPercent / 100.0);
            brushAngle = brush.EffectiveAngleDegrees();
            brushRoundness = brush.EffectiveRoundnessPercent();
        }

        public void SetAutoPreview(bool value)
        {
            if (autoPreview != value)
            {
                autoPreview = value;
                InvalidateVisual();
            }
        }

        public bool AutoPreview => autoPreview;

        /// <summary>参数被手动调整后调用，强制下次重画预览。</summary>
        public void InvalidatePreview()
        {
            previewImage = null;
            previewCacheKey = "";
        }

        public void ClearStrokes()
        {
            layer.Children.Clear();
            InvalidateVisual();
        }

        // ============ 笔尖形状（alpha 掩码 → 指定颜色位图 → ImageSource） ============

        private sealed class ShapeMask
        {
            public byte[] Alpha;
            public int Width;
            public int Height;
        }
        private static readonly Dictionary<string, ShapeMask> MaskCache = new Dictionary<string, ShapeMask>();
        private const int MaskCacheMax = 12;

        private void RebuildShape()
        {
            shapeImage = null;
            if (brush == null)
            {
                return;
            }
            string path = brush.ShapePath ?? brush.GrainPath;
            if (path == null || !File.Exists(path))
            {
                return;
            }
            bool effInvert = brush.ManualInvert.HasValue ? brush.ManualInvert.Value : invert;
            string cacheKey = path + "|" + (effInvert ? "1" : "0");
            try
            {
                ShapeMask mask;
                if (!MaskCache.TryGetValue(cacheKey, out mask))
                {
                    AbrBrush abr;
                    if (!AbrWriter.TryLoadAlpha(new AbrInput { ImagePath = path, DisplayName = "preview", Spacing = 25, Invert = brush.ManualInvert }, invert, false, out abr))
                    {
                        using (Bitmap fallback = new Bitmap(path))
                        using (Bitmap scaled = ImageUtil.ScaleTo(fallback, ShapeMaxSide))
                        {
                            shapeImage = ImageUtil.ToImageSource(scaled);
                        }
                        return;
                    }
                    mask = new ShapeMask { Alpha = abr.Alpha, Width = abr.Width, Height = abr.Height };
                    MaskCache[cacheKey] = mask;
                    if (MaskCache.Count > MaskCacheMax)
                    {
                        Dictionary<string, ShapeMask>.Enumerator e = MaskCache.GetEnumerator();
                        if (e.MoveNext())
                        {
                            MaskCache.Remove(e.Current.Key);
                        }
                    }
                }
                using (Bitmap tinted = ImageUtil.TintedFromAlpha(mask.Alpha, mask.Width, mask.Height, brushColor))
                using (Bitmap scaled = ImageUtil.ScaleTo(tinted, ShapeMaxSide))
                {
                    // 预缩放笔尖图：避免每画一笔都对 2048px 大图做高保真缩小，造成渲染线程拥塞卡顿
                    shapeImage = ImageUtil.ToImageSource(scaled);
                }
            }
            catch
            {
                shapeImage = null;
            }
        }

        // ============ 标准笔划预览 ============

        private void RebuildPreview()
        {
            if (brush == null)
            {
                return;
            }
            bool previewInvert = brush.ManualInvert.HasValue ? brush.ManualInvert.Value : invert;
            string key = brush.SourceId + "|" + (previewInvert ? "1" : "0") + "|" + brushColor.R + "," + brushColor.G + "," + brushColor.B + "|" + stampSize;
            if (previewImage != null && previewCacheKey == key)
            {
                return;
            }
            previewImage = null;
            previewCacheKey = "";
            try
            {
                using (Bitmap stroke = StrokeSimulator.RenderBrushPreview(brush, stampSize, previewInvert))
                {
                    if (stroke == null)
                    {
                        return;
                    }
                    using (Bitmap tinted = ImageUtil.TintToColor(stroke, brushColor))
                    {
                        previewImage = ImageUtil.ToImageSource(tinted);
                    }
                }
                previewCacheKey = key;
            }
            catch
            {
            }
        }

        // ============ 盖章手绘 ============

        private void Stamp(Point p)
        {
            if (shapeImage == null)
            {
                return;
            }
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                TransformGroup tg = new TransformGroup();
                tg.Children.Add(new TranslateTransform(p.X, p.Y));
                tg.Children.Add(new RotateTransform(brushAngle));
                tg.Children.Add(new ScaleTransform(brushRoundness / 100.0, 1.0));
                double half = stampSize / 2.0;
                dc.PushTransform(tg);
                dc.DrawImage(shapeImage, new Rect(-half, -half, stampSize, stampSize));
                dc.Pop();
            }
            layer.Children.Add(visual);
            // 必须强制触发渲染：只改 ContainerVisual 子节点不会可靠地立即合成，
            // 否则笔画要等下一次全量重绘（如切换）才显示。
            InvalidateVisual();
        }

        private Point ScreenToLayer(Point screen)
        {
            if (zoom <= 0)
            {
                return screen;
            }
            return new Point((screen.X - pan.X) / zoom, (screen.Y - pan.Y) / zoom);
        }

        // ============ 鼠标交互 ============

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Middle)
            {
                panning = true;
                panLast = e.GetPosition(this);
                Cursor = Cursors.SizeAll;
                CaptureMouse();
            }
            else if (e.ChangedButton == MouseButton.Left && hasBrush && shapeImage != null)
            {
                if (autoPreview)
                {
                    autoPreview = false;
                    InvalidateVisual();
                }
                drawing = true;
                last = ScreenToLayer(e.GetPosition(this));
                Stamp(last);
                CaptureMouse();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (panning)
            {
                Point current = e.GetPosition(this);
                pan.X += current.X - panLast.X;
                pan.Y += current.Y - panLast.Y;
                panLast = current;
                UpdateLayerTransform();
                InvalidateVisual();
            }
            if (drawing && shapeImage != null)
            {
                Point p = ScreenToLayer(e.GetPosition(this));
                double dx = p.X - last.X;
                double dy = p.Y - last.Y;
                if (Math.Sqrt(dx * dx + dy * dy) >= stampStep)
                {
                    last = p;
                    Stamp(p);
                }
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            drawing = false;
            panning = false;
            Cursor = Cursors.Cross;
            ReleaseMouseCapture();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                double delta = e.Delta > 0 ? 1.25 : 0.8;
                Point anchor = ScreenToLayer(e.GetPosition(this));
                double oldZoom = zoom;
                zoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom * delta));
                if (oldZoom <= 0)
                {
                    oldZoom = 1;
                }
                double ratio = zoom / oldZoom;
                pan = new Point(anchor.X - (anchor.X - pan.X) * ratio, anchor.Y - (anchor.Y - pan.Y) * ratio);
                UpdateLayerTransform();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        // ============ 渲染 ============

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            Size size = RenderSize;
            DrawBackground(dc, new Rect(0, 0, size.Width, size.Height));

            if (autoPreview && previewImage != null)
            {
                DrawPreview(dc);
            }

            // 笔画图层由视觉子元素 layer 自动叠加渲染（增量绘制，见 Stamp）

            Color hintColor = background == PreviewBackground.Black
                ? Color.FromRgb(190, 190, 195)
                : background == PreviewBackground.Gray ? Theme.TextSecondary : Theme.Faint;
            if (!hasBrush)
            {
                DrawHint(dc, "未选择笔刷", hintColor);
            }
            else if (shapeImage == null)
            {
                DrawHint(dc, "缺少笔尖图片", hintColor);
            }
        }

        private void DrawBackground(DrawingContext dc, Rect bounds)
        {
            if (background == PreviewBackground.White)
            {
                dc.DrawRectangle(WhiteBrush, null, bounds);
            }
            else if (background == PreviewBackground.Gray)
            {
                dc.DrawRectangle(GrayBrush, null, bounds);
            }
            else if (background == PreviewBackground.Black)
            {
                dc.DrawRectangle(DarkBrush, null, bounds);
            }
            else
            {
                // 可平铺画刷：GPU 平铺渲染，消除逐格绘制造成的卡顿
                dc.DrawRectangle(Theme.Checker, null, bounds);
            }
        }

        private void DrawPreview(DrawingContext dc)
        {
            double cw = RenderSize.Width;
            double ch = RenderSize.Height;
            if (cw <= 0 || ch <= 0 || previewImage == null)
            {
                return;
            }
            double scale = Math.Min(cw / previewImage.Width, ch / previewImage.Height);
            double dw = Math.Max(1, previewImage.Width * scale);
            double dh = Math.Max(1, previewImage.Height * scale);
            double dx = (cw - dw) / 2;
            double dy = (ch - dh) / 2;
            dc.DrawImage(previewImage, new Rect(dx, dy, dw, dh));

            string hint = "标准笔划预览 · 拖动可手绘";
            FormattedText ft = MakeText(hint, 11, Color.FromRgb(140, 140, 145));
            dc.DrawText(ft, new Point(cw - ft.Width - 12, ch - ft.Height - 6));
        }

        private void DrawHint(DrawingContext dc, string text, Color color)
        {
            FormattedText ft = MakeText(text, 13, color);
            dc.DrawText(ft, new Point((RenderSize.Width - ft.Width) / 2, (RenderSize.Height - ft.Height) / 2));
        }

        private FormattedText MakeText(string text, double size, Color color)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Noto Sans SC, Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                size,
                new SolidColorBrush(color),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }
    }
}
