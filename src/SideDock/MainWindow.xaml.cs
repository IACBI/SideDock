using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SideDock.Interop;
using SideDock.Models;
using SideDock.Services;

namespace SideDock;

/// <summary>
/// The dock window. It docks itself to a screen edge (AppBar), shows app
/// launchers, a clock, and the list of open windows, and is fully driven by the
/// JSON config file — which it also watches so edits apply live.
/// </summary>
public partial class MainWindow : Window
{
    private AppBarManager? _appBar;
    private TrayIconService? _tray;
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _windowTimer;

    // Live config reload.
    private FileSystemWatcher? _configWatcher;
    private DispatcherTimer? _reloadDebounce;

    private DockConfig _config = new();
    private string _lastWindowSignature = "";

    // Track the current geometry so theme-only edits don't re-dock the window.
    private DockEdge? _currentEdge;
    private double _currentThickness = -1;

    // Icon sizes (pixels), set from config.
    private double _appIconSize = 32;
    private double _windowIconSize = 26;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Fires once the underlying Win32 window handle (HWND) actually exists.
    /// We must wait for this before talking to the AppBar API, because that API
    /// needs our window handle.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _appBar = new AppBarManager(this);

        // Listen for AppBar notifications (e.g. a full-screen app appearing) so
        // we can drop out of "topmost" and not float over games/videos.
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WndProc);

        // Load the config, then immediately write it back so the file always
        // contains the full set of options (older files that only had "Apps"
        // get upgraded with all settings, defaults filled in). This save runs
        // BEFORE the file watcher starts, so it can't trigger a reload loop.
        _config = ConfigService.LoadOrCreateDefault();
        try
        {
            ConfigService.Save(_config);
        }
        catch
        {
            // Best-effort: if the file is locked/unwritable we still run with the
            // loaded (in-memory) config rather than crashing at startup.
        }

        ApplyConfig(_config); // size/theme/apps/edge (also docks us)

        // Phase 3: clock + system-tray icon.
        StartClock();
        _tray = new TrayIconService(
            onToggleVisibility: ToggleDockVisibility,
            onOpenConfig: OpenConfigFile,
            onExit: () => System.Windows.Application.Current.Shutdown());

        // Phase 4: watch the list of open windows.
        StartWindowWatcher();

        // Phase 5: watch the config file for live edits.
        StartConfigWatcher();
    }

    // ABN_FULLSCREENAPP: the shell tells appbars when a full-screen app opens
    // (lParam != 0) or closes (lParam == 0).
    private const int ABN_FULLSCREENAPP = 0x0002;

    /// <summary>
    /// Handles AppBar shell notifications. When a full-screen app opens we stop
    /// being topmost so it can cover us; when it closes we go back on top.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == AppBarManager.CallbackMessage && wParam.ToInt32() == ABN_FULLSCREENAPP)
        {
            bool fullScreenOpening = lParam != IntPtr.Zero;
            Topmost = !fullScreenOpening;
        }
        return IntPtr.Zero;
    }

    // --- Phase 5: apply / reload configuration -------------------------------

    /// <summary>
    /// Applies every config setting to the running dock: size, theme colors,
    /// app buttons, and docked edge. Safe to call repeatedly (used on reload).
    /// </summary>
    private void ApplyConfig(DockConfig config)
    {
        // Icon sizes (content only — these don't change the window geometry).
        _appIconSize = config.IconSize;
        _windowIconSize = config.WindowIconSize;

        // Keep the "start with Windows" registry entry in sync with the config.
        StartupService.Apply(config.StartWithWindows);

        ThemeSettings theme = config.Theme;

        // Background gradient. BackgroundOpacity (0..1) is the easy transparency
        // knob; it scales the brush only, so icons/text stay fully opaque.
        var gradient = new LinearGradientBrush(
            ParseColor(theme.BackgroundTop, Color.FromRgb(0x1E, 0x1E, 0x2E)),
            ParseColor(theme.BackgroundBottom, Color.FromRgb(0x11, 0x11, 0x1B)),
            new Point(0, 0), new Point(0, 1))
        {
            Opacity = Math.Clamp(theme.BackgroundOpacity, 0.0, 1.0),
        };
        gradient.Freeze();
        RootBar.Background = gradient;

        // Clock/date colors, fonts, and sizes.
        var fontFamily = new FontFamily(
            string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI" : theme.FontFamily);
        TimeText.Foreground = FrozenBrush(theme.TimeColor, Color.FromRgb(0xE6, 0xE6, 0xF0));
        DateText.Foreground = FrozenBrush(theme.DateColor, Color.FromRgb(0x9A, 0x9A, 0xB0));
        TimeText.FontSize = theme.TimeFontSize;
        DateText.FontSize = theme.DateFontSize;
        TimeText.FontFamily = fontFamily;
        DateText.FontFamily = fontFamily;

        // Divider color.
        WindowSeparator.Background = FrozenBrush(theme.DividerColor, Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        // Hover/press highlight brushes + button corner radius (dynamic resources
        // referenced by the button style in App.xaml).
        var resources = System.Windows.Application.Current.Resources;
        resources["DockHoverBrush"] =
            FrozenBrush(theme.HoverColor, Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        resources["DockPressedBrush"] =
            FrozenBrush(theme.PressedColor, Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
        resources["DockButtonCornerRadius"] = new CornerRadius(theme.ButtonCornerRadius);

        // Clock visibility / format.
        ClockPanel.Visibility = config.ShowClock ? Visibility.Visible : Visibility.Collapsed;
        DateText.Visibility = config.ShowDate ? Visibility.Visible : Visibility.Collapsed;
        UpdateClock(); // reflect 12/24-hour change immediately

        // App launcher buttons.
        BuildAppButtons(config.Apps);

        // Edge / placement: ONLY touch the AppBar (which moves/resizes the
        // window) when the edge or thickness actually changed. This keeps
        // theme-only edits (opacity, colors, fonts) from shifting the dock.
        DockEdge edge = ParseEdge(config.Edge);
        bool geometryChanged =
            _currentEdge != edge || Math.Abs(_currentThickness - config.BarThickness) > 0.01;

        if (geometryChanged)
        {
            Width = config.BarThickness;
            _currentEdge = edge;
            _currentThickness = config.BarThickness;

            if (_appBar is not null)
            {
                _appBar.Edge = edge;
                _appBar.Unregister(); // no-op the first time
                _appBar.Register();
            }
        }

        // Re-arm the clock cadence (handles ShowSeconds changes).
        RestartClockTimer();

        // Rebuild the open-windows list (new icon sizes) and start/stop its poll
        // timer depending on whether the feature is enabled.
        _lastWindowSignature = "";
        UpdateWindowWatcher();
    }

    private void StartConfigWatcher()
    {
        // Editors often raise several change events per save, so we debounce:
        // restart this short timer on each event and only reload when it's quiet.
        _reloadDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _reloadDebounce.Tick += (_, _) =>
        {
            _reloadDebounce!.Stop();
            ReloadConfig();
        };

        try
        {
            string? directory = Path.GetDirectoryName(ConfigService.FilePath);
            string fileName = Path.GetFileName(ConfigService.FilePath);
            if (directory is null) return;

            _configWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            // FileSystemWatcher events fire on a background thread; marshal to
            // the UI thread (BeginInvoke = async, safe during shutdown) before
            // touching any WPF objects.
            _configWatcher.Changed += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                _reloadDebounce!.Stop();
                _reloadDebounce!.Start();
            });
        }
        catch
        {
            // Live reload is best-effort; the dock still works without it.
        }
    }

    private void ReloadConfig()
    {
        try
        {
            _config = ConfigService.LoadOrCreateDefault();
            ApplyConfig(_config);
        }
        catch
        {
            // Ignore a half-written/invalid edit; keep the current look until
            // the file becomes valid again.
        }
    }

    // --- Phase 4: open windows ----------------------------------------------

    /// <summary>Creates the window-poll timer and starts it if enabled.</summary>
    private void StartWindowWatcher()
    {
        _windowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _windowTimer.Tick += (_, _) => RefreshWindows();
        UpdateWindowWatcher();
    }

    /// <summary>
    /// Starts polling (with an immediate refresh) when the open-windows feature
    /// is on; otherwise stops the timer entirely and clears the section. This
    /// avoids any idle polling when the feature is disabled.
    /// </summary>
    private void UpdateWindowWatcher()
    {
        if (_windowTimer is null) return;

        if (_config.ShowOpenWindows)
        {
            RefreshWindows();
            _windowTimer.Start();
        }
        else
        {
            _windowTimer.Stop();
            WindowList.Children.Clear();
            WindowSeparator.Visibility = Visibility.Collapsed;
            _lastWindowSignature = "";
        }
    }

    private void RefreshWindows()
    {
        // Feature can be turned off in config: clear and hide the section.
        if (!_config.ShowOpenWindows)
        {
            WindowList.Children.Clear();
            WindowSeparator.Visibility = Visibility.Collapsed;
            _lastWindowSignature = "";
            return;
        }

        IntPtr self = new WindowInteropHelper(this).Handle;
        List<OpenWindowInfo> windows = WindowEnumerator.GetOpenWindows(self);

        // Skip rebuilding the buttons when nothing changed (prevents flicker
        // and needless icon queries). The signature captures handle + minimized
        // state + title for every window.
        string signature = string.Join("|",
            windows.Select(w => $"{w.Handle}:{w.IsMinimized}:{w.Title}"));
        if (signature == _lastWindowSignature)
            return;
        _lastWindowSignature = signature;

        WindowList.Children.Clear();
        foreach (OpenWindowInfo w in windows)
            WindowList.Children.Add(CreateWindowButton(w));

        // Only show the divider when there's at least one open window.
        WindowSeparator.Visibility =
            windows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Builds a button representing one open window.</summary>
    private Button CreateWindowButton(OpenWindowInfo window)
    {
        var image = new Image
        {
            Width = _windowIconSize,
            Height = _windowIconSize,
            Stretch = Stretch.Uniform,
            Source = WindowEnumerator.GetIcon(window.Handle), // fetched lazily here
        };

        var button = new Button
        {
            Content = image,
            Style = (Style)FindResource("DockButtonStyle"),
            ToolTip = window.Title,
            Tag = window.Handle,
            // Dim minimized windows so they're visually distinct.
            Opacity = window.IsMinimized ? 0.45 : 1.0,
        };
        button.Click += WindowButton_Click;
        return button;
    }

    /// <summary>Activates (focuses/restores) the clicked window.</summary>
    private void WindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: IntPtr handle })
            WindowEnumerator.Activate(handle);
    }

    // --- Phase 3: clock ------------------------------------------------------

    /// <summary>Shows the current time/date and starts the clock timer.</summary>
    private void StartClock()
    {
        UpdateClock();
        _clockTimer = new DispatcherTimer();
        _clockTimer.Tick += ClockTick;
        ScheduleNextClockTick();
    }

    private void ClockTick(object? sender, EventArgs e)
    {
        _clockTimer!.Stop();
        UpdateClock();
        ScheduleNextClockTick();
    }

    /// <summary>
    /// Arms the clock timer: every second when seconds are shown, otherwise
    /// aligned to the next minute boundary (so it only fires ~once a minute).
    /// </summary>
    private void ScheduleNextClockTick()
    {
        if (_clockTimer is null) return;

        if (_config.ShowSeconds)
        {
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
        }
        else
        {
            DateTime now = DateTime.Now;
            double msToNextMinute = (60 - now.Second) * 1000.0 - now.Millisecond;
            if (msToNextMinute < 50) msToNextMinute = 50; // never schedule ~0ms
            _clockTimer.Interval = TimeSpan.FromMilliseconds(msToNextMinute);
        }
        _clockTimer.Start();
    }

    /// <summary>Re-applies the clock cadence after a config change.</summary>
    private void RestartClockTimer()
    {
        if (_clockTimer is null) return;
        _clockTimer.Stop();
        UpdateClock();
        ScheduleNextClockTick();
    }

    private void UpdateClock()
    {
        DateTime now = DateTime.Now;

        // Build the time format from the 24-hour and seconds options.
        // 24h: "HH:mm" / "HH:mm:ss"   12h: "h:mm tt" / "h:mm:ss tt"
        string format = _config.Use24HourClock
            ? (_config.ShowSeconds ? "HH:mm:ss" : "HH:mm")
            : (_config.ShowSeconds ? "h:mm:ss tt" : "h:mm tt");

        TimeText.Text = now.ToString(format);
        DateText.Text = now.ToString("dd MMM"); // e.g. 04 Jun
    }

    // --- Phase 3: tray / menu actions ---------------------------------------

    /// <summary>
    /// Hides the dock (freeing its reserved space and pausing timers) or shows
    /// it again. Pausing timers while hidden is part of the performance pass.
    /// </summary>
    private void ToggleDockVisibility()
    {
        if (IsVisible)
        {
            _clockTimer?.Stop();
            _windowTimer?.Stop();
            _appBar?.Unregister(); // give the reserved strip back while hidden
            Hide();
        }
        else
        {
            Show();
            _appBar?.Register();    // re-reserve and reposition
            UpdateClock();
            ScheduleNextClockTick(); // resume the clock cadence
            UpdateWindowWatcher();   // resume window polling if enabled
        }
    }

    // Right-click menu handlers on the bar (same actions as the tray menu).
    private void OnMenuHide(object sender, RoutedEventArgs e) => ToggleDockVisibility();
    private void OnMenuOpenConfig(object sender, RoutedEventArgs e) => OpenConfigFile();
    private void OnMenuExit(object sender, RoutedEventArgs e) =>
        System.Windows.Application.Current.Shutdown();

    /// <summary>Opens config.json in the user's default editor.</summary>
    private void OpenConfigFile()
    {
        try
        {
            // Make sure the file exists before trying to open it.
            ConfigService.LoadOrCreateDefault();
            Process.Start(new ProcessStartInfo
            {
                FileName = ConfigService.FilePath,
                UseShellExecute = true, // open with whatever app handles .json
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Couldn't open the config file.\n\n{ex.Message}",
                "SideDock", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --- App launcher buttons (Phase 2) -------------------------------------

    private void BuildAppButtons(IEnumerable<AppEntry> apps)
    {
        AppList.Children.Clear();
        foreach (AppEntry app in apps)
            AppList.Children.Add(CreateAppButton(app));
    }

    /// <summary>Builds a single clickable icon button for an app entry.</summary>
    private Button CreateAppButton(AppEntry app)
    {
        var image = new Image
        {
            Width = _appIconSize,
            Height = _appIconSize,
            Stretch = Stretch.Uniform,
            Source = ResolveIcon(app),
        };

        var button = new Button
        {
            Content = image,
            Style = (Style)FindResource("DockButtonStyle"),
            ToolTip = app.Name,
            Tag = app, // stash the entry so the click handler knows what to launch
        };
        button.Click += AppButton_Click;
        return button;
    }

    /// <summary>
    /// Picks the icon to show: an explicit IconPath override if present and
    /// valid, otherwise the icon extracted from the executable itself.
    /// </summary>
    private static ImageSource? ResolveIcon(AppEntry app)
    {
        if (!string.IsNullOrWhiteSpace(app.IconPath) && File.Exists(app.IconPath))
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(app.IconPath));
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                // Bad icon path: fall back to the extracted executable icon.
            }
        }

        return IconExtractor.TryGetIcon(app.Path);
    }

    /// <summary>Launches the app associated with the clicked button.</summary>
    private void AppButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AppEntry app })
        {
            try
            {
                AppLauncher.Launch(app);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Couldn't launch \"{app.Name}\".\n\n{ex.Message}",
                    "SideDock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    // --- Small helpers -------------------------------------------------------

    /// <summary>Parses a hex color string, returning a fallback if invalid.</summary>
    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex) &&
                ColorConverter.ConvertFromString(hex) is Color color)
            {
                return color;
            }
        }
        catch
        {
            // Bad color string: use the fallback below.
        }
        return fallback;
    }

    /// <summary>Builds a frozen (immutable, efficient) brush from a hex color.</summary>
    private static SolidColorBrush FrozenBrush(string hex, Color fallback)
    {
        var brush = new SolidColorBrush(ParseColor(hex, fallback));
        brush.Freeze();
        return brush;
    }

    private static DockEdge ParseEdge(string edge) =>
        string.Equals(edge, "Right", StringComparison.OrdinalIgnoreCase)
            ? DockEdge.Right
            : DockEdge.Left;

    /// <summary>
    /// Frees the externally-visible OS resources: the AppBar reservation (so the
    /// screen strip is given back) and the tray icon. Idempotent and safe to call
    /// from both normal shutdown and the global crash handler.
    /// </summary>
    public void ReleaseDockResources()
    {
        _appBar?.Unregister(); // give the reserved screen space back
        _tray?.Dispose();      // remove the tray icon so it doesn't linger
    }

    /// <summary>
    /// Always release resources when the window closes. We free the AppBar and
    /// tray FIRST (most important — leaving them would reserve screen space or
    /// strand a tray icon), then stop timers and dispose the watcher.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        ReleaseDockResources();
        _clockTimer?.Stop();
        _windowTimer?.Stop();
        _reloadDebounce?.Stop();
        _configWatcher?.Dispose();
    }
}
