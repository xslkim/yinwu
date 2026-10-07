using System.Net;
using System.Text;
using System.Text.Json;

namespace TvgunBridge.Core.Protocol;

/// <summary>
/// HTTP endpoint for tvgun trigger events and extension commands. The phone sends
/// <c>POST /shot</c> with a JSON body <c>{"x":123.4,"y":567.8}</c> on every trigger
/// press (press only, no release) and expects <c>{"hit":true,"score":0}</c> back for
/// its haptic feedback. Extension endpoints (<c>/coin</c>, <c>/start</c>,
/// <c>/reload</c>, <c>/exit</c>, <c>GET /health</c>) are not called by the tvgun phone app; they
/// exist for future phone-UI / web remotes and are protocol-compatible additions.
/// </summary>
/// <remarks>
/// Binding to a wildcard host (<c>+</c>) on Windows requires either elevation or a
/// URL ACL reservation (<c>netsh http add urlacl</c>). For loopback use (tests,
/// ReplayClient) bind to <c>127.0.0.1</c>.
/// </remarks>
public sealed class ShotHttpServer : IDisposable
{
    private static readonly byte[] ShotResponseBytes =
        Encoding.UTF8.GetBytes("""{"hit":true,"score":0}""");

    private static readonly byte[] OkResponseBytes =
        Encoding.UTF8.GetBytes("""{"ok":true}""");

    private readonly HttpListener _listener = new();
    private readonly AimSlot? _aimSlot;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>
    /// Creates a server on <c>http://{host}:{port}/</c>.
    /// </summary>
    /// <param name="port">TCP port to listen on (tvgun default: 8000).</param>
    /// <param name="host">HttpListener host segment. <c>"+"</c> (all interfaces) is the
    /// production default; use <c>"127.0.0.1"</c> for loopback-only scenarios.</param>
    /// <param name="aimSlot">Optional aim slot used to report <c>aimAge</c> in <c>GET /health</c>.</param>
    public ShotHttpServer(int port, string host = "+", AimSlot? aimSlot = null)
    {
        _listener.Prefixes.Add($"http://{host}:{port}/");
        _aimSlot = aimSlot;
    }

    /// <summary>Raised on a thread-pool thread for every valid <c>POST /shot</c>.</summary>
    public event EventHandler<ShotEvent>? ShotReceived;

    /// <summary>Raised for <c>POST /coin</c> (extension endpoint).</summary>
    public event EventHandler? CoinReceived;

    /// <summary>Raised for <c>POST /start</c> (extension endpoint).</summary>
    public event EventHandler? StartReceived;

    /// <summary>Raised for <c>POST /reload</c> (extension endpoint).</summary>
    public event EventHandler? ReloadReceived;

    /// <summary>Raised for <c>POST /exit</c> (extension endpoint; the phone's "exit game" button).</summary>
    public event EventHandler? ExitReceived;

    /// <summary>Starts accepting requests.</summary>
    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>Stops the server and releases the port.</summary>
    public async Task StopAsync()
    {
        var cts = _cts;
        var loop = _loop;
        _cts = null;
        _loop = null;
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

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

        cts?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        ((IDisposable)_listener).Dispose();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context), cancellationToken);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath ?? string.Empty;

            if (request.HttpMethod == "GET" && path == "/health")
            {
                var age = _aimSlot?.AgeMs;
                var body = age.HasValue
                    ? $$"""{"ok":true,"aimAge":{{age.Value:0}}}"""
                    : """{"ok":true,"aimAge":null}""";
                await WriteJsonAsync(context, HttpStatusCode.OK, Encoding.UTF8.GetBytes(body)).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod != "POST")
            {
                await WriteJsonAsync(context, HttpStatusCode.NotFound, "not found"u8.ToArray()).ConfigureAwait(false);
                return;
            }

            switch (path)
            {
                case "/shot":
                    await HandleShotAsync(context).ConfigureAwait(false);
                    return;
                case "/coin":
                    CoinReceived?.Invoke(this, EventArgs.Empty);
                    await WriteJsonAsync(context, HttpStatusCode.OK, OkResponseBytes).ConfigureAwait(false);
                    return;
                case "/start":
                    StartReceived?.Invoke(this, EventArgs.Empty);
                    await WriteJsonAsync(context, HttpStatusCode.OK, OkResponseBytes).ConfigureAwait(false);
                    return;
                case "/reload":
                    ReloadReceived?.Invoke(this, EventArgs.Empty);
                    await WriteJsonAsync(context, HttpStatusCode.OK, OkResponseBytes).ConfigureAwait(false);
                    return;
                case "/exit":
                    ExitReceived?.Invoke(this, EventArgs.Empty);
                    await WriteJsonAsync(context, HttpStatusCode.OK, OkResponseBytes).ConfigureAwait(false);
                    return;
                default:
                    await WriteJsonAsync(context, HttpStatusCode.NotFound, "not found"u8.ToArray()).ConfigureAwait(false);
                    return;
            }
        }
        catch (Exception)
        {
            try
            {
                context.Response.Abort();
            }
            catch (Exception)
            {
                // Client is already gone.
            }
        }
    }

    private async Task HandleShotAsync(HttpListenerContext context)
    {
        double x;
        double y;
        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.InputStream).ConfigureAwait(false);
            var root = document.RootElement;
            x = root.GetProperty("x").GetDouble();
            y = root.GetProperty("y").GetDouble();
        }
        catch (Exception)
        {
            var error = Encoding.UTF8.GetBytes("""{"error":"invalid JSON body, expected {\"x\": float, \"y\": float}"}""");
            await WriteJsonAsync(context, HttpStatusCode.BadRequest, error).ConfigureAwait(false);
            return;
        }

        ShotReceived?.Invoke(this, new ShotEvent(x, y, DateTimeOffset.UtcNow));
        await WriteJsonAsync(context, HttpStatusCode.OK, ShotResponseBytes).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, HttpStatusCode status, byte[] body)
    {
        var response = context.Response;
        response.StatusCode = (int)status;
        response.ContentType = "application/json";
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body).ConfigureAwait(false);
        response.OutputStream.Close();
    }
}
