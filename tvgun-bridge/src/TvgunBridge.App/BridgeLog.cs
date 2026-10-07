using System.IO;

namespace TvgunBridge.App;

/// <summary>
/// Machine-readable event log, appended to %APPDATA%/TvgunBridge/bridge.log with a
/// local-time timestamp per line. Shared by GUI and autostart modes so closed-loop
/// tests can assert the same key events (SERVER_LISTENING, WINDOW_FOUND, ...) in
/// either mode. Logging is best-effort and never throws.
/// </summary>
public static class BridgeLog
{
    private static readonly object Gate = new();

    /// <summary>The log file path: %APPDATA%/TvgunBridge/bridge.log.</summary>
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TvgunBridge",
        "bridge.log");

    /// <summary>Appends one timestamped line, e.g. <c>SERVER_LISTENING port=8000</c>.</summary>
    public static void Event(string line)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Logging must never take the bridge down.
        }
    }
}
