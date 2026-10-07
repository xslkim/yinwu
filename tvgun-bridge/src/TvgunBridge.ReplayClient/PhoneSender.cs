using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TvgunBridge.ReplayClient;

/// <summary>Outcome of one <c>POST /shot</c>.</summary>
/// <param name="Success">An HTTP response was received with a success status code.</param>
/// <param name="HitValid">The response body is valid JSON containing <c>"hit":true</c>.</param>
/// <param name="Body">Raw response body (or the error message when the request failed).</param>
/// <param name="StatusCode">HTTP status code, or 0 when no response was received.</param>
/// <param name="LatencyMs">Round-trip time in milliseconds.</param>
public readonly record struct ShotResult(bool Success, bool HitValid, string Body, int StatusCode, double LatencyMs);

/// <summary>
/// Protocol senders that mimic the tvgun Android phone app: UDP aim datagrams in ASCII
/// <c>"x,y"</c> form (one decimal place), <c>POST /shot</c> trigger JSON, and the
/// <c>TVGUN_DISCOVER</c> LAN auto-discovery exchange on the service port + 1. Exposed as
/// a public static class so the E2E test suite drives the bridge through the exact same
/// code path as the ReplayClient executable.
/// </summary>
public static class PhoneSender
{
    /// <summary>Discovery request payload, matching the phone app.</summary>
    public const string DiscoveryRequest = "TVGUN_DISCOVER";

    /// <summary>Expected prefix of a discovery reply (<c>TVGUN_HERE &lt;port&gt;</c>).</summary>
    public const string DiscoveryReplyPrefix = "TVGUN_HERE";

    /// <summary>Formats an aim datagram payload: <c>"x,y"</c> with one decimal place.</summary>
    public static string FormatAimPayload(double x, double y) =>
        string.Create(CultureInfo.InvariantCulture, $"{x:0.0},{y:0.0}");

    /// <summary>Formats a <c>POST /shot</c> JSON body: <c>{"x":123.4,"y":567.8}</c>.</summary>
    public static string FormatShotBody(double x, double y) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"x\":{x:0.0####},\"y\":{y:0.0####}}}");

    /// <summary>Sends one aim datagram to <paramref name="host"/>:<paramref name="port"/>.</summary>
    public static async Task<int> SendAimAsync(
        UdpClient udp, string host, int port, double x, double y,
        CancellationToken cancellationToken = default)
    {
        var payload = Encoding.ASCII.GetBytes(FormatAimPayload(x, y));
        return await udp.SendAsync(payload, new IPEndPoint(IPAddress.Parse(host), port), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sends one trigger pull via <c>POST /shot</c> and measures the round-trip latency.
    /// Transport failures are reported in the result instead of thrown.
    /// </summary>
    public static async Task<ShotResult> SendShotAsync(
        HttpClient http, string host, int port, double x, double y,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var content = new StringContent(FormatShotBody(x, y), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http
                .PostAsync($"http://{host}:{port}/shot", content, cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return new ShotResult(
                response.IsSuccessStatusCode, IsValidHitResponse(body), body,
                (int)response.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or SocketException)
        {
            stopwatch.Stop();
            return new ShotResult(false, false, ex.Message, 0, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>Checks whether a /shot response body is JSON containing <c>"hit":true</c>.</summary>
    public static bool IsValidHitResponse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("hit", out var hit)
                && hit.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sends <c>TVGUN_DISCOVER</c> to <paramref name="host"/> on the discovery port
    /// (<paramref name="servicePort"/> + 1) and waits up to <paramref name="timeout"/> for a
    /// <c>TVGUN_HERE &lt;port&gt;</c> reply. Returns the reply text, or null on timeout.
    /// </summary>
    public static async Task<string?> DiscoverAsync(
        string host, int servicePort, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        var request = Encoding.ASCII.GetBytes(DiscoveryRequest);
        await udp.SendAsync(request, new IPEndPoint(IPAddress.Parse(host), servicePort + 1), cancellationToken)
            .ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        while (!timeoutCts.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null; // discovery timeout
            }
            catch (SocketException)
            {
                // Windows delivers ICMP port-unreachable from a previous send as
                // WSAECONNRESET on the next receive; keep waiting until the timeout.
                continue;
            }

            var text = Encoding.ASCII.GetString(result.Buffer).Trim();
            if (text.StartsWith(DiscoveryReplyPrefix, StringComparison.Ordinal))
            {
                return text;
            }
        }

        return null;
    }
}
