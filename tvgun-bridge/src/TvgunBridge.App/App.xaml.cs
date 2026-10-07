using System.IO;
using System.Windows;
using System.Windows.Threading;
using TvgunBridge.App.Input;
using TvgunBridge.Core.Config;
using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.App;

/// <summary>
/// Startup wiring. Two modes:
/// no arguments → the classic manual GUI (control panel, unchanged behavior);
/// <c>--game &lt;adapterId&gt;</c> → one-shot autostart: loads the adapter, writes its
/// teknoparrot.ini, starts the bridge with injection armed, forces the game window
/// borderless full-screen with the overlay frame, and (default) exits gracefully
/// once the game window has been gone for 10 seconds. The control panel stays hidden
/// unless <c>--show-ui</c> is passed. Both modes write the machine-readable event
/// log to %APPDATA%/TvgunBridge/bridge.log. On exit everything is stopped, forced
/// windows are restored, and the keyboard hook is released.
/// </summary>
public partial class App : Application
{
    private BridgeRuntime? _runtime;
    private MainWindow? _mainWindow;
    private GlobalHotkeyHook? _hotkeyHook;
    private AutostartController? _autostart;
    private DispatcherTimer? _autostartTimer;
    private string _shutdownReason = "user_exit";
    private bool _shutdownLogged;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!CliOptions.TryParse(e.Args, out var cli, out var error))
        {
            BridgeLog.Event($"CLI_ERROR error=\"{error}\"");
            _shutdownReason = "cli_error";
            ConsoleOutput.WriteError($"参数错误：{error}\n\n{CliOptions.Usage}");
            Shutdown(2);
            return;
        }

        if (cli.IsAutoMode)
        {
            StartAuto(cli);
        }
        else
        {
            StartGui();
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _autostartTimer?.Stop();
        _autostartTimer = null;

        _hotkeyHook?.Dispose();
        _hotkeyHook = null;

        if (_runtime is not null)
        {
            _runtime.Overlay?.Close();
            _runtime.Dispose();
            _runtime = null;
        }

        LogShutdown();
        base.OnExit(e);
    }

    private void StartGui()
    {
        var config = BridgeConfig.Load();
        var calibration = AffineCalibration.Load();
        var hotkeys = HotkeySettings.Load();
        var keys = new HotkeySettingsProvider(hotkeys);

        BridgeLog.Event("STARTUP mode=gui");
        LogSetupCheck(config.Port);

        _runtime = new BridgeRuntime(config, calibration, keys, Dispatcher)
        {
            // Injection stays off until the user opts in (anti-misfire default).
            InjectionEnabled = false,
            Overlay = new OverlayBorderWindow(),
        };

        _mainWindow = new MainWindow(_runtime)
        {
            HotkeySummary = BuildHotkeySummary(hotkeys),
        };
        _mainWindow.Closed += (_, _) => Shutdown();
        _mainWindow.Show();

        InstallHotkeys(hotkeys);

        // Start bridging immediately so the phone can discover/connect; failures are
        // shown in the panel and can be retried with the 开始桥接 button.
        _ = _runtime.StartBridge();
        _mainWindow.RefreshState();
    }

    private void StartAuto(CliOptions cli)
    {
        // No window may be shown in this mode; shutdown happens only on request.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var config = BridgeConfig.Load();
        BridgeLog.Event(
            $"STARTUP mode=auto game={cli.GameId} inject={!cli.NoInject} exitAfterGame={cli.ExitAfterGame}");
        LogSetupCheck(config.Port);

        var adapter = LoadAdapter(cli.GameId!);
        if (adapter is null)
        {
            BridgeLog.Event($"GAME_NOT_FOUND id={cli.GameId}");
            _shutdownReason = "game_not_found";
            ConsoleOutput.WriteError($"找不到游戏适配文件：games/{cli.GameId}.json");
            Shutdown(2);
            return;
        }

        var calibration = AffineCalibration.Load();
        var hotkeys = HotkeySettings.Load();
        var keys = new HotkeySettingsProvider(hotkeys);

        var runtime = new BridgeRuntime(config, calibration, keys, Dispatcher)
        {
            // Autostart arms injection immediately unless --no-inject was given.
            InjectionEnabled = !cli.NoInject,
            Overlay = new OverlayBorderWindow(),
        };
        _runtime = runtime;

        // Applies window matching / reload strategy / click hold and writes the
        // adapter's teknoparrot.ini (skipped with a warning when the game directory
        // is absent — the bridge keeps running).
        runtime.SelectGame(adapter);

        _autostart = new AutostartController(
            new AutostartActions
            {
                ApplyBorderless = runtime.ApplyBorderlessFullScreen,
                RequestShutdown = RequestAutoShutdown,
            },
            exitAfterGame: cli.ExitAfterGame);
        runtime.WindowFound += _autostart.OnWindowFound;
        runtime.WindowLost += _autostart.OnWindowLost;

        if (!runtime.StartBridge())
        {
            _shutdownReason = "server_bind_failed";
            ConsoleOutput.WriteError(runtime.LastError ?? "端口绑定失败");
            Shutdown(1);
            return;
        }

        _autostartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autostartTimer.Tick += (_, _) => _autostart.Tick();
        _autostartTimer.Start();

        InstallHotkeys(hotkeys);

        if (cli.ShowUi)
        {
            _mainWindow = new MainWindow(runtime)
            {
                HotkeySummary = BuildHotkeySummary(hotkeys),
            };
            _mainWindow.Closed += (_, _) => Shutdown();
            _mainWindow.Show();
        }
    }

    private void RequestAutoShutdown(string reason)
    {
        _shutdownReason = reason;
        Shutdown();
    }

    private void LogShutdown()
    {
        if (_shutdownLogged)
        {
            return;
        }

        _shutdownLogged = true;
        BridgeLog.Event($"SHUTDOWN reason={_shutdownReason}");
    }

    private static void LogSetupCheck(int port)
    {
        foreach (var line in SetupCheck.Check(port))
        {
            BridgeLog.Event(line);
        }
    }

    private static GameAdapter? LoadAdapter(string id)
    {
        var gamesDir = Path.Combine(AppContext.BaseDirectory, "games");
        try
        {
            return GameAdapter.LoadAll(gamesDir)
                .FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            BridgeLog.Event($"GAMES_LOAD_FAILED error=\"{ex.Message}\"");
            return null;
        }
    }

    private static string BuildHotkeySummary(HotkeySettings hotkeys) =>
        $"本地热键：{hotkeys.CoinHotkey}=投币({hotkeys.CoinKey})  " +
        $"{hotkeys.StartHotkey}=开始({hotkeys.StartKey})  " +
        $"{hotkeys.ToggleInjectionHotkey}=注入开关  {hotkeys.ToggleWindowHotkey}=显示/隐藏本窗口\n" +
        "（可在 %APPDATA%/TvgunBridge/hotkeys.json 修改）";

    private void InstallHotkeys(HotkeySettings hotkeys)
    {
        var runtime = _runtime;
        if (runtime is null)
        {
            return;
        }

        _hotkeyHook = new GlobalHotkeyHook();
        RegisterHotkey(hotkeys.CoinHotkey, runtime.InjectCoin);
        RegisterHotkey(hotkeys.StartHotkey, runtime.InjectStart);
        RegisterHotkey(hotkeys.ToggleInjectionHotkey, () => runtime.InjectionEnabled = !runtime.InjectionEnabled);
        if (_mainWindow is { } mainWindow)
        {
            RegisterHotkey(hotkeys.ToggleWindowHotkey, mainWindow.ToggleVisibility);
        }

        _hotkeyHook.Start();
    }

    private void RegisterHotkey(string name, Action action)
    {
        if (_hotkeyHook is not null && KeyNameParser.TryParseVk(name, out var vk))
        {
            _hotkeyHook.Register(vk, action);
        }
    }
}
