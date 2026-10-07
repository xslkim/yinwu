using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.Tests;

public class AffineCalibrationTests
{
    [Fact]
    public void IdentityLeavesCoordinatesUntouched()
    {
        var identity = AffineCalibration.Identity;
        var result = identity.Transform(123.4, 567.8);
        Assert.Equal(123.4, result.X, 9);
        Assert.Equal(567.8, result.Y, 9);
    }

    [Fact]
    public void SolveRecoversKnownAffine()
    {
        // Ground truth: cx = 1.02*nx - 3, cy = 0.98*ny + 5.
        PointD Raw(double nx, double ny) => new((nx + 3) / 1.02, (ny - 5) / 0.98);

        var target1 = new PointD(200, 300);
        var target2 = new PointD(1700, 900);
        var calibration = AffineCalibration.Solve(Raw(200, 300), target1, Raw(1700, 900), target2);

        Assert.Equal(1.02, calibration.A, 6);
        Assert.Equal(-3, calibration.B, 6);
        Assert.Equal(0.98, calibration.C, 6);
        Assert.Equal(5, calibration.D, 6);
    }

    [Fact]
    public void SolvedCalibrationRoundTripsArbitraryPoints()
    {
        var calibration = AffineCalibration.Solve(
            new PointD(210, 310), new PointD(200, 300),
            new PointD(1690, 880), new PointD(1700, 900));

        var corrected = calibration.Transform(210, 310);
        Assert.Equal(200, corrected.X, 6);
        Assert.Equal(300, corrected.Y, 6);

        corrected = calibration.Transform(1690, 880);
        Assert.Equal(1700, corrected.X, 6);
        Assert.Equal(900, corrected.Y, 6);
    }

    [Fact]
    public void DegeneratePairsAreRejected()
    {
        var ok = AffineCalibration.TrySolve(
            new PointD(100, 100), new PointD(200, 200),
            new PointD(150, 800), new PointD(250, 900), // |dx| = 50 < 100
            out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Throws<ArgumentException>(() =>
            AffineCalibration.Solve(
                new PointD(100, 100), new PointD(200, 200),
                new PointD(150, 800), new PointD(250, 900)));
    }

    [Fact]
    public void JsonRoundTripPreservesCoefficients()
    {
        var calibration = new AffineCalibration(1.01, -2.5, 0.99, 4.25);
        var restored = AffineCalibration.FromJson(calibration.ToJson());
        Assert.Equal(calibration, restored);
    }

    [Fact]
    public void SaveAndLoadRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "calibration.json");
        try
        {
            var calibration = new AffineCalibration(1.02, -3, 0.98, 5);
            calibration.Save(path);
            Assert.Equal(calibration, AffineCalibration.Load(path));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void LoadMissingFileReturnsIdentity()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");
        Assert.Equal(AffineCalibration.Identity, AffineCalibration.Load(path));
    }

    [Fact]
    public void SolveFromScreenPointsMatchesNormalizedSolve()
    {
        var rect = new RectD(100, 50, 1920, 1080);
        // Target dots at rect center and bottom-right quadrant; raw reports are offset.
        var raw1 = new PointD(965, 545);
        var screen1 = new PointD(100 + 960, 50 + 540);   // true norm (960, 540)
        var raw2 = new PointD(1450, 815);
        var screen2 = new PointD(100 + 1440, 50 + 810);  // true norm (1440, 810)

        Assert.True(AffineCalibration.TrySolveFromScreenPoints(
            raw1, screen1, raw2, screen2, rect, out var calibration, out _));

        var corrected = calibration.Transform(raw1.X, raw1.Y);
        Assert.Equal(960, corrected.X, 6);
        Assert.Equal(540, corrected.Y, 6);
    }
}

public class CoordinateMapperTests
{
    private static readonly RectD Rect = new(100, 50, 960, 540);

    public static TheoryData<double, double, double, double> Corners => new()
    {
        { 0, 0, 100, 50 },
        { 1920, 0, 1060, 50 },
        { 0, 1080, 100, 590 },
        { 1920, 1080, 1060, 590 },
        { 960, 540, 580, 320 },
    };

    [Theory]
    [MemberData(nameof(Corners))]
    public void IdentityMappingHitsExpectedPixels(double nx, double ny, double px, double py)
    {
        var mapper = new CoordinateMapper();
        var result = mapper.Map(nx, ny, Rect);
        Assert.Equal(px, result.X, 6);
        Assert.Equal(py, result.Y, 6);
    }

    [Fact]
    public void OutOfFrameCoordinatesMapOutsideRect()
    {
        var mapper = new CoordinateMapper();
        var result = mapper.Map(-96, 1080 + 54, Rect); // 5% beyond each edge
        Assert.Equal(52, result.X, 6);
        Assert.Equal(617, result.Y, 6);
    }

    [Fact]
    public void CalibrationIsAppliedBeforeMapping()
    {
        var mapper = new CoordinateMapper(new AffineCalibration(1.0, 96, 1.0, -54));
        var result = mapper.Map(864, 594, Rect); // corrected to (960, 540) = center
        Assert.Equal(580, result.X, 6);
        Assert.Equal(320, result.Y, 6);
    }

    [Fact]
    public void MapInverseUndoesMap()
    {
        var mapper = new CoordinateMapper(new AffineCalibration(1.02, -3, 0.98, 5));
        var screen = mapper.Map(700, 400, Rect);
        var norm = mapper.MapInverse(screen.X, screen.Y, Rect);
        Assert.Equal(700, norm.X, 6);
        Assert.Equal(400, norm.Y, 6);
    }
}

public class VirtualDeskNormalizerTests
{
    private static VirtualDeskNormalizer Create() =>
        new(() => new VirtualScreenMetrics(-1920, -100, 5760, 2160));

    [Fact]
    public void ScreenOriginMapsToZero()
    {
        var n = Create();
        Assert.Equal((0, 0), n.ToAbsolute(-1920, -100));
    }

    [Fact]
    public void ScreenFarCornerMapsToFullScale()
    {
        var n = Create();
        var (ax, ay) = n.ToAbsolute(-1920 + 5760, -100 + 2160);
        Assert.Equal(65535, ax);
        Assert.Equal(65535, ay);
    }

    [Fact]
    public void CenterMapsToHalfScale()
    {
        var n = Create();
        var (ax, ay) = n.ToAbsolute(-1920 + 2880, -100 + 1080);
        Assert.Equal(32768, ax); // 32767.5 + 0.5 truncation compensation
        Assert.Equal(32768, ay);
    }

    [Fact]
    public void RoundTripIsStable()
    {
        var n = Create();
        foreach (var (sx, sy) in new[] { (-1920.0, -100.0), (0.0, 0.0), (1919.5, 979.25), (3839.0, 2059.0) })
        {
            var (ax, ay) = n.ToAbsolute(sx, sy);
            var back = n.FromAbsolute(ax, ay);
            var (ax2, ay2) = n.ToAbsolute(back.X, back.Y);
            Assert.Equal(ax, ax2);
            Assert.Equal(ay, ay2);
        }
    }

    [Fact]
    public void OutOfVirtualScreenIsClamped()
    {
        var n = Create();
        Assert.Equal((0, 0), n.ToAbsolute(-10000, -10000));
        Assert.Equal((65535, 65535), n.ToAbsolute(100000, 100000));
    }
}
