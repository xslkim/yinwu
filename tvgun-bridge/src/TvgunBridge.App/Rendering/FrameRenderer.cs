using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.App.Rendering;

/// <summary>
/// Draws the frame produced by <see cref="FrameGeometry"/> onto a canvas as white
/// rectangles. Shared by the overlay window and the self-test window.
/// </summary>
public static class FrameRenderer
{
    /// <summary>
    /// Replaces the canvas content with the white frame. The middle of the canvas is
    /// left untouched (fully transparent / black depending on the window background).
    /// </summary>
    /// <param name="canvas">Target canvas (its children are cleared first).</param>
    /// <param name="width">Surface width in DIPs.</param>
    /// <param name="height">Surface height in DIPs.</param>
    /// <param name="dpiScale">DPI scale of the surface (physical px per DIP).</param>
    public static void Draw(Canvas canvas, double width, double height, double dpiScale)
    {
        canvas.Children.Clear();
        if (width <= 0 || height <= 0 || dpiScale <= 0)
        {
            return;
        }

        var thicknessDip = FrameGeometry.BorderThicknessPx / dpiScale;
        foreach (RectD rect in FrameGeometry.Compute(width, height, thicknessDip))
        {
            var bar = new Rectangle
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = Brushes.White,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(bar, rect.X);
            Canvas.SetTop(bar, rect.Y);
            canvas.Children.Add(bar);
        }
    }
}
