using System.Runtime.InteropServices;
using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.Core.Injection;

/// <summary>
/// Injects input via the Win32 SendInput API. Pointer moves use
/// MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK so coordinates span the whole
/// virtual screen. MoveAbsolute is throttled: moves smaller than 1 px that arrive
/// within 8 ms of the previous send are dropped (the phone streams at ~120 Hz;
/// duplicate positions add no information).
/// </summary>
public sealed class SendInputInjector : IInputInjector
{
    private readonly VirtualDeskNormalizer _normalizer;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _moveGate = new();
    private readonly SemaphoreSlim _clickGate = new(1, 1);
    private (double X, double Y)? _lastMove;
    private DateTimeOffset _lastMoveSent;

    /// <summary>
    /// Creates an injector.
    /// </summary>
    /// <param name="normalizer">Screen-to-absolute converter. Defaults to the live
    /// virtual-screen metrics.</param>
    /// <param name="clock">Time source for throttling, injectable for tests.</param>
    public SendInputInjector(VirtualDeskNormalizer? normalizer = null, Func<DateTimeOffset>? clock = null)
    {
        _normalizer = normalizer ?? new VirtualDeskNormalizer();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Minimum pointer displacement in pixels for a move to be sent.</summary>
    public double MinMoveDeltaPx { get; set; } = 1.0;

    /// <summary>Minimum interval between sends for sub-threshold moves.</summary>
    public TimeSpan MinMoveInterval { get; set; } = TimeSpan.FromMilliseconds(8);

    /// <inheritdoc />
    public void MoveAbsolute(double screenX, double screenY)
    {
        lock (_moveGate)
        {
            var now = _clock();
            if (_lastMove is { } last)
            {
                var dx = screenX - last.X;
                var dy = screenY - last.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < MinMoveDeltaPx && now - _lastMoveSent < MinMoveInterval)
                {
                    return;
                }
            }

            var (ax, ay) = _normalizer.ToAbsolute(screenX, screenY);
            var input = INPUT.MouseMove(ax, ay);
            NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            _lastMove = (screenX, screenY);
            _lastMoveSent = now;
        }
    }

    /// <inheritdoc />
    public void ButtonDown(MouseButton button) => SendMouseButton(button, down: true);

    /// <inheritdoc />
    public void ButtonUp(MouseButton button) => SendMouseButton(button, down: false);

    /// <inheritdoc />
    public void Click(MouseButton button, int holdMs)
    {
        // Fire-and-forget; the semaphore serializes overlapping clicks so each
        // down is paired with its own up without blocking the caller.
        _ = ClickAsync(button, holdMs);
    }

    /// <inheritdoc />
    public void KeyTap(VirtualKey key)
    {
        var inputs = new[]
        {
            INPUT.Keyboard((ushort)key, NativeMethods.KeyEventFKeyDown),
            INPUT.Keyboard((ushort)key, NativeMethods.KeyEventFKeyUp),
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private async Task ClickAsync(MouseButton button, int holdMs)
    {
        await _clickGate.WaitAsync().ConfigureAwait(false);
        try
        {
            ButtonDown(button);
            if (holdMs > 0)
            {
                await Task.Delay(holdMs).ConfigureAwait(false);
            }

            ButtonUp(button);
        }
        finally
        {
            _clickGate.Release();
        }
    }

    private void SendMouseButton(MouseButton button, bool down)
    {
        var flags = (button, down) switch
        {
            (MouseButton.Left, true) => NativeMethods.MouseEventFLeftDown,
            (MouseButton.Left, false) => NativeMethods.MouseEventFLeftUp,
            (MouseButton.Right, true) => NativeMethods.MouseEventFRightDown,
            (MouseButton.Right, false) => NativeMethods.MouseEventFRightUp,
            (MouseButton.Middle, true) => NativeMethods.MouseEventFMiddleDown,
            (MouseButton.Middle, false) => NativeMethods.MouseEventFMiddleUp,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null),
        };
        var input = INPUT.MouseButton(flags);
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}
