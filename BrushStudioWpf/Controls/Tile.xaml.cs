using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BrushStudio
{
    /// <summary>
    /// 笔刷磁贴：缩略图 + 名称 + 保真色点 + 收藏星标 + 多选徽标。
    /// 单点 = 选中（Click），双击 = 打开笔刷预览（DoubleClick）。
    /// </summary>
    public partial class Tile : System.Windows.Controls.UserControl
    {
        private bool selected;
        private bool inSelection;
        private bool favorite;

        public Tile()
        {
            InitializeComponent();
        }

        public event MouseButtonEventHandler Click;
        public event MouseButtonEventHandler DoubleClick;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                UpdateVisual();
            }
        }

        public bool InSelection
        {
            get => inSelection;
            set
            {
                inSelection = value;
                SelectBadge.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool Favorite
        {
            get => favorite;
            set
            {
                favorite = value;
                StarText.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void SetThumb(ImageSource image)
        {
            ThumbImage.Source = image;
        }

        public void SetDisplayName(string text)
        {
            NameText.Text = text;
        }

        public void SetTooltip(string text)
        {
            ToolTip = text;
        }

        internal void SetFidelity(FidelityLevel level)
        {
            FidelityBadge.Background = new SolidColorBrush(Theme.FidelityColor(level));
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
            else if (e.ClickCount == 2)
            {
                DoubleClick?.Invoke(this, e);
            }
        }
    }
}
