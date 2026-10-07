namespace TvgunBridge.Core.Protocol;

/// <summary>
/// Thread-safe "latest wins" slot for aim samples. Samples expire after a configurable
/// timeout (default 0.6 s, matching the tvgun Python reference): once the phone stops
/// streaming, readers observe an empty slot instead of a stale crosshair position.
/// </summary>
public sealed class AimSlot
{
    private readonly object _gate = new();
    private readonly TimeSpan _timeout;
    private readonly Func<DateTimeOffset> _clock;
    private AimPoint? _latest;

    /// <summary>
    /// Creates an empty slot.
    /// </summary>
    /// <param name="timeout">How long a sample stays valid. Defaults to 600 ms.</param>
    /// <param name="clock">Time source, injectable for tests. Defaults to the system clock.</param>
    public AimSlot(TimeSpan? timeout = null, Func<DateTimeOffset>? clock = null)
    {
        _timeout = timeout ?? TimeSpan.FromMilliseconds(600);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Stores a new aim sample, replacing any previous one. The timestamp of the
    /// incoming point is ignored; arrival time from the slot's clock is used so that
    /// expiry is measured against local receive time.
    /// </summary>
    public void Set(double x, double y)
    {
        lock (_gate)
        {
            _latest = new AimPoint(x, y, _clock());
        }
    }

    /// <summary>
    /// Removes the current sample, if any.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _latest = null;
        }
    }

    /// <summary>
    /// Gets whether a non-expired sample is available.
    /// </summary>
    public bool HasValue => TryGet(out _);

    /// <summary>
    /// Gets the current sample. Throws <see cref="InvalidOperationException"/> when the
    /// slot is empty or the sample has expired.
    /// </summary>
    public AimPoint Value =>
        TryGet(out var point)
            ? point
            : throw new InvalidOperationException("No fresh aim sample is available.");

    /// <summary>
    /// Attempts to read the current sample. Returns false when the slot is empty or
    /// the newest sample is older than the configured timeout.
    /// </summary>
    public bool TryGet(out AimPoint point)
    {
        lock (_gate)
        {
            if (_latest is { } latest && _clock() - latest.Timestamp <= _timeout)
            {
                point = latest;
                return true;
            }
        }

        point = default;
        return false;
    }

    /// <summary>
    /// Age of the newest sample in milliseconds, or null when the slot is empty.
    /// </summary>
    public double? AgeMs
    {
        get
        {
            lock (_gate)
            {
                return _latest is { } latest ? (_clock() - latest.Timestamp).TotalMilliseconds : null;
            }
        }
    }
}
