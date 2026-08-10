using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace BrushStudio
{
    public partial class BrushPreviewWindow : Window
    {
        private readonly ExtractedBrush brush;
        private readonly bool invert;

        internal BrushPreviewWindow(ExtractedBrush brush, bool invert = false)
        {
            InitializeComponent();
            this.brush = brush;
            this.invert = invert;
            TitleText.Text = brush == null ? "未命名笔刷" : (brush.EffectiveName ?? brush.Name);
            Title = "笔刷预览 - " + TitleText.Text;
            InfoText.Text = BuildInfo(brush);
            BgBox.SelectedIndex = 0;
            LoadPreview();
        }

        private void LoadPreview()
        {
            if (brush == null)
            {
                return;
            }
            string path = brush.ShapePath ?? brush.GrainPath;
            if (path == null || !File.Exists(path))
            {
                return;
            }
            try
            {
                using (Bitmap bmp = AbrWriter.BuildAlphaPreviewBitmap(path, invert, false) ?? new Bitmap(path))
                {
                    PreviewImage.Source = ImageUtil.ToImageSource(bmp);
                }
            }
            catch
            {
            }
        }

        private static string BuildInfo(ExtractedBrush brush)
        {
            if (brush == null)
            {
                return "";
            }
            string kind = brush.ShapePath != null ? "Shape 笔尖" : "Grain 笔尖";
            string path = brush.ShapePath ?? brush.GrainPath;
            if (path != null && File.Exists(path))
            {
                try
                {
                    using (Bitmap bmp = new Bitmap(path))
                    {
                        return kind + " · " + bmp.Width + " × " + bmp.Height + " px · 间距 " + brush.EffectiveSpacingPercent + "%";
                    }
                }
                catch
                {
                }
            }
            return kind + " · 间距 " + brush.EffectiveSpacingPercent + "%";
        }

        private void OnBackgroundChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = BgBox.SelectedIndex;
            if (idx == 1)
            {
                ImageHost.Background = Brushes.White;
            }
            else if (idx == 2)
            {
                ImageHost.Background = new SolidColorBrush(Color.FromRgb(216, 216, 222));
            }
            else if (idx == 3)
            {
                ImageHost.Background = new SolidColorBrush(Color.FromRgb(20, 20, 22));
            }
            else
            {
                ImageHost.Background = (Brush)FindResource("CheckerBrush");
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
