using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.Core.Windowing;

/// <summary>
/// Polls the game window (default every 500 ms) and raises events as it appears,
/// moves/resizes, or disappears. When the tracked handle becomes invalid the window is
/// re-located automatically via the supplied <see cref="GameWindowFinder"/>.
/// </summary>
public sealed class WindowTracker : IDisposable
{
    private readonly GameWindowFinder _finder;
    private readonly TimeSpan _pollInterval;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private IntPtr _hwnd;
    private RectD? _lastRect;

    /// <summary>
    /// Creates a tracker.
    /// </summary>
    /// <param name="finder">Finder used to (re-)locate the window.</param>
    /// <param name="pollInterval">Polling interval. Defaults to 500 ms.</param>
    public WindowTracker(GameWindowFinder finder, TimeSpan? pollInterval = null)
    {
        _finder = finder;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>Raised when the window is (re-)located after being unknown or lost.</summary>
    public event EventHandler<GameWindowInfo>? Found;

    /// <summary>Raised when the located window's client rectangle changes.</summary>
    public event EventHandler<GameWindowInfo>? BoundsChanged;

    /// <summary>Raised when the tracked window disappears.</summary>
    public event EventHandler? Lost;

    /// <summary>The currently tracked window info, if any.</summary>
    public GameWindowInfo? Current { get; private set; }

    /// <summary>Starts polling.</summary>
    /// <exception cref="InvalidOperationException">The tracker is already running.</exception>
    public void Start()
    {
        if (_cts is not null)
        {
            throw new InvalidOperationException("The tracker is already running.");
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>Stops polling.</summary>
    public async Task StopAsync()
    {
        var cts = _cts;
        var loop = _loop;
        _cts = null;
        _loop = null;
        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => _cts?.Cancel();

    /// <summary>
    /// Performs one polling iteration. Exposed so tests and hosts can drive the tracker
    /// without the timer.
    /// </summary>
    public void Poll()
    {
        if (_hwnd != IntPtr.Zero && WindowNative.IsWindow(_hwnd)
            && GameWindowFinder.TryGetClientRect(_hwnd, out var rect))
        {
            if (_lastRect != rect)
            {
                _lastRect = rect;
                var updated = new GameWindowInfo(_hwnd, Current?.Title ?? string.Empty, rect);
                Current = updated;
                BoundsChanged?.Invoke(this, updated);
            }

            return;
        }

        if (_hwnd != IntPtr.Zero || Current is not null)
        {
            _hwnd = IntPtr.Zero;
            _lastRect = null;
            Current = null;
            Lost?.Invoke(this, EventArgs.Empty);
        }

        var found = _finder.FindBest();
        if (found is { } info)
        {
            _hwnd = info.Hwnd;
            _lastRect = info.ClientRect;
            Current = info;
            Found?.Invoke(this, info);
            BoundsChanged?.Invoke(this, info);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        try
        {
            Poll();
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Poll();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
