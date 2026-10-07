using System.Runtime.InteropServices;

namespace TvgunBridge.App;

/// <summary>
/// Best-effort console output for CLI errors and usage text. A WinExe has no console
/// of its own; when launched from one (e.g. by a launcher bat) we attach to the
/// parent console so the message is visible there. Otherwise the text still lands in
/// bridge.log.
/// </summary>
public static class ConsoleOutput
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint dwProcessId);

    /// <summary>Writes an error message to the parent console when one is available.</summary>
    public static void WriteError(string text)
    {
        try
        {
            if (AttachConsole(AttachParentProcess))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(text);
                Console.Error.Flush();
            }
        }
        catch (Exception)
        {
            // No console available; bridge.log carries the same information.
        }
    }
}
