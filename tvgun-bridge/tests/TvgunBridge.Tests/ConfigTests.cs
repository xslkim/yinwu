using TvgunBridge.Core.Config;
using TvgunBridge.Core.Injection;

namespace TvgunBridge.Tests;

public class TeknoParrotIniWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TeknoParrotIniWriterTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string IniPath => Path.Combine(_dir, "teknoparrot.ini");

    [Fact]
    public void MissingFileIsCreated()
    {
        TeknoParrotIniWriter.WriteValues(IniPath, "General",
            new Dictionary<string, string> { ["Windowed"] = "1", ["Input API"] = "RawInput" });

        var lines = File.ReadAllLines(IniPath);
        Assert.Contains("[General]", lines);
        Assert.Contains("Windowed=1", lines);
        Assert.Contains("Input API=RawInput", lines);
    }

    [Fact]
    public void ExistingKeysAreRewrittenOtherContentPreserved()
    {
        File.WriteAllLines(IniPath, new[]
        {
            "[GlobalHotkeys]",
            "ExitKey=0x1B",
            "PauseKey=0x13",
            "[General]",
            "; comment line",
            "Input API=DirectInput",
            "Windowed=0",
            "ResolutionWidth=1920",
        });

        TeknoParrotIniWriter.WriteValues(IniPath, "General",
            new Dictionary<string, string> { ["Windowed"] = "1", ["Input API"] = "RawInput", ["HideCursor"] = "1" });

        var lines = File.ReadAllLines(IniPath);
        Assert.Contains("Windowed=1", lines);
        Assert.Contains("Input API=RawInput", lines);
        Assert.DoesNotContain("Windowed=0", lines);
        Assert.DoesNotContain("Input API=DirectInput", lines);
        Assert.Contains("ResolutionWidth=1920", lines);   // untouched keys survive
        Assert.Contains("; comment line", lines);          // comments survive
        Assert.Contains("HideCursor=1", lines);            // missing key appended to section
        Assert.Equal("[GlobalHotkeys]", lines[0]);         // section order preserved
        Assert.Contains("ExitKey=0x1B", lines);
    }

    [Fact]
    public void MissingSectionIsAppended()
    {
        File.WriteAllLines(IniPath, new[] { "[General]", "Windowed=1" });

        TeknoParrotIniWriter.WriteValues(IniPath,
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["Advanced"] = new Dictionary<string, string> { ["Foo"] = "Bar" },
            });

        var text = File.ReadAllText(IniPath);
        Assert.Contains("[General]", text);
        Assert.Contains("[Advanced]", text);
        Assert.Contains("Foo=Bar", text);
    }

    [Fact]
    public void BackupIsWrittenBeforeModify()
    {
        var original = new[] { "[General]", "Windowed=0" };
        File.WriteAllLines(IniPath, original);

        TeknoParrotIniWriter.WriteValues(IniPath, "General",
            new Dictionary<string, string> { ["Windowed"] = "1" });

        Assert.True(File.Exists(IniPath + ".bak"));
        Assert.Equal(original, File.ReadAllLines(IniPath + ".bak"));
        Assert.Contains("Windowed=1", File.ReadAllLines(IniPath));
    }

    [Fact]
    public void KeyMatchingIsCaseInsensitive()
    {
        File.WriteAllLines(IniPath, new[] { "[general]", "windowed=0" });

        TeknoParrotIniWriter.WriteValues(IniPath, "General",
            new Dictionary<string, string> { ["Windowed"] = "1" });

        var lines = File.ReadAllLines(IniPath);
        Assert.Single(lines, l => l.EndsWith("=1", StringComparison.Ordinal));
        Assert.DoesNotContain("windowed=0", lines);
    }
}

public class GameAdapterTests
{
    private static readonly string GamesDir = Path.Combine(AppContext.BaseDirectory, "games");

    public static TheoryData<string> GameFiles =>
        new(Directory.EnumerateFiles(GamesDir, "*.json").Select(Path.GetFileName)!);

    [Fact]
    public void AllGameAdaptersAreShipped()
    {
        Assert.True(Directory.Exists(GamesDir), $"games directory not found at {GamesDir}");
        Assert.Equal(27, Directory.EnumerateFiles(GamesDir, "*.json").Count());
    }

