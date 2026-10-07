using CoreVirtualKey = TvgunBridge.Core.Injection.VirtualKey;

namespace TvgunBridge.App.Input;

/// <summary>
/// Parses human-readable key names ("5", "F5", "ESC", "A") into Win32 virtual-key
/// codes and converts them to the Core <see cref="CoreVirtualKey"/> enum used by the
/// injector. Pure logic; no UI dependencies.
/// </summary>
public static class KeyNameParser
{
    /// <summary>
    /// Parses a key name into a Win32 virtual-key code. Supports single letters and
    /// digits, F1-F12, and the names ESC/ESCAPE, ENTER/RETURN, SPACE, TAB.
    /// </summary>
    public static bool TryParseVk(string? name, out int vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.Length == 1)
        {
            var c = char.ToUpperInvariant(trimmed[0]);
            if (c is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                vk = c;
                return true;
            }

            return false;
        }

        if ((trimmed[0] is 'F' or 'f') && int.TryParse(trimmed[1..], out var f) && f is >= 1 and <= 12)
        {
            vk = 0x6F + f; // VK_F1 = 0x70
            return true;
        }

        switch (trimmed.ToUpperInvariant())
        {
            case "ESC":
            case "ESCAPE":
                vk = 0x1B;
                return true;
            case "ENTER":
            case "RETURN":
                vk = 0x0D;
                return true;
            case "SPACE":
                vk = 0x20;
                return true;
            case "TAB":
                vk = 0x09;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Converts a Win32 virtual-key code to the Core enum. Codes outside the named
    /// members still round-trip because the injector only uses the numeric value.
    /// </summary>
    public static CoreVirtualKey ToCoreVirtualKey(int vk) => (CoreVirtualKey)(ushort)vk;

    /// <summary>Parses a key name directly into the Core enum.</summary>
    public static bool TryParseCoreKey(string? name, out CoreVirtualKey key)
    {
        if (TryParseVk(name, out var vk))
        {
            key = ToCoreVirtualKey(vk);
            return true;
        }

        key = default;
        return false;
    }
}
