using TvgunBridge.Core.Coordinates;
using TvgunBridge.Core.Protocol;

namespace TvgunBridge.Core.Injection;

/// <summary>
/// High-level input pipeline. A timer (default 125 Hz) polls the <see cref="AimSlot"/> and
/// injects the freshest aim, mapped through a <see cref="CoordinateMapper"/>, as absolute
/// pointer moves; when aim samples expire (> 0.6 s without updates) movement injection
/// stops automatically. Trigger events move the pointer to the shot coordinate first and
/// then synthesize a click. The <see cref="Enabled"/> switch gates all injection (e.g.
/// disabled while the game window is lost).
/// </summary>
public sealed class InputPipeline : IDisposable
{
    private readonly IInputInjector _injector;
    private readonly CoordinateMapper _mapper;
    private readonly AimSlot _aimSlot;
    private readonly Func<RectD> _targetRectProvider;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>
    /// Creates a pipeline. Nothing runs until <see cref="Start"/> is called; shot handling
    /// works immediately once <see cref="Attach"/> or <see cref="HandleShot"/> is used.
    /// </summary>
    /// <param name="injector">Injection target.</param>
    /// <param name="mapper">Normalized→screen coordinate mapper.</param>
    /// <param name="aimSlot">Source of the latest aim sample.</param>
    /// <param name="targetRectProvider">Supplies the current game client rectangle in
    /// screen coordinates (typically fed by a Windowing.WindowTracker).</param>
    public InputPipeline(
        IInputInjector injector,
        CoordinateMapper mapper,
        AimSlot aimSlot,
        Func<RectD> targetRectProvider)
    {
        _injector = injector;
        _mapper = mapper;
        _aimSlot = aimSlot;
        _targetRectProvider = targetRectProvider;
    }

    /// <summary>Master injection switch. When false, neither moves nor clicks are injected.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Polling rate for aim-driven pointer moves, in Hz. Default 125.</summary>
    public double MoveRateHz { get; set; } = 125.0;

    /// <summary>Mouse button injected for trigger pulls. Default <see cref="MouseButton.Left"/>.</summary>
    public MouseButton TriggerButton { get; set; } = MouseButton.Left;

    /// <summary>Hold time of synthesized trigger clicks, in milliseconds. Default 30.</summary>
    public int ClickHoldMs { get; set; } = 30;

    /// <summary>Starts the aim polling timer.</summary>
    /// <exception cref="InvalidOperationException">The pipeline is already running.</exception>
    public void Start()
    {
        if (_cts is not null)
        {
            throw new InvalidOperationException("The pipeline is already running.");
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>Stops the polling timer.</summary>
    public async Task StopAsync()
    {
        var cts = _cts;
        var loop = _loop;
        _cts = null;
        _loop = null;
        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => _cts?.Cancel();

    /// <summary>
    /// Subscribes this pipeline to a server's <see cref="ShotHttpServer.ShotReceived"/> event.
    /// </summary>
    public void Attach(ShotHttpServer server) => server.ShotReceived += OnShotReceived;

    /// <summary>Detaches from a server previously passed to <see cref="Attach"/>.</summary>
    public void Detach(ShotHttpServer server) => server.ShotReceived -= OnShotReceived;

    /// <summary>
    /// Performs one polling iteration: reads the freshest aim sample and, when present
    /// and injection is enabled, injects the mapped pointer move. Exposed so tests can
    /// drive the pipeline deterministically without the timer.
    /// </summary>
    public void Tick()
    {
        if (!Enabled)
        {
            return;
        }

        if (!_aimSlot.TryGet(out var aim))
        {
            return; // expired or empty: stop moving
        }

        var target = _mapper.Map(aim.X, aim.Y, _targetRectProvider());
        _injector.MoveAbsolute(target.X, target.Y);
    }

    /// <summary>
    /// Handles one trigger pull: moves the pointer to the mapped shot coordinate, then
    /// clicks <see cref="TriggerButton"/> with <see cref="ClickHoldMs"/>.
    /// </summary>
    public void HandleShot(ShotEvent shot)
    {
        if (!Enabled)
        {
            return;
        }

        var target = _mapper.Map(shot.X, shot.Y, _targetRectProvider());
        _injector.MoveAbsolute(target.X, target.Y);
        _injector.Click(TriggerButton, ClickHoldMs);
    }

    private void OnShotReceived(object? sender, ShotEvent shot) => HandleShot(shot);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / MoveRateHz));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Tick();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
