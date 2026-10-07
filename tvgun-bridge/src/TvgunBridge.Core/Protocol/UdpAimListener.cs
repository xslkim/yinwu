using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TvgunBridge.Core.Protocol;

/// <summary>
/// Receives tvgun aim datagrams on UDP. The phone streams ASCII
/// <c>"x,y"</c> packets (one decimal place, e.g. <c>"111.5,222.5"</c>) at ~120 Hz in
/// 1920x1080 normalized coordinates. Delivery is fire-and-forget: malformed packets are
/// silently discarded and packet loss is tolerated.
/// </summary>
public sealed class UdpAimListener : IDisposable
{
    private readonly IPAddress _bindAddress;
    private readonly int _port;
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>
    /// Creates a listener that will bind to <paramref name="bindAddress"/>:<paramref name="port"/>.
    /// </summary>
    /// <param name="port">UDP port to listen on (tvgun default: 8000).</param>
    /// <param name="bindAddress">Local address to bind. Defaults to <see cref="IPAddress.Any"/> (0.0.0.0).</param>
    public UdpAimListener(int port, IPAddress? bindAddress = null)
    {
        _port = port;
        _bindAddress = bindAddress ?? IPAddress.Any;
    }

    /// <summary>
    /// Raised on a thread-pool thread for every well-formed aim packet received.
    /// </summary>
    public event EventHandler<AimPoint>? AimReceived;

    /// <summary>
    /// Starts the background receive loop.
    /// </summary>
    /// <exception cref="InvalidOperationException">The listener is already running.</exception>
    public void Start()
    {
        if (_udp is not null)
        {
            throw new InvalidOperationException("The listener is already running.");
        }

        _udp = new UdpClient(new IPEndPoint(_bindAddress, _port));
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the receive loop and releases the socket.
    /// </summary>
    public async Task StopAsync()
    {
        var cts = _cts;
        var loop = _loop;
        _cts = null;
        _loop = null;
        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        _udp?.Dispose(); // unblocks ReceiveAsync with ObjectDisposedException
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _udp = null;
        cts.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _udp?.Dispose();
        _cts?.Dispose();
        _cts = null;
        _udp = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var udp = _udp!;
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            var text = Encoding.ASCII.GetString(result.Buffer);
            if (TryParseAim(text, out var x, out var y))
            {
                AimReceived?.Invoke(this, new AimPoint(x, y, DateTimeOffset.UtcNow));
            }
            // Malformed packets are dropped silently, as in the Python reference.
        }
    }

    /// <summary>
    /// Parses a tvgun aim datagram of the form <c>"x,y"</c>. Whitespace is trimmed;
    /// numbers use invariant-culture float parsing so any digit count is accepted.
    /// </summary>
    public static bool TryParseAim(string text, out double x, out double y)
    {
        x = 0;
        y = 0;
        var parts = text.Trim().Split(',');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
    }
}
