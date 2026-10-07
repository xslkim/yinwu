namespace TvgunBridge.App;

/// <summary>
/// Command-line options for the one-shot autostart mode
/// (<c>--game &lt;adapterId&gt; [--show-ui] [--no-inject] [--exit-after-game]</c>).
/// Pure parsing logic with no UI or runtime dependencies. No arguments at all means
/// the classic GUI mode (<see cref="IsAutoMode"/> is false).
/// </summary>
public sealed class CliOptions
{
    private CliOptions()
    {
    }

    /// <summary>Adapter id from <c>--game</c> (matches games/*.json), or null in GUI mode.</summary>
    public string? GameId { get; private set; }

    /// <summary>Show the control panel in autostart mode (default: hidden, overlay only).</summary>
    public bool ShowUi { get; private set; }

    /// <summary>Disable input injection in autostart mode (default: enabled).</summary>
    public bool NoInject { get; private set; }

    /// <summary>
    /// Exit the bridge after the game window has been lost for the grace period.
    /// Default true in autostart mode; the flag exists so launcher scripts can state
    /// the behavior explicitly.
    /// </summary>
    public bool ExitAfterGame { get; private set; } = true;

    /// <summary>True when <c>--game</c> was given (one-shot autostart mode).</summary>
    public bool IsAutoMode => GameId is not null;

    /// <summary>Usage text shown on parameter errors.</summary>
    public const string Usage = """
        用法：TvgunBridge.App.exe [--game <adapterId> [--show-ui] [--no-inject] [--exit-after-game]]

        无参数                 图形界面模式（默认，行为与旧版一致）
        --game <adapterId>     一键启动 games/<adapterId>.json 适配的游戏（自动模式）
        --show-ui              自动模式下仍显示控制面板（默认隐藏面板，仅保留叠加边框）
        --no-inject            自动模式下关闭输入注入（默认开启）
        --exit-after-game      游戏窗口连续丢失 10 秒后自动退出（自动模式默认开启）
        """;

    /// <summary>
    /// Parses the argument list. Returns false (with <paramref name="error"/> set) on
    /// unknown arguments, a missing <c>--game</c> value, or a repeated <c>--game</c>.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> args, out CliOptions options, out string? error)
    {
        options = new CliOptions();
        error = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? id;
            switch (arg)
            {
                case "--show-ui":
                    options.ShowUi = true;
                    continue;
                case "--no-inject":
                    options.NoInject = true;
                    continue;
                case "--exit-after-game":
                    options.ExitAfterGame = true;
                    continue;
                case "--game":
                    if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "--game 缺少适配器 id 参数";
                        return false;
                    }

                    id = args[++i];
                    break;
                default:
                    const string prefix = "--game=";
                    if (!arg.StartsWith(prefix, StringComparison.Ordinal) || arg.Length == prefix.Length)
                    {
                        error = $"未知参数：{arg}";
                        return false;
                    }

                    id = arg[prefix.Length..];
                    break;
            }

            if (options.GameId is not null)
            {
                error = "--game 只能指定一次";
                return false;
            }

            options.GameId = id;
        }

        return true;
    }
}
