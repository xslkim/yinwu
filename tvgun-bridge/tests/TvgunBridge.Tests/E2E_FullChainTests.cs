using System.Net.Sockets;
using TvgunBridge.Core.Coordinates;
using TvgunBridge.Core.Injection;
using TvgunBridge.Core.Protocol;
using TvgunBridge.ReplayClient;

namespace TvgunBridge.Tests;

/// <summary>
/// Core closed-loop chain: BridgeServer (real UDP/HTTP loopback) → AimSlot/ShotReceived →
/// InputPipeline (driven deterministically via Tick) → RecordingInjector. Identity
/// calibration with the target rect at (0,0,1920,1080), so normalized coordinates map
/// 1:1 to screen pixels.
/// </summary>
public class E2E_FullChainTests
{
    private static readonly RectD Rect = new(0, 0, 1920, 1080);

    /// <summary>
    /// Expands <see cref="IInputInjector.Click"/> into ButtonDown/ButtonUp calls on the
    /// inner injector, mirroring what SendInputInjector does on the OS (down → hold → up),
    /// so the test can assert press/release ordering at the injector boundary.
    /// </summary>
    private sealed class DownUpExpandingInjector : IInputInjector
    {
        private readonly RecordingInjector _inner = new();

        public IReadOnlyList<RecordedInputEvent> Events => _inner.Events;

        public void MoveAbsolute(double screenX, double screenY) => _inner.MoveAbsolute(screenX, screenY);

        public void ButtonDown(MouseButton button) => _inner.ButtonDown(button);

        public void ButtonUp(MouseButton button) => _inner.ButtonUp(button);

        public void Click(MouseButton button, int holdMs)
        {
            _inner.ButtonDown(button);
            _inner.ButtonUp(button);
        }

        public void KeyTap(VirtualKey key) => _inner.KeyTap(key);
    }

    private sealed class Harness : IDisposable
    {
        private static readonly RectD TargetRect = Rect;

        public readonly int Port;
        public readonly BridgeServer Server;
        public readonly RecordingInjector Recorder;
        public readonly InputPipeline Pipeline;
        public readonly UdpClient Udp = new();
        public readonly HttpClient Http = new();

        public Harness(TimeSpan? aimTimeout = null)
        {
            Server = E2EHelpers.StartBridgeServer(aimTimeout);
            Port = Server.Port;
            Recorder = new RecordingInjector();
            Pipeline = new InputPipeline(
                Recorder, new CoordinateMapper(AffineCalibration.Identity), Server.Aim, () => TargetRect);
            Pipeline.ClickHoldMs = 30;
            Pipeline.Attach(Server.ShotServer);
        }

        /// <summary>Sends one aim datagram and waits until the server's aim slot holds exactly it.</summary>
        public async Task AimAsync(double x, double y)
        {
            await PhoneSender.SendAimAsync(Udp, "127.0.0.1", Port, x, y);
            Assert.True(
                E2EHelpers.WaitFor(() =>
                    Server.Aim.TryGet(out var aim) && aim.X == x && aim.Y == y),
                $"aim slot never received ({x},{y})");
        }

        /// <summary>Sends one shot and waits until the pipeline has injected for it.</summary>
        public async Task<ShotResult> ShotAsync(double x, double y, Func<bool> injected)
        {
            var result = await PhoneSender.SendShotAsync(Http, "127.0.0.1", Port, x, y);
            Assert.True(result.HitValid, $"shot response invalid: HTTP {result.StatusCode} {result.Body}");
            Assert.True(E2EHelpers.WaitFor(injected), "pipeline did not inject for the shot");
            return result;
        }

        public void Dispose()
        {
            Pipeline.Dispose();
            Server.Dispose();
            Udp.Dispose();
            Http.Dispose();
        }
    }

