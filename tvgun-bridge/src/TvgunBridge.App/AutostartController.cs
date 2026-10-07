using TvgunBridge.Core.Windowing;

namespace TvgunBridge.App;

/// <summary>Host actions the <see cref="AutostartController"/> drives (injected, UI-free).</summary>
public sealed class AutostartActions
{
    /// <summary>Applies borderless full-screen to the found game window handle.</summary>
    public required Action<IntPtr> ApplyBorderless { get; init; }

    /// <summary>Requests a graceful application shutdown with the given reason token.</summary>
    public required Action<string> RequestShutdown { get; init; }
}

/// <summary>
/// Headless autostart state machine. Inputs are window-tracker events (forwarded by
/// the runtime on the UI thread) and <see cref="Tick"/> calls from a timer; outputs
/// are the injected <see cref="AutostartActions"/>. Pure logic with an injectable
/// clock so the exit-after-game timing can be tested without a desktop.
/// </summary>
public sealed class AutostartController
{
    private readonly AutostartActions _actions;
    private readonly bool _exitAfterGame;
    private readonly TimeSpan _lostGrace;
    private readonly Func<DateTimeOffset> _clock;
    private DateTimeOffset? _lostSince;

    /// <summary>
    /// Creates the controller. <paramref name="lostGrace"/> defaults to 10 seconds;
    /// <paramref name="clock"/> defaults to the system clock.
    /// </summary>
    public AutostartController(
        AutostartActions actions,
        bool exitAfterGame = true,
        TimeSpan? lostGrace = null,
        Func<DateTimeOffset>? clock = null)
    {
        _actions = actions;
        _exitAfterGame = exitAfterGame;
        _lostGrace = lostGrace ?? TimeSpan.FromSeconds(10);
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    /// <summary>True once the game window has been found at least once.</summary>
    public bool EverFound { get; private set; }

    /// <summary>True after the controller has requested shutdown.</summary>
    public bool ShutdownRequested { get; private set; }

    /// <summary>Window located: arms borderless full-screen and clears the lost timer.</summary>
    public void OnWindowFound(object? sender, GameWindowInfo info)
    {
        EverFound = true;
        _lostSince = null;
        _actions.ApplyBorderless(info.Hwnd);
    }

    /// <summary>Window lost: starts the grace timer (only after the first Found).</summary>
    public void OnWindowLost(object? sender, EventArgs e)
    {
        if (EverFound && _lostSince is null)
        {
            _lostSince = _clock();
        }
    }

    /// <summary>
    /// Periodic check (call from a ~1 s timer): once the window has been found and
    /// then stays lost for the grace period, request a graceful shutdown with reason
    /// <c>game_exited</c>. Never fires before the first Found (the game may simply
    /// still be starting).
    /// </summary>
    public void Tick()
    {
        if (ShutdownRequested || !_exitAfterGame || !EverFound || _lostSince is not { } since)
        {
            return;
        }

        if (_clock() - since >= _lostGrace)
        {
            ShutdownRequested = true;
            _actions.RequestShutdown("game_exited");
        }
    }
}
