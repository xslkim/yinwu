using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;

namespace TvgunBridge.ReplayClient;

/// <summary>
/// tvgun replay client: a "virtual phone" that drives the bridge over the real wire
/// protocol (UDP aim, HTTP /shot, LAN discovery) for closed-loop verification without
/// a handset. Exit codes: 0 = success (every shot got a valid {"hit":true,...} reply),
/// 1 = failure/usage error, 2 = discovery timed out.
/// </summary>
public static class Program
{
    private const string Usage = """
        tvgun-bridge ReplayClient - virtual phone for closed-loop bridge testing.

        Usage:
          TvgunBridge.ReplayClient [options]

        Options:
          --host <ip>          Bridge host (default 127.0.0.1).
          --port <n>           Bridge service port (default 8000; discovery uses port+1).
          --script <csv>       Replay a CSV script: lines of 'tMs,type,x,y', type aim|shot.
          --pattern <name>     Built-in trajectory: grid|circle|jitter|edges.
          --duration <s>       Pattern duration in seconds (default 10).
          --rate <hz>          Aim datagram rate (default 120).
          --shots <n>          Pattern mode: number of shots, spread evenly (default 0).
          --shot-at <x,y>      Pattern mode: shot position (default 960,540).
          --discover           Only run auto-discovery (2s timeout, exit code 2 on silence).
          --help               Show this text.

        Exit codes: 0 = all shots answered with {"hit":true,...}; 1 = failure; 2 = discovery timeout.
        """;

    /// <summary>Application entry point.</summary>
    public static async Task<int> Main(string[] args)
    {
        string host = "127.0.0.1";
        var port = 8000;
        string? scriptPath = null;
        string? pattern = null;
        var duration = 10.0;
        var rate = 120.0;
        var shots = 0;
        (double X, double Y) shotAt = (960.0, 540.0);
        var discoverOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new UsageException($"Missing value for {args[i]}.");
            try
            {
                switch (args[i])
                {
                    case "--host": host = Next(); break;
                    case "--port": port = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--script": scriptPath = Next(); break;
                    case "--pattern": pattern = Next().ToLowerInvariant(); break;
                    case "--duration": duration = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--rate": rate = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--shots": shots = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--shot-at": shotAt = ParsePoint(Next()); break;
                    case "--discover": discoverOnly = true; break;
                    case "--help" or "-h":
                        Console.WriteLine(Usage);
                        return 0;
                    default:
                        throw new UsageException($"Unknown argument '{args[i]}'.");
                }
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine($"Invalid argument: {ex.Message}");
                return 1;
            }
            catch (UsageException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        if (discoverOnly)
        {
            return await RunDiscoveryAsync(host, port).ConfigureAwait(false);
        }

        IReadOnlyList<ScriptEntry> entries;
        try
        {
            if (scriptPath is not null)
            {
                entries = ReplayScript.Load(scriptPath);
            }
            else if (pattern is not null)
            {
                entries = Patterns.Generate(pattern, duration, rate, shots, shotAt);
            }
            else
            {
                Console.Error.WriteLine("Nothing to do: pass --script, --pattern, or --discover.");
                Console.Error.WriteLine(Usage);
                return 1;
            }
        }
        catch (Exception ex) when (ex is IOException or FormatException or ArgumentException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        return await RunReplayAsync(host, port, entries).ConfigureAwait(false);
    }

    private static async Task<int> RunDiscoveryAsync(string host, int port)
    {
        Console.WriteLine($"Discovering bridge at {host}:{port + 1} (service port {port})...");
        var reply = await PhoneSender.DiscoverAsync(host, port, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        if (reply is null)
        {
            Console.WriteLine("No discovery reply within 2s.");
            return 2;
        }

        Console.WriteLine($"Reply: {reply}");
        return 0;
    }

    private static async Task<int> RunReplayAsync(string host, int port, IReadOnlyList<ScriptEntry> entries)
    {
        using var udp = new UdpClient();
        using var http = new HttpClient();
        var aimsSent = 0;
        var shotsSent = 0;
        var shotsOk = 0;
        var latencies = new List<double>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var entry in entries)
        {
            var waitMs = entry.TMs - stopwatch.ElapsedMilliseconds;
            if (waitMs > 0)
            {
                await Task.Delay((int)waitMs).ConfigureAwait(false);
            }

            if (entry.Kind == ScriptEntryKind.Aim)
            {
                await PhoneSender.SendAimAsync(udp, host, port, entry.X, entry.Y).ConfigureAwait(false);
                aimsSent++;
            }
            else
            {
                var result = await PhoneSender.SendShotAsync(http, host, port, entry.X, entry.Y).ConfigureAwait(false);
                shotsSent++;
                latencies.Add(result.LatencyMs);
                if (result.HitValid)
                {
                    shotsOk++;
                    Console.WriteLine($"[{stopwatch.ElapsedMilliseconds,6} ms] shot ({entry.X:0.0},{entry.Y:0.0}) -> {result.Body} ({result.LatencyMs:0.0} ms)");
                }
                else
                {
                    Console.WriteLine($"[{stopwatch.ElapsedMilliseconds,6} ms] shot ({entry.X:0.0},{entry.Y:0.0}) FAILED: HTTP {result.StatusCode} {result.Body}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- Replay statistics ---");
        Console.WriteLine($"aim datagrams sent : {aimsSent} (UDP fire-and-forget; loss not tracked)");
        Console.WriteLine($"shots sent         : {shotsSent}");
        Console.WriteLine($"shots hit-valid    : {shotsOk}");
        if (latencies.Count > 0)
        {
            latencies.Sort();
            Console.WriteLine($"shot latency p50   : {Percentile(latencies, 0.50):0.0} ms");
            Console.WriteLine($"shot latency p95   : {Percentile(latencies, 0.95):0.0} ms");
        }

        return shotsSent == shotsOk ? 0 : 1;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p) =>
        sorted[Math.Min((int)Math.Ceiling(p * sorted.Count) - 1, sorted.Count - 1)];

    private static (double X, double Y) ParsePoint(string text)
    {
        var parts = text.Split(',');
        if (parts.Length == 2
            && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return (x, y);
        }

        throw new UsageException($"Invalid point '{text}'. Expected 'x,y', e.g. '960,540'.");
    }

    private sealed class UsageException(string message) : Exception(message);
}
