using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.Core.Windowing;

/// <summary>
/// Forces a game window into borderless mode: strips WS_CAPTION | WS_THICKFRAME |
/// WS_MAXIMIZEBOX | WS_MINIMIZEBOX and resizes the window to fill a given work area
/// (SWP_NOZORDER | SWP_FRAMECHANGED). The original style and placement are remembered
/// per window so <see cref="Undo"/> can restore them.
/// </summary>
public sealed class BorderlessForcer
{
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long StripMask = WsCaption | WsThickFrame | WsMaximizeBox | WsMinimizeBox;

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;

    private const int SwRestore = 9;

    private readonly Dictionary<IntPtr, OriginalState> _originals = new();

    /// <summary>
    /// Applies borderless mode to <paramref name="hwnd"/> and stretches it over
    /// <paramref name="workArea"/> (screen coordinates). Calling Apply twice for the
    /// same window keeps the first captured original state.
    /// </summary>
    public void Apply(IntPtr hwnd, RectD workArea)
    {
        if (!_originals.ContainsKey(hwnd))
        {
            var style = WindowNative.GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
            WindowNative.GetWindowRect(hwnd, out var rect);
            _originals[hwnd] = new OriginalState(style, rect);
        }

        var newStyle = _originals[hwnd].Style & ~StripMask;
        _ = WindowNative.SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(newStyle));
        WindowNative.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)workArea.X,
            (int)workArea.Y,
            (int)workArea.Width,
            (int)workArea.Height,
            SwpNoZOrder | SwpFrameChanged);
    }

    /// <summary>
    /// Restores the window style and placement captured by <see cref="Apply"/>.
    /// No-op when the window was never forced.
    /// </summary>
    public bool Undo(IntPtr hwnd)
    {
        if (!_originals.Remove(hwnd, out var original))
        {
            return false;
        }

        if (!WindowNative.IsWindow(hwnd))
        {
            return true;
        }

        _ = WindowNative.SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(original.Style));
        WindowNative.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            original.WindowRect.Left,
            original.WindowRect.Top,
            original.WindowRect.Right - original.WindowRect.Left,
            original.WindowRect.Bottom - original.WindowRect.Top,
            SwpNoZOrder | SwpFrameChanged);
        return true;
    }

    /// <summary>Restores every forced window and clears the saved state.</summary>
    public void UndoAll()
    {
        foreach (var hwnd in _originals.Keys.ToArray())
        {
            Undo(hwnd);
        }
    }

    /// <summary>Shows a window in restored (normal) state.</summary>
    public static void ShowRestored(IntPtr hwnd) => WindowNative.ShowWindow(hwnd, SwRestore);

    private sealed record OriginalState(long Style, WindowNative.RECT WindowRect);
}
