using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TvgunBridge.Core.Protocol;

/// <summary>
/// Answers tvgun LAN auto-discovery broadcasts. The phone broadcasts the ASCII text
/// <c>TVGUN_DISCOVER</c> to the discovery port (service port + 1); this responder
/// replies <c>TVGUN_HERE &lt;port&gt;</c> to the sender, which learns the PC address
/// from the reply's source address.
/// </summary>
public sealed class DiscoveryResponder : IDisposable
{
    /// <summary>Discovery request payload sent by the phone.</summary>
    public const string DiscoveryRequest = "TVGUN_DISCOVER";

    /// <summary>Discovery reply prefix; followed by a space and the service port.</summary>
    public const string DiscoveryReplyPrefix = "TVGUN_HERE";

    private readonly IPAddress _bindAddress;
    private readonly int _listenPort;
    private readonly int _servicePort;
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>
    /// Creates a responder.
    /// </summary>
    /// <param name="servicePort">The bridge service port reported in replies (tvgun default: 8000).</param>
    /// <param name="listenPort">UDP port to listen on. Defaults to <paramref name="servicePort"/> + 1.</param>
    /// <param name="bindAddress">Local address to bind. Defaults to <see cref="IPAddress.Any"/>.</param>
    public DiscoveryResponder(int servicePort, int? listenPort = null, IPAddress? bindAddress = null)
    {
        _servicePort = servicePort;
        _listenPort = listenPort ?? servicePort + 1;
        _bindAddress = bindAddress ?? IPAddress.Any;
    }

    /// <summary>
    /// Starts the background receive loop.
    /// </summary>
    /// <exception cref="InvalidOperationException">The responder is already running.</exception>
    public void Start()
    {
        if (_udp is not null)
        {
            throw new InvalidOperationException("The responder is already running.");
        }

        _udp = new UdpClient(new IPEndPoint(_bindAddress, _listenPort));
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
        _udp?.Dispose();
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
        var reply = Encoding.ASCII.GetBytes($"{DiscoveryReplyPrefix} {_servicePort}");
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

            if (Encoding.ASCII.GetString(result.Buffer).Trim() != DiscoveryRequest)
            {
                continue;
            }

            try
            {
                await udp.SendAsync(reply, result.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException)
            {
                // Unreachable sender; keep listening.
            }
        }
    }
}
