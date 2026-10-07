using TvgunBridge.Core.Coordinates;

namespace TvgunBridge.Core.Windowing;

/// <summary>
/// Snapshot of a located game window.
/// </summary>
/// <param name="Hwnd">Window handle.</param>
/// <param name="Title">Window title at the time of the snapshot.</param>
/// <param name="ClientRect">Client area in screen coordinates (origin of the client
/// area plus client size), i.e. the rectangle aim coordinates map into.</param>
public readonly record struct GameWindowInfo(IntPtr Hwnd, string Title, RectD ClientRect);