    [Theory]
    [MemberData(nameof(GameFiles))]
    public void EveryAdapterDeserializesCompletely(string fileName)
    {
        var adapter = GameAdapter.Load(Path.Combine(GamesDir, fileName));

        Assert.False(string.IsNullOrWhiteSpace(adapter.Id));
        Assert.False(string.IsNullOrWhiteSpace(adapter.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(adapter.WindowTitleRegex));
        Assert.NotEmpty(adapter.ProcessNames);
        Assert.False(string.IsNullOrWhiteSpace(adapter.TeknoParrotIniPath));
        Assert.False(string.IsNullOrWhiteSpace(adapter.TeknoParrotProfile));
        Assert.False(string.IsNullOrWhiteSpace(adapter.GameDirectory));
        Assert.True(adapter.TeknoParrotIniValues.ContainsKey("General"));

        var general = adapter.TeknoParrotIniValues["General"];
        Assert.Equal("1", general["Windowed"]);
        Assert.Equal("1", general["HideCursor"]);
        Assert.Equal("RawInput", general["Input API"]);
    }

    /// <summary>
    /// The adapter INI paths are absolute paths on the development machine (D:\yinwu).
    /// This test is only meaningful there; elsewhere it returns early so CI stays portable.
    /// </summary>
    [Theory]
    [MemberData(nameof(GameFiles))]
    public void TeknoParrotIniPathParentDirectoryExists(string fileName)
    {
        if (!Directory.Exists(@"D:\yinwu"))
        {
            return; // skip: game library not present on this machine
        }

        var adapter = GameAdapter.Load(Path.Combine(GamesDir, fileName));
        Assert.False(string.IsNullOrWhiteSpace(adapter.TeknoParrotIniPath));

        var parent = Path.GetDirectoryName(adapter.TeknoParrotIniPath);
        Assert.True(parent is not null && Directory.Exists(parent),
            $"{adapter.Id}: ini parent directory missing: {parent}");
    }

    /// <summary>
    /// gameDirectory must be the directory part of teknoParrotIniPath
    /// (Path.GetDirectoryName semantics). Portable: pure string check.
    /// </summary>
    [Theory]
    [MemberData(nameof(GameFiles))]
    public void GameDirectoryMatchesIniPathParent(string fileName)
    {
        var adapter = GameAdapter.Load(Path.Combine(GamesDir, fileName));
        Assert.Equal(Path.GetDirectoryName(adapter.TeknoParrotIniPath), adapter.GameDirectory);
    }

    /// <summary>
    /// teknoParrotProfile must exist under TeknoParrot's UserProfiles or GameProfiles.
    /// Only meaningful on the development machine (D:\yinwu); elsewhere returns early.
    /// </summary>
    [Theory]
    [MemberData(nameof(GameFiles))]
    public void TeknoParrotProfileExistsInUserOrGameProfiles(string fileName)
    {
        if (!Directory.Exists(@"D:\yinwu"))
        {
            return; // skip: game library not present on this machine
        }

        var adapter = GameAdapter.Load(Path.Combine(GamesDir, fileName));
        Assert.False(string.IsNullOrWhiteSpace(adapter.TeknoParrotProfile));

        var inUserProfiles = File.Exists(Path.Combine(@"D:\yinwu\1846\UserProfiles", adapter.TeknoParrotProfile!));
        var inGameProfiles = File.Exists(Path.Combine(@"D:\yinwu\1846\GameProfiles", adapter.TeknoParrotProfile!));
        Assert.True(inUserProfiles || inGameProfiles,
            $"{adapter.Id}: profile {adapter.TeknoParrotProfile} found in neither UserProfiles nor GameProfiles");
    }

    [Fact]
    public void MissingProfileAndDirectoryFieldsDeserializeAsNull()
    {
        var adapter = GameAdapter.FromJson("""
        {
          "id": "t",
          "displayName": "T"
        }
        """);
        Assert.Null(adapter.TeknoParrotProfile);
        Assert.Null(adapter.GameDirectory);
    }

    [Fact]
    public void EnumFieldsDeserializeFromCamelCaseStrings()
    {
        var adapter = GameAdapter.FromJson("""
        {
          "id": "t",
          "displayName": "T",
          "triggerButton": "right",
          "reloadStrategy": "rightClick"
        }
        """);
        Assert.Equal(MouseButton.Right, adapter.TriggerButton);
        Assert.Equal(ReloadStrategy.RightClick, adapter.ReloadStrategy);
    }

    [Fact]
    public void KnownGamesHaveExpectedStrategies()
    {
        var adapters = GameAdapter.LoadAll(GamesDir).ToDictionary(a => a.Id);

        Assert.Equal(ReloadStrategy.RightClick, adapters["big-buck-hunter-pro"].ReloadStrategy);
        Assert.Equal(ReloadStrategy.None, adapters["aliens-extermination"].ReloadStrategy);
        Assert.Contains("PBX100-2-NA-MPR0-A63", adapters["point-blank-x"].ProcessNames);
        Assert.Equal(30, adapters["aliens-extermination"].ClickHoldMs);
    }
}

public class BridgeConfigTests
{
    [Fact]
    public void DefaultsAreComplete()
    {
        var config = new BridgeConfig();
        Assert.Equal(8000, config.Port);
        Assert.Equal(30, config.ClickHoldMs);
        Assert.Equal(600, config.AimTimeoutMs);
        Assert.True(config.EnableInjection);
    }

    [Fact]
    public void JsonRoundTripPreservesValues()
    {
        var config = new BridgeConfig
        {
            Port = 9000,
            ClickHoldMs = 55,
            EnableInjection = false,
            GameWindowTitlePatterns = { "TeknoBudgie" },
            ProcessNames = { "vsg" },
        };

        var restored = BridgeConfig.FromJson(config.ToJson());
        Assert.Equal(9000, restored.Port);
        Assert.Equal(55, restored.ClickHoldMs);
        Assert.False(restored.EnableInjection);
        Assert.Equal(new[] { "TeknoBudgie" }, restored.GameWindowTitlePatterns);
        Assert.Equal(new[] { "vsg" }, restored.ProcessNames);
    }

    [Fact]
    public void LoadMissingFileReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        Assert.Equal(8000, BridgeConfig.Load(path).Port);
    }

    [Fact]
    public void SaveThenLoadRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        try
        {
            new BridgeConfig { Port = 8123 }.Save(path);
            Assert.Equal(8123, BridgeConfig.Load(path).Port);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
