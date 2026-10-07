using System.Text.Json;
using System.Text.Json.Serialization;
using TvgunBridge.Core.Injection;

namespace TvgunBridge.Core.Config;

/// <summary>How a reload is performed for a game.</summary>
public enum ReloadStrategy
{
    /// <summary>No reload action (e.g. automatic or magazine-less games).</summary>
    None,

    /// <summary>Right mouse click (e.g. pump-action in Big Buck Hunter Pro).</summary>
    RightClick,

    /// <summary>Fire a shot off-screen.</summary>
    OffscreenShot,
}

/// <summary>
/// Per-game adaptation description, loaded from a JSON file (games/*.json).
/// </summary>
public sealed class GameAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Stable identifier (file name without extension).</summary>
    public required string Id { get; set; }

    /// <summary>Human-readable game name.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Free-form documentation (executable names, TeknoParrot profile findings, etc.).</summary>
    public string? Notes { get; set; }

    /// <summary>Regex matched against the game window title.</summary>
    public string? WindowTitleRegex { get; set; }

    /// <summary>Process names (with or without ".exe") of the game.</summary>
    public List<string> ProcessNames { get; set; } = new();

    /// <summary>Mouse button used as the trigger. Default Left.</summary>
    public MouseButton TriggerButton { get; set; } = MouseButton.Left;

    /// <summary>Reload behavior. Default None.</summary>
    public ReloadStrategy ReloadStrategy { get; set; } = ReloadStrategy.None;

    /// <summary>Optional per-game click hold time override, in milliseconds.</summary>
    public int? ClickHoldMs { get; set; }

    /// <summary>Absolute path of the game's teknoparrot.ini, if managed by the bridge.</summary>
    public string? TeknoParrotIniPath { get; set; }

    /// <summary>
    /// INI values to enforce before launch: section → key → value. The bridge writes
    /// <c>[General] Windowed=1, HideCursor=1, Input API=RawInput</c> for managed games.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> TeknoParrotIniValues { get; set; } = new();

    /// <summary>Deserializes one adapter from JSON.</summary>
    /// <exception cref="InvalidOperationException">The JSON is not a valid adapter.</exception>
    public static GameAdapter FromJson(string json) =>
        JsonSerializer.Deserialize<GameAdapter>(json, JsonOptions)
        ?? throw new InvalidOperationException("Invalid game adapter JSON.");

    /// <summary>Loads one adapter from a file.</summary>
    public static GameAdapter Load(string path) => FromJson(File.ReadAllText(path));

    /// <summary>
    /// Loads all adapters from a directory (every *.json file), sorted by id.
    /// Missing directory yields an empty list.
    /// </summary>
    public static IReadOnlyList<GameAdapter> LoadAll(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<GameAdapter>();
        }

        return Directory.EnumerateFiles(directory, "*.json")
            .Select(Load)
            .OrderBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
