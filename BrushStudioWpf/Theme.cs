using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using FontStyle = System.Drawing.FontStyle;
using Media = System.Windows.Media;

namespace BrushStudio
{
    /// <summary>
    /// 设计 token 的 C# 侧适配：
    ///  - UiFont 供核心引擎（RenderSim）在合成对比图时使用（System.Drawing.Font，同旧 Tokens.cs）。
    ///  - WPF 调色板常量与取画刷助手，供代码后置使用（与 Themes/Colors.xaml 保持一致）。
    /// </summary>
    internal static class Theme
    {
        // ===== WPF 配色（对应 Themes/Colors.xaml，Acubi 韩系极简微甜） =====
        public static readonly Media.Color Background = Media.Color.FromRgb(0xE9, 0xE9, 0xEC);   // 窗口背景（低饱和暖灰奶白）
        public static readonly Media.Color Card = Media.Color.FromRgb(0xFF, 0xFF, 0xFF);         // 卡片（纯白）
        public static readonly Media.Color CardAlt = Media.Color.FromRgb(0xFA, 0xFA, 0xFB);      // 卡片次级底色
        public static readonly Media.Color Border = Media.Color.FromRgb(0xE3, 0xE3, 0xE8);       // 1px 极细描边
        public static readonly Media.Color BorderStrong = Media.Color.FromRgb(0xC9, 0xC9, 0xD0); // 强调描边
        public static readonly Media.Color Text = Media.Color.FromRgb(0x2C, 0x2C, 0x30);         // 主文字（高级炭灰）
        public static readonly Media.Color TextStrong = Media.Color.FromRgb(0x2C, 0x2C, 0x30);   // 标题（炭灰）
        public static readonly Media.Color TextSecondary = Media.Color.FromRgb(0x6B, 0x6B, 0x72);// 字段标签（中灰）
        public static readonly Media.Color Muted = Media.Color.FromRgb(0x8E, 0x8E, 0x96);        // 辅助提示文字
        public static readonly Media.Color Faint = Media.Color.FromRgb(0xB4, 0xB4, 0xBC);        // 占位/弱文字
        public static readonly Media.Color Accent = Media.Color.FromRgb(0xB8, 0x86, 0x91);       // 主色（Acubi 烟熏灰粉）
        public static readonly Media.Color AccentHover = Media.Color.FromRgb(0xC7, 0x9A, 0xA4);  // 主色 hover
        public static readonly Media.Color AccentDown = Media.Color.FromRgb(0xA5, 0x71, 0x7C);   // 主色 press
        public static readonly Media.Color AccentSoft = Media.Color.FromRgb(0xF6, 0xEA, 0xED);   // 主色浅底
        public static readonly Media.Color Success = Media.Color.FromRgb(0x6F, 0xA8, 0x78);      // 成功
        public static readonly Media.Color SuccessSoft = Media.Color.FromRgb(0xEA, 0xF3, 0xEC);  // 成功浅底
        public static readonly Media.Color Warning = Media.Color.FromRgb(0xC9, 0xA2, 0x6B);      // 警告
        public static readonly Media.Color Danger = Media.Color.FromRgb(0xC0, 0x77, 0x77);       // 危险
        public static readonly Media.Color DangerSoft = Media.Color.FromRgb(0xF7, 0xEB, 0xEB);   // 危险浅底
        public static readonly Media.Color CheckerA = Media.Color.FromRgb(0xDC, 0xDC, 0xE2);     // 透明棋盘 A（中性灰）
        public static readonly Media.Color CheckerB = Media.Color.FromRgb(0xE8, 0xE8, 0xEE);     // 透明棋盘 B

        // ===== WPF 画刷助手 =====
        public static Media.SolidColorBrush Brush(string resourceKey)
        {
            if (Application.Current != null && Application.Current.TryFindResource(resourceKey) is Media.SolidColorBrush b)
            {
                return b;
            }
            return new Media.SolidColorBrush(Media.Color.FromRgb(0xFB, 0xF4, 0xF8));
        }

        public static Media.SolidColorBrush Solid(Media.Color color)
        {
            return new Media.SolidColorBrush(color);
        }

        public static Media.Color FidelityColor(FidelityLevel level)
        {
            if (level == FidelityLevel.High)
            {
                return Success;
            }
            return level == FidelityLevel.Medium ? Warning : Danger;
        }

        // ===== 可平铺透明棋盘画刷（画布背景，GPU 平铺渲染，性能远优于逐格绘制） =====
        public static readonly Media.DrawingBrush Checker = CreateChecker();

        private static Media.DrawingBrush CreateChecker()
        {
            DrawingGroup group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(new Media.SolidColorBrush(CheckerA), null, new RectangleGeometry(new Rect(0, 0, 10, 10))));
            group.Children.Add(new GeometryDrawing(new Media.SolidColorBrush(CheckerB), null, new RectangleGeometry(new Rect(0, 0, 5, 5))));
            group.Children.Add(new GeometryDrawing(new Media.SolidColorBrush(CheckerB), null, new RectangleGeometry(new Rect(5, 5, 5, 5))));
            Media.DrawingBrush brush = new Media.DrawingBrush(group);
            brush.TileMode = TileMode.Tile;
            brush.Viewport = new Rect(0, 0, 10, 10);
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Freeze();
            return brush;
        }

        // ===== 字体（核心引擎 RenderSim 合成对比图时调用） =====
        public static Font UiFont(float size, FontStyle style = FontStyle.Regular)
        {
            // 思源黑体（Source Han Sans SC / Noto Sans SC）优先；未安装则回退微软雅黑
            try
            {
                return new Font("Source Han Sans SC", size, style);
            }
            catch
            {
                try
                {
                    return new Font("Noto Sans SC", size, style);
                }
                catch
                {
                    try
                    {
                        return new Font("Microsoft YaHei UI", size, style);
                    }
                    catch
                    {
                        return new Font("Microsoft Sans Serif", size - 1f, style);
                    }
                }
            }
        }
    }
}
