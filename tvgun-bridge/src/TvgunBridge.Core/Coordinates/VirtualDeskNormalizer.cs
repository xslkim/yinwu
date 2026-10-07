using System.Runtime.InteropServices;

namespace TvgunBridge.Core.Coordinates;

/// <summary>Virtual-screen metrics used by <see cref="VirtualDeskNormalizer"/>.</summary>
/// <param name="X">Left edge of the virtual screen (SM_XVIRTUALSCREEN).</param>
/// <param name="Y">Top edge of the virtual screen (SM_YVIRTUALSCREEN).</param>
/// <param name="Width">Width in pixels (SM_CXVIRTUALSCREEN).</param>
/// <param name="Height">Height in pixels (SM_CYVIRTUALSCREEN).</param>
public readonly record struct VirtualScreenMetrics(int X, int Y, int Width, int Height);

/// <summary>
/// Converts between screen pixel coordinates and the 0..65535 absolute range expected
/// by SendInput with MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, based on the
/// virtual-screen metrics. The conversion adds 0.5 before truncation so the rounded
/// value is not systematically biased by the OS's integer truncation. Metrics are
/// injectable so the conversion is unit-testable without a desktop session.
/// </summary>
public sealed class VirtualDeskNormalizer
{
    /// <summary>Full scale of the SendInput absolute coordinate space.</summary>
    public const double AbsoluteRange = 65535.0;

    private readonly Func<VirtualScreenMetrics> _metricsProvider;

    /// <summary>
    /// Creates a normalizer.
    /// </summary>
    /// <param name="metricsProvider">Supplies the virtual-screen metrics. Defaults to
    /// GetSystemMetrics (SM_X/Y/CX/CYVIRTUALSCREEN).</param>
    public VirtualDeskNormalizer(Func<VirtualScreenMetrics>? metricsProvider = null)
    {
        _metricsProvider = metricsProvider ?? GetVirtualScreenMetrics;
    }

    /// <summary>
    /// Converts a screen pixel coordinate to SendInput absolute units (0..65535).
    /// </summary>
    public (int X, int Y) ToAbsolute(double screenX, double screenY)
    {
        var m = _metricsProvider();
        var ax = (int)((screenX - m.X) * AbsoluteRange / m.Width + 0.5);
        var ay = (int)((screenY - m.Y) * AbsoluteRange / m.Height + 0.5);
        return (Math.Clamp(ax, 0, 65535), Math.Clamp(ay, 0, 65535));
    }

    /// <summary>
    /// Converts SendInput absolute units back to a screen pixel coordinate.
    /// </summary>
    public PointD FromAbsolute(int absoluteX, int absoluteY)
    {
        var m = _metricsProvider();
        return new PointD(
            m.X + absoluteX / AbsoluteRange * m.Width,
            m.Y + absoluteY / AbsoluteRange * m.Height);
    }

    /// <summary>Reads the virtual-screen metrics from user32.</summary>
    public static VirtualScreenMetrics GetVirtualScreenMetrics() =>
        new(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmCyVirtualScreen));

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
