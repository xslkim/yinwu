using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.App.Injection;

/// <summary>
/// Pure shot/reload coordinate helpers implementing the per-game reload strategies
/// (task T2.2). No UI dependencies.
/// </summary>
public static class ReloadPlanner
{
    /// <summary>Width of the band just outside the target rectangle, in pixels.</summary>
    public const double OutsideBandPx = 8.0;

    /// <summary>
    /// Normalized coordinate used for synthesized off-screen reload shots (well
    /// outside the [0,1920]x[0,1080] frame).
    /// </summary>
    public static PointD OffscreenReloadNorm => new(-32.0, -32.0);

    /// <summary>
    /// For the offscreen-shot reload strategy: when a mapped shot point lies outside
    /// the target rectangle, clamp it into the thin band (<see cref="OutsideBandPx"/>)
    /// just outside the rectangle so the cursor registers as "off screen" for the game
    /// without wandering far away. Points inside the rectangle pass through unchanged.
    /// </summary>
    public static PointD ClampToOutsideBand(PointD point, RectD rect, double band = OutsideBandPx)
    {
        var inside = point.X >= rect.X && point.X <= rect.Right
            && point.Y >= rect.Y && point.Y <= rect.Bottom;
        if (inside)
        {
            return point;
        }

        return new PointD(
            Math.Clamp(point.X, rect.X - band, rect.Right + band),
            Math.Clamp(point.Y, rect.Y - band, rect.Bottom + band));
    }
}
