using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TvgunBridge.Core.Config;

namespace TvgunBridge.App;

/// <summary>
/// Control panel: live status (services, aim stream, tracked window, injection), game
/// adapter selection (applies window matching, reload strategy, teknoparrot.ini), and
/// the bridge / injection / calibration / self-test / borderless controls. The window
/// is deliberately thin: a 10 Hz timer renders <see cref="BridgeRuntime"/> state and
/// every button delegates straight to the runtime. Closing the window exits the app.
/// </summary>
public partial class MainWindow : Window
{
    private readonly BridgeRuntime _runtime;
    private readonly IReadOnlyList<GameAdapter> _adapters;
    private readonly DispatcherTimer _statusTimer;
    private string? _loadError;
    private bool _syncing;

    /// <summary>Creates the panel, loads the game adapters, and starts status polling.</summary>
    public MainWindow(BridgeRuntime runtime)
    {
        _runtime = runtime;
        InitializeComponent();

        var gamesDir = Path.Combine(AppContext.BaseDirectory, "games");
        try
        {
            _adapters = GameAdapter.LoadAll(gamesDir);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            _adapters = Array.Empty<GameAdapter>();
            _loadError = $"games 目录读取失败：{ex.Message}";
        }

        ComboGames.ItemsSource = _adapters;
        var active = _adapters.FirstOrDefault(a =>
            string.Equals(a.Id, _runtime.Config.ActiveGameId, StringComparison.OrdinalIgnoreCase));
        if (active is not null)
        {
            ComboGames.SelectedItem = active;
        }

        ComboGames.SelectionChanged += OnGameSelected;
        if (active is not null)
        {
            _runtime.SelectGame(active);
        }

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _statusTimer.Tick += (_, _) => RefreshState();
        _statusTimer.Start();
        RefreshState();
    }

    /// <summary>Summary line of the configured hotkeys, shown at the bottom.</summary>
    public string HotkeySummary
    {
        set => TxtHotkeys.Text = value;
    }

    /// <summary>Re-renders all status fields from runtime state (called at 10 Hz).</summary>
    public void RefreshState()
    {
        _syncing = true;
        try
        {
            TxtService.Text = _runtime.IsBridging
                ? $"协议服务：运行中  UDP/HTTP :{_runtime.Port}  发现 :{_runtime.Port + 1}"
                : "协议服务：已停止";

            TxtAim.Text = _runtime.Aim is { } aim && aim.TryGet(out var point)
                ? $"Aim：({point.X:0.0}, {point.Y:0.0})  信号年龄 {aim.AgeMs:0} ms"
                : "Aim：无信号（手机未推流或已超时）";

            TxtWindow.Text = _runtime.TrackedWindow is { } info
                ? $"游戏窗口：已锁定 \"{info.Title}\"  客户区 ({info.ClientRect.X:0},{info.ClientRect.Y:0}) {info.ClientRect.Width:0}×{info.ClientRect.Height:0}"
                : _runtime.IsBridging
                    ? "游戏窗口：搜索中（未匹配到进程/标题）"
                    : "游戏窗口：未追踪";

            TxtInjection.Text = _runtime.InjectionEnabled
                ? "输入注入：已启用（F7 切换）"
                : "输入注入：关闭（F7 切换）";

            TxtLastAction.Text = _runtime.LastAction is { } action ? $"最近动作：{action}" : string.Empty;
            TxtError.Text = _runtime.LastError ?? string.Empty;
            TxtIni.Text = _runtime.IniWriteResult ?? _loadError ?? string.Empty;

            BtnStartStop.Content = _runtime.IsBridging ? "停止桥接" : "开始桥接";
            BtnCalibrate.IsEnabled = _runtime.IsBridging;
            BtnSelfTest.IsEnabled = _runtime.IsBridging;
            BtnBorderless.Content = _runtime.BorderlessArmed ? "恢复游戏窗口边框" : "游戏窗口无边框化";
            ChkInject.IsChecked = _runtime.InjectionEnabled;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnGameSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        _runtime.SelectGame(ComboGames.SelectedItem as GameAdapter);
        try
        {
            _runtime.Config.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = ex; // config persistence is best-effort; the selection still applies
        }

        RefreshState();
    }

    private void OnStartStopClick(object sender, RoutedEventArgs e)
    {
        if (_runtime.IsBridging)
        {
            _runtime.StopBridge();
        }
        else
        {
            _ = _runtime.StartBridge();
        }

        RefreshState();
    }

    private void OnInjectChecked(object sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _runtime.InjectionEnabled = true;
        }
    }

    private void OnInjectUnchecked(object sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _runtime.InjectionEnabled = false;
        }
    }

    private void OnCalibrateClick(object sender, RoutedEventArgs e)
    {
        var window = new CalibrationWindow(_runtime) { Owner = this };
        window.Show();
    }

    private void OnSelfTestClick(object sender, RoutedEventArgs e)
    {
        var window = new SelfTestWindow(_runtime) { Owner = this };
        window.Show();
    }

    private void OnBorderlessClick(object sender, RoutedEventArgs e)
    {
        _runtime.ToggleBorderless();
        RefreshState();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Toggles visibility (F8 hotkey).</summary>
    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
        }
    }
}