    [Fact]
    public async Task GridTrajectoryMovesMatchIdentityMappingPointByPoint()
    {
        using var h = new Harness();

        // First 10 points of the ReplayClient "grid" pattern (row-major, 480x270 spacing).
        var points = Enumerable.Range(0, 10)
            .Select(i => Patterns.GridPoint(i % 5, i / 5))
            .ToArray();

        var moveCount = 0;
        foreach (var (x, y) in points)
        {
            await h.AimAsync(x, y);
            h.Pipeline.Tick();

            var moves = h.Recorder.Events.Where(e => e.Kind == InjectionKind.Move).ToArray();
            Assert.Equal(++moveCount, moves.Length);
            // Identity calibration into rect (0,0,1920,1080): pixels == normalized coords.
            Assert.True(Math.Abs(moves[^1].X - x) <= 1.0, $"move X {moves[^1].X} != {x}");
            Assert.True(Math.Abs(moves[^1].Y - y) <= 1.0, $"move Y {moves[^1].Y} != {y}");
        }
    }

    [Fact]
    public async Task ShotsProduceMoveThenClickInOrder()
    {
        using var h = new Harness();
        var shotPoints = new[] { (960.0, 540.0), (480.0, 270.0), (1440.0, 810.0) };

        var expectedEvents = 0;
        foreach (var (x, y) in shotPoints)
        {
            expectedEvents += 2;
            await h.ShotAsync(x, y, () => h.Recorder.Events.Count >= expectedEvents);

            var pair = h.Recorder.Events.TakeLast(2).ToArray();
            Assert.Equal(InjectionKind.Move, pair[0].Kind);
            Assert.True(Math.Abs(pair[0].X - x) <= 1.0 && Math.Abs(pair[0].Y - y) <= 1.0);
            // RecordingInjector records Click as one event; per the IInputInjector
            // contract a Click is a serialized button down → hold → button up.
            Assert.Equal(InjectionKind.Click, pair[1].Kind);
            Assert.Equal(MouseButton.Left, pair[1].Button);
            Assert.Equal(30, pair[1].HoldMs);
        }
    }

    [Fact]
    public async Task ShotClickExpandsToButtonDownBeforeButtonUp()
    {
        using var server = E2EHelpers.StartBridgeServer();
        var injector = new DownUpExpandingInjector();
        using var pipeline = new InputPipeline(
            injector, new CoordinateMapper(AffineCalibration.Identity), server.Aim, () => Rect);
        pipeline.Attach(server.ShotServer);
        using var http = new HttpClient();

        var result = await PhoneSender.SendShotAsync(http, "127.0.0.1", server.Port, 640.0, 360.0);
        Assert.True(result.HitValid);
        Assert.True(E2EHelpers.WaitFor(() => injector.Events.Count >= 3));

        var kinds = injector.Events.TakeLast(3).Select(e => e.Kind).ToArray();
        Assert.Equal(
            new[] { InjectionKind.Move, InjectionKind.ButtonDown, InjectionKind.ButtonUp },
            kinds);
        var events = injector.Events.TakeLast(3).ToArray();
        Assert.Equal(MouseButton.Left, events[1].Button);
        Assert.Equal(MouseButton.Left, events[2].Button);
        Assert.True(events[1].Timestamp <= events[2].Timestamp, "button down must precede button up");
    }

    [Fact]
    public async Task AimTimeoutStopsMovementInjection()
    {
        // Short aim expiry so the test does not wait a full 0.6s: after 0.3s of silence
        // (2x the 150ms timeout) the sample is guaranteed expired.
        using var h = new Harness(aimTimeout: TimeSpan.FromMilliseconds(150));

        await h.AimAsync(500.0, 500.0);
        h.Pipeline.Tick();
        Assert.Single(h.Recorder.Events);

        await Task.Delay(300); // deterministic: sample is certainly expired now
        h.Pipeline.Tick();
        h.Pipeline.Tick();

        Assert.Single(h.Recorder.Events); // no new moves after aim stopped
    }

    [Fact]
    public async Task DisabledPipelineInjectsNothing()
    {
        using var h = new Harness();
        h.Pipeline.Enabled = false;

        await h.AimAsync(500.0, 500.0);
        h.Pipeline.Tick();
        Assert.Empty(h.Recorder.Events);

        // ShotReceived fires synchronously before the HTTP response is written, so by
        // the time SendShotAsync returns the (disabled) handler has already run.
        var result = await PhoneSender.SendShotAsync(h.Http, "127.0.0.1", h.Port, 500.0, 500.0);
        Assert.True(result.HitValid);
        Assert.Empty(h.Recorder.Events);
    }
}
