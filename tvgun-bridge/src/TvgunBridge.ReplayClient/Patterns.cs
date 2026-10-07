namespace TvgunBridge.ReplayClient;

/// <summary>
/// Built-in trajectory generators producing deterministic replay timelines in the
/// 1920x1080 normalized coordinate space:
/// <list type="bullet">
/// <item><c>grid</c>: 5x5 lattice, dwelling 0.8 s per point (20 s cycle, looped).</item>
/// <item><c>circle</c>: radius 400 around the center, two uniform laps over the duration.</item>
/// <item><c>jitter</c>: center point plus Gaussian noise (sigma 2 px, clamped to ±6 px,
/// fixed seed) simulating stationary-hand noise.</item>
/// <item><c>edges</c>: the four corners and four edge midpoints, dwelled evenly.</item>
/// </list>
/// Aim entries are emitted at the configured rate; shots are spread evenly over the
/// duration at a fixed position.
/// </summary>
public static class Patterns
{
    /// <summary>Names of the supported patterns.</summary>
    public static readonly IReadOnlyList<string> Names = ["grid", "circle", "jitter", "edges"];

    private const double NormWidth = 1920.0;
    private const double NormHeight = 1080.0;
    private const double CenterX = NormWidth / 2.0;
    private const double CenterY = NormHeight / 2.0;
    private const int JitterSeed = 42;

    private static readonly (double X, double Y)[] EdgePoints =
    [
        (0, 0), (CenterX, 0), (NormWidth, 0), (NormWidth, CenterY),
        (NormWidth, NormHeight), (CenterX, NormHeight), (0, NormHeight), (0, CenterY),
    ];

    /// <summary>
    /// Generates a replay timeline for <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">One of <see cref="Names"/>.</param>
    /// <param name="durationSeconds">Total replay duration.</param>
    /// <param name="rateHz">Aim datagram rate.</param>
    /// <param name="shotCount">Number of trigger pulls, spread evenly over the duration.</param>
    /// <param name="shotAt">Normalized position of every shot.</param>
    /// <exception cref="ArgumentException">Unknown pattern name or non-positive duration/rate.</exception>
    public static IReadOnlyList<ScriptEntry> Generate(
        string pattern, double durationSeconds, double rateHz, int shotCount, (double X, double Y) shotAt)
    {
        if (!Names.Contains(pattern, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unknown pattern '{pattern}'. Expected one of: {string.Join(", ", Names)}.");
        }

        if (durationSeconds <= 0 || rateHz <= 0)
        {
            throw new ArgumentException("Duration and rate must be positive.");
        }

        var durationMs = durationSeconds * 1000.0;
        var aimPeriodMs = 1000.0 / rateHz;
        var aimCount = (int)Math.Round(durationSeconds * rateHz);
        var entries = new List<ScriptEntry>();
        var random = new Random(JitterSeed);

        for (var i = 0; i < aimCount; i++)
        {
            var t = i * aimPeriodMs;
            var (x, y) = PositionAt(pattern, t, durationMs, random);
            entries.Add(new ScriptEntry((int)t, ScriptEntryKind.Aim, x, y));
        }

        for (var i = 0; i < shotCount; i++)
        {
            var t = durationMs * (i + 1) / (shotCount + 1);
            entries.Add(new ScriptEntry((int)t, ScriptEntryKind.Shot, shotAt.X, shotAt.Y));
        }

        entries.Sort(static (a, b) => a.TMs.CompareTo(b.TMs));
        return entries;
    }

    /// <summary>Grid point (column, row) of the 5x5 lattice: spacing 480 x 270.</summary>
    public static (double X, double Y) GridPoint(int column, int row) => (column * 480.0, row * 270.0);

    private static (double X, double Y) PositionAt(string pattern, double tMs, double durationMs, Random random) =>
        pattern switch
        {
            "grid" => GridPosition(tMs),
            "circle" => CirclePosition(tMs, durationMs),
            "jitter" => JitterPosition(random),
            "edges" => EdgePosition(tMs, durationMs),
            _ => throw new ArgumentException($"Unknown pattern '{pattern}'."),
        };

    private static (double X, double Y) GridPosition(double tMs)
    {
        const double dwellMs = 800.0;
        var index = (int)(tMs / dwellMs) % 25;
        return GridPoint(index % 5, index / 5);
    }

    private static (double X, double Y) CirclePosition(double tMs, double durationMs)
    {
        var angle = 4.0 * Math.PI * tMs / durationMs; // two laps
        return (CenterX + 400.0 * Math.Cos(angle), CenterY + 400.0 * Math.Sin(angle));
    }

    private static (double X, double Y) JitterPosition(Random random) =>
        (CenterX + NextGaussian(random, sigma: 2.0), CenterY + NextGaussian(random, sigma: 2.0));

    private static (double X, double Y) EdgePosition(double tMs, double durationMs)
    {
        var dwellMs = durationMs / EdgePoints.Length;
        var index = Math.Min((int)(tMs / dwellMs), EdgePoints.Length - 1);
        return EdgePoints[index];
    }

    private static double NextGaussian(Random random, double sigma)
    {
        // Box-Muller, clamped to ±3 sigma so the crosshair never leaves the frame.
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        var sample = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2) * sigma;
        return Math.Clamp(sample, -3.0 * sigma, 3.0 * sigma);
    }
}
