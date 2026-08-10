using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BrushStudio
{
    /// <summary>笔刷包卡片：缩略图 + 包名 + 元信息（笔刷数/大小/平均保真分）。</summary>
    public partial class PackageCard : System.Windows.Controls.UserControl
    {
        private bool selected;

        public PackageCard()
        {
            InitializeComponent();
        }

        public event MouseButtonEventHandler Click;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                UpdateVisual();
            }
        }

        public void SetThumb(ImageSource image)
        {
            ThumbImage.Source = image;
        }

        public void SetName(string name)
        {
            NameText.Text = name;
        }

        public void SetMeta(string meta)
        {
            MetaText.Text = meta;
        }

        public void SetTooltip(string text)
        {
            ToolTip = text;
        }

        private void UpdateVisual()
        {
            if (selected)
            {
                Root.Background = (Brush)FindResource("AccentSoftBrush");
                Root.BorderBrush = (Brush)FindResource("AccentBrush");
                Root.BorderThickness = new Thickness(1.5);
            }
            else
            {
                Root.Background = (Brush)FindResource("CardBrush");
                Root.BorderBrush = (Brush)FindResource("BorderBrush");
                Root.BorderThickness = new Thickness(1);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ClickCount == 1)
            {
                Click?.Invoke(this, e);
            }
        }
    }
}
