namespace TvgunBridge.Core.Coordinates;

/// <summary>
/// Maps tvgun normalized coordinates (0..1920, 0..1080, origin at the outer edge of
/// the white frame) into screen pixels of a target rectangle. The target rectangle is
/// the game client area in screen coordinates; mapping is linear, matching
/// run_tv.py's norm_to_px with the client rect as the frame: an optional
/// <see cref="AffineCalibration"/> is applied first, then
/// <c>px = rect.X + cx/1920*rect.Width, py = rect.Y + cy/1080*rect.Height</c>.
/// </summary>
public sealed class CoordinateMapper
{
    /// <summary>Width of the normalized coordinate space.</summary>
    public const double NormWidth = 1920.0;

    /// <summary>Height of the normalized coordinate space.</summary>
    public const double NormHeight = 1080.0;

    /// <summary>
    /// Creates a mapper.
    /// </summary>
    /// <param name="calibration">Calibration to apply before the linear mapping.
    /// Defaults to <see cref="AffineCalibration.Identity"/>.</param>
    public CoordinateMapper(AffineCalibration? calibration = null)
    {
        Calibration = calibration ?? AffineCalibration.Identity;
    }

    /// <summary>The calibration applied before the linear mapping.</summary>
    public AffineCalibration Calibration { get; set; }

    /// <summary>
    /// Maps a normalized coordinate to a screen pixel inside <paramref name="target"/>.
    /// No clamping is applied: coordinates outside the frame map outside the rectangle,
    /// which is required for off-screen reload shots.
    /// </summary>
    public PointD Map(double normX, double normY, RectD target)
    {
        var corrected = Calibration.Transform(normX, normY);
        return new PointD(
            target.X + corrected.X / NormWidth * target.Width,
            target.Y + corrected.Y / NormHeight * target.Height);
    }

    /// <summary>Inverse of <see cref="Map"/>: screen pixel back to raw normalized coordinates.</summary>
    public PointD MapInverse(double screenX, double screenY, RectD target)
    {
        var nx = (screenX - target.X) / target.Width * NormWidth;
        var ny = (screenY - target.Y) / target.Height * NormHeight;
        // Invert the calibration: nx = (cx - B)/A, ny = (cy - D)/C.
        var cal = Calibration;
        return new PointD(
            cal.A != 0 ? (nx - cal.B) / cal.A : nx,
            cal.C != 0 ? (ny - cal.D) / cal.C : ny);
    }
}
