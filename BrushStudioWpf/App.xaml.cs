using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;

namespace BrushStudio
{
    /// <summary>
    /// WPF 应用入口：负责 CLI 分发与 GUI 启动。
    /// 核心转换引擎（Core/Mapping/Models/RenderSim/PatternLibrary/PatternData/BrushLibrary）
    /// 保持原样链接，本类仅承担原先 Program.Main 的参数分发职责。
    /// </summary>
    public partial class App : Application
    {
        private MainWindow mainWindow;

        private void OnStartup(object sender, StartupEventArgs e)
        {
            string[] args = e.Args;

            AttachErrorHandlers();

            if (args.Length > 0)
            {
                int exitCode = DispatchCli(args);
                if (exitCode >= 0)
                {
                    Environment.ExitCode = exitCode;
                    Shutdown();
                    return;
                }
                // exitCode < 0 表示走 GUI 分支（--screenshot / --smoke-gui 等）
            }

            // ===== GUI 模式 =====
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            bool smoke = args.Length > 0 && args[0] == "--smoke-gui";
            // 所有 GUI 自动化模式（截图/冒烟/控件画廊）都禁止弹窗，避免对话框阻塞截图
            bool suppressDialogs = args.Length > 0 && (args[0] == "--smoke-gui" || args[0] == "--screenshot" || args[0] == "--screenshot-empty" || args[0] == "--preview-controls");
            mainWindow = new MainWindow(smoke, false, suppressDialogs);
            MainWindow = mainWindow;
            mainWindow.Show();
            if (args.Length > 0 && args[0] == "--preview-controls")
            {
                // 控件画廊：直接复用主窗口空态截图（开发用调试工具，非产品功能）
                ScheduleCapture(args.Length > 1 ? args[1] : "controls-preview.png", 800, smoke);
            }
            else if (args.Length > 0 && args[0] == "--screenshot-empty")
            {
                ScheduleCapture(args.Length > 1 ? args[1] : "ui-empty.png", 900, smoke);
            }
            else if (args.Length > 0 && args[0] == "--screenshot")
            {
                // 等示例笔刷包加载完成再截图（加载是异步的，给足时间）
                string shotPath = args.Length > 1 ? args[1] : "ui-shot.png";
                string sample = FindScreenshotSample();
                if (sample != null)
                {
                    mainWindow.AddPaths(new[] { sample });
                }
                ScheduleCapture(shotPath, 9000, smoke);
            }
            else if (args.Length > 0)
            {
                // 命令行直接传入笔刷包路径（拖拽 / 文件关联）
                List<string> paths = new List<string>();
                foreach (string a in args)
                {
                    if (File.Exists(a) || Directory.Exists(a))
                    {
                        paths.Add(a);
                    }
                }
                if (paths.Count > 0)
                {
                    mainWindow.AddPaths(paths.ToArray());
                }
            }
        }

        /// <summary>
        /// 分发纯 CLI 命令。返回退出码；返回 -1 表示该参数属于 GUI 分支（截图/冒烟），需走窗口流程。
        /// </summary>
        private static int DispatchCli(string[] args)
        {
            string cmd = args[0];
            if (cmd == "--screenshot" || cmd == "--screenshot-empty" || cmd == "--smoke-gui" || cmd == "--preview-controls")
            {
                return -1;
            }
            // 纯引擎命令：附加到父控制台以便看到输出
            AttachConsole();
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch
            {
            }
            if (cmd == "--cli")
            {
                return Cli.MakeAbr(args);
            }
            if (cmd == "--convert-brushset")
            {
                return Cli.ConvertBrushset(args);
            }
            if (cmd == "--cli-extract")
            {
                return Cli.Extract(args);
            }
            if (cmd == "--compare-pack")
            {
                return Cli.MakeComparePack(args);
            }
            if (cmd == "--render-compare")
            {
                return RenderCompareTool.Run(args);
            }
            if (cmd == "--test-sample")
            {
                return RunSampleTest();
            }
            if (cmd == "--help" || cmd == "-h")
            {
                PrintHelp();
                return 0;
            }
            Console.Error.WriteLine("未知参数: " + cmd);
            PrintHelp();
            return 1;
        }

        private static int RunSampleTest()
        {
            string zipPath = SampleBrushset.CreateSample();
            if (zipPath == null)
            {
                return 1;
            }
            string outAbr = Path.Combine(Path.GetTempPath(), "sample-test-" + Guid.NewGuid().ToString("N") + ".abr");
            try
            {
                int brushCount;
                int generated = BrushsetConverter.ConvertToAbr(outAbr, new List<string> { zipPath }, 7, true, false, true, out brushCount, p => { });
                Console.WriteLine("sample test: count=" + brushCount + " generated=" + generated);
                return generated > 0 ? 0 : 1;
            }
            finally
            {
                try
                {
                    string dir = Path.GetDirectoryName(zipPath);
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch
                {
                }
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("用法: PNGtoABR.exe [命令]");
            Console.WriteLine("无参数：启动图形界面");
            Console.WriteLine("--cli 输出.abr 图片1 [图片2 ...] [--spacing N] [--version N]");
            Console.WriteLine("--convert-brushset 输出.abr 文件.brushset [--version N]");
            Console.WriteLine("--cli-extract 输出目录 文件.brushset|文件.abr");
            Console.WriteLine("--compare-pack 输出目录 文件.brushset [--version N] [--force]");
            Console.WriteLine("--render-compare 输出目录 文件.brushset [--brush 子串]");
            Console.WriteLine("--test-sample");
            Console.WriteLine("--screenshot 输出.png / --screenshot-empty 输出.png / --smoke-gui");
        }

        private static string FindScreenshotSample()
        {
            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Synthetic-Brushes.brushset"),
                @"D:\ps\FidelityTest\compare-test.brushset",
                @"D:\ps\FidelityTest\src-brushset.brushset"
            };
            foreach (string candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        private void ScheduleCapture(string shotPath, int delayMs, bool smoke)
        {
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(delayMs);
            timer.Tick += delegate
            {
                timer.Stop();
                try
                {
                    mainWindow.CaptureToFile(shotPath);
                }
                catch (Exception ex)
                {
                    AppLog.Write("截图失败: " + shotPath + " -> " + ex);
                }
                mainWindow.Close();
            };
            timer.Start();
        }

        private static void AttachErrorHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                AppLog.Write("Unhandled: " + (e.ExceptionObject == null ? "null" : e.ExceptionObject.ToString()));
            };
            if (Current != null)
            {
                Current.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                {
                    AppLog.Write("DispatcherUnhandled: " + (e.Exception == null ? "null" : e.Exception.ToString()));
                };
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        private const int ATTACH_PARENT_PROCESS = -1;

        private static void AttachConsole()
        {
            try
            {
                if (!AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    return;
                }
            }
            catch
            {
            }
        }
    }
}
