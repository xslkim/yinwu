using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.App.Rendering;

/// <summary>
/// Computes the white reference frame drawn around the play area: four solid edge
/// bars plus two bars per corner forming L-shaped corner marks. Geometry follows the
/// tvgun reference (BORDER_THICK = 24 px, corner length = 3x the border thickness).
/// Units are caller-supplied (DIPs or pixels); the result uses the same units.
/// </summary>
public static class FrameGeometry
{
    /// <summary>Border thickness in physical pixels (tvgun BORDER_THICK).</summary>
    public const double BorderThicknessPx = 24.0;

    /// <summary>Corner mark length as a multiple of the border thickness (tvgun CORNER_LEN).</summary>
    public const double CornerLengthFactor = 3.0;

    /// <summary>
    /// Returns the frame rectangles for a surface of the given size: entries 0-3 are
    /// the top/bottom/left/right edge bars, entries 4-11 are the corner L marks
    /// (horizontal then vertical bar for top-left, top-right, bottom-left, bottom-right).
    /// </summary>
    public static IReadOnlyList<RectD> Compute(double width, double height, double thickness)
    {
        var t = thickness;
        var corner = t * CornerLengthFactor;
        return new[]
        {
            new RectD(0, 0, width, t),                  // top edge
            new RectD(0, height - t, width, t),         // bottom edge
            new RectD(0, 0, t, height),                 // left edge
            new RectD(width - t, 0, t, height),         // right edge
            new RectD(0, 0, corner, t),                 // top-left horizontal
            new RectD(0, 0, t, corner),                 // top-left vertical
            new RectD(width - corner, 0, corner, t),    // top-right horizontal
            new RectD(width - t, 0, t, corner),         // top-right vertical
            new RectD(0, height - t, corner, t),        // bottom-left horizontal
            new RectD(0, height - corner, t, corner),   // bottom-left vertical
            new RectD(width - corner, height - t, corner, t), // bottom-right horizontal
            new RectD(width - t, height - corner, t, corner), // bottom-right vertical
        };
    }
}
