using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace BrushStudio
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = "版本 " + version.Major + "." + version.Minor + "." + version.Build;
            SettingsPathText.Text = "设置文件：" + AppSettings.SettingsFilePath;
            LogPathText.Text = "错误日志：" + AppLog.LogFilePath;
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnLinkClicked(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
            }
            e.Handled = true;
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
