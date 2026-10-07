using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.Core.Windowing;

/// <summary>
/// Locates the game window among top-level visible windows by matching process names
/// and window-title regular expressions. Candidates are listed in HookedWindows.txt of
/// the 1846 TeknoParrot install; which patterns to use is a configuration concern.
/// </summary>
public sealed class GameWindowFinder
{
    private readonly IReadOnlyList<string> _processNames;
    private readonly IReadOnlyList<Regex> _titlePatterns;

    /// <summary>
    /// Creates a finder.
    /// </summary>
    /// <param name="processNames">Process names to match, case-insensitive, with or
    /// without the ".exe" suffix.</param>
    /// <param name="titlePatterns">Regular expressions matched against window titles,
    /// case-insensitive.</param>
    public GameWindowFinder(IEnumerable<string> processNames, IEnumerable<string> titlePatterns)
    {
        _processNames = processNames.ToArray();
        _titlePatterns = titlePatterns
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();
    }

    /// <summary>
    /// Enumerates top-level visible windows and returns the best match, or null.
    /// Scoring: +2 for a title-pattern match, +1 for a process-name match; the first
    /// window with the highest positive score wins.
    /// </summary>
    public GameWindowInfo? FindBest()
    {
        GameWindowInfo? best = null;
        var bestScore = 0;

        WindowNative.EnumWindows((hwnd, _) =>
        {
            if (!WindowNative.IsWindowVisible(hwnd))
            {
                return true;
            }

            var title = GetWindowTitle(hwnd);
            if (title.Length == 0)
            {
                return true;
            }

            var score = 0;
            if (_titlePatterns.Any(p => p.IsMatch(title)))
            {
                score += 2;
            }

            if (ProcessMatches(hwnd))
            {
                score += 1;
            }

            if (score > bestScore && TryGetClientRect(hwnd, out var clientRect))
            {
                bestScore = score;
                best = new GameWindowInfo(hwnd, title, clientRect);
            }

            return true;
        }, IntPtr.Zero);

        return best;
    }

    /// <summary>
    /// Reads the client rectangle of a window in screen coordinates.
    /// </summary>
    public static bool TryGetClientRect(IntPtr hwnd, out RectD clientRect)
    {
        clientRect = default;
        if (!WindowNative.GetClientRect(hwnd, out var rect))
        {
            return false;
        }

        var origin = new WindowNative.POINT { X = 0, Y = 0 };
        if (!WindowNative.ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        clientRect = new RectD(origin.X, origin.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return true;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = WindowNative.GetWindowTextLength(hwnd);
        if (length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = WindowNative.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private bool ProcessMatches(IntPtr hwnd)
    {
        if (_processNames.Count == 0)
        {
            return false;
        }

        _ = WindowNative.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return _processNames.Any(name =>
                string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(process.ProcessName + ".exe", name, StringComparison.OrdinalIgnoreCase));
        }
        catch (ArgumentException)
        {
            return false; // process exited between EnumWindows and the lookup
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

internal static class WindowNative
{
    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }
}
