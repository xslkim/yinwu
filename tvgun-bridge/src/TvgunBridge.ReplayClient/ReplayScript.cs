using System.Globalization;

namespace TvgunBridge.ReplayClient;

/// <summary>Kind of a scripted replay event.</summary>
public enum ScriptEntryKind
{
    /// <summary>UDP aim sample.</summary>
    Aim,

    /// <summary>HTTP trigger pull (<c>POST /shot</c>).</summary>
    Shot,
}

/// <summary>One timed replay event: at <paramref name="TMs"/> milliseconds, send <paramref name="Kind"/> at (X, Y).</summary>
public readonly record struct ScriptEntry(int TMs, ScriptEntryKind Kind, double X, double Y);

/// <summary>
/// Loads replay scripts in CSV form: <c>tMs,type,x,y</c> with type <c>aim</c> or
/// <c>shot</c> (case-insensitive). Blank lines and lines starting with <c>#</c> are
/// ignored; numbers are parsed invariant-culture. Entries are returned sorted by time.
/// </summary>
public static class ReplayScript
{
    /// <summary>Loads and parses a script file.</summary>
    /// <exception cref="FormatException">A line does not match the expected CSV shape.</exception>
    public static IReadOnlyList<ScriptEntry> Load(string path) => Parse(File.ReadLines(path));

    /// <summary>Parses script lines.</summary>
    /// <exception cref="FormatException">A line does not match the expected CSV shape.</exception>
    public static IReadOnlyList<ScriptEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<ScriptEntry>();
        var lineNo = 0;
        foreach (var raw in lines)
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length != 4
                || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tMs)
                || !Enum.TryParse(parts[1].Trim(), ignoreCase: true, out ScriptEntryKind kind)
                || !double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                throw new FormatException(
                    $"Invalid script line {lineNo}: '{line}'. Expected 'tMs,type,x,y' with type aim|shot.");
            }

            entries.Add(new ScriptEntry(tMs, kind, x, y));
        }

        entries.Sort(static (a, b) => a.TMs.CompareTo(b.TMs));
        return entries;
    }
}
