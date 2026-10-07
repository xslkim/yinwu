using System.Net;
using System.Text;
using TvgunBridge.Core.Protocol;

namespace TvgunBridge.Tests;

public class ShotHttpServerTests
{
    private static async Task<(ShotHttpServer Server, HttpClient Client, string Base)> StartAsync(AimSlot? aimSlot = null)
    {
        var port = TestPorts.GetFreePort();
        var server = new ShotHttpServer(port, "127.0.0.1", aimSlot);
        server.Start();
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        await Task.CompletedTask;
        return (server, client, $"http://127.0.0.1:{port}");
    }

    [Fact]
    public async Task ShotReturnsHitTrueAndRaisesEvent()
    {
        var (server, client, _) = await StartAsync();
        using var s = server;
        using var c = client;
        var shotReceived = new TaskCompletionSource<ShotEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ShotReceived += (_, shot) => shotReceived.TrySetResult(shot);

        var response = await client.PostAsync(
            "/shot",
            new StringContent("""{"x":123.4,"y":567.8}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("""{"hit":true,"score":0}""", body);

        var shot = await shotReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(123.4, shot.X, 6);
        Assert.Equal(567.8, shot.Y, 6);
    }

    [Fact]
    public async Task ShotAcceptsVaryingDigitCounts()
    {
        var (server, client, _) = await StartAsync();
        using var s = server;
        using var c = client;
        var shotReceived = new TaskCompletionSource<ShotEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ShotReceived += (_, shot) => shotReceived.TrySetResult(shot);

        var response = await client.PostAsync(
            "/shot",
            new StringContent("""{"x":960,"y":540.125}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shot = await shotReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(960, shot.X);
        Assert.Equal(540.125, shot.Y, 6);
    }

    [Fact]
    public async Task MalformedShotBodyGets400()
    {
        var (server, client, _) = await StartAsync();
        using var s = server;
        using var c = client;

        var response = await client.PostAsync(
            "/shot",
            new StringContent("""{"foo":1}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownPathGets404()
    {
        var (server, client, _) = await StartAsync();
        using var s = server;
        using var c = client;

        var post = await client.PostAsync("/nope", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);

        var get = await client.GetAsync("/shot");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task ExtensionEndpointsRaiseEventsAndReturnOk()
    {
        var (server, client, _) = await StartAsync();
        using var s = server;
        using var c = client;
        var coin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.CoinReceived += (_, _) => coin.TrySetResult();
        server.StartReceived += (_, _) => start.TrySetResult();
        server.ReloadReceived += (_, _) => reload.TrySetResult();

        Assert.Equal("""{"ok":true}""", await (await client.PostAsync("/coin", new StringContent(""))).Content.ReadAsStringAsync());
        Assert.Equal("""{"ok":true}""", await (await client.PostAsync("/start", new StringContent(""))).Content.ReadAsStringAsync());
        Assert.Equal("""{"ok":true}""", await (await client.PostAsync("/reload", new StringContent(""))).Content.ReadAsStringAsync());

        await coin.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await start.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reload.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExitPostRaisesEventAndReturnsOk()
    {
        var (server, client, _) = await StartAsync();
        using var s = server;
        using var c = client;
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ExitReceived += (_, _) => exit.TrySetResult();

        var response = await client.PostAsync("/exit", new StringContent(""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"ok":true}""", await response.Content.ReadAsStringAsync());
        await exit.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task HealthReportsAimAge()
    {
        var now = DateTimeOffset.UtcNow;
        var slot = new AimSlot(clock: () => now);
        slot.Set(10, 20);
        now = now.AddMilliseconds(123);

        var (server, client, _) = await StartAsync(slot);
        using var s = server;
        using var c = client;

        var body = await client.GetStringAsync("/health");
        Assert.Equal("""{"ok":true,"aimAge":123}""", body);
    }

    [Fact]
    public async Task HealthWithoutAimReportsNullAge()
    {
        var slot = new AimSlot();
        var (server, client, _) = await StartAsync(slot);
        using var s = server;
        using var c = client;

        var body = await client.GetStringAsync("/health");
        Assert.Equal("""{"ok":true,"aimAge":null}""", body);
    }
}
