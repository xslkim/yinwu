using System.Runtime.InteropServices;

namespace TvgunBridge.Core.Injection;

internal static class NativeMethods
{
    internal const uint MouseEventFMove = 0x0001;
    internal const uint MouseEventFLeftDown = 0x0002;
    internal const uint MouseEventFLeftUp = 0x0004;
    internal const uint MouseEventFRightDown = 0x0008;
    internal const uint MouseEventFRightUp = 0x0010;
    internal const uint MouseEventFMiddleDown = 0x0020;
    internal const uint MouseEventFMiddleUp = 0x0040;
    internal const uint MouseEventFAbsolute = 0x8000;
    internal const uint MouseEventFVirtualDesk = 0x4000;

    internal const uint KeyEventFKeyDown = 0x0000;
    internal const uint KeyEventFKeyUp = 0x0002;

    internal const uint InputMouse = 0;
    internal const uint InputKeyboard = 1;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}

[StructLayout(LayoutKind.Explicit)]
internal struct INPUTUNION
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public INPUTUNION U;

    internal static INPUT MouseMove(int absoluteX, int absoluteY) => new()
    {
        type = NativeMethods.InputMouse,
        U = new INPUTUNION
        {
            mi = new MOUSEINPUT
            {
                dx = absoluteX,
                dy = absoluteY,
                dwFlags = NativeMethods.MouseEventFMove
                    | NativeMethods.MouseEventFAbsolute
                    | NativeMethods.MouseEventFVirtualDesk,
            },
        },
    };

    internal static INPUT MouseButton(uint flags) => new()
    {
        type = NativeMethods.InputMouse,
        U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags } },
    };

    internal static INPUT Keyboard(ushort vk, uint flags) => new()
    {
        type = NativeMethods.InputKeyboard,
        U = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags } },
    };
}
