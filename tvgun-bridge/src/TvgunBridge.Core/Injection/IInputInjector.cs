namespace TvgunBridge.Core.Injection;

/// <summary>
/// Abstraction over OS-level input injection. Implementations: <see cref="SendInputInjector"/>
/// (real injection) and <see cref="RecordingInjector"/> (test double that records calls).
/// </summary>
public interface IInputInjector
{
    /// <summary>Moves the pointer to an absolute screen pixel coordinate.</summary>
    void MoveAbsolute(double screenX, double screenY);

    /// <summary>Presses a mouse button.</summary>
    void ButtonDown(MouseButton button);

    /// <summary>Releases a mouse button.</summary>
    void ButtonUp(MouseButton button);

    /// <summary>
    /// Synthesizes a click: button down, hold for <paramref name="holdMs"/> milliseconds,
    /// button up. The hold must not block the calling thread, and overlapping clicks must
    /// be serialized so that a press is always followed by its own release.
    /// </summary>
    void Click(MouseButton button, int holdMs);

    /// <summary>Synthesizes a key press and release.</summary>
    void KeyTap(VirtualKey key);
}
