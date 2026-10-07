using System.Net;

namespace TvgunBridge.Core.Protocol;

/// <summary>
/// High-level façade that composes the three tvgun protocol services on one port:
/// UDP aim datagrams, HTTP trigger/extension endpoints, and LAN auto-discovery
/// (service port + 1).
/// </summary>
public sealed class BridgeServer : IDisposable
{
    private readonly int _port;

    /// <summary>
    /// Creates a server. Nothing is bound until <see cref="Start"/> is called.
    /// </summary>
    /// <param name="port">Service port (tvgun default: 8000). Discovery listens on port + 1.</param>
    /// <param name="httpHost">HttpListener host segment. <c>"+"</c> for production,
    /// <c>"127.0.0.1"</c> for loopback-only use.</param>
    /// <param name="aimTimeout">Aim sample expiry. Defaults to 600 ms.</param>
    public BridgeServer(int port = 8000, string httpHost = "+", TimeSpan? aimTimeout = null)
    {
        _port = port;
        Aim = new AimSlot(aimTimeout);
        AimListener = new UdpAimListener(port);
        ShotServer = new ShotHttpServer(port, httpHost, Aim);
        Discovery = new DiscoveryResponder(port);
        AimListener.AimReceived += OnAimReceived;
    }

    /// <summary>Latest-aim slot fed by the UDP listener.</summary>
    public AimSlot Aim { get; }

    /// <summary>The UDP aim listener.</summary>
    public UdpAimListener AimListener { get; }

    /// <summary>The HTTP trigger/extension server.</summary>
    public ShotHttpServer ShotServer { get; }

    /// <summary>The discovery responder.</summary>
    public DiscoveryResponder Discovery { get; }

    /// <summary>Raised for every valid aim datagram.</summary>
    public event EventHandler<AimPoint>? AimReceived
    {
        add => AimListener.AimReceived += value;
        remove => AimListener.AimReceived -= value;
    }

    /// <summary>Raised for every valid <c>POST /shot</c>.</summary>
    public event EventHandler<ShotEvent>? ShotReceived
    {
        add => ShotServer.ShotReceived += value;
        remove => ShotServer.ShotReceived -= value;
    }

    /// <summary>Raised for <c>POST /coin</c>.</summary>
    public event EventHandler? CoinReceived
    {
        add => ShotServer.CoinReceived += value;
        remove => ShotServer.CoinReceived -= value;
    }

    /// <summary>Raised for <c>POST /start</c>.</summary>
    public event EventHandler? StartReceived
    {
        add => ShotServer.StartReceived += value;
        remove => ShotServer.StartReceived -= value;
    }

    /// <summary>Raised for <c>POST /reload</c>.</summary>
    public event EventHandler? ReloadReceived
    {
        add => ShotServer.ReloadReceived += value;
        remove => ShotServer.ReloadReceived -= value;
    }

    /// <summary>Binds all three services.</summary>
    public void Start()
    {
        AimListener.Start();
        ShotServer.Start();
        Discovery.Start();
    }

    /// <summary>Stops all services and releases the ports.</summary>
    public async Task StopAsync()
    {
        await AimListener.StopAsync().ConfigureAwait(false);
        await ShotServer.StopAsync().ConfigureAwait(false);
        await Discovery.StopAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        AimListener.AimReceived -= OnAimReceived;
        AimListener.Dispose();
        ShotServer.Dispose();
        Discovery.Dispose();
    }

    /// <summary>The configured service port.</summary>
    public int Port => _port;

    private void OnAimReceived(object? sender, AimPoint point) => Aim.Set(point.X, point.Y);
}
