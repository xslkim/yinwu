using System.Runtime.InteropServices;
using TvgunBridge.App.Native;

namespace TvgunBridge.App.Input;

/// <summary>
/// Global low-level keyboard hook (WH_KEYBOARD_LL) used as the local hotkey fallback
/// (task T2.3): F5/F6 inject coin/start keys, F7 toggles injection, F8 toggles the
/// control panel. A low-level hook is used instead of RegisterHotKey because it keeps
/// working while a game window holds the foreground. Requires a message loop on the
/// calling thread (the WPF UI thread provides one).
/// </summary>
public sealed class GlobalHotkeyHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly Dictionary<int, Action> _actions = new();
    private IntPtr _hook;

    /// <summary>Creates the hook. Call <see cref="Start"/> to install it.</summary>
    public GlobalHotkeyHook()
    {
        // Keep a rooted reference so the delegate is not collected while native code holds it.
        _callback = HookCallback;
    }

    /// <summary>Binds a virtual-key code to an action, invoked on the UI thread on key-down.</summary>
    public void Register(int vk, Action action) => _actions[vk] = action;

    /// <summary>Installs the hook. Returns false when installation fails.</summary>
    public bool Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return true;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);
        return _hook != IntPtr.Zero;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var message = wParam.ToInt32();
        if (nCode >= 0 && (message == NativeMethods.WmKeyDown || message == NativeMethods.WmSysKeyDown))
        {
            var vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode is the first field
            if (_actions.TryGetValue(vk, out var action))
            {
                action();
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
