using System.Net;
using System.Net.Sockets;
using System.Text;
using TvgunBridge.Core.Protocol;

namespace TvgunBridge.Tests;

public class AimParsingTests
{
    [Theory]
    [InlineData("111.5,222.5", 111.5, 222.5)]
    [InlineData("0,0", 0, 0)]
    [InlineData("1920.0,1080.0", 1920.0, 1080.0)]
    [InlineData(" 12.5 , 34.5 ", 12.5, 34.5)]
    [InlineData("123,456.789", 123, 456.789)]
    public void ValidAimTextParses(string text, double expectedX, double expectedY)
    {
        Assert.True(UdpAimListener.TryParseAim(text, out var x, out var y));
        Assert.Equal(expectedX, x, 6);
        Assert.Equal(expectedY, y, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("1,")]
    [InlineData(",2")]
    [InlineData("x,y")]
    [InlineData("1.2.3,4")]
    public void MalformedAimTextIsRejected(string text)
    {
        Assert.False(UdpAimListener.TryParseAim(text, out _, out _));
    }
}

public class AimSlotTests
{
    [Fact]
    public void EmptySlotHasNoValue()
    {
        var slot = new AimSlot();
        Assert.False(slot.HasValue);
        Assert.False(slot.TryGet(out _));
        Assert.Throws<InvalidOperationException>(() => slot.Value);
    }

    [Fact]
    public void LatestWriteWins()
    {
        var now = DateTimeOffset.UtcNow;
        var slot = new AimSlot(clock: () => now);
        slot.Set(1, 2);
        slot.Set(100.5, 200.5);

        Assert.True(slot.TryGet(out var point));
        Assert.Equal(100.5, point.X);
        Assert.Equal(200.5, point.Y);
    }

    [Fact]
    public void SampleExpiresAfterTimeout()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var slot = new AimSlot(TimeSpan.FromMilliseconds(600), () => now);
        slot.Set(10, 20);

        now = now.AddMilliseconds(599);
        Assert.True(slot.TryGet(out _));

        now = now.AddMilliseconds(2);
        Assert.False(slot.HasValue);
        Assert.False(slot.TryGet(out _));
    }

    [Fact]
    public void RefreshResetsExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var slot = new AimSlot(TimeSpan.FromMilliseconds(600), () => now);
        slot.Set(1, 1);
        now = now.AddMilliseconds(500);
        slot.Set(2, 2);
        now = now.AddMilliseconds(500);
        Assert.True(slot.TryGet(out var point));
        Assert.Equal(2, point.X);
    }

    [Fact]
    public void AgeMsReflectsClock()
    {
        var now = DateTimeOffset.UtcNow;
        var slot = new AimSlot(clock: () => now);
        Assert.Null(slot.AgeMs);
        slot.Set(1, 1);
        now = now.AddMilliseconds(250);
        Assert.Equal(250, slot.AgeMs);
    }
}

public class UdpAimListenerTests
{
    [Fact]
    public async Task WellFormedPacketsRaiseEventsInOrder()
    {
        var port = TestPorts.GetFreePort();
        using var listener = new UdpAimListener(port, IPAddress.Loopback);
        var received = new List<AimPoint>();
        var allReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.AimReceived += (_, point) =>
        {
            lock (received)
            {
                received.Add(point);
                if (received.Count == 3)
                {
                    allReceived.TrySetResult();
                }
            }
        };
        listener.Start();

        using var sender = new UdpClient();
        var target = new IPEndPoint(IPAddress.Loopback, port);
        foreach (var text in new[] { "1.0,2.0", "3.5,4.5", "100.0,200.0" })
        {
            await sender.SendAsync(Encoding.ASCII.GetBytes(text), target);
        }

        var completed = await Task.WhenAny(allReceived.Task, Task.Delay(5000));
        Assert.Same(allReceived.Task, completed);
        lock (received)
        {
            Assert.Equal(3, received.Count);
            Assert.Equal((1.0, 2.0), (received[0].X, received[0].Y));
            Assert.Equal((3.5, 4.5), (received[1].X, received[1].Y));
            Assert.Equal((100.0, 200.0), (received[2].X, received[2].Y));
        }

        await listener.StopAsync();
    }

    [Fact]
    public async Task MalformedPacketsAreSilentlyDropped()
    {
        var port = TestPorts.GetFreePort();
        using var listener = new UdpAimListener(port, IPAddress.Loopback);
        var received = new TaskCompletionSource<AimPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.AimReceived += (_, point) => received.TrySetResult(point);
        listener.Start();

        using var sender = new UdpClient();
        var target = new IPEndPoint(IPAddress.Loopback, port);
        await sender.SendAsync("garbage"u8.ToArray(), target);
        await sender.SendAsync("1,2,3"u8.ToArray(), target);
        await sender.SendAsync(",,"u8.ToArray(), target);
        await sender.SendAsync(Encoding.ASCII.GetBytes("42.5,84.5"), target);

        var completed = await Task.WhenAny(received.Task, Task.Delay(5000));
        Assert.Same(received.Task, completed);
        var point = await received.Task;
        Assert.Equal((42.5, 84.5), (point.X, point.Y));

        await listener.StopAsync();
    }
}

public class DiscoveryResponderTests
{
    [Fact]
    public async Task DiscoverBroadcastGetsHereReply()
    {
        var servicePort = TestPorts.GetFreePort();
        var listenPort = TestPorts.GetFreePort();
        using var responder = new DiscoveryResponder(servicePort, listenPort, IPAddress.Loopback);
        responder.Start();

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await client.SendAsync(
            Encoding.ASCII.GetBytes(DiscoveryResponder.DiscoveryRequest),
            new IPEndPoint(IPAddress.Loopback, listenPort));

        var received = await client.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var reply = Encoding.ASCII.GetString(received.Buffer);
        Assert.Equal($"TVGUN_HERE {servicePort}", reply);

        await responder.StopAsync();
    }

    [Fact]
    public async Task UnrelatedPacketsGetNoReply()
    {
        var servicePort = TestPorts.GetFreePort();
        var listenPort = TestPorts.GetFreePort();
        using var responder = new DiscoveryResponder(servicePort, listenPort, IPAddress.Loopback);
        responder.Start();

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await client.SendAsync("SOMETHING_ELSE"u8.ToArray(), new IPEndPoint(IPAddress.Loopback, listenPort));

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.ReceiveAsync().WaitAsync(TimeSpan.FromMilliseconds(500)));

        await responder.StopAsync();
    }
}
