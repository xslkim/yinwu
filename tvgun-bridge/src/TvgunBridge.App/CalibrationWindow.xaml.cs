using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using TvgunBridge.Core.Coordinates;
using TvgunBridge.Core.Protocol;
using TvgunBridge.Core.Windowing;

namespace TvgunBridge.App;

/// <summary>
/// Two-point calibration wizard (task T1.4 UI): shows a target ring at 15%/15% then
/// at 85%/85% of the screen; the user aims with the phone and pulls the trigger at
/// each. The raw normalized shot coordinates plus the targets' screen-pixel positions
/// are fed to <see cref="AffineCalibration.TrySolveFromScreenPoints"/>; a successful
/// solution is applied to the runtime and persisted. ESC cancels, R restarts.
/// </summary>
public partial class CalibrationWindow : Window
{
    private static readonly (double Fx, double Fy)[] TargetFractions = { (0.15, 0.15), (0.85, 0.85) };

    private readonly BridgeRuntime _runtime;
    private readonly List<PointD> _rawPoints = new();
    private readonly List<PointD> _screenPoints = new();
    private RectD _windowRectPx;
    private bool _done;

    /// <summary>Creates the wizard bound to the runtime's shot feed and mapper.</summary>
    public CalibrationWindow(BridgeRuntime runtime)
    {
        _runtime = runtime;
        InitializeComponent();
        _runtime.ShotFeed += OnShotFeed;
        SizeChanged += (_, _) => ShowCurrentTarget();
        Closed += (_, _) => _runtime.ShotFeed -= OnShotFeed;
        UpdateInstruction();
    }

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        _ = GameWindowFinder.TryGetClientRect(hwnd, out _windowRectPx);
        ShowCurrentTarget();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.R && _done)
        {
            Restart();
        }
    }

    private void OnRetryClick(object sender, RoutedEventArgs e) => Restart();

    private void Restart()
    {
        _rawPoints.Clear();
        _screenPoints.Clear();
        _done = false;
        TxtResult.Text = string.Empty;
        BtnRetry.Visibility = Visibility.Collapsed;
        UpdateInstruction();
        ShowCurrentTarget();
    }

    private void OnShotFeed(object? sender, ShotEvent shot)
    {
        Dispatcher.BeginInvoke(() => HandleShot(shot));
    }

    private void HandleShot(ShotEvent shot)
    {
        if (_done || _rawPoints.Count >= TargetFractions.Length)
        {
            return;
        }

        if (_windowRectPx.Width <= 0 || _windowRectPx.Height <= 0)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (!GameWindowFinder.TryGetClientRect(hwnd, out _windowRectPx))
            {
                TxtResult.Text = "无法读取窗口位置，请重试。";
                return;
            }
        }

        var targetDip = CurrentTargetDip();
        var dpi = VisualTreeHelper.GetDpi(this);
        _rawPoints.Add(new PointD(shot.X, shot.Y));
        _screenPoints.Add(new PointD(
            _windowRectPx.X + targetDip.X * dpi.DpiScaleX,
            _windowRectPx.Y + targetDip.Y * dpi.DpiScaleY));

        if (_rawPoints.Count < TargetFractions.Length)
        {
            UpdateInstruction();
            ShowCurrentTarget();
            return;
        }

        Finish();
    }

    private void Finish()
    {
        _done = true;
        TargetCanvas.Children.Clear();
        var ok = AffineCalibration.TrySolveFromScreenPoints(
            _rawPoints[0], _screenPoints[0],
            _rawPoints[1], _screenPoints[1],
            _windowRectPx,
            out var calibration, out var error);

        if (ok)
        {
            try
            {
                _runtime.ApplyCalibration(calibration);
                TxtInstruction.Text = "校准完成，已保存（ESC 关闭）";
                TxtResult.Text =
                    $"A={calibration.A:0.####}  B={calibration.B:0.##}  C={calibration.C:0.####}  D={calibration.D:0.##}\n" +
                    $"原始点1=({_rawPoints[0].X:0.#},{_rawPoints[0].Y:0.#})  原始点2=({_rawPoints[1].X:0.#},{_rawPoints[1].Y:0.#})";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TxtInstruction.Text = "校准解算成功但保存失败";
                TxtResult.Text = ex.Message;
            }
        }
        else
        {
            TxtInstruction.Text = "校准失败";
            TxtResult.Text = error;
        }

        BtnRetry.Visibility = Visibility.Visible;
    }

    private void UpdateInstruction() =>
        TxtInstruction.Text =
            $"第 {_rawPoints.Count + 1}/2 点：用手机瞄准{(_rawPoints.Count == 0 ? "左上" : "右下")}靶心并开枪（ESC 取消）";

    private PointD CurrentTargetDip()
    {
        var (fx, fy) = TargetFractions[Math.Min(_rawPoints.Count, TargetFractions.Length - 1)];
        return new PointD(ActualWidth * fx, ActualHeight * fy);
    }

    private void ShowCurrentTarget()
    {
        TargetCanvas.Children.Clear();
        if (_done || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var center = CurrentTargetDip();
        const double ringRadius = 40.0;
        const double crossArm = 60.0;

        var ring = new Ellipse
        {
            Width = ringRadius * 2,
            Height = ringRadius * 2,
            Stroke = Brushes.White,
            StrokeThickness = 3,
        };
        Canvas.SetLeft(ring, center.X - ringRadius);
        Canvas.SetTop(ring, center.Y - ringRadius);
        TargetCanvas.Children.Add(ring);

        AddCrossLine(center.X - crossArm, center.Y, center.X + crossArm, center.Y);
        AddCrossLine(center.X, center.Y - crossArm, center.X, center.Y + crossArm);
    }

    private void AddCrossLine(double x1, double y1, double x2, double y2)
    {
        TargetCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = Brushes.White,
            StrokeThickness = 2,
        });
    }
}
