namespace TvgunBridge.Core.Coordinates;

/// <summary>A point with double-precision coordinates.</summary>
public readonly record struct PointD(double X, double Y);

/// <summary>An axis-aligned rectangle with double-precision coordinates.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    /// <summary>Right edge (X + Width).</summary>
    public double Right => X + Width;

    /// <summary>Bottom edge (Y + Height).</summary>
    public double Bottom => Y + Height;
}
