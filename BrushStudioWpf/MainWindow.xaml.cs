using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace BrushStudio
{
    /// <summary>
    /// 主窗口（WPF 版）：三栏响应式布局 + 底部操作栏。
    /// 业务逻辑从 WinForms MainForm 完整平移：导入/列表/筛选/收藏/改名/反转/
    /// 参数微调/还原度报告/转换流程/拖拽/快捷键/设置持久化。
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int BrushBatchSize = 30;

        private readonly bool smokeMode;
        private readonly bool suppressDialogs;
        private bool stableSize;
        private bool busy;
        private bool updating;
        private bool updatingParams;
        private bool ready;
        private bool loadingBatch;
        private bool importBusy;
        private readonly Queue<List<string>> importQueue = new Queue<List<string>>();

        private SourceFile selectedSource;
        private ExtractedBrush currentBrush;
        private readonly List<SourceFile> sources = new List<SourceFile>();
        private readonly List<string> tempCleanup = new List<string>();
        private string lastOutput;
        private string sortMode = "name";
        private List<ExtractedBrush> filteredBrushes = new List<ExtractedBrush>();
        private int brushBatchIndex;
        private readonly List<PackageCard> packageCards = new List<PackageCard>();
        private readonly List<Tile> brushTiles = new List<Tile>();
        private PackageCard favoritesCard;
        private bool favoritesMode;   // 跨包收藏夹：把各包收藏汇总成一个"收藏夹"虚拟包
        private readonly Dictionary<ExtractedBrush, Bitmap> brushThumbCache = new Dictionary<ExtractedBrush, Bitmap>();
        private readonly HashSet<ExtractedBrush> selectedBrushes = new HashSet<ExtractedBrush>();
        private DispatcherTimer searchDebounce;
        private WpfColor brushColor = Colors.Black;

        public MainWindow(bool smoke = false, bool stableSize = false, bool suppressDialogs = false)
        {
            InitializeComponent();

            smokeMode = smoke;
            this.suppressDialogs = suppressDialogs;
            this.stableSize = stableSize;
            AppSettings.Load();

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = "v" + version.Major + "." + version.Minor + "." + version.Build;

            try
            {
                using (System.Drawing.Icon appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath))
                {
                    if (appIcon != null)
                    {
                        using (Bitmap bmp = appIcon.ToBitmap())
                        {
                            Icon = ImageUtil.ToImageSource(bmp);
                        }
                    }
                }
            }
            catch
            {
            }

            SourceInitialized += OnSourceInitialized;

            // 搜索防抖 300ms
            searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            searchDebounce.Tick += (s, e) => { searchDebounce.Stop(); RefreshBrushList(); };
            Search.QueryChanged += (s, e) => { searchDebounce.Stop(); searchDebounce.Start(); };

            // 参数滑块联动
            InitParamSliders();
            WireParamPair(SpacingSlider, SpacingBox);
            WireParamPair(SizeSlider, SizeBox);
            WireParamPair(AngleSlider, AngleBox);
            WireParamPair(RoundnessSlider, RoundnessBox);

            // 初始化默认值
            VersionBox.SelectedIndex = Math.Max(0, Math.Min(2, AppSettings.VersionIndex));
            BackgroundBox.SelectedIndex = 0;
            FidelityFilter.SelectedIndex = 0;
            SortBox.SelectedIndex = 0;
            InvertBox.IsChecked = AppSettings.Invert;
            PureBox.IsChecked = AppSettings.Pure;
            CropBox.IsChecked = AppSettings.Autocrop;
            RawBox.IsChecked = !AppSettings.UseRle;
            AutoOpenBox.IsChecked = AppSettings.AutoOpen;
            SeparateExportBox.IsChecked = true;
            BrushInvertBox.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(AppSettings.LastOutput) && File.Exists(AppSettings.LastOutput))
            {
                OutputBox.Text = AppSettings.LastOutput;
            }

            AllowDrop = true;
            DragOver += OnDragOver;
            Drop += OnDrop;
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += OnWindowClosing;

            ready = true;

            if (!stableSize)
            {
                ApplySavedWindowState();
            }

            Loaded += (s, e) =>
            {
                RefreshPackageList();   // 初始化收藏夹卡片与空态列表（无素材时也要显示收藏夹）
                strokeCanvas.FitToWindow();
                UpdateZoomLabel();
                if (!smokeMode && !this.suppressDialogs)
                {
                    ShowAbout();        // 用户要求：一打开 app 先弹出「关于」页
                }
                if (AppSettings.ShowGuide && !smokeMode && !this.suppressDialogs)
                {
                    ShowGuide();
                }
            };

            if (smoke)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
                timer.Tick += (s, e) => { timer.Stop(); Close(); };
                timer.Start();
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                try
                {
                    int round = 2; // DWMWCP_ROUND（Win11；Win10 忽略不报错）
                    DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
                }
                catch
                {
                }
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // ================================================================
        //  标题栏
        // ================================================================
        private void OnMinimize(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximize(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            MaxButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (MaxButton != null)
            {
                MaxButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
            }
        }

        // ================================================================
        //  键盘快捷键
        // ================================================================
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                RemoveSelected();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
            {
                AddFilesDialog();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                BrowseOutput();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
            {
                _ = StartWork();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.Add || e.Key == Key.OemPlus))
            {
                strokeCanvas.ZoomBy(1.25);
                UpdateZoomLabel();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.Subtract || e.Key == Key.OemMinus))
            {
                strokeCanvas.ZoomBy(0.8);
                UpdateZoomLabel();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D0)
            {
                strokeCanvas.FitToWindow();
                UpdateZoomLabel();
                e.Handled = true;
            }
            else if (e.Key == Key.F1)
            {
                ShowAbout();
                e.Handled = true;
            }
        }

        // ================================================================
        //  参数滑块
        // ================================================================
        private void InitParamSliders()
        {
            SpacingSlider.Minimum = 1; SpacingSlider.Maximum = 100; SpacingSlider.Value = 25;
            SizeSlider.Minimum = 1; SizeSlider.Maximum = 2500; SizeSlider.Value = 100;
            AngleSlider.Minimum = 0; AngleSlider.Maximum = 360; AngleSlider.Value = 0;
            RoundnessSlider.Minimum = 1; RoundnessSlider.Maximum = 100; RoundnessSlider.Value = 100;
            ParamCard.IsEnabled = false;
            BrushOpsCard.IsEnabled = false;
        }

        private void WireParamPair(Slider slider, TextBox box)
        {
            slider.ValueChanged += (s, e) =>
            {
                if (updatingParams)
                {
                    return;
                }
                updatingParams = true;
                try
                {
                    box.Text = ((int)Math.Round(slider.Value)).ToString();
                }
                finally
                {
                    updatingParams = false;
                }
                ApplyManualParams();
            };
            box.LostFocus += (s, e) =>
            {
                if (updatingParams)
                {
                    return;
                }
                updatingParams = true;
                try
                {
                    int val;
                    if (int.TryParse(box.Text, out val))
                    {
                        val = (int)Math.Min(slider.Maximum, Math.Max(slider.Minimum, val));
                        slider.Value = val;
                        box.Text = val.ToString();
                    }
                    else
                    {
                        box.Text = ((int)Math.Round(slider.Value)).ToString();
                    }
                }
                finally
                {
                    updatingParams = false;
                }
                ApplyManualParams();
            };
        }

        private void ApplyManualParams()
        {
            if (updatingParams || currentBrush == null || SpacingBox == null)
            {
                return;
            }
            int spacing;
            double size, angle, roundness;
            if (!int.TryParse(SpacingBox.Text, out spacing)) spacing = 25;
            if (!double.TryParse(SizeBox.Text, out size)) size = 100;
            if (!double.TryParse(AngleBox.Text, out angle)) angle = 0;
            if (!double.TryParse(RoundnessBox.Text, out roundness)) roundness = 100;
            currentBrush.ManualSpacing = spacing;
            currentBrush.ManualDiameter = size;
            currentBrush.ManualAngle = angle;
            currentBrush.ManualRoundness = roundness;
            strokeCanvas.InvalidatePreview();
            strokeCanvas.ApplyTuning();
            UpdateFidelity(currentBrush);
            UpdateBrushTileSubtext();
        }

        private void SyncSlidersToBoxes()
        {
            SpacingSlider.Value = Math.Min(SpacingSlider.Maximum, Math.Max(SpacingSlider.Minimum, ReadDouble(SpacingBox, 25)));
            SizeSlider.Value = Math.Min(SizeSlider.Maximum, Math.Max(SizeSlider.Minimum, ReadDouble(SizeBox, 100)));
            AngleSlider.Value = Math.Min(AngleSlider.Maximum, Math.Max(AngleSlider.Minimum, ReadDouble(AngleBox, 0)));
            RoundnessSlider.Value = Math.Min(RoundnessSlider.Maximum, Math.Max(RoundnessSlider.Minimum, ReadDouble(RoundnessBox, 100)));
        }

        private static double ReadDouble(TextBox box, double fallback)
        {
            double v;
            return double.TryParse(box.Text, out v) ? v : fallback;
        }

        private void LoadManualParams(ExtractedBrush brush)
        {
            if (ParamCard == null)
            {
                return;
            }
            updatingParams = true;
            try
            {
                if (brush == null)
                {
                    ParamCard.IsEnabled = false;
                    SpacingBox.Text = "25";
                    SizeBox.Text = "100";
                    AngleBox.Text = "0";
                    RoundnessBox.Text = "100";
                    SyncSlidersToBoxes();
                    return;
                }
                SpacingBox.Text = brush.EffectiveSpacingPercent.ToString();
                SizeBox.Text = ((int)Math.Max(1, Math.Min(2500, DefaultManualSize(brush)))).ToString();
                AngleBox.Text = ((int)Math.Max(0, Math.Min(360, Math.Round(brush.EffectiveAngleDegrees())))).ToString();
                RoundnessBox.Text = ((int)brush.EffectiveRoundnessPercent()).ToString();
                SyncSlidersToBoxes();
                ParamCard.IsEnabled = true;
            }
            finally
            {
                updatingParams = false;
            }
        }

        private double DefaultManualSize(ExtractedBrush brush)
        {
            if (brush.ManualDiameter.HasValue)
            {
                return brush.ManualDiameter.Value;
            }
            // 优先用 Procreate 的真实笔刷大小（maxSize），这才是"实际尺寸"；
            // 形状图原始像素（2048 这类）只是位图分辨率，不是笔刷直径。
            if (brush.Params != null)
            {
                object maxSize;
                if (brush.Params.TryGetValue("maxSize", out maxSize))
                {
                    try
                    {
                        double size = Convert.ToDouble(maxSize, CultureInfo.InvariantCulture);
                        if (size > 0)
                        {
                            return Math.Max(1, Math.Min(2500, size));
                        }
                    }
                    catch
                    {
                    }
                }
            }
            string path = brush.ShapePath ?? brush.GrainPath;
            if (path != null && File.Exists(path))
            {
                try
                {
                    using (Bitmap bmp = new Bitmap(path))
                    {
                        return Math.Max(1, Math.Min(2500, Math.Max(bmp.Width, bmp.Height)));
                    }
                }
                catch
                {
                }
            }
            return 100;
        }

        // ================================================================
        //  包列表
        // ================================================================
        private void RefreshPackageList()
        {
            if (PackageList == null)
            {
                return;
            }
            updating = true;
            try
            {
                PackageList.Children.Clear();
                FavoritesHost.Children.Clear();
                packageCards.Clear();

                // 收藏夹：跨包整合的虚拟包（固定置顶，始终可见）
                favoritesCard = new PackageCard();
                favoritesCard.Tag = null;
                favoritesCard.SetName("★ 收藏夹");
                int favCount = CountFavorites();
                favoritesCard.SetMeta(favCount > 0 ? favCount + " 支 · 跨包整合" : "收藏的笔刷会汇总到这里");
                favoritesCard.SetThumb(favCount > 0 ? ImageUtil.ToImageSource(ImageUtil.MakeStarIcon()) : null);
                favoritesCard.SetTooltip("跨笔刷包收藏整合：把各包的收藏汇总到这里，可整体导出一个 ABR");
                favoritesCard.Click += OnPackageCardClicked;
                FavoritesHost.Children.Add(favoritesCard);
                packageCards.Add(favoritesCard);

                foreach (SourceFile source in sources)
                {
                    string scoreLine = PackageScoreLine(source);
                    PackageCard card = new PackageCard();
                    card.Tag = source;
                    card.SetName(source.DisplayName);
                    card.SetMeta(source.Meta + (scoreLine.Length > 0 ? " · " + scoreLine : ""));
                    card.SetThumb(source.Thumb != null ? ImageUtil.ToImageSource(source.Thumb) : null);
                    card.SetTooltip(source.DisplayName);
                    card.Click += OnPackageCardClicked;
                    PackageList.Children.Add(card);
                    packageCards.Add(card);
                }
                EmptyState.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                PackageTitle.Text = sources.Count == 0 ? "笔刷包" : "笔刷包（" + sources.Count + "）";
            }
            finally
            {
                updating = false;
            }

            if (favoritesMode)
            {
                favoritesCard.Selected = true;
                foreach (PackageCard card in packageCards)
                {
                    if (card != favoritesCard)
                    {
                        card.Selected = false;
                    }
                }
                RefreshBrushList();
                return;
            }
            if (sources.Count > 0)
            {
                if (selectedSource == null || !sources.Contains(selectedSource))
                {
                    SelectPackage(sources[0]);
                }
                else
                {
                    foreach (PackageCard card in packageCards)
                    {
                        card.Selected = card.Tag == selectedSource;
                    }
                    RefreshBrushList();
                }
            }
            else
            {
                selectedSource = null;
                favoritesCard.Selected = false;
                RefreshBrushList();
            }
        }

        private void OnPackageCardClicked(object sender, MouseButtonEventArgs e)
        {
            if (updating)
            {
                return;
            }
            PackageCard card = (PackageCard)sender;
            if (card.Tag == null)
            {
                SelectFavorites();
            }
            else
            {
                SelectPackage((SourceFile)card.Tag);
            }
        }

        private void SelectFavorites()
        {
            favoritesMode = true;
            selectedSource = null;
            ClearBrushThumbCache();
            foreach (PackageCard card in packageCards)
            {
                card.Selected = card == favoritesCard;
            }
            RefreshBrushList();
        }

        private void SelectPackage(SourceFile source)
        {
            favoritesMode = false;
            selectedSource = source;
            ClearBrushThumbCache();
            foreach (PackageCard card in packageCards)
            {
                card.Selected = card.Tag == source;
            }
            RefreshBrushList();
        }

        // ================================================================
        //  笔刷列表（筛选 / 排序 / 分批渲染）
        // ================================================================
        private void RefreshBrushList()
        {
            if (BrushList == null)
            {
                return;
            }
            if (FavoritesOnly != null)
            {
                FavoritesOnly.IsEnabled = !favoritesMode;   // 收藏夹里全是收藏，不需要再"只看收藏"
            }
            string preserveSourceId = currentBrush != null ? currentBrush.SourceId : null;
            updating = true;
            try
            {
                brushTiles.Clear();
                BrushList.Items.Clear();
                selectedBrushes.Clear();

                SourceFile selected = selectedSource;
                List<ExtractedBrush> all;
                if (favoritesMode)
                {
                    // 收藏夹：跨包汇总所有已收藏笔刷
                    all = new List<ExtractedBrush>();
                    foreach (SourceFile s in sources)
                    {
                        if (s.ExtractedBrushes == null)
                        {
                            continue;
                        }
                        foreach (ExtractedBrush b in s.ExtractedBrushes)
                        {
                            if (b.Favorite)
                            {
                                all.Add(b);
                            }
                        }
                    }
                }
                else
                {
                    all = selected == null || selected.ExtractedBrushes == null
                        ? new List<ExtractedBrush>()
                        : selected.ExtractedBrushes;
                }
                string query = Search == null ? "" : (Search.Value ?? "").Trim().ToLower();
                List<ExtractedBrush> filtered = new List<ExtractedBrush>();
                foreach (ExtractedBrush brush in all)
                {
                    string image = brush.ShapePath ?? brush.GrainPath;
                    if (image == null || !File.Exists(image))
                    {
                        continue;
                    }
                    string name = brush.Name == null ? "" : brush.Name;
                    if (query.Length > 0 && !name.ToLower().Contains(query))
                    {
                        continue;
                    }
                    if (!favoritesMode && FavoritesOnly != null && FavoritesOnly.IsChecked == true && !brush.Favorite)
                    {
                        continue;
                    }
                    FidelityReport report = BrushFidelity.Evaluate(brush);
                    if (FidelityFilter != null && FidelityFilter.SelectedIndex > 0)
                    {
                        int filterIndex = FidelityFilter.SelectedIndex;
                        if (filterIndex == 1 && report.Level != FidelityLevel.High) continue;
                        if (filterIndex == 2 && report.Level != FidelityLevel.Medium) continue;
                        if (filterIndex == 3 && report.Level != FidelityLevel.Low) continue;
                    }
                    filtered.Add(brush);
                }
                if (sortMode == "score")
                {
                    filtered.Sort((a, b) => BrushFidelity.Evaluate(b).Score.CompareTo(BrushFidelity.Evaluate(a).Score));
                }
                else
                {
                    filtered.Sort((a, b) => string.Compare(a.Name ?? "", b.Name ?? "", StringComparison.Ordinal));
                }
                filteredBrushes = filtered;
                brushBatchIndex = 0;
                loadingBatch = false;
                LoadNextBrushBatch();

                BrushEmpty.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                BrushTitle.Text = favoritesMode
                    ? "收藏夹 · " + all.Count + " 支"
                    : (selected == null ? "笔刷" : selected.DisplayName + " · " + all.Count + " 支");
                if (filtered.Count == 0)
                {
                    if (favoritesMode)
                    {
                        BrushEmpty.Text = all.Count == 0 ? "还没有收藏笔刷。选中笔刷后点「☆ 收藏」" : "没有匹配的收藏笔刷";
                    }
                    else
                    {
                        BrushEmpty.Text = selected == null ? "点选左侧笔刷包查看笔刷" : "没有匹配的笔刷";
                    }
                }
            }
            finally
            {
                updating = false;
            }

            ExtractedBrush toSelect = null;
            if (preserveSourceId != null)
            {
                foreach (ExtractedBrush b in filteredBrushes)
                {
                    if (b.SourceId == preserveSourceId)
                    {
                        toSelect = b;
                        break;
                    }
                }
            }
            if (toSelect == null && brushTiles.Count > 0)
            {
                toSelect = brushTiles[0].Tag as ExtractedBrush;
            }
            if (toSelect == null && filteredBrushes.Count > 0)
            {
                toSelect = filteredBrushes[0];
            }
            SelectCurrentBrush(toSelect);
            UpdateFavoritesButtonText();
        }

        private void OnBrushScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeight - e.ViewportHeight - e.VerticalOffset < 400)
            {
                LoadNextBrushBatch();
            }
        }

        private void LoadNextBrushBatch()
        {
            if (BrushList == null || loadingBatch)
            {
                return;
            }
            loadingBatch = true;
            try
            {
                int end = Math.Min(filteredBrushes.Count, brushBatchIndex + BrushBatchSize);
                for (int i = brushBatchIndex; i < end; i++)
                {
                    ExtractedBrush brush = filteredBrushes[i];
                    Tile tile = BuildTile(brush);
                    BrushList.Items.Add(tile);
                    brushTiles.Add(tile);
                }
                brushBatchIndex = end;
            }
            finally
            {
                loadingBatch = false;
            }
        }

        private Tile BuildTile(ExtractedBrush brush)
        {
            Bitmap thumb = BuildBrushThumb(brush);
            FidelityReport report = BrushFidelity.Evaluate(brush);
            Tile tile = new Tile();
            tile.Tag = brush;
            tile.SetThumb(thumb != null ? ImageUtil.ToImageSource(thumb) : null);
            tile.SetFidelity(report.Level);
            tile.Favorite = brush.Favorite;
            tile.SetTooltip(brush.EffectiveName ?? brush.Name ?? "");
            tile.Click += OnTileClicked;
            tile.DoubleClick += OnTileDoubleClicked;
            UpdateTileText(tile);
            return tile;
        }

        private void UpdateTileText(Tile tile)
        {
            ExtractedBrush brush = (ExtractedBrush)tile.Tag;
            if (brush == null)
            {
                return;
            }
            FidelityReport report = BrushFidelity.Evaluate(brush);
            string text = (brush.EffectiveName ?? "未命名笔刷")
                + "\n" + report.Score + " 分 · 间距 " + brush.EffectiveSpacingPercent + "%"
                + (brush.HasManualAdjust ? " · 手动" : "");
            tile.SetDisplayName(text);
        }

        private Bitmap BuildBrushThumb(ExtractedBrush brush)
        {
            Bitmap cached;
            if (brushThumbCache.TryGetValue(brush, out cached) && cached != null)
            {
                return cached;
            }
            Bitmap thumb = null;
            string image = brush.ShapePath ?? brush.GrainPath;
            if (image != null && File.Exists(image))
            {
                try
                {
                    using (Bitmap source = AbrWriter.BuildAlphaPreviewBitmap(image, false, false) ?? new Bitmap(image))
                    {
                        thumb = ImageUtil.MakeThumbnail(source);
                    }
                }
                catch
                {
                }
            }
            if (thumb == null)
            {
                thumb = ImageUtil.MakeFallbackThumb();
            }
            brushThumbCache[brush] = thumb;
            return thumb;
        }

        private void ClearBrushThumbCache()
        {
            foreach (KeyValuePair<ExtractedBrush, Bitmap> pair in brushThumbCache)
            {
                if (pair.Value != null)
                {
                    pair.Value.Dispose();
                }
            }
            brushThumbCache.Clear();
        }

        // ================================================================
        //  选中 / 多选 / 收藏 / 改名 / 反转
        // ================================================================
        private void OnTileClicked(object sender, MouseButtonEventArgs e)
        {
            if (updating)
            {
                return;
            }
            Tile tile = (Tile)sender;
            ExtractedBrush clickedBrush = (ExtractedBrush)tile.Tag;
            if (clickedBrush == null)
            {
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (selectedBrushes.Contains(clickedBrush))
                {
                    selectedBrushes.Remove(clickedBrush);
                }
                else
                {
                    selectedBrushes.Add(clickedBrush);
                }
                currentBrush = clickedBrush;
                strokeCanvas.SetBrush(clickedBrush);
                LoadManualParams(clickedBrush);
                LoadBrushOps(clickedBrush);
                UpdateFidelity(clickedBrush);
                CanvasTitle.Text = clickedBrush.EffectiveName ?? "未命名笔刷";
                RefreshTileSelection();
            }
            else
            {
                SelectCurrentBrush(clickedBrush);
            }
        }

        private void OnTileDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            Tile tile = (Tile)sender;
            if ((ExtractedBrush)tile.Tag != null)
            {
                ShowBrushPreview((ExtractedBrush)tile.Tag);
            }
        }

        private void SelectCurrentBrush(ExtractedBrush brush)
        {
            currentBrush = brush;
            selectedBrushes.Clear();
            strokeCanvas.SetBrush(brush);
            LoadManualParams(brush);
            LoadBrushOps(brush);
            UpdateFidelity(brush);
            CanvasTitle.Text = brush == null ? "画布" : (brush.EffectiveName ?? "未命名笔刷");
            RefreshTileSelection();
        }

        private void RefreshTileSelection()
        {
            foreach (Tile tile in brushTiles)
            {
                ExtractedBrush b = tile.Tag as ExtractedBrush;
                tile.Selected = b != null && b == currentBrush;
                tile.InSelection = b != null && selectedBrushes.Contains(b);
            }
            UpdateConvertButtonText();
        }

        private static int VersionFromIndex(int index)
        {
            if (index == 1)
            {
                return 7;
            }
            if (index == 2)
            {
                return 2;
            }
            return 6;
        }

        private static int TipCapIndexToSide(int index)
        {
            switch (index)
            {
                case 1: return 2048;
                case 2: return 1024;
                case 3: return 512;
                default: return 0;
            }
        }

        private void UpdateConvertButtonText()
        {
            if (ConvertButton == null)
            {
                return;
            }
            if (favoritesMode)
            {
                int n = CountFavorites();
                ConvertButton.Content = n > 0 ? "转成 ABR（收藏 " + n + " 支）" : "转成 ABR";
                return;
            }
            ConvertButton.Content = selectedBrushes.Count > 0
                ? "转成 ABR（选中 " + selectedBrushes.Count + " 支）"
                : "转成 ABR";
        }

        private int CountFavorites()
        {
            int count = 0;
            foreach (SourceFile source in sources)
            {
                if (source.ExtractedBrushes == null)
                {
                    continue;
                }
                foreach (ExtractedBrush b in source.ExtractedBrushes)
                {
                    if (b.Favorite)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private void UpdateFavoritesButtonText()
        {
            if (FavoritesConvertButton == null)
            {
                return;
            }
            int count = CountFavorites();
            FavoritesConvertButton.Content = count > 0 ? "转收藏（" + count + "）" : "转收藏";
            if (favoritesCard != null)
            {
                favoritesCard.SetMeta(count > 0 ? count + " 支 · 跨包整合" : "收藏的笔刷会汇总到这里");
            }
        }

        private void LoadBrushOps(ExtractedBrush brush)
        {
            if (BrushNameBox == null)
            {
                return;
            }
            updatingParams = true;
            try
            {
                BrushOpsCard.IsEnabled = brush != null;
                if (brush == null)
                {
                    BrushNameBox.Text = "";
                    BrushInvertBox.SelectedIndex = 0;
                    FavoriteButton.Content = "☆ 收藏";
                    return;
                }
                BrushNameBox.Text = brush.EffectiveName ?? "";
                BrushInvertBox.SelectedIndex = !brush.ManualInvert.HasValue ? 0 : (brush.ManualInvert.Value ? 1 : 2);
                FavoriteButton.Content = brush.Favorite ? "★ 已收藏" : "☆ 收藏";
            }
            finally
            {
                updatingParams = false;
            }
        }

        private void OnBrushNameEdited(object sender, TextChangedEventArgs e)
        {
            if (updatingParams || currentBrush == null || BrushNameBox == null)
            {
                return;
            }
            string value = BrushNameBox.Text == null ? "" : BrushNameBox.Text.Trim();
            currentBrush.ManualName = value.Length == 0 ? null : value;
            if (selectedSource != null)
            {
                BrushLibrary.SetName(selectedSource.Path, currentBrush.SourceId, currentBrush.ManualName);
            }
            CanvasTitle.Text = currentBrush.EffectiveName ?? "未命名笔刷";
            foreach (Tile tile in brushTiles)
            {
                if ((ExtractedBrush)tile.Tag == currentBrush)
                {
                    UpdateTileText(tile);
                    tile.SetTooltip(currentBrush.EffectiveName ?? currentBrush.Name ?? "");
                }
            }
            UpdateFidelity(currentBrush);
        }

        private void OnSmartInvert(object sender, RoutedEventArgs e)
        {
            if (currentBrush == null)
            {
                return;
            }
            string image = currentBrush.ShapePath ?? currentBrush.GrainPath;
            if (image == null || !File.Exists(image))
            {
                MessageBox.Show(this, "这支笔刷没有可检测的形状图。", "无法检测", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            bool needInvert = AbrWriter.ShouldAutoInvert(image);
            currentBrush.ManualInvert = needInvert;
            if (!string.IsNullOrEmpty(currentBrush.BrushsetPath))
            {
                BrushLibrary.SetInvert(currentBrush.BrushsetPath, currentBrush.SourceId, needInvert);
            }
            // 同步下拉与画布
            updatingParams = true;
            try
            {
                BrushInvertBox.SelectedIndex = needInvert ? 1 : 2;
            }
            finally
            {
                updatingParams = false;
            }
            if (strokeCanvas != null)
            {
                strokeCanvas.SetBrush(currentBrush);
            }
            UpdateFidelity(currentBrush);
            StatusText.Text = "智能反相检测：该笔刷" + (needInvert ? "需要反转" : "无需反转") + "，已应用到「反转」设置。";
        }

        private void OnBrushInvertChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingParams || currentBrush == null || BrushInvertBox == null)
            {
                return;
            }
            bool? next;
            if (BrushInvertBox.SelectedIndex <= 0)
            {
                next = null;
            }
            else if (BrushInvertBox.SelectedIndex == 1)
            {
                next = true;
            }
            else
            {
                next = false;
            }
            currentBrush.ManualInvert = next;
            if (selectedSource != null)
            {
                BrushLibrary.SetInvert(selectedSource.Path, currentBrush.SourceId, next);
            }
            if (strokeCanvas != null)
            {
                strokeCanvas.SetBrush(currentBrush);
            }
            UpdateFidelity(currentBrush);
        }

        private void OnToggleFavorite(object sender, RoutedEventArgs e)
        {
            if (currentBrush == null)
            {
                return;
            }
            currentBrush.Favorite = !currentBrush.Favorite;
            FavoriteButton.Content = currentBrush.Favorite ? "★ 已收藏" : "☆ 收藏";
            // 用笔刷自身所在包路径持久化（收藏夹模式下 selectedSource 为 null）
            if (!string.IsNullOrEmpty(currentBrush.BrushsetPath))
            {
                BrushLibrary.SetFavorite(currentBrush.BrushsetPath, currentBrush.SourceId, currentBrush.Favorite);
            }
            bool removedFromView = (favoritesMode || (FavoritesOnly != null && FavoritesOnly.IsChecked == true)) && !currentBrush.Favorite;
            if (removedFromView)
            {
                // 取消收藏且当前在看收藏 → 该笔刷从列表消失
                RefreshBrushList();
            }
            else
            {
                foreach (Tile tile in brushTiles)
                {
                    if ((ExtractedBrush)tile.Tag == currentBrush)
                    {
                        tile.Favorite = currentBrush.Favorite;
                        UpdateTileText(tile);
                    }
                }
                UpdateFidelity(currentBrush);
            }
            UpdateFavoritesButtonText();
        }

        private void UpdateBrushTileSubtext()
        {
            if (currentBrush == null)
            {
                return;
            }
            foreach (Tile tile in brushTiles)
            {
                if ((ExtractedBrush)tile.Tag == currentBrush)
                {
                    UpdateTileText(tile);
                }
            }
        }

        // ================================================================
        //  还原度报告
        // ================================================================
        private void UpdateFidelity(ExtractedBrush brush)
        {
            if (FidelityHint == null)
            {
                return;
            }
            bool has = brush != null;
            FidelityHint.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            ScoreText.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            LevelBadge.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            ParamsText.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            NotesText.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            if (!has)
            {
                return;
            }
            FidelityReport report = BrushFidelity.Evaluate(brush);
            WpfColor color = Theme.FidelityColor(report.Level);
            ScoreText.Text = report.Score + " / 100";
            ScoreText.Foreground = new SolidColorBrush(color);
            LevelText.Text = BrushFidelity.LevelLabel(report.Level);
            LevelBadge.Background = new SolidColorBrush(color);
            List<string> paramLines = BrushFidelity.DescribeParams(brush);
            ParamsText.Text = paramLines.Count == 0 ? "无" : string.Join(Environment.NewLine, paramLines.ToArray());
            NotesText.Text = report.Notes.Count == 0
                ? "没有扣分项，可直接高保真转换。"
                : string.Join(Environment.NewLine, report.Notes.ToArray());
        }

        // ================================================================
        //  文件与导入
        // ================================================================
        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddPaths(files);
            }
        }

        public void AddPaths(string[] paths)
        {
            List<string> files = new List<string>();
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    files.AddRange(Directory.GetFiles(path));
                }
                else if (File.Exists(path))
                {
                    files.Add(path);
                }
            }
            List<string> added = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (string file in files)
            {
                if (!Path.GetExtension(file).Equals(".brushset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string full;
                try
                {
                    full = Path.GetFullPath(file);
                }
                catch
                {
                    full = file;
                }
                // 去重：同一个包拖两次不重复导入
                if (seen.Add(full) && !sources.Any(s => string.Equals(Path.GetFullPath(s.Path), full, StringComparison.OrdinalIgnoreCase)))
                {
                    added.Add(file);
                }
            }
            if (added.Count == 0)
            {
                if (files.Count > 0)
                {
                    MessageBox.Show(this, "这个程序只支持 .brushset 笔刷包。", "不支持的文件", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }
            // 统一走后台导入（唯一入口），避免重复添加
            StartImport(added);
        }

        private async void StartImport(List<string> files)
        {
            if (files.Count == 0)
            {
                return;
            }
            // 串行队列：连拖多个包都逐个导入，不丢不重
            importQueue.Enqueue(files);
            if (importBusy)
            {
                return;
            }
            importBusy = true;
            ImportPanel.Visibility = Visibility.Visible;

            IProgress<(int pct, string name)> progress = new Progress<(int pct, string name)>(t =>
            {
                ImportProgress.Value = Math.Min(100, Math.Max(0, t.pct));
                ImportStatus.Text = "正在导入：" + t.name + "…";
            });

            while (importQueue.Count > 0)
            {
                List<string> batch = importQueue.Dequeue();
                ImportStatus.Text = "正在导入 " + batch.Count + " 个笔刷包…";
                ImportProgress.Value = 0;

                List<SourceFile> result = await Task.Run(() =>
                {
                    List<SourceFile> parsed = new List<SourceFile>();
                    for (int i = 0; i < batch.Count; i++)
                    {
                        try
                        {
                            SourceFile sf = CreateSource(batch[i]);
                            if (sf != null)
                            {
                                parsed.Add(sf);
                            }
                        }
                        catch (Exception ex)
                        {
                            AppLog.Write("添加文件失败: " + batch[i] + " -> " + ex);
                        }
                        progress.Report(((i + 1) * 100 / batch.Count, Path.GetFileName(batch[i])));
                    }
                    return parsed;
                });

                foreach (SourceFile sf in result)
                {
                    sources.Add(sf);
                }
            }

            importBusy = false;
            ImportPanel.Visibility = Visibility.Collapsed;
            if (sources.Count > 0 && string.IsNullOrWhiteSpace(OutputBox.Text))
            {
                string first = sources[0].Path;
                OutputBox.Text = Path.Combine(Path.GetDirectoryName(first), Path.GetFileNameWithoutExtension(first) + ".abr");
            }
            RefreshPackageList();
            StatusText.Text = "就绪";
        }

        private SourceFile CreateSource(string file)
        {
            SourceFile source = new SourceFile { Path = file, DisplayName = Path.GetFileName(file) };
            FileInfo info = new FileInfo(file);
            source.Meta = SourceFile.HumanSize(info.Length);
            try
            {
                string temp = Path.Combine(Path.GetTempPath(), "BrushStudio-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                tempCleanup.Add(temp);
                List<ExtractedBrush> brushes = BrushsetExtractor.Extract(file, temp, false);
                source.ExtractedBrushes = brushes;
                foreach (ExtractedBrush brush in brushes)
                {
                    LibraryEntry entry = BrushLibrary.Get(file, brush.SourceId);
                    if (entry != null)
                    {
                        brush.ManualName = entry.Name;
                        brush.ManualInvert = entry.Invert;
                        brush.Favorite = entry.Favorite;
                    }
                }
                source.Meta = brushes.Count + " 支笔刷  ·  " + SourceFile.HumanSize(info.Length);
                foreach (ExtractedBrush brush in brushes)
                {
                    if (brush.ShapePath != null && File.Exists(brush.ShapePath))
                    {
                        try
                        {
                            using (Bitmap bmp = AbrWriter.BuildAlphaPreviewBitmap(brush.ShapePath, false, false) ?? new Bitmap(brush.ShapePath))
                            {
                                source.Thumb = ImageUtil.MakeThumbnail(bmp);
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                if (source.Thumb == null)
                {
                    source.Thumb = ImageUtil.MakeFallbackThumb();
                }
            }
            catch (Exception ex)
            {
                source.Meta = "解析失败：" + ex.Message;
                source.Thumb = ImageUtil.MakeFallbackThumb();
            }
            return source;
        }

        private static string PackageScoreLine(SourceFile source)
        {
            List<ExtractedBrush> brushes = source.ExtractedBrushes;
            if (brushes == null || brushes.Count == 0)
            {
                return "";
            }
            int total = 0;
            foreach (ExtractedBrush brush in brushes)
            {
                total += BrushFidelity.Evaluate(brush).Score;
            }
            int average = total / brushes.Count;
            FidelityLevel level = average >= 85 ? FidelityLevel.High : average >= 60 ? FidelityLevel.Medium : FidelityLevel.Low;
            return "平均 " + average + " 分 · " + BrushFidelity.LevelLabel(level);
        }

        // ================================================================
        //  操作按钮
        // ================================================================
        private void OnAddFiles(object sender, RoutedEventArgs e)
        {
            AddFilesDialog();
        }

        private void OnRemove(object sender, RoutedEventArgs e)
        {
            RemoveSelected();
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            ClearAll();
        }

        private void RemoveSelected()
        {
            if (selectedSource == null)
            {
                return;
            }
            if (selectedSource.Thumb != null)
            {
                selectedSource.Thumb.Dispose();
            }
            sources.Remove(selectedSource);
            selectedSource = null;
            ClearBrushThumbCache();
            RefreshPackageList();
        }

        private void ClearAll()
        {
            foreach (SourceFile source in sources)
            {
                if (source.Thumb != null)
                {
                    source.Thumb.Dispose();
                }
            }
            sources.Clear();
            selectedSource = null;
            favoritesMode = false;
            ClearBrushThumbCache();
            RefreshPackageList();
            SelectCurrentBrush(null);
        }

        private void AddFilesDialog()
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Title = "添加笔刷包";
            dialog.Filter = "Procreate 笔刷包 (*.brushset)|*.brushset|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog(this) == true)
            {
                AddPaths(dialog.FileNames);
            }
        }

        private void OnBrowseOutput(object sender, RoutedEventArgs e)
        {
            BrowseOutput();
        }

        private void BrowseOutput()
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Title = "保存 ABR 文件";
            dialog.Filter = "Photoshop 笔刷 (*.abr)|*.abr|所有文件 (*.*)|*.*";
            dialog.DefaultExt = "abr";
            dialog.AddExtension = true;
            string initialFolder = "";
            if (!string.IsNullOrEmpty(AppSettings.LastFolder) && Directory.Exists(AppSettings.LastFolder))
            {
                initialFolder = AppSettings.LastFolder;
            }
            else if (sources.Count > 0)
            {
                initialFolder = Path.GetDirectoryName(sources[0].Path);
            }
            if (initialFolder.Length > 0)
            {
                dialog.InitialDirectory = initialFolder;
            }
            if (!string.IsNullOrEmpty(AppSettings.LastOutput))
            {
                dialog.FileName = Path.GetFileName(AppSettings.LastOutput);
            }
            if (dialog.ShowDialog(this) == true)
            {
                OutputBox.Text = dialog.FileName;
                AppSettings.LastOutput = dialog.FileName;
                AppSettings.LastFolder = Path.GetDirectoryName(dialog.FileName);
            }
        }

        // ================================================================
        //  画布工具栏
        // ================================================================
        private void OnPickColor(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.ColorDialog dialog = new System.Windows.Forms.ColorDialog())
            {
                dialog.Color = System.Drawing.Color.FromArgb(brushColor.R, brushColor.G, brushColor.B);
                dialog.FullOpen = true;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    brushColor = WpfColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    strokeCanvas.SetBrushColor(brushColor);
                    if (ColorSwatch != null)
                    {
                        ColorSwatch.Fill = new SolidColorBrush(brushColor);
                    }
                }
            }
        }

        private void OnCanvasBackgroundChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ready || strokeCanvas == null || BackgroundBox.SelectedIndex < 0)
            {
                return;
            }
            strokeCanvas.SetBackgroundMode((PreviewBackground)BackgroundBox.SelectedIndex);
        }

        private void OnClearCanvas(object sender, RoutedEventArgs e)
        {
            strokeCanvas.ClearStrokes();
        }

        private void OnToggleAutoPreview(object sender, RoutedEventArgs e)
        {
            bool next = !strokeCanvas.AutoPreview;
            strokeCanvas.SetAutoPreview(next);
            AutoPreviewButton.Background = next
                ? (Brush)FindResource("AccentSoftBrush")
                : (Brush)FindResource("CardBrush");
        }

        private void OnZoomOut(object sender, RoutedEventArgs e)
        {
            strokeCanvas.ZoomBy(0.8);
            UpdateZoomLabel();
        }

        private void OnZoomIn(object sender, RoutedEventArgs e)
        {
            strokeCanvas.ZoomBy(1.25);
            UpdateZoomLabel();
        }

        private void OnFit(object sender, RoutedEventArgs e)
        {
            strokeCanvas.FitToWindow();
            UpdateZoomLabel();
        }

        private async void OnCompare(object sender, RoutedEventArgs e)
        {
            if (currentBrush == null || string.IsNullOrEmpty(currentBrush.BrushsetPath))
            {
                MessageBox.Show(this, "先选择一支笔刷。", "没有笔刷", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ExtractedBrush brush = currentBrush;
            string brushsetPath = brush.BrushsetPath;
            StatusText.Text = "正在渲染对比图…";
            Bitmap sheet = await Task.Run(() => RenderCompareTool.RenderSingleCompare(brush, brushsetPath));
            StatusText.Text = "就绪";
            if (sheet == null)
            {
                MessageBox.Show(this, "对比图渲染失败。", "渲染失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ImageSource src = ImageUtil.ToImageSource(sheet);
            sheet.Dispose();
            Window w = new Window
            {
                Title = "Procreate 对比 · " + (brush.EffectiveName ?? brush.Name),
                Width = 900,
                Height = 560,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = (Brush)FindResource("BgBrush"),
                Content = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new System.Windows.Controls.Image { Source = src, Stretch = Stretch.None, Margin = new Thickness(12) }
                }
            };
            w.ShowDialog();
        }

        private void UpdateZoomLabel()
        {
            if (ZoomText != null && strokeCanvas != null)
            {
                ZoomText.Text = ((int)Math.Round(strokeCanvas.Zoom * 100)) + "%";
            }
        }

        // ================================================================
        //  转换流程
        // ================================================================
        private async void OnExportBrushset(object sender, RoutedEventArgs e)
        {
            List<ExtractedBrush> brushes = new List<ExtractedBrush>();
            if (favoritesMode)
            {
                foreach (SourceFile s in sources)
                {
                    if (s.ExtractedBrushes == null)
                    {
                        continue;
                    }
                    foreach (ExtractedBrush b in s.ExtractedBrushes)
                    {
                        if (b.Favorite)
                        {
                            brushes.Add(b);
                        }
                    }
                }
            }
            else if (selectedBrushes.Count > 0)
            {
                brushes.AddRange(selectedBrushes);
            }
            else if (selectedSource != null && selectedSource.ExtractedBrushes != null)
            {
                brushes.AddRange(selectedSource.ExtractedBrushes);
            }
            if (brushes.Count == 0)
            {
                MessageBox.Show(this, "没有可导出的笔刷。", "没有素材", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Title = "导出 Procreate 笔刷包";
            dialog.Filter = "Procreate 笔刷包 (*.brushset)|*.brushset|所有文件 (*.*)|*.*";
            dialog.DefaultExt = "brushset";
            dialog.AddExtension = true;
            dialog.FileName = (favoritesMode
                ? "收藏夹"
                : (selectedSource != null ? Path.GetFileNameWithoutExtension(selectedSource.DisplayName) : "笔刷")) + ".brushset";
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            int count = await Task.Run(() => BrushsetBuilder.Build(dialog.FileName, brushes));
            if (count == 0)
            {
                MessageBox.Show(this, "导出失败：没有可用笔刷。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MessageBox.Show(this, "已导出 " + count + " 支笔刷到 .brushset 文件。\n\n" + dialog.FileName, "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void OnConvert(object sender, RoutedEventArgs e)
        {
            await StartWork();
        }

        private void OnConvertFavorites(object sender, RoutedEventArgs e)
        {
            _ = StartFavoritesWork();
        }

        private async Task StartWork()
        {
            HashSet<string> include = null;
            if (favoritesMode)
            {
                // 收藏夹整体导出：跨包收藏汇总成一个 ABR
                if (CountFavorites() == 0)
                {
                    MessageBox.Show(this, "还没有收藏任何笔刷。选中笔刷后点「☆ 收藏」。", "没有收藏", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                include = new HashSet<string>();
                foreach (SourceFile source in sources)
                {
                    if (source.ExtractedBrushes == null)
                    {
                        continue;
                    }
                    foreach (ExtractedBrush b in source.ExtractedBrushes)
                    {
                        if (b.Favorite)
                        {
                            include.Add(b.SourceId);
                        }
                    }
                }
                await StartWorkWithInclude(include, true);
                return;
            }
            if (selectedBrushes.Count > 0)
            {
                include = new HashSet<string>();
                foreach (ExtractedBrush b in selectedBrushes)
                {
                    include.Add(b.SourceId);
                }
            }
            await StartWorkWithInclude(include, false);
        }

        private async Task StartFavoritesWork()
        {
            HashSet<string> include = new HashSet<string>();
            foreach (SourceFile source in sources)
            {
                if (source.ExtractedBrushes == null)
                {
                    continue;
                }
                foreach (ExtractedBrush b in source.ExtractedBrushes)
                {
                    if (b.Favorite)
                    {
                        include.Add(b.SourceId);
                    }
                }
            }
            if (include.Count == 0)
            {
                MessageBox.Show(this, "还没有收藏任何笔刷。选中笔刷后点「☆ 收藏」。", "没有收藏", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await StartWorkWithInclude(include, true);
        }

        private async Task StartWorkWithInclude(HashSet<string> includeIds, bool forceMerge = false)
        {
            if (busy)
            {
                return;
            }
            if (sources.Count == 0)
            {
                MessageBox.Show(this, "请先添加要处理的文件。", "没有素材", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string output = OutputBox.Text.Trim();
            if (output.Length == 0)
            {
                MessageBox.Show(this, "请选择输出位置。", "没有输出路径", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!output.EndsWith(".abr", StringComparison.OrdinalIgnoreCase))
            {
                output += ".abr";
                OutputBox.Text = output;
            }
            if (File.Exists(output))
            {
                MessageBoxResult answer = MessageBox.Show(this, "文件已存在，是否覆盖？\n\n" + output, "覆盖文件", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            WorkArgs args = new WorkArgs
            {
                Output = output,
                Sources = new List<SourceFile>(sources),
                Version = VersionFromIndex(VersionBox.SelectedIndex),
                Invert = InvertBox.IsChecked == true,
                Autocrop = CropBox.IsChecked == true,
                UseRle = RawBox.IsChecked != true,
                IncludeIds = includeIds,
                SeparateExport = !forceMerge && SeparateExportBox.IsChecked == true,
                MaxTipSide = TipCapBox == null ? 0 : TipCapIndexToSide(TipCapBox.SelectedIndex)
            };

            SetBusy(true);
            StatusText.Text = "正在读取笔刷并写入 ABR…";
            ProgressBar.Value = 0;

            var progress = new Progress<(int pct, string text)>(t =>
            {
                ProgressBar.Value = Math.Min(100, Math.Max(0, t.pct));
                if (!string.IsNullOrEmpty(t.text))
                {
                    StatusText.Text = t.text;
                }
            });

            try
            {
                WorkResult result = await Task.Run(() => DoWork(args, progress));
                SetBusy(false);
                ProgressBar.Value = 100;
                lastOutput = result.Output;
                OpenOutputButton.IsEnabled = true;
                StatusText.Text = "完成：已生成 " + result.Generated + " 支 ABR 笔刷";
                int failed = result.Count - result.Generated;
                string summary = "转换完成：共处理 " + result.Count + " 支笔刷，成功生成 " + result.Generated + " 支 Photoshop 笔刷";
                if (failed > 0)
                {
                    summary += "，" + failed + " 支跳过或失败";
                }
                if (result.Outputs != null && result.Outputs.Count > 1)
                {
                    summary += "\n\n共 " + result.Outputs.Count + " 个 ABR 文件：";
                    foreach (string f in result.Outputs)
                    {
                        summary += "\n" + f;
                    }
                }
                else
                {
                    summary += "。\n\nABR 文件：" + result.AbrPath;
                }
                MessageBox.Show(this, summary, "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                if (AutoOpenBox.IsChecked == true)
                {
                    OpenOutput();
                }
            }
            catch (Exception ex)
            {
                SetBusy(false);
                StatusText.Text = "处理失败";
                MessageBox.Show(this, ex.Message + "\n\n错误日志：" + AppLog.LogFilePath, "处理失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static WorkResult DoWork(WorkArgs args, IProgress<(int pct, string text)> progress)
        {
            int totalCount = 0;
            int totalGenerated = 0;
            List<string> outputs = new List<string>();
            if (args.SeparateExport && args.Sources.Count > 1)
            {
                string dir = Path.GetDirectoryName(args.Output);
                for (int i = 0; i < args.Sources.Count; i++)
                {
                    string name = SanitizeFileName(Path.GetFileNameWithoutExtension(args.Sources[i].DisplayName));
                    if (name.Length == 0)
                    {
                        name = "brush" + (i + 1);
                    }
                    string outPath = Path.Combine(dir, name + ".abr");
                    int counter = 2;
                    while (File.Exists(outPath) || outputs.Contains(outPath))
                    {
                        outPath = Path.Combine(dir, name + " " + counter + ".abr");
                        counter++;
                    }
                    int count;
                    int gen = BrushsetConverter.ConvertToAbr(outPath, new List<string> { args.Sources[i].Path }, args.Version, args.UseRle, args.Invert, args.Autocrop, out count, p => progress.Report((((i * 100) + p) / args.Sources.Count, "正在转换：" + args.Sources[i].DisplayName)), null, args.IncludeIds, args.MaxTipSide);
                    totalCount += count;
                    totalGenerated += gen;
                    if (gen > 0)
                    {
                        outputs.Add(outPath);
                    }
                }
            }
            else
            {
                List<string> brushsetPaths = new List<string>(args.Sources.Count);
                foreach (SourceFile source in args.Sources)
                {
                    brushsetPaths.Add(source.Path);
                }
                int brushCount;
                int generated = BrushsetConverter.ConvertToAbr(args.Output, brushsetPaths, args.Version, args.UseRle, args.Invert, args.Autocrop, out brushCount, p => progress.Report((p, (string)null)), null, args.IncludeIds, args.MaxTipSide);
                totalCount = brushCount;
                totalGenerated = generated;
                outputs.Add(args.Output);
            }
            return new WorkResult
            {
                Count = totalCount,
                Output = args.Output,
                Generated = totalGenerated,
                AbrPath = outputs.Count > 0 ? outputs[0] : args.Output,
                Outputs = outputs
            };
        }

        private static string SanitizeFileName(string name)
        {
            if (name == null)
            {
                return "";
            }
            char[] invalid = Path.GetInvalidFileNameChars();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return sb.ToString();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            AddButton.IsEnabled = !value;
            RemoveButton.IsEnabled = !value;
            ClearButton.IsEnabled = !value;
            ConvertButton.IsEnabled = !value;
            FavoritesConvertButton.IsEnabled = !value;
            BrowseButton.IsEnabled = !value;
            OutputBox.IsEnabled = !value;
            VersionBox.IsEnabled = !value;
            InvertBox.IsEnabled = !value;
            CropBox.IsEnabled = !value;
            RawBox.IsEnabled = !value;
            AutoOpenBox.IsEnabled = !value;
            ClearCanvasButton.IsEnabled = !value;
            if (value)
            {
                OpenOutputButton.IsEnabled = false;
            }
        }

        // ================================================================
        //  其他
        // ================================================================
        private void OpenOutput()
        {
            if (string.IsNullOrEmpty(lastOutput))
            {
                return;
            }
            try
            {
                if (File.Exists(lastOutput))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + lastOutput + "\"");
                }
                else if (Directory.Exists(lastOutput))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + lastOutput + "\"");
                }
            }
            catch
            {
            }
        }

        private void OnOpenOutput(object sender, RoutedEventArgs e)
        {
            OpenOutput();
        }

        private void ShowAbout()
        {
            AboutWindow dialog = new AboutWindow { Owner = this };
            dialog.ShowDialog();
        }

        private void OnShowAbout(object sender, RoutedEventArgs e)
        {
            ShowAbout();
        }

        private void ShowBrushPreview(ExtractedBrush brush)
        {
            if (brush == null)
            {
                return;
            }
            BrushPreviewWindow dialog = new BrushPreviewWindow(brush, InvertBox != null && InvertBox.IsChecked == true) { Owner = this };
            dialog.ShowDialog();
        }

        private void OnLoadSample(object sender, RoutedEventArgs e)
        {
            LoadSampleBrushset();
        }

        private void LoadSampleBrushset()
        {
            try
            {
                string samplePath = SampleBrushset.CreateSample();
                if (samplePath != null)
                {
                    tempCleanup.Add(Path.GetDirectoryName(samplePath));
                    AddPaths(new[] { samplePath });
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("加载示例笔刷包失败: " + ex);
            }
        }

        private void OnGuideDone(object sender, RoutedEventArgs e)
        {
            AppSettings.ShowGuide = false;
            GuideOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowGuide()
        {
            GuideOverlay.Visibility = Visibility.Visible;
        }

        private void OnFavoritesFilter(object sender, RoutedEventArgs e)
        {
            if (ready)
            {
                RefreshBrushList();
            }
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ready)
            {
                RefreshBrushList();
            }
        }

        private void OnSortChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ready || SortBox == null)
            {
                return;
            }
            sortMode = SortBox.SelectedIndex == 1 ? "score" : "name";
            RefreshBrushList();
        }

        private void OnSeparateExportChanged(object sender, RoutedEventArgs e)
        {
            UpdateConvertButtonText();
        }

        private void OnInvertChanged(object sender, RoutedEventArgs e)
        {
            if (strokeCanvas != null)
            {
                strokeCanvas.SetInvert(InvertBox.IsChecked == true);
            }
        }

        private void OnPureChanged(object sender, RoutedEventArgs e)
        {
            AppSettings.Pure = PureBox.IsChecked == true;
            AppSettings.Save();
        }

        private void ApplySavedWindowState()
        {
            try
            {
                if (AppSettings.WindowWidth >= MinWidth && AppSettings.WindowHeight >= MinHeight)
                {
                    System.Drawing.Rectangle saved = new System.Drawing.Rectangle(AppSettings.WindowX, AppSettings.WindowY, AppSettings.WindowWidth, AppSettings.WindowHeight);
                    bool visible = false;
                    foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                    {
                        if (screen.WorkingArea.IntersectsWith(saved))
                        {
                            visible = true;
                            break;
                        }
                    }
                    if (visible)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual;
                        Left = saved.X;
                        Top = saved.Y;
                        Width = saved.Width;
                        Height = saved.Height;
                    }
                }
                if (AppSettings.WindowMaximized)
                {
                    WindowState = WindowState.Maximized;
                }
            }
            catch
            {
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            CleanupTemp();
        }

        private void SaveSettings()
        {
            if (OutputBox != null)
            {
                AppSettings.LastOutput = OutputBox.Text.Trim();
            }
            if (VersionBox != null)
            {
                AppSettings.VersionIndex = VersionBox.SelectedIndex;
            }
            if (InvertBox != null)
            {
                AppSettings.Invert = InvertBox.IsChecked == true;
                AppSettings.Pure = PureBox.IsChecked == true;
                AppSettings.Autocrop = CropBox.IsChecked == true;
                AppSettings.UseRle = RawBox.IsChecked != true;
                AppSettings.AutoOpen = AutoOpenBox.IsChecked == true;
            }
            if (WindowState == WindowState.Normal)
            {
                AppSettings.WindowX = (int)Left;
                AppSettings.WindowY = (int)Top;
                AppSettings.WindowWidth = (int)Width;
                AppSettings.WindowHeight = (int)Height;
            }
            AppSettings.WindowMaximized = WindowState == WindowState.Maximized;
            AppSettings.Save();
        }

        private void CleanupTemp()
        {
            foreach (string dir in tempCleanup)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch
                {
                }
            }
            tempCleanup.Clear();
        }

        /// <summary>把当前窗口渲染为 PNG（--screenshot 等 CLI 使用）。</summary>
        public void CaptureToFile(string path)
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(this);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path))
            {
                encoder.Save(fs);
            }
        }

        private sealed class WorkArgs
        {
            public string Output;
            public List<SourceFile> Sources;
            public int Version;
            public bool Invert;
            public bool Autocrop;
            public bool UseRle;
            public HashSet<string> IncludeIds;
            public bool SeparateExport;
            public int MaxTipSide;
        }

        private sealed class WorkResult
        {
            public int Count;
            public string Output;
            public int Generated;
            public string AbrPath;
            public List<string> Outputs = new List<string>();
        }
    }
}
