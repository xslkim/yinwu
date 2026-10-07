using System.Net.Sockets;
using TvgunBridge.Core.Coordinates;
using TvgunBridge.Core.Injection;
using TvgunBridge.Core.Protocol;
using TvgunBridge.ReplayClient;

namespace TvgunBridge.Tests;

/// <summary>
/// Closed-loop chain with a non-identity affine calibration (a=0.9, b=50, c=1.1, d=-30)
/// and a non-origin target rect (100,50,1600,900): pushed normalized coordinates must
/// arrive at the injector as rect.X + (0.9*nx+50)/1920*1600, rect.Y + (1.1*ny-30)/1080*900.
/// </summary>
public class E2E_CalibratedChainTests
{
    private static readonly AffineCalibration Calibration = new(0.9, 50.0, 1.1, -30.0);
    private static readonly RectD Rect = new(100.0, 50.0, 1600.0, 900.0);

    private static (double X, double Y) Expected(double nx, double ny) =>
        (Rect.X + (Calibration.A * nx + Calibration.B) / CoordinateMapper.NormWidth * Rect.Width,
         Rect.Y + (Calibration.C * ny + Calibration.D) / CoordinateMapper.NormHeight * Rect.Height);

    private sealed class Harness : IDisposable
    {
        public readonly int Port;
        public readonly BridgeServer Server;
        public readonly RecordingInjector Recorder = new();
        public readonly InputPipeline Pipeline;
        public readonly UdpClient Udp = new();
        public readonly HttpClient Http = new();

        public Harness()
        {
            Server = E2EHelpers.StartBridgeServer();
            Port = Server.Port;
            Pipeline = new InputPipeline(Recorder, new CoordinateMapper(Calibration), Server.Aim, () => Rect);
            Pipeline.Attach(Server.ShotServer);
        }

        public void Dispose()
        {
            Pipeline.Dispose();
            Server.Dispose();
            Udp.Dispose();
            Http.Dispose();
        }
    }

    public static IEnumerable<object[]> AimPoints()
    {
        // Values survive the 1-decimal wire format exactly.
        yield return [1000.0, 500.0];
        yield return [0.0, 0.0];
        yield return [1920.0, 1080.0];
        yield return [640.5, 360.5];
    }

    [Theory]
    [MemberData(nameof(AimPoints))]
    public async Task CalibratedAimMapsToExpectedPixels(double nx, double ny)
    {
        using var h = new Harness();

        await PhoneSender.SendAimAsync(h.Udp, "127.0.0.1", h.Port, nx, ny);
        Assert.True(E2EHelpers.WaitFor(() =>
            h.Server.Aim.TryGet(out var aim) && aim.X == nx && aim.Y == ny));
        h.Pipeline.Tick();

        var move = Assert.Single(h.Recorder.Events);
        Assert.Equal(InjectionKind.Move, move.Kind);
        var (expectedX, expectedY) = Expected(nx, ny);
        Assert.True(Math.Abs(move.X - expectedX) <= 1.0, $"X: {move.X} != {expectedX}");
        Assert.True(Math.Abs(move.Y - expectedY) <= 1.0, $"Y: {move.Y} != {expectedY}");
    }

    [Fact]
    public async Task CalibratedShotMapsToExpectedPixels()
    {
        using var h = new Harness();

        var result = await PhoneSender.SendShotAsync(h.Http, "127.0.0.1", h.Port, 1000.0, 500.0);
        Assert.True(result.HitValid);
        Assert.True(E2EHelpers.WaitFor(() => h.Recorder.Events.Count >= 2));

        var pair = h.Recorder.Events.TakeLast(2).ToArray();
        Assert.Equal(InjectionKind.Move, pair[0].Kind);
        Assert.Equal(InjectionKind.Click, pair[1].Kind);
        var (expectedX, expectedY) = Expected(1000.0, 500.0);
        Assert.True(Math.Abs(pair[0].X - expectedX) <= 1.0, $"X: {pair[0].X} != {expectedX}");
        Assert.True(Math.Abs(pair[0].Y - expectedY) <= 1.0, $"Y: {pair[0].Y} != {expectedY}");
    }
}
