using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BrushStudio
{
    /// <summary>搜索框：占位符 + 一键清空。TextChanged 时抛出 QueryChanged，由外层做防抖。</summary>
    public partial class SearchBox : System.Windows.Controls.UserControl
    {
        public SearchBox()
        {
            InitializeComponent();
            Input.TextChanged += (s, e) => OnTextChanged();
            ClearButton.MouseLeftButtonUp += (s, e) => Clear();
        }

        public event RoutedEventHandler QueryChanged;

        public string Value => Input.Text;

        public void Clear()
        {
            Input.Text = "";
        }

        private void OnTextChanged()
        {
            Placeholder.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClearButton.Visibility = Input.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            QueryChanged?.Invoke(this, new RoutedEventArgs());
        }
    }
}
