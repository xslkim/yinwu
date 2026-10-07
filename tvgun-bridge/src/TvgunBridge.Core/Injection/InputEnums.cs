namespace TvgunBridge.Core.Injection;

/// <summary>Mouse buttons that can be injected.</summary>
public enum MouseButton
{
    /// <summary>Left button (the trigger in most gun games).</summary>
    Left,

    /// <summary>Right button (e.g. pump-action reload in Big Buck Hunter).</summary>
    Right,

    /// <summary>Middle button.</summary>
    Middle,
}

/// <summary>
/// Virtual keys usable for injection. Values are the Win32 virtual-key codes so they
/// can be passed to SendInput directly.
/// </summary>
public enum VirtualKey : ushort
{
    /// <summary>Escape (0x1B).</summary>
    Escape = 0x1B,

    /// <summary>Enter (0x0D).</summary>
    Enter = 0x0D,

    /// <summary>Space (0x20).</summary>
    Space = 0x20,

    /// <summary>Digit 1 (0x31).</summary>
    Digit1 = 0x31,

    /// <summary>Digit 2 (0x32).</summary>
    Digit2 = 0x32,

    /// <summary>Digit 3 (0x33).</summary>
    Digit3 = 0x33,

    /// <summary>Digit 4 (0x34).</summary>
    Digit4 = 0x34,

    /// <summary>Digit 5 (0x35).</summary>
    Digit5 = 0x35,

    /// <summary>Letter C (0x43), commonly coin.</summary>
    KeyC = 0x43,

    /// <summary>Letter R (0x52), commonly reload.</summary>
    KeyR = 0x52,

    /// <summary>Letter S (0x53), commonly start.</summary>
    KeyS = 0x53,

    /// <summary>F1 (0x70).</summary>
    F1 = 0x70,

    /// <summary>F2 (0x71).</summary>
    F2 = 0x71,

    /// <summary>F3 (0x72).</summary>
    F3 = 0x72,

    /// <summary>F4 (0x73).</summary>
    F4 = 0x73,
}
