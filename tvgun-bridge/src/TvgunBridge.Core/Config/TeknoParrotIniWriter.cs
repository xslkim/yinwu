namespace TvgunBridge.Core.Config;

/// <summary>
/// Minimal INI writer for teknoparrot.ini files. It preserves all existing content
/// (sections, keys, comments, ordering) and only rewrites the requested section/key
/// values; missing keys are appended to their section, missing sections are appended at
/// the end. Before writing, the previous file content is backed up to a ".bak" file.
/// Section and key matching is case-insensitive.
/// </summary>
public static class TeknoParrotIniWriter
{
    /// <summary>
    /// Applies <paramref name="values"/> (section → key → value) to the INI file at
    /// <paramref name="path"/>. The file is created when missing; otherwise its previous
    /// content is copied to <paramref name="path"/>.bak first.
    /// </summary>
    public static void WriteValues(string path, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> values)
    {
        var lines = File.Exists(path)
            ? File.ReadAllLines(path).ToList()
            : new List<string>();

        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        var pending = values.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<string, string>(pair.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        string? currentSection = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                // Leaving a section: flush any keys that were not found in it, then
                // skip past the inserted lines so they are not re-examined.
                i += FlushMissingKeys(lines, i, currentSection, pending);
                currentSection = trimmed[1..^1];
                continue;
            }

            if (currentSection is null || !pending.TryGetValue(currentSection, out var sectionValues))
            {
                continue;
            }

            var equals = trimmed.IndexOf('=');
            if (equals <= 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            var key = trimmed[..equals].Trim();
            if (sectionValues.Remove(key, out var value))
            {
                lines[i] = $"{key}={value}";
            }

            if (sectionValues.Count == 0)
            {
                pending.Remove(currentSection);
            }
        }

        // End of file: flush keys missing from the last section.
        FlushMissingKeys(lines, lines.Count, currentSection, pending);

        // Sections that never existed are appended whole.
        foreach (var (section, sectionValues) in pending)
        {
            lines.Add($"[{section}]");
            foreach (var (key, value) in sectionValues)
            {
                lines.Add($"{key}={value}");
            }
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(path, lines);
    }

    /// <summary>
    /// Convenience wrapper for a single section (teknoparrot.ini only uses [General]
    /// for the bridge's purposes).
    /// </summary>
    public static void WriteValues(string path, string section, IReadOnlyDictionary<string, string> values) =>
        WriteValues(path, new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [section] = values,
        });

    private static int FlushMissingKeys(
        List<string> lines, int insertAt, string? section,
        Dictionary<string, Dictionary<string, string>> pending)
    {
        if (section is null || !pending.TryGetValue(section, out var sectionValues))
        {
            return 0;
        }

        var index = insertAt;
        foreach (var (key, value) in sectionValues)
        {
            lines.Insert(index, $"{key}={value}");
            index++;
        }

        pending.Remove(section);
        return sectionValues.Count;
    }
}
