using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Threading;
using TvgunBridge.App.Injection;
using TvgunBridge.App.Input;
using TvgunBridge.App.Native;
using TvgunBridge.Core.Config;
using TvgunBridge.Core.Coordinates;
using TvgunBridge.Core.Injection;
using TvgunBridge.Core.Protocol;
using TvgunBridge.Core.Windowing;
using MouseButton = TvgunBridge.Core.Injection.MouseButton;

namespace TvgunBridge.App;

/// <summary>
/// Owns the bridge runtime: protocol server, window tracking, the injection pipeline,
/// the borderless forcer, and the overlay window. The WPF windows talk only to this
/// class; UI concerns (dispatcher marshalling of tracker events, status strings) live
/// here so the windows stay thin.
/// </summary>
public sealed class BridgeRuntime : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly HotkeySettingsProvider _keys;
    private BridgeServer? _server;
    private InputPipeline? _pipeline;
    private WindowTracker? _tracker;
    private bool _injectionEnabled;

    /// <summary>Creates the runtime. Nothing is bound until <see cref="StartBridge"/>.</summary>
    public BridgeRuntime(BridgeConfig config, AffineCalibration calibration, HotkeySettingsProvider keys, Dispatcher dispatcher)
    {
        Config = config;
        Calibration = calibration;
        Mapper = new CoordinateMapper(calibration);
        Injector = new SendInputInjector();
        Forcer = new BorderlessForcer();
        _keys = keys;
        _dispatcher = dispatcher;
    }

    /// <summary>Bridge configuration loaded at startup.</summary>
    public BridgeConfig Config { get; }

    /// <summary>Current calibration (applied by <see cref="ApplyCalibration"/>).</summary>
    public AffineCalibration Calibration { get; private set; }

    /// <summary>Normalized→screen mapper shared by injection, self-test, and calibration.</summary>
    public CoordinateMapper Mapper { get; }

    /// <summary>The OS-level injector.</summary>
    public SendInputInjector Injector { get; }

    /// <summary>Borderless forcer (restored on exit).</summary>
    public BorderlessForcer Forcer { get; }

    /// <summary>The overlay window, assigned by the App at startup.</summary>
    public OverlayBorderWindow? Overlay { get; set; }

    /// <summary>The active game adapter, if one is selected.</summary>
    public GameAdapter? ActiveAdapter { get; private set; }

    /// <summary>True while the bridge (server + tracker + pipeline) is running.</summary>
    public bool IsBridging { get; private set; }

    /// <summary>Last startup/runtime error to display in the UI, if any.</summary>
    public string? LastError { get; private set; }

    /// <summary>Result of the last teknoparrot.ini write attempt.</summary>
    public string? IniWriteResult { get; private set; }

    /// <summary>Short description of the most recent injected action (status line).</summary>
    public string? LastAction { get; private set; }

    /// <summary>Whether borderless forcing is currently armed.</summary>
    public bool BorderlessArmed { get; private set; }

    /// <summary>Latest aim slot of the running server, or null when stopped.</summary>
    public AimSlot? Aim => _server?.Aim;

    /// <summary>Currently tracked game window, if any.</summary>
    public GameWindowInfo? TrackedWindow => _tracker?.Current;

    /// <summary>Configured service port (discovery listens on port + 1).</summary>
    public int Port => Config.Port;

    /// <summary>
    /// Raised for every <c>POST /shot</c>, before injection gating. Self-test and
    /// calibration windows subscribe here. Raised on a thread-pool thread.
    /// </summary>
    public event EventHandler<ShotEvent>? ShotFeed;

    /// <summary>
    /// Master injection switch. Default off so nothing is injected before the user
    /// opts in; applies to pointer moves, clicks, and injected coin/start/reload keys.
    /// </summary>
    public bool InjectionEnabled
    {
        get => _injectionEnabled;
        set
        {
            _injectionEnabled = value;
            if (_pipeline is not null)
            {
                _pipeline.Enabled = value;
            }
        }
    }

    /// <summary>
    /// Starts the protocol server, the input pipeline, and window tracking.
    /// Returns false (with <see cref="LastError"/> set) when the ports cannot be bound,
    /// e.g. HttpListener on a wildcard host without a URL ACL.
    /// </summary>
    public bool StartBridge()
    {
        if (IsBridging)
        {
            return true;
        }

        LastError = null;
        try
        {
            _server = new BridgeServer(Config.Port, Config.HttpHost, TimeSpan.FromMilliseconds(Config.AimTimeoutMs));
            _server.ShotServer.ShotReceived += OnShotReceived;
            _server.ShotServer.CoinReceived += OnCoinReceived;
            _server.ShotServer.StartReceived += OnStartReceived;
            _server.ShotServer.ReloadReceived += OnReloadReceived;
            _server.Start();
        }
        catch (HttpListenerException ex)
        {
            LastError =
                $"HTTP 端口 {Config.Port} 绑定失败：{ex.Message}\n" +
                "解决办法（管理员 CMD 执行一次，或改用 config.json 的 httpHost=127.0.0.1）：\n" +
                $"netsh http add urlacl url=http://+:{Config.Port}/ user=%USERDOMAIN%\\%USERNAME%";
            CleanupServer();
            return false;
        }
        catch (SocketException ex)
        {
            LastError = $"UDP 端口 {Config.Port} 绑定失败：{ex.Message}（端口被占用？）";
            CleanupServer();
            return false;
        }

        _pipeline = new InputPipeline(Injector, Mapper, _server.Aim, TargetRectProvider)
        {
            Enabled = InjectionEnabled,
            ClickHoldMs = ActiveAdapter?.ClickHoldMs ?? Config.ClickHoldMs,
        };
        _pipeline.Start();

        RecreateTracker();
        _tracker?.Start();

        IsBridging = true;
        return true;
    }

    /// <summary>Stops everything and releases the ports.</summary>
    public void StopBridge()
    {
        if (!IsBridging)
        {
            return;
        }

        IsBridging = false;
        StopTracker();
        if (_pipeline is not null)
        {
            Wait(_pipeline.StopAsync());
            _pipeline.Dispose();
            _pipeline = null;
        }

        CleanupServer();
        _dispatcher.Invoke(() => Overlay?.HideFrame());
    }

    /// <summary>
    /// Selects a game adapter: applies its window-matching patterns to the tracker,
    /// its reload strategy / trigger button / click hold to injection, and writes its
    /// teknoparrot.ini values (skipped with a note when the game directory is absent).
    /// </summary>
    public void SelectGame(GameAdapter? adapter)
    {
        ActiveAdapter = adapter;
        Config.ActiveGameId = adapter?.Id;

        if (adapter is not null)
        {
            if (_pipeline is not null)
            {
                _pipeline.ClickHoldMs = adapter.ClickHoldMs ?? Config.ClickHoldMs;
            }

            WriteGameIni(adapter);
        }
        else
        {
            IniWriteResult = null;
        }

        if (IsBridging)
        {
            StopTracker();
            RecreateTracker();
            _tracker?.Start();
        }
    }

    /// <summary>Applies a solved calibration to the mapper and persists it.</summary>
    public void ApplyCalibration(AffineCalibration calibration)
    {
        Calibration = calibration;
        Mapper.Calibration = calibration;
        calibration.Save();
    }

    /// <summary>
    /// Toggles borderless mode for the currently tracked game window: strips the
    /// caption and stretches the window over its monitor's work area. Newly found
    /// windows are forced automatically while armed.
    /// </summary>
    public void ToggleBorderless()
    {
        BorderlessArmed = !BorderlessArmed;
        if (!BorderlessArmed)
        {
            Forcer.UndoAll();
            return;
        }

        if (TrackedWindow is { } info)
        {
            ForceBorderless(info.Hwnd);
        }
    }

    /// <summary>Injects the configured coin key (F5 hotkey / POST /coin path).</summary>
    public void InjectCoin()
    {
        if (!InjectionEnabled)
        {
            LastAction = "投币被忽略（注入未启用）";
            return;
        }

        if (_keys.CoinKey is { } key)
        {
            Injector.KeyTap(key);
            LastAction = $"投币 → 按键 {_keys.CoinKeyName}";
        }
        else
        {
            LastAction = $"投币键名无效：{_keys.CoinKeyName}";
        }
    }

    /// <summary>Injects the configured start key (F6 hotkey / POST /start path).</summary>
    public void InjectStart()
    {
        if (!InjectionEnabled)
        {
            LastAction = "开始被忽略（注入未启用）";
            return;
        }

        if (_keys.StartKey is { } key)
        {
            Injector.KeyTap(key);
            LastAction = $"开始 → 按键 {_keys.StartKeyName}";
        }
        else
        {
            LastAction = $"开始键名无效：{_keys.StartKeyName}";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            StopBridge();
        }
        catch (Exception)
        {
            // Best effort during shutdown.
        }

        Forcer.UndoAll();
    }

    private void OnShotReceived(object? sender, ShotEvent shot)
    {
        ShotFeed?.Invoke(this, shot);
        if (_pipeline is not { Enabled: true })
        {
            return;
        }

        var rect = TargetRectProvider();
        var target = Mapper.Map(shot.X, shot.Y, rect);
        if (ActiveAdapter?.ReloadStrategy == ReloadStrategy.OffscreenShot)
        {
            target = ReloadPlanner.ClampToOutsideBand(target, rect);
        }

        var button = ActiveAdapter?.TriggerButton ?? MouseButton.Left;
        Injector.MoveAbsolute(target.X, target.Y);
        Injector.Click(button, _pipeline.ClickHoldMs);
        LastAction = $"开枪 ({shot.X:0},{shot.Y:0}) → ({target.X:0},{target.Y:0})";
    }

    private void OnCoinReceived(object? sender, EventArgs e) => _dispatcher.Invoke(InjectCoin);

    private void OnStartReceived(object? sender, EventArgs e) => _dispatcher.Invoke(InjectStart);

    private void OnReloadReceived(object? sender, EventArgs e)
    {
        if (!InjectionEnabled)
        {
            LastAction = "换弹被忽略（注入未启用）";
            return;
        }

        switch (ActiveAdapter?.ReloadStrategy)
        {
            case ReloadStrategy.RightClick:
                Injector.Click(MouseButton.Right, _pipeline?.ClickHoldMs ?? Config.ClickHoldMs);
                LastAction = "换弹 → 右键点击";
                break;
            case ReloadStrategy.OffscreenShot:
            {
                var rect = TargetRectProvider();
                var point = Mapper.Map(ReloadPlanner.OffscreenReloadNorm.X, ReloadPlanner.OffscreenReloadNorm.Y, rect);
                point = ReloadPlanner.ClampToOutsideBand(point, rect);
                Injector.MoveAbsolute(point.X, point.Y);
                Injector.Click(ActiveAdapter.TriggerButton, _pipeline?.ClickHoldMs ?? Config.ClickHoldMs);
                LastAction = "换弹 → 屏外开枪";
                break;
            }
            default:
                LastAction = "换弹被忽略（当前游戏 ReloadStrategy=none）";
                break;
        }
    }

    private RectD TargetRectProvider() =>
        _tracker?.Current?.ClientRect
        ?? new RectD(
            0,
            0,
            NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen),
            NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen));

    private void RecreateTracker()
    {
        var processes = new List<string>(Config.ProcessNames);
        var titles = new List<string>(Config.GameWindowTitlePatterns);
        if (ActiveAdapter is { } adapter)
        {
            processes.AddRange(adapter.ProcessNames);
            if (!string.IsNullOrWhiteSpace(adapter.WindowTitleRegex))
            {
                titles.Add(adapter.WindowTitleRegex);
            }
        }

        if (processes.Count == 0 && titles.Count == 0)
        {
            _tracker = null;
            return;
        }

        _tracker = new WindowTracker(new GameWindowFinder(processes, titles));
        _tracker.Found += OnWindowFound;
        _tracker.BoundsChanged += OnWindowBoundsChanged;
        _tracker.Lost += OnWindowLost;
    }

    private void StopTracker()
    {
        if (_tracker is null)
        {
            return;
        }

        _tracker.Found -= OnWindowFound;
        _tracker.BoundsChanged -= OnWindowBoundsChanged;
        _tracker.Lost -= OnWindowLost;
        Wait(_tracker.StopAsync());
        _tracker.Dispose();
        _tracker = null;
    }

    private void OnWindowFound(object? sender, GameWindowInfo info) => _dispatcher.Invoke(() =>
    {
        if (BorderlessArmed)
        {
            ForceBorderless(info.Hwnd);
        }

        Overlay?.FollowRect(info.ClientRect);
    });

    private void OnWindowBoundsChanged(object? sender, GameWindowInfo info) =>
        _dispatcher.Invoke(() => Overlay?.FollowRect(info.ClientRect));

    private void OnWindowLost(object? sender, EventArgs e) =>
        _dispatcher.Invoke(() => Overlay?.HideFrame());

    private void ForceBorderless(IntPtr hwnd)
    {
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        var info = NativeMethods.MONITORINFO.Create();
        if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            var work = info.RcWork;
            Forcer.Apply(hwnd, new RectD(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top));
        }
    }

    private void WriteGameIni(GameAdapter adapter)
    {
        if (string.IsNullOrWhiteSpace(adapter.TeknoParrotIniPath) || adapter.TeknoParrotIniValues.Count == 0)
        {
            IniWriteResult = "该适配器未配置 teknoparrot.ini，跳过写入。";
            return;
        }

        var path = adapter.TeknoParrotIniPath;
        var directory = Path.GetDirectoryName(path);
        if (!File.Exists(path) && (directory is null || !Directory.Exists(directory)))
        {
            IniWriteResult = $"跳过 ini 写入（游戏目录不存在）：{path}";
            return;
        }

        try
        {
            var values = adapter.TeknoParrotIniValues.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, string>)pair.Value,
                StringComparer.OrdinalIgnoreCase);
            TeknoParrotIniWriter.WriteValues(path, values);
            IniWriteResult = $"已写入 {path}（原文件备份为 .bak）";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IniWriteResult = $"ini 写入失败：{ex.Message}";
        }
    }

    private void CleanupServer()
    {
        if (_server is null)
        {
            return;
        }

        _server.ShotServer.ShotReceived -= OnShotReceived;
        _server.ShotServer.CoinReceived -= OnCoinReceived;
        _server.ShotServer.StartReceived -= OnStartReceived;
        _server.ShotServer.ReloadReceived -= OnReloadReceived;
        Wait(_server.StopAsync());
        _server.Dispose();
        _server = null;
    }

    private static void Wait(Task task)
    {
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Best effort during teardown.
        }
    }
}

/// <summary>
/// Resolved key bindings: wraps the raw hotkey settings with parsed Core virtual keys.
/// </summary>
public sealed class HotkeySettingsProvider
{
    /// <summary>Creates a provider from settings, parsing the injectable key names.</summary>
    public HotkeySettingsProvider(HotkeySettings settings)
    {
        Settings = settings;
        CoinKey = KeyNameParser.TryParseCoreKey(settings.CoinKey, out var coin) ? coin : null;
        StartKey = KeyNameParser.TryParseCoreKey(settings.StartKey, out var start) ? start : null;
    }

    /// <summary>The raw settings.</summary>
    public HotkeySettings Settings { get; }

    /// <summary>Configured coin key name (for display).</summary>
    public string CoinKeyName => Settings.CoinKey;

    /// <summary>Configured start key name (for display).</summary>
    public string StartKeyName => Settings.StartKey;

    /// <summary>Parsed coin key, or null when the name is invalid.</summary>
    public TvgunBridge.Core.Injection.VirtualKey? CoinKey { get; }

    /// <summary>Parsed start key, or null when the name is invalid.</summary>
    public TvgunBridge.Core.Injection.VirtualKey? StartKey { get; }
}
