using System.Text.Json;

namespace TvgunBridge.Core.Coordinates;

/// <summary>
/// Two-point affine calibration with translation and anisotropic scaling, expressed in
/// the tvgun normalized coordinate space (1920x1080). The user pulls the trigger while
/// aiming at two on-screen targets with known normalized positions; the reported raw
/// coordinates are then corrected as
/// <c>cx = A*nx + B, cy = C*ny + D</c>. With no calibration data the transform is the
/// identity, i.e. raw coordinates are used as-is.
/// </summary>
public readonly record struct AffineCalibration(double A, double B, double C, double D)
{
    /// <summary>
    /// Minimum horizontal/vertical span (in normalized units) between the two
    /// calibration points. Pairs closer than this cannot produce a stable solution.
    /// </summary>
    public const double MinSpan = 100.0;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Identity transform (no correction).</summary>
    public static AffineCalibration Identity => new(1.0, 0.0, 1.0, 0.0);

    /// <summary>Default calibration file path: %APPDATA%/TvgunBridge/calibration.json.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TvgunBridge",
        "calibration.json");

    /// <summary>
    /// Solves the affine coefficients from two raw→target point correspondences.
    /// </summary>
    /// <param name="raw1">Raw normalized coordinates reported at target 1.</param>
    /// <param name="target1">True normalized coordinates of target 1.</param>
    /// <param name="raw2">Raw normalized coordinates reported at target 2.</param>
    /// <param name="target2">True normalized coordinates of target 2.</param>
    /// <param name="calibration">The solved calibration on success.</param>
    /// <param name="error">Rejection reason on failure.</param>
    /// <returns>False when the two raw points span less than <see cref="MinSpan"/>
    /// normalized units on either axis (degenerate input).</returns>
    public static bool TrySolve(
        PointD raw1, PointD target1, PointD raw2, PointD target2,
        out AffineCalibration calibration, out string? error)
    {
        var spanX = raw2.X - raw1.X;
        var spanY = raw2.Y - raw1.Y;
        if (Math.Abs(spanX) < MinSpan || Math.Abs(spanY) < MinSpan)
        {
            calibration = Identity;
            error = $"Calibration points must span at least {MinSpan:0} normalized units on both axes " +
                    $"(got |dx|={Math.Abs(spanX):0.#}, |dy|={Math.Abs(spanY):0.#}).";
            return false;
        }

        var a = (target2.X - target1.X) / spanX;
        var b = target1.X - a * raw1.X;
        var c = (target2.Y - target1.Y) / spanY;
        var d = target1.Y - c * raw1.Y;
        calibration = new AffineCalibration(a, b, c, d);
        error = null;
        return true;
    }

    /// <summary>
    /// Solves the affine coefficients from two correspondences.
    /// </summary>
    /// <exception cref="ArgumentException">The point pair is degenerate; see <see cref="TrySolve"/>.</exception>
    public static AffineCalibration Solve(PointD raw1, PointD target1, PointD raw2, PointD target2)
    {
        if (!TrySolve(raw1, target1, raw2, target2, out var calibration, out var error))
        {
            throw new ArgumentException(error);
        }

        return calibration;
    }

    /// <summary>
    /// Convenience overload of <see cref="TrySolve(PointD, PointD, PointD, PointD, out AffineCalibration, out string?)"/>
    /// that takes the target points in screen pixels and converts them to normalized
    /// coordinates against the given target rectangle (the game client area).
    /// </summary>
    public static bool TrySolveFromScreenPoints(
        PointD raw1, PointD screen1, PointD raw2, PointD screen2, RectD targetRect,
        out AffineCalibration calibration, out string? error)
    {
        var target1 = new PointD(
            (screen1.X - targetRect.X) / targetRect.Width * CoordinateMapper.NormWidth,
            (screen1.Y - targetRect.Y) / targetRect.Height * CoordinateMapper.NormHeight);
        var target2 = new PointD(
            (screen2.X - targetRect.X) / targetRect.Width * CoordinateMapper.NormWidth,
            (screen2.Y - targetRect.Y) / targetRect.Height * CoordinateMapper.NormHeight);
        return TrySolve(raw1, target1, raw2, target2, out calibration, out error);
    }

    /// <summary>Applies the transform: <c>cx = A*nx + B, cy = C*ny + D</c>.</summary>
    public PointD Transform(double nx, double ny) => new(A * nx + B, C * ny + D);

    /// <summary>Serializes to JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Deserializes from JSON.</summary>
    public static AffineCalibration FromJson(string json) =>
        JsonSerializer.Deserialize<AffineCalibration>(json, JsonOptions);

    /// <summary>Saves to a JSON file, creating the directory when needed.</summary>
    public void Save(string? path = null)
    {
        var target = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(target, ToJson());
    }

    /// <summary>Loads from a JSON file; returns <see cref="Identity"/> when the file is missing.</summary>
    public static AffineCalibration Load(string? path = null)
    {
        var source = path ?? DefaultPath;
        return File.Exists(source) ? FromJson(File.ReadAllText(source)) : Identity;
    }
}
