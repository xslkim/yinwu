using TvgunBridge.Core.Coordinates;
using TvgunBridge.Core.Injection;
using TvgunBridge.Core.Protocol;

namespace TvgunBridge.Tests;

public class InputPipelineTests
{
    private static readonly RectD Rect = new(0, 0, 1920, 1080); // identity mapping

    private sealed class Harness : IDisposable
    {
        public DateTimeOffset Now;
        public readonly AimSlot Slot;
        public readonly RecordingInjector Recorder;
        public readonly InputPipeline Pipeline;

        public Harness()
        {
            Now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Slot = new AimSlot(clock: () => Now);
            Recorder = new RecordingInjector(clock: () => Now);
            Pipeline = new InputPipeline(Recorder, new CoordinateMapper(), Slot, () => Rect);
        }

        public void Advance(TimeSpan delta) => Now += delta;

        public void Dispose() => Pipeline.Dispose();
    }

    [Fact]
    public void AimSequenceProducesMappedMoves()
    {
        using var h = new Harness();
        h.Slot.Set(0, 0);
        h.Pipeline.Tick();
        h.Slot.Set(960, 540);
        h.Pipeline.Tick();
        h.Slot.Set(1920, 1080);
        h.Pipeline.Tick();

        var moves = h.Recorder.Events.Where(e => e.Kind == InjectionKind.Move).ToArray();
        Assert.Equal(3, moves.Length);
        Assert.Equal((0.0, 0.0), (moves[0].X, moves[0].Y));
        Assert.Equal((960.0, 540.0), (moves[1].X, moves[1].Y));
        Assert.Equal((1920.0, 1080.0), (moves[2].X, moves[2].Y));
    }

    [Fact]
    public void ShotProducesMoveThenClick()
    {
        using var h = new Harness();
        h.Pipeline.HandleShot(new ShotEvent(480, 270, h.Now));

        var events = h.Recorder.Events;
        Assert.Equal(2, events.Count);
        Assert.Equal(InjectionKind.Move, events[0].Kind);
        Assert.Equal((480.0, 270.0), (events[0].X, events[0].Y));
        Assert.Equal(InjectionKind.Click, events[1].Kind);
        Assert.Equal(MouseButton.Left, events[1].Button);
        Assert.Equal(30, events[1].HoldMs);
    }

    [Fact]
    public void ExpiredAimStopsMovement()
    {
        using var h = new Harness();
        h.Slot.Set(100, 100);
        h.Pipeline.Tick();
        Assert.Single(h.Recorder.Events);

        h.Advance(TimeSpan.FromMilliseconds(700));
        h.Pipeline.Tick();
        h.Pipeline.Tick();
        Assert.Single(h.Recorder.Events); // no further moves after timeout
    }

    [Fact]
    public void DisabledPipelineInjectsNothing()
    {
        using var h = new Harness();
        h.Pipeline.Enabled = false;
        h.Slot.Set(100, 100);
        h.Pipeline.Tick();
        h.Pipeline.HandleShot(new ShotEvent(100, 100, h.Now));
        Assert.Empty(h.Recorder.Events);
    }

    [Fact]
    public void ClickHoldMsIsConfigurable()
    {
        using var h = new Harness();
        h.Pipeline.ClickHoldMs = 80;
        h.Pipeline.HandleShot(new ShotEvent(10, 10, h.Now));
        Assert.Equal(80, h.Recorder.Events[^1].HoldMs);
    }

    [Fact]
    public async Task TimerDrivenMovesWork()
    {
        using var h = new Harness();
        h.Pipeline.MoveRateHz = 200;
        h.Pipeline.Start();
        h.Slot.Set(640, 360);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (h.Recorder.Events.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.Contains(h.Recorder.Events, e => e.Kind == InjectionKind.Move);
        await h.Pipeline.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
