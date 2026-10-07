using System.Windows;
using TvgunBridge.App.Input;
using TvgunBridge.Core.Config;
using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.App;

/// <summary>
/// Startup wiring: loads config + calibration, builds the runtime (protocol server,
/// window tracker, injection pipeline, borderless forcer), creates the click-through
/// overlay, installs the F5-F8 fallback hotkeys, and starts bridging. A port-binding
/// failure (e.g. HttpListener on "+" without a URL ACL) is reported in the control
/// panel instead of crashing. On exit everything is stopped, forced windows are
/// restored, and the keyboard hook is released.
/// </summary>
public partial class App : Application
{
    private BridgeRuntime? _runtime;
    private MainWindow? _mainWindow;
    private GlobalHotkeyHook? _hotkeyHook;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = BridgeConfig.Load();
        var calibration = AffineCalibration.Load();
        var hotkeys = HotkeySettings.Load();
        var keys = new HotkeySettingsProvider(hotkeys);

        _runtime = new BridgeRuntime(config, calibration, keys, Dispatcher)
        {
            // Injection stays off until the user opts in (anti-misfire default).
            InjectionEnabled = false,
            Overlay = new OverlayBorderWindow(),
        };

        _mainWindow = new MainWindow(_runtime)
        {
            HotkeySummary =
                $"本地热键：{hotkeys.CoinHotkey}=投币({hotkeys.CoinKey})  " +
                $"{hotkeys.StartHotkey}=开始({hotkeys.StartKey})  " +
                $"{hotkeys.ToggleInjectionHotkey}=注入开关  {hotkeys.ToggleWindowHotkey}=显示/隐藏本窗口\n" +
                "（可在 %APPDATA%/TvgunBridge/hotkeys.json 修改）",
        };
        _mainWindow.Closed += (_, _) => Shutdown();
        _mainWindow.Show();

        InstallHotkeys(hotkeys);

        // Start bridging immediately so the phone can discover/connect; failures are
        // shown in the panel and can be retried with the 开始桥接 button.
        _ = _runtime.StartBridge();
        _mainWindow.RefreshState();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyHook?.Dispose();
        _hotkeyHook = null;

        if (_runtime is not null)
        {
            _runtime.Overlay?.Close();
            _runtime.Dispose();
            _runtime = null;
        }

        base.OnExit(e);
    }

    private void InstallHotkeys(HotkeySettings hotkeys)
    {
        var runtime = _runtime;
        var mainWindow = _mainWindow;
        if (runtime is null || mainWindow is null)
        {
            return;
        }

        _hotkeyHook = new GlobalHotkeyHook();
        RegisterHotkey(hotkeys.CoinHotkey, runtime.InjectCoin);
        RegisterHotkey(hotkeys.StartHotkey, runtime.InjectStart);
        RegisterHotkey(hotkeys.ToggleInjectionHotkey, () => runtime.InjectionEnabled = !runtime.InjectionEnabled);
        RegisterHotkey(hotkeys.ToggleWindowHotkey, mainWindow.ToggleVisibility);
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
