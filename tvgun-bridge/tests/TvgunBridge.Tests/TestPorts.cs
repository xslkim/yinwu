using System.Net;
using System.Net.Sockets;

namespace TvgunBridge.Tests;

internal static class TestPorts
{
    public static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
