namespace TvgunBridge.Core.Injection;

/// <summary>Kinds of calls recorded by <see cref="RecordingInjector"/>.</summary>
public enum InjectionKind
{
    /// <summary><see cref="IInputInjector.MoveAbsolute"/>.</summary>
    Move,

    /// <summary><see cref="IInputInjector.ButtonDown"/>.</summary>
    ButtonDown,

    /// <summary><see cref="IInputInjector.ButtonUp"/>.</summary>
    ButtonUp,

    /// <summary><see cref="IInputInjector.Click"/>.</summary>
    Click,

    /// <summary><see cref="IInputInjector.KeyTap"/>.</summary>
    KeyTap,
}

/// <summary>
/// One recorded injector call. Fields not relevant to <see cref="Kind"/> are null.
/// </summary>
public readonly record struct RecordedInputEvent(
    InjectionKind Kind,
    DateTimeOffset Timestamp,
    double X,
    double Y,
    MouseButton? Button,
    VirtualKey? Key,
    int HoldMs);

/// <summary>
/// <see cref="IInputInjector"/> test double: records every call, with a timestamp, into
/// a thread-safe event list for closed-loop test assertions. <see cref="Click"/> records
/// a single Click event synchronously (down, hold and up are implied by
/// <see cref="RecordedInputEvent.HoldMs"/>).
/// </summary>
public sealed class RecordingInjector : IInputInjector
{
    private readonly object _gate = new();
    private readonly List<RecordedInputEvent> _events = new();
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// Creates a recorder.
    /// </summary>
    /// <param name="clock">Time source, injectable for deterministic tests.</param>
    public RecordingInjector(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Snapshot of all recorded events, in call order.</summary>
    public IReadOnlyList<RecordedInputEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    /// <summary>Removes all recorded events.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }

    /// <inheritdoc />
    public void MoveAbsolute(double screenX, double screenY) =>
        Record(new RecordedInputEvent(InjectionKind.Move, _clock(), screenX, screenY, null, null, 0));

    /// <inheritdoc />
    public void ButtonDown(MouseButton button) =>
        Record(new RecordedInputEvent(InjectionKind.ButtonDown, _clock(), 0, 0, button, null, 0));

    /// <inheritdoc />
    public void ButtonUp(MouseButton button) =>
        Record(new RecordedInputEvent(InjectionKind.ButtonUp, _clock(), 0, 0, button, null, 0));

    /// <inheritdoc />
    public void Click(MouseButton button, int holdMs) =>
        Record(new RecordedInputEvent(InjectionKind.Click, _clock(), 0, 0, button, null, holdMs));

    /// <inheritdoc />
    public void KeyTap(VirtualKey key) =>
        Record(new RecordedInputEvent(InjectionKind.KeyTap, _clock(), 0, 0, null, key, 0));

    private void Record(RecordedInputEvent inputEvent)
    {
        lock (_gate)
        {
            _events.Add(inputEvent);
        }
    }
}
