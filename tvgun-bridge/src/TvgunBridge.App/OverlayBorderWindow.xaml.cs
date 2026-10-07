using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TvgunBridge.App.Native;
using TvgunBridge.App.Rendering;
using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.App;

/// <summary>
/// Topmost, transparent, click-through window that draws the 24 px white reference
/// frame (plus corner L marks) around the game client area (task T1.1). The window
/// never takes focus and never appears in Alt-Tab: in addition to the WPF flags, the
/// extended styles WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE |
/// WS_EX_TOOLWINDOW are applied when the handle is created. The middle of the window
/// draws nothing, so the game below stays fully visible and receives all input.
/// </summary>
public partial class OverlayBorderWindow : Window
{
    /// <summary>Creates the overlay. It stays hidden until <see cref="FollowRect"/> is called.</summary>
    public OverlayBorderWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    /// <summary>
    /// Positions the overlay over the given screen-pixel rectangle (the game client
    /// area) and shows it. Coordinates are converted to DIPs with the overlay's
    /// current DPI scale (the process is PerMonitorV2-aware; a DPI change re-triggers
    /// the layout via <see cref="OnDpiChanged"/> and the next tracker BoundsChanged).
    /// </summary>
    public void FollowRect(RectD screenRectPx)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = screenRectPx.X / dpi.DpiScaleX;
        Top = screenRectPx.Y / dpi.DpiScaleY;
        Width = screenRectPx.Width / dpi.DpiScaleX;
        Height = screenRectPx.Height / dpi.DpiScaleY;
        if (!IsVisible)
        {
            Show();
        }

        Redraw();
    }

    /// <summary>Hides the frame (game window lost or bridge stopped).</summary>
    public void HideFrame() => Hide();

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        exStyle |= NativeMethods.WsExTransparent
            | NativeMethods.WsExLayered
            | NativeMethods.WsExNoActivate
            | NativeMethods.WsExToolWindow;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(exStyle));
        Redraw();
    }

    /// <inheritdoc />
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Redraw();
    }

    private void Redraw()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        FrameRenderer.Draw(BorderCanvas, ActualWidth, ActualHeight, dpi.DpiScaleX);
    }
}
