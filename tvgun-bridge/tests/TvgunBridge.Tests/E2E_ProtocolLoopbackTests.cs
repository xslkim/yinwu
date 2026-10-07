using System.Net;
using System.Net.Sockets;
using TvgunBridge.Core.Protocol;
using TvgunBridge.ReplayClient;

namespace TvgunBridge.Tests;

/// <summary>
/// Shared helpers for the E2E suite: consecutive free ports (BridgeServer needs the
/// service port for UDP+TCP and port+1 for UDP discovery) and bounded spin waits for
/// loopback network delivery.
/// </summary>
internal static class E2EHelpers
{
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Finds a port whose UDP/TCP service sockets and UDP discovery socket (port+1) are all free.</summary>
    public static int GetFreePortPair()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var port = TestPorts.GetFreePort();
            if (port < 65535 && IsUdpFree(port) && IsUdpFree(port + 1))
            {
                return port;
            }
        }

        throw new InvalidOperationException("Could not find a free consecutive port pair.");
    }

    /// <summary>Spins until <paramref name="condition"/> holds; returns false on timeout.</summary>
    public static bool WaitFor(Func<bool> condition, TimeSpan? timeout = null) =>
        SpinWait.SpinUntil(condition, timeout ?? NetworkTimeout);

    /// <summary>
    /// Creates and starts a BridgeServer on a free port pair, retrying with a fresh port
    /// when a parallel test wins the probe-then-bind race.
    /// </summary>
    public static BridgeServer StartBridgeServer(TimeSpan? aimTimeout = null)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var server = new BridgeServer(GetFreePortPair(), httpHost: "127.0.0.1", aimTimeout: aimTimeout);
            try
            {
                server.Start();
                return server;
            }
            catch (Exception ex) when (ex is SocketException or HttpListenerException)
            {
                server.Dispose();
            }
        }

        throw new InvalidOperationException("Could not bind a BridgeServer after 20 port attempts.");
    }

    private static bool IsUdpFree(int port)
    {
        try
        {
            using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

/// <summary>
/// Protocol-level loopback tests: BridgeServer on a random loopback port driven by the
/// ReplayClient wire format (same code path as the virtual phone).
/// </summary>
public class E2E_ProtocolLoopbackTests
{
    [Fact]
    public async Task UdpAimPacketFillsAimSlot()
    {
        using var server = E2EHelpers.StartBridgeServer();
        var port = server.Port;
        using var udp = new UdpClient();

        await PhoneSender.SendAimAsync(udp, "127.0.0.1", port, 123.4, 567.8);

        Assert.True(E2EHelpers.WaitFor(() => server.Aim.TryGet(out _)), "aim slot was not filled within the timeout");
        Assert.True(server.Aim.TryGet(out var aim));
        Assert.Equal(123.4, aim.X, 3);
        Assert.Equal(567.8, aim.Y, 3);
    }

    [Fact]
    public async Task ShotPostRaisesEventAndReturnsHitJson()
    {
        using var server = E2EHelpers.StartBridgeServer();
        var port = server.Port;
        ShotEvent? received = null;
        server.ShotServer.ShotReceived += (_, shot) => received = shot;
        using var http = new HttpClient();

        var result = await PhoneSender.SendShotAsync(http, "127.0.0.1", port, 123.4, 567.8);

        Assert.True(result.Success, $"shot request failed: HTTP {result.StatusCode} {result.Body}");
        Assert.Equal("""{"hit":true,"score":0}""", result.Body);
        Assert.True(result.HitValid);
        Assert.True(E2EHelpers.WaitFor(() => received is not null), "ShotReceived was not raised");
        Assert.Equal(123.4, received!.Value.X, 3);
        Assert.Equal(567.8, received.Value.Y, 3);
    }

    [Fact]
    public async Task DiscoveryBroadcastIsAnswered()
    {
        using var server = E2EHelpers.StartBridgeServer();
        var port = server.Port;

        var reply = await PhoneSender.DiscoverAsync("127.0.0.1", port, TimeSpan.FromSeconds(2));

        Assert.Equal($"TVGUN_HERE {port}", reply);
    }
}
