using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace IT_viddil_monitoring;

public partial class MainWindow : Window
{
    private readonly ProcessMonitor _monitor = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<ProcessRow> _processRows = [];
    private readonly ObservableCollection<BarRow> _barRows = [];
    private bool _sampling;
    private bool _closed;
    private bool _uiReady;
    private bool _isLight = true;
    private MonitorSnapshot? _snapshot;
    private List<ProcessGroup> _groups = [];
    private List<ProcessGroup> _visibleGroups = [];

    public MainWindow()
    {
        InitializeComponent();
        var workArea = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, Math.Max(1, workArea.Width - 24));
        MinHeight = Math.Min(MinHeight, Math.Max(1, workArea.Height - 24));
        Width = Math.Min(Width, workArea.Width - 24);
        Height = Math.Min(Height, workArea.Height - 24);
        ProcessList.ItemsSource = _processRows;
        ChartBars.ItemsSource = _barRows;
        _uiReady = true;
        ApplyTheme();
        SourceInitialized += (_, _) => NativeWindowTheme.Apply(this, _isLight);
        SystemParameters.StaticPropertyChanged += SystemParameters_Changed;
        _timer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) => { UpdateLayoutMode(); _timer.Start(); await RefreshAsync(); };
        Closed += (_, _) => { _closed = true; _timer.Stop(); SystemParameters.StaticPropertyChanged -= SystemParameters_Changed; };
    }

    private async Task RefreshAsync()
    {
        if (_sampling || _closed) return;
        _sampling = true;
        try
        {
            var result = await Task.Run(_monitor.Sample);
            if (_closed) return;
            _snapshot = result;
            Render();
            StatusText.Text = $"● Оновлено {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { if (!_closed) StatusText.Text = "Помилка вимірювання: " + ex.Message; }
        finally { _sampling = false; }
    }

    private void Render()
    {
        if (_snapshot is null) return;
        var live = LiveToggle.IsChecked == true;
        var seconds = _snapshot.Elapsed.TotalSeconds;
        ElapsedText.Text = _snapshot.Elapsed.TotalDays >= 1
            ? $"{(int)_snapshot.Elapsed.TotalDays}д " + _snapshot.Elapsed.ToString(@"hh\:mm\:ss")
            : _snapshot.Elapsed.ToString(@"hh\:mm\:ss");
        ElapsedText.FontSize = _snapshot.Elapsed.TotalDays >= 1 ? 24 : 30;
        ElapsedText.ToolTip = ElapsedText.Text;
        ModeHint.Text = live ? "Навантаження зараз" : "З моменту відкриття";
        ModeDescription.Text = live ? "Поточний інтервал вимірювання · приблизно 1 секунда"
            : "Середні показники за весь час спостереження";
        var records = _snapshot.Processes.Where(p =>
            !ProcessMonitor.IsMonitorProcess(p.Pid, p.Name) && (!live || p.Active));
        _groups = records.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProcessGroup(
                group.Key, group.Count(), group.Count(p => p.Active),
                group.Select(p => p.Pid).Distinct().ToArray(),
                group.SelectMany(p => p.Services).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                group.Sum(p => p.CpuPercent), group.Sum(p => p.CpuSeconds),
                group.Sum(p => p.DiskBytesPerSecond), group.Sum(p => p.DiskBytes),
                group.Sum(p => p.MemoryBytes), group.Sum(p => p.AverageMemoryBytes)))
            .ToList();

        OverallCaption.Text = live ? "Топ 1 · Зараз" : "Топ 1 · З моменту відкриття";
        var cpuTotal = _groups.Sum(p => Cpu(p, live, seconds));
        var diskTotal = _groups.Sum(p => Disk(p, live, seconds));
        var memoryTotal = _groups.Sum(p => Memory(p, live));
        static double Share(double amount, double total) => total > 0 ? amount / total : 0;
        var overall = _groups.OrderByDescending(p =>
            Share(Cpu(p, live, seconds), cpuTotal) +
            Share(Disk(p, live, seconds), diskTotal) +
            Share(Memory(p, live), memoryTotal)).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        OverallLeader.Text = overall is null ? "Немає даних" :
            overall.Services.Length == 1 ? $"{overall.Name} · {overall.Services[0]}" : overall.Name;
        OverallLeader.ToolTip = overall is null ? null : Details(overall) +
            "\n\nСукупний рейтинг: CPU, пам’ять та I/O мають рівну вагу.";
        OverallStats.Text = overall is null ? "CPU + Пам’ять + I/O" :
            $"CPU {Cpu(overall, live, seconds):0.0} %   ·   ОЗП {FormatBytes(Memory(overall, live))}   ·   I/O {FormatRate(Disk(overall, live, seconds))}";
        SetLeader(_groups.OrderByDescending(p => Cpu(p, live, seconds)).ThenBy(p => p.Name).FirstOrDefault(),
            CpuLeader, CpuValue, p => $"{Cpu(p, live, seconds):0.0} %");
        SetLeader(_groups.OrderByDescending(p => Disk(p, live, seconds)).ThenBy(p => p.Name).FirstOrDefault(),
            DiskLeader, DiskValue, p => FormatRate(Disk(p, live, seconds)));
        SetLeader(_groups.OrderByDescending(p => Memory(p, live)).ThenBy(p => p.Name).FirstOrDefault(),
            MemoryLeader, MemoryValue, p => FormatBytes(Memory(p, live)));

        var query = SearchBox.Text.Trim();
        _visibleGroups = _groups.Where(p => query.Length == 0 ||
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Services.Any(s => s.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        ProcessCountText.Text = query.Length == 0 ? $"{_groups.Count} програм" : $"{_visibleGroups.Count} з {_groups.Count}";
        var metric = SortMetric.SelectedIndex;
        var sorted = _visibleGroups.OrderByDescending(p => Amount(p, metric, live, seconds))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var rows = sorted.Select(p => new ProcessRow(p.Name)
        {
            CountDetail = $"{p.Count} {CountLabel(p.Count)}" +
                (p.Services.Length > 0 ? $" · служб: {p.Services.Length}" : "") +
                (p.ActiveCount == 0 ? " · завершено" : ""),
            Detail = Details(p),
            CpuText = $"{Cpu(p, live, seconds):0.0} %",
            MemoryText = FormatBytes(Memory(p, live)),
            DiskText = FormatRate(Disk(p, live, seconds))
        }).ToList();
        SyncRows(_processRows, rows, row => row.Name, (current, incoming) => current.CopyFrom(incoming));
        ProcessEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DrawChart();
    }

    private static void SetLeader(ProcessGroup? group, TextBlock name, TextBlock value, Func<ProcessGroup, string> format)
    {
        name.Text = group?.Name ?? "Немає даних";
        name.ToolTip = group is null ? null : Details(group);
        value.Text = group is null ? "—" : format(group);
    }

    private static string Details(ProcessGroup group) =>
        group.Name + $"\n{group.Count} {CountLabel(group.Count)} · PID: " + string.Join(", ", group.Pids) +
        (group.Services.Length == 0 ? "" : "\nСлужби: " + string.Join(", ", group.Services)) +
        (group.ActiveCount == 0 ? "\nУсі процеси групи завершено" : "");

    private static string CountLabel(int count) => count % 10 == 1 && count % 100 != 11 ? "процес"
        : count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) ? "процеси" : "процесів";
    private static double Cpu(ProcessGroup p, bool live, double seconds) =>
        live ? p.CpuPercent : p.CpuSeconds / Math.Max(1, seconds) / Environment.ProcessorCount * 100;
    private static double Disk(ProcessGroup p, bool live, double seconds) =>
        live ? p.DiskBytesPerSecond : p.DiskBytes / Math.Max(1, seconds);
    private static double Memory(ProcessGroup p, bool live) => live ? p.MemoryBytes : p.AverageMemoryBytes;
    private static double Amount(ProcessGroup p, int metric, bool live, double seconds) => metric switch
    {
        1 => Disk(p, live, seconds),
        2 => Memory(p, live),
        _ => Cpu(p, live, seconds)
    };
    private static string FormatBytes(double bytes) => bytes >= 1024 * 1024 * 1024
        ? $"{bytes / (1024 * 1024 * 1024):0.0} ГБ" : $"{bytes / (1024 * 1024):0.0} МБ";
    private static string FormatRate(double bytesPerSecond) => FormatBytes(bytesPerSecond) + "/с";

    private void Mode_Changed(object sender, RoutedEventArgs e) { if (_uiReady) Render(); }
    private void SortMetric_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_uiReady) Render(); }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (_uiReady) Render(); }
    private void ChartMetric_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        ChartCpuSegment.IsChecked = ChartMetric.SelectedIndex == 0;
        ChartDiskSegment.IsChecked = ChartMetric.SelectedIndex == 1;
        ChartMemorySegment.IsChecked = ChartMetric.SelectedIndex == 2;
        DrawChart();
    }
    private void ChartSegment_Checked(object sender, RoutedEventArgs e)
    {
        if (_uiReady && sender is RadioButton { Tag: string tag } && int.TryParse(tag, out var index) &&
            ChartMetric.SelectedIndex != index) ChartMetric.SelectedIndex = index;
    }
    private void ChartArea_SizeChanged(object sender, SizeChangedEventArgs e) { if (_uiReady) DrawChart(); }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) { if (_uiReady) UpdateLayoutMode(); }

    private void UpdateLayoutMode() => UpdateLayoutMode(ActualWidth, ActualHeight);

    private void UpdateLayoutMode(double width, double height)
    {
        var narrow = width < 1220;
        Grid.SetColumn(RankingPanel, 0);
        Grid.SetColumnSpan(RankingPanel, narrow ? 3 : 1);
        Grid.SetRow(ProcessesPanel, narrow ? 2 : 0);
        Grid.SetColumn(ProcessesPanel, narrow ? 0 : 2);
        Grid.SetColumnSpan(ProcessesPanel, narrow ? 3 : 1);
        MainPanels.RowDefinitions[0].Height = narrow ? new GridLength(360) : new GridLength(1, GridUnitType.Star);
        MainPanels.RowDefinitions[1].Height = new GridLength(narrow ? 16 : 0);
        MainPanels.RowDefinitions[2].Height = new GridLength(narrow ? 410 : 0);
        MainPanels.Height = narrow ? 786 : Math.Max(330, height - 540);
    }

    private void DrawChart()
    {
        if (_snapshot is null || ChartArea.ActualWidth < 10) return;
        var metric = ChartMetric.SelectedIndex;
        ChartHint.Text = metric switch
        {
            1 => "Читання й запис · найбільше зверху",
            2 => "Пам’ять · найбільше зверху",
            _ => "Процесор · найбільше зверху"
        };
        var live = LiveToggle.IsChecked == true;
        var seconds = _snapshot.Elapsed.TotalSeconds;
        var ranked = _visibleGroups.OrderByDescending(p => Amount(p, metric, live, seconds))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Take(12).ToList();
        var maximum = ranked.Count == 0 ? 0 : Amount(ranked[0], metric, live, seconds);
        var width = Math.Max(0, (ChartScroll.ViewportWidth > 0 ? ChartScroll.ViewportWidth : ChartArea.ActualWidth - 10) - 8);
        var rows = ranked.Select((p, index) =>
        {
            var amount = Amount(p, metric, live, seconds);
            return new BarRow(p.Name)
            {
                DisplayName = p.Name + (p.Count > 1 ? $" ({p.Count})" : ""),
                Rank = (index + 1).ToString("00"),
                Detail = Details(p),
                Value = metric switch { 1 => FormatRate(amount), 2 => FormatBytes(amount), _ => $"{amount:0.0} %" },
                BarWidth = maximum <= 0 ? 0 : Math.Clamp(width * amount / maximum, 0, width)
            };
        }).ToList();
        SyncRows(_barRows, rows, row => row.Key, (current, incoming) => current.CopyFrom(incoming));
        ChartEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Retain item containers and scroll position during once-per-second updates.
    private static void SyncRows<T>(ObservableCollection<T> target, IReadOnlyList<T> incoming,
        Func<T, string> key, Action<T, T> update) where T : class
    {
        var existing = target.ToDictionary(key, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < incoming.Count; index++)
        {
            var next = incoming[index];
            if (existing.TryGetValue(key(next), out var row))
            {
                update(row, next);
                var oldIndex = target.IndexOf(row);
                if (oldIndex != index) target.Move(oldIndex, index);
            }
            else target.Insert(index, next);
        }
        while (target.Count > incoming.Count) target.RemoveAt(target.Count - 1);
    }

    private void Theme_Click(object sender, RoutedEventArgs e) { _isLight = !_isLight; ApplyTheme(); }
    private void SystemParameters_Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SystemParameters.HighContrast) && !_closed)
            Dispatcher.InvokeAsync(ApplyTheme);
    }

    private void ApplyTheme()
    {
        void Brush(string key, string dark, string light) =>
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isLight ? light : dark));
        void ColorValue(string key, string dark, string light) =>
            Resources[key] = (Color)ColorConverter.ConvertFromString(_isLight ? light : dark);
        Brush("WindowBrush", "#12131B", "#F2F3F7");
        Brush("SurfaceBrush", "#F01C1E28", "#F4FFFFFF");
        Brush("ElevatedBrush", "#252732", "#FFFFFF");
        Brush("BorderBrush", "#25FFFFFF", "#1834476A");
        Brush("TextBrush", "#F5F5F7", "#1D1D1F");
        Brush("SecondaryBrush", "#B0B3C2", "#5F6470");
        Brush("AccentBrush", "#74B6FF", "#0066D9");
        Brush("PurpleBrush", "#C5ACFA", "#8455C9");
        Brush("BlueBrush", "#FFBC70", "#AE5B00");
        Brush("ControlBrush", "#30333F", "#EBEEF4");
        Brush("ControlHoverBrush", "#414655", "#DDE4EF");
        Brush("TrackBrush", "#383D4B", "#E8ECF3");
        Brush("ScrollThumbBrush", "#757C91", "#A5AAB7");
        Brush("SelectionBrush", "#314C74", "#CFE1FF");
        Brush("SelectionTextBrush", "#F5F5F7", "#1D1D1F");
        Brush("RowSelectionBrush", "#28364F", "#EBF3FF");
        Brush("SegmentSelectedBrush", "#545A6D", "#FFFFFF");
        Brush("SwitchOnBrush", "#319E59", "#238E4C");
        Brush("SwitchThumbBrush", "#FFFFFF", "#FFFFFF");
        Brush("BadgeBrush", "#2A3E5D", "#DDEBFF");
        Brush("GlassTintBrush", "#A6222530", "#88FFFFFF");
        Brush("GlassOpaqueBrush", "#1E202C", "#F5F6FA");
        ColorValue("BackgroundTopColor", "#161822", "#F6F7FB");
        ColorValue("BackgroundBottomColor", "#191A28", "#E8EAF5");
        ColorValue("RibbonBlueColor", "#97414F83", "#979BD5F9");
        ColorValue("RibbonPurpleColor", "#AD655786", "#ADC6BFF0");
        ColorValue("RibbonPinkColor", "#8156404D", "#81F6D2C4");
        ColorValue("RibbonFadeColor", "#001C1F2B", "#00DBE5F6");
        ColorValue("GlassEdgeTopColor", "#98E0E7FF", "#F7FFFFFF");
        ColorValue("GlassEdgeBottomColor", "#415F7394", "#605D789F");
        ColorValue("GlassSheenColor", "#30E2E8FF", "#60FFFFFF");
        ColorValue("GlassShadowColor", "#000000", "#425778");
        ColorValue("CardShadowColor", "#000000", "#3C5070");
        ColorValue("GlassClearColor", "#00FFFFFF", "#00FFFFFF");
        // Two cached backdrop layers; software-rendered and high contrast sessions
        // use solid materials instead of doing repeated blur work on the CPU.
        Resources["GlassBackdropOpacity"] = (RenderCapability.Tier >> 16) > 0 ? 1d : 0d;
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "WindowBrush", "SurfaceBrush", "ElevatedBrush", "ControlBrush", "ControlHoverBrush", "TrackBrush", "GlassTintBrush", "GlassOpaqueBrush", "BadgeBrush", "SegmentSelectedBrush" })
                Resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "TextBrush", "SecondaryBrush", "AccentBrush", "PurpleBrush", "BlueBrush", "BorderBrush", "ScrollThumbBrush" })
                Resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "BackgroundTopColor", "BackgroundBottomColor", "RibbonBlueColor", "RibbonPurpleColor", "RibbonPinkColor", "RibbonFadeColor", "GlassSheenColor" })
                Resources[key] = SystemColors.WindowColor;
            Resources["GlassEdgeTopColor"] = SystemColors.WindowTextColor;
            Resources["GlassEdgeBottomColor"] = SystemColors.WindowTextColor;
            Resources["SelectionBrush"] = SystemColors.HighlightBrush;
            Resources["SelectionTextBrush"] = SystemColors.HighlightTextBrush;
            Resources["RowSelectionBrush"] = SystemColors.WindowBrush;
            Resources["SwitchOnBrush"] = SystemColors.HighlightBrush;
            Resources["SwitchThumbBrush"] = SystemColors.HighlightTextBrush;
            Resources["GlassBackdropOpacity"] = 0d;
        }
        ThemeButton.Content = _isLight ? "Темна тема" : "Світла тема";
        LeaderGlass.RefreshBackdrop();
        ModeGlass.RefreshBackdrop();
        NativeWindowTheme.Apply(this, _isLight);
    }

    private abstract class ViewRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private sealed class ProcessRow(string name) : ViewRow
    {
        public string Name { get; } = name;
        public string CountDetail { get; set; } = "";
        public string Detail { get; set; } = "";
        public string CpuText { get; set; } = "";
        public string MemoryText { get; set; } = "";
        public string DiskText { get; set; } = "";
        public void CopyFrom(ProcessRow row)
        {
            if (CountDetail == row.CountDetail && Detail == row.Detail && CpuText == row.CpuText &&
                MemoryText == row.MemoryText && DiskText == row.DiskText) return;
            CountDetail = row.CountDetail; Detail = row.Detail;
            CpuText = row.CpuText; MemoryText = row.MemoryText; DiskText = row.DiskText;
            Changed();
        }
    }

    private sealed class BarRow(string key) : ViewRow
    {
        public string Key { get; } = key;
        public string DisplayName { get; set; } = key;
        public string Name => DisplayName;
        public string Rank { get; set; } = "";
        public string Value { get; set; } = "";
        public string Detail { get; set; } = "";
        public double BarWidth { get; set; }
        public void CopyFrom(BarRow row)
        {
            if (DisplayName == row.DisplayName && Rank == row.Rank && Value == row.Value &&
                Detail == row.Detail && Math.Abs(BarWidth - row.BarWidth) < 0.1) return;
            DisplayName = row.DisplayName; Rank = row.Rank; Value = row.Value;
            Detail = row.Detail; BarWidth = row.BarWidth; Changed();
        }
    }

    private record ProcessGroup(string Name, int Count, int ActiveCount, int[] Pids, string[] Services,
        double CpuPercent, double CpuSeconds, double DiskBytesPerSecond, double DiskBytes,
        double MemoryBytes, double AverageMemoryBytes);
}
