using System.Collections.Concurrent;
using System.Diagnostics;
using TvgunBridge.Core.Protocol;

namespace TvgunBridge.Tests;

/// <summary>
/// Runs the ReplayClient as a real process (dotnet exec on the built dll copied next to
/// the test assembly) against a loopback BridgeServer, validating discovery, script
/// replay, pattern replay, and exit codes end to end.
/// </summary>
public class E2E_ReplayClientTests
{
    private static readonly string ClientDll =
        Path.Combine(AppContext.BaseDirectory, "TvgunBridge.ReplayClient.dll");

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunClientAsync(
        string arguments, TimeSpan timeout)
    {
        Assert.True(File.Exists(ClientDll), $"ReplayClient dll not found at {ClientDll}");
        var startInfo = new ProcessStartInfo("dotnet", $"exec \"{ClientDll}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"ReplayClient did not exit within {timeout}. stdout: {await stdout} stderr: {await stderr}");
        }

        return (process.ExitCode, await stdout, await stderr);
    }

    [Fact]
    public async Task DiscoverModeFindsBridgeServer()
    {
        using var server = E2EHelpers.StartBridgeServer();
        var port = server.Port;

        var (exitCode, stdout, stderr) = await RunClientAsync(
            $"--discover --host 127.0.0.1 --port {port}", TimeSpan.FromSeconds(15));

        Assert.True(exitCode == 0, $"exit {exitCode}; stdout: {stdout}; stderr: {stderr}");
        Assert.Contains($"TVGUN_HERE {port}", stdout);
    }

    [Fact]
    public async Task DiscoverModeTimesOutWithExitCode2()
    {
        var port = E2EHelpers.GetFreePortPair(); // nothing listens here

        var (exitCode, stdout, _) = await RunClientAsync(
            $"--discover --host 127.0.0.1 --port {port}", TimeSpan.FromSeconds(15));

        Assert.Equal(2, exitCode);
        Assert.Contains("No discovery reply", stdout);
    }

    [Fact]
    public async Task ScriptModeReplaysAimsAndShotsEndToEnd()
    {
        using var server = E2EHelpers.StartBridgeServer();
        var port = server.Port;
        var aims = new ConcurrentBag<(double X, double Y)>();
        var shots = new ConcurrentBag<(double X, double Y)>();
        server.AimReceived += (_, aim) => aims.Add((aim.X, aim.Y));
        server.ShotReceived += (_, shot) => shots.Add((shot.X, shot.Y));

        var scriptPath = Path.Combine(Path.GetTempPath(), $"tvgun-e2e-{Guid.NewGuid():N}.csv");
        await File.WriteAllLinesAsync(scriptPath,
        [
            "# tMs,type,x,y",
            "0,aim,100.0,100.0",
            "50,aim,200.0,200.0",
            "100,aim,300.0,300.0",
            "150,shot,960.0,540.0",
            "250,shot,480.0,270.0",
        ]);
        try
        {
            var (exitCode, stdout, stderr) = await RunClientAsync(
                $"--script \"{scriptPath}\" --host 127.0.0.1 --port {port}", TimeSpan.FromSeconds(20));

            Assert.True(exitCode == 0, $"exit {exitCode}; stdout: {stdout}; stderr: {stderr}");
            Assert.Contains("aim datagrams sent : 3", stdout);
            Assert.Contains("shots sent         : 2", stdout);
            Assert.Contains("shots hit-valid    : 2", stdout);
            Assert.Contains("shot latency p50", stdout);

            Assert.True(E2EHelpers.WaitFor(() => shots.Count >= 2));
            Assert.Contains((960.0, 540.0), shots);
            Assert.Contains((480.0, 270.0), shots);
            Assert.True(E2EHelpers.WaitFor(() => aims.Count >= 3));
            Assert.Contains((300.0, 300.0), aims);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public async Task ScriptModeFailsWithExitCode1WhenShotsAreNotAnswered()
    {
        var port = E2EHelpers.GetFreePortPair(); // nothing listens: shots cannot be answered
        var scriptPath = Path.Combine(Path.GetTempPath(), $"tvgun-e2e-{Guid.NewGuid():N}.csv");
        await File.WriteAllLinesAsync(scriptPath, ["0,shot,960.0,540.0"]);
        try
        {
            var (exitCode, _, _) = await RunClientAsync(
                $"--script \"{scriptPath}\" --host 127.0.0.1 --port {port}", TimeSpan.FromSeconds(30));

            Assert.Equal(1, exitCode);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public async Task PatternModeStreamsAimAtRateAndFiresShots()
    {
        using var server = E2EHelpers.StartBridgeServer();
        var port = server.Port;
        var aims = 0;
        var shots = new ConcurrentBag<(double X, double Y)>();
        server.AimReceived += (_, _) => Interlocked.Increment(ref aims);
        server.ShotReceived += (_, shot) => shots.Add((shot.X, shot.Y));

        // 1s of circle at 60 Hz with one shot at the center.
        var (exitCode, stdout, stderr) = await RunClientAsync(
            "--pattern circle --duration 1 --rate 60 --shots 1 --shot-at 960,540 " +
            $"--host 127.0.0.1 --port {port}",
            TimeSpan.FromSeconds(20));

        Assert.True(exitCode == 0, $"exit {exitCode}; stdout: {stdout}; stderr: {stderr}");
        Assert.Contains("aim datagrams sent : 60", stdout);
        Assert.True(E2EHelpers.WaitFor(() => shots.Count >= 1));
        Assert.Contains((960.0, 540.0), shots);
        // UDP loss on loopback is unlikely; require the bulk of the stream to arrive.
        Assert.True(E2EHelpers.WaitFor(() => Volatile.Read(ref aims) >= 50),
            $"only {Volatile.Read(ref aims)}/60 aim datagrams arrived");
    }
}
