using System.Text.Json;
using System.Text.Json.Serialization;

namespace TvgunBridge.Core.Config;

/// <summary>
/// Bridge configuration, mapped from a JSON file (default location:
/// %APPDATA%/TvgunBridge/config.json). All properties have complete defaults, so a
/// missing or empty file yields a working configuration.
/// </summary>
public sealed class BridgeConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Service port for UDP aim and HTTP trigger endpoints. Default 8000.</summary>
    public int Port { get; set; } = 8000;

    /// <summary>HttpListener host segment: "+" for all interfaces, "127.0.0.1" for loopback.</summary>
    public string HttpHost { get; set; } = "+";

    /// <summary>Hold time of synthesized trigger clicks, in milliseconds. Default 30.</summary>
    public int ClickHoldMs { get; set; } = 30;

    /// <summary>Aim sample expiry in milliseconds. Default 600.</summary>
    public int AimTimeoutMs { get; set; } = 600;

    /// <summary>Master switch for input injection. Default true.</summary>
    public bool EnableInjection { get; set; } = true;

    /// <summary>Window-title regexes used to locate the game window.</summary>
    public List<string> GameWindowTitlePatterns { get; set; } = new();

    /// <summary>Process names used to locate the game window.</summary>
    public List<string> ProcessNames { get; set; } = new();

    /// <summary>Id of the active game adapter (matches games/*.json), if any.</summary>
    public string? ActiveGameId { get; set; }

    /// <summary>Index of the screen whose work area the game window is stretched over. Default 0 (primary).</summary>
    public int ScreenIndex { get; set; }

    /// <summary>Default configuration file path: %APPDATA%/TvgunBridge/config.json.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TvgunBridge",
        "config.json");

    /// <summary>Serializes to JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Deserializes from JSON.</summary>
    public static BridgeConfig FromJson(string json) =>
        JsonSerializer.Deserialize<BridgeConfig>(json, JsonOptions) ?? new BridgeConfig();

    /// <summary>Loads from a file; returns defaults when the file is missing.</summary>
    public static BridgeConfig Load(string? path = null)
    {
        var source = path ?? DefaultPath;
        return File.Exists(source) ? FromJson(File.ReadAllText(source)) : new BridgeConfig();
    }

    /// <summary>Saves to a file, creating the directory when needed.</summary>
    public void Save(string? path = null)
    {
        var target = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(target, ToJson());
    }
}
