using System.IO;
using System.Text.Json;

namespace TvgunBridge.App.Input;

/// <summary>
/// Hotkey and injected-key bindings, stored separately from the Core bridge config at
/// %APPDATA%/TvgunBridge/hotkeys.json so the App layer owns its UI-specific settings.
/// TeknoParrot defaults: coin = "5", start = "1"; local fallback hotkeys F5-F8.
/// </summary>
public sealed class HotkeySettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Key injected for a coin drop (TeknoParrot default "5").</summary>
    public string CoinKey { get; set; } = "5";

    /// <summary>Key injected for game start (TeknoParrot default "1").</summary>
    public string StartKey { get; set; } = "1";

    /// <summary>Local hotkey that injects <see cref="CoinKey"/>. Default F5.</summary>
    public string CoinHotkey { get; set; } = "F5";

    /// <summary>Local hotkey that injects <see cref="StartKey"/>. Default F6.</summary>
    public string StartHotkey { get; set; } = "F6";

    /// <summary>Local hotkey that toggles input injection. Default F7.</summary>
    public string ToggleInjectionHotkey { get; set; } = "F7";

    /// <summary>Local hotkey that shows/hides the control panel. Default F8.</summary>
    public string ToggleWindowHotkey { get; set; } = "F8";

    /// <summary>Default settings file path: %APPDATA%/TvgunBridge/hotkeys.json.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TvgunBridge",
        "hotkeys.json");

    /// <summary>Loads from a file; returns defaults when the file is missing or invalid.</summary>
    public static HotkeySettings Load(string? path = null)
    {
        var source = path ?? DefaultPath;
        if (!File.Exists(source))
        {
            return new HotkeySettings();
        }

        try
        {
            return JsonSerializer.Deserialize<HotkeySettings>(File.ReadAllText(source), JsonOptions)
                ?? new HotkeySettings();
        }
        catch (JsonException)
        {
            return new HotkeySettings();
        }
    }
}
