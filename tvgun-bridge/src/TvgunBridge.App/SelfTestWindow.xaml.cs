using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TvgunBridge.App.Rendering;
using TvgunBridge.App.SelfTest;
using TvgunBridge.Core.Coordinates;
using TvgunBridge.Core.Protocol;
using TvgunBridge.Core.Windowing;

namespace TvgunBridge.App;

/// <summary>
/// Self-test mode (task T0.4): a fullscreen black window with the same 24 px white
/// frame as the overlay, a slowly bouncing white target, and a cyan crosshair driven
/// by the live aim stream mapped through the runtime's CoordinateMapper. Shooting the
/// target (crosshair within the target radius when POST /shot arrives) scores a point
/// and relocates the target. This validates the full phone → bridge → mapping → hit
/// chain without TeknoParrot. ESC closes.
/// </summary>
public partial class SelfTestWindow : Window
{
    private const double TargetRadiusDip = 48.0;
    private const double TargetSpeedDipPerSecond = 220.0;
    private const double CrosshairRadiusDip = 14.0;

    private readonly BridgeRuntime _runtime;
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly Ellipse _targetVisual;
    private readonly Ellipse _crosshairRing;
    private readonly Line _crosshairH;
    private readonly Line _crosshairV;
    private BouncingTarget? _target;
    private RectD _windowRectPx;
    private int _score;

    /// <summary>Creates the self-test window bound to the runtime's aim slot and shot feed.</summary>
    public SelfTestWindow(BridgeRuntime runtime)
    {
        _runtime = runtime;
        InitializeComponent();

        _targetVisual = new Ellipse
        {
            Width = TargetRadiusDip * 2,
            Height = TargetRadiusDip * 2,
            Fill = Brushes.White,
            IsHitTestVisible = false,
        };
        _crosshairRing = new Ellipse
        {
            Width = CrosshairRadiusDip * 2,
            Height = CrosshairRadiusDip * 2,
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            IsHitTestVisible = false,
        };
        _crosshairH = new Line { Stroke = Brushes.Cyan, StrokeThickness = 2, IsHitTestVisible = false };
        _crosshairV = new Line { Stroke = Brushes.Cyan, StrokeThickness = 2, IsHitTestVisible = false };

        _runtime.ShotFeed += OnShotFeed;
        Closed += OnClosed;
        SizeChanged += (_, _) => OnSurfaceChanged();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => OnFrame();
        UpdateScore();
    }

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        _ = GameWindowFinder.TryGetClientRect(hwnd, out _windowRectPx);
        OnSurfaceChanged();
        _clock.Start();
        _timer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _runtime.ShotFeed -= OnShotFeed;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnSurfaceChanged()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        FrameRenderer.Draw(PlayCanvas, ActualWidth, ActualHeight, dpi);
        var inset = FrameGeometry.BorderThicknessPx / dpi;
        _target = new BouncingTarget(TargetRadiusDip, TargetSpeedDipPerSecond, _random, ActualWidth, ActualHeight, inset);

        PlayCanvas.Children.Add(_targetVisual);
        PlayCanvas.Children.Add(_crosshairRing);
        PlayCanvas.Children.Add(_crosshairH);
        PlayCanvas.Children.Add(_crosshairV);
    }

    private void OnFrame()
    {
        if (_target is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();

        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var inset = FrameGeometry.BorderThicknessPx / dpi;
        _target.Advance(elapsed, ActualWidth, ActualHeight, inset);
        Canvas.SetLeft(_targetVisual, _target.X - TargetRadiusDip);
        Canvas.SetTop(_targetVisual, _target.Y - TargetRadiusDip);

        if (TryMapAim(_runtime.Aim, out var crossDip))
        {
            ShowCrosshair(crossDip);
        }
        else
        {
            HideCrosshair();
        }
    }

    private void OnShotFeed(object? sender, ShotEvent shot) => Dispatcher.BeginInvoke(() =>
    {
        if (_target is null)
        {
            return;
        }

        if (TryMapShot(shot, out var hitDip) && _target.IsHit(hitDip.X, hitDip.Y))
        {
            _score++;
            UpdateScore();
            var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var inset = FrameGeometry.BorderThicknessPx / dpi;
            _target.Relocate(_random, ActualWidth, ActualHeight, inset);
        }
    });

    private bool TryMapAim(AimSlot? aim, out PointD dip)
    {
        dip = default;
        if (aim is null || !aim.TryGet(out var point))
        {
            return false;
        }

        return TryMapToWindowDip(point.X, point.Y, out dip);
    }

    private bool TryMapShot(ShotEvent shot, out PointD dip) => TryMapToWindowDip(shot.X, shot.Y, out dip);

    private bool TryMapToWindowDip(double normX, double normY, out PointD dip)
    {
        dip = default;
        if (_windowRectPx.Width <= 0 || _windowRectPx.Height <= 0)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (!GameWindowFinder.TryGetClientRect(hwnd, out _windowRectPx))
            {
                return false;
            }
        }

        var px = _runtime.Mapper.Map(normX, normY, _windowRectPx);
        var dpi = VisualTreeHelper.GetDpi(this);
        dip = new PointD(
            (px.X - _windowRectPx.X) / dpi.DpiScaleX,
            (px.Y - _windowRectPx.Y) / dpi.DpiScaleY);
        return true;
    }

    private void ShowCrosshair(PointD dip)
    {
        const double arm = CrosshairRadiusDip * 1.8;
        Canvas.SetLeft(_crosshairRing, dip.X - CrosshairRadiusDip);
        Canvas.SetTop(_crosshairRing, dip.Y - CrosshairRadiusDip);
        _crosshairH.X1 = dip.X - arm;
        _crosshairH.X2 = dip.X + arm;
        _crosshairH.Y1 = dip.Y;
        _crosshairH.Y2 = dip.Y;
        _crosshairV.X1 = dip.X;
        _crosshairV.X2 = dip.X;
        _crosshairV.Y1 = dip.Y - arm;
        _crosshairV.Y2 = dip.Y + arm;

        _crosshairRing.Visibility = Visibility.Visible;
        _crosshairH.Visibility = Visibility.Visible;
        _crosshairV.Visibility = Visibility.Visible;
    }

    private void HideCrosshair()
    {
        _crosshairRing.Visibility = Visibility.Collapsed;
        _crosshairH.Visibility = Visibility.Collapsed;
        _crosshairV.Visibility = Visibility.Collapsed;
    }

    private void UpdateScore() => TxtScore.Text = $"得分 {_score}";
}
