using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BrushStudio
{
    /// <summary>
    /// 代码后置用的小工具：取资源风格、工厂建控件。视觉全部由 Themes/*.xaml 决定。
    /// </summary>
    internal static class Ui
    {
        public static Style Style(string key)
        {
            return Application.Current != null && Application.Current.TryFindResource(key) is Style s ? s : null;
        }

        public static Button Button(string text, string styleKey = "ModernButton.Secondary")
        {
            Button b = new Button();
            b.Style = Style(styleKey);
            b.Content = text;
            b.Margin = new Thickness(0);
            return b;
        }

        public static CheckBox Check(string text, bool checkedState)
        {
            CheckBox c = new CheckBox();
            c.Style = Style("ModernCheckBox");
            c.Content = text;
            c.IsChecked = checkedState;
            c.Margin = new Thickness(0);
            c.VerticalContentAlignment = VerticalAlignment.Center;
            return c;
        }

        public static TextBlock Label(string text, double fontSize, Color color, FontWeight? weight = null)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = fontSize;
            t.Foreground = new SolidColorBrush(color);
            t.FontWeight = weight ?? FontWeights.Normal;
            t.VerticalAlignment = VerticalAlignment.Center;
            return t;
        }

        /// <summary>字段标签（右栏表单左列标题）。</summary>
        public static TextBlock FieldLabel(string text)
        {
            return Label(text, 12, Theme.TextSecondary);
        }

        public static ComboBox Combo(string[] items, int selectedIndex = 0)
        {
            ComboBox c = new ComboBox();
            c.Style = Style("ModernComboBox");
            c.MinHeight = 30;
            foreach (string item in items)
            {
                c.Items.Add(item);
            }
            c.SelectedIndex = selectedIndex;
            return c;
        }

        public static TextBox TextBox(string text, bool readOnly = false)
        {
            TextBox t = new TextBox();
            t.Style = Style("ModernTextBox");
            t.MinHeight = 30;
            t.Text = text ?? "";
            t.IsReadOnly = readOnly;
            return t;
        }

        public static ProgressBar Progress(double value)
        {
            ProgressBar p = new ProgressBar();
            p.Style = Style("ModernProgress");
            p.Minimum = 0;
            p.Maximum = 100;
            p.Value = value;
            return p;
        }

        public static Slider Slider(double min, double max, double value)
        {
            Slider s = new Slider();
            s.Style = Style("ModernSlider");
            s.Minimum = min;
            s.Maximum = max;
            s.Value = value;
            s.IsSnapToTickEnabled = false;
            s.TickFrequency = 1;
            return s;
        }
    }
}
