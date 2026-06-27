using System.Text.Json;
using Azookey.Core.Config;
using Xunit;

namespace Azookey.Core.Tests.Config;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "azookey-config-tests", Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void MissingSettingsCreatesDefaultSettings()
    {
        var store = new ConfigStore(Path.Combine(root, "Azookey"));

        ConfigLoadResult result = store.LoadWithRecovery();

        Assert.Null(result.Recovery);
        Assert.Equal(AppConfig.ConfigVersion, result.Config.Version);
        Assert.True(File.Exists(Path.Combine(root, "Azookey", "settings.json")));
    }

    [Fact]
    public void CorruptedSettingsAreBackedUpAndReplaced()
    {
        string configRoot = Path.Combine(root, "Azookey");
        Directory.CreateDirectory(configRoot);
        string settings = Path.Combine(configRoot, "settings.json");
        File.WriteAllText(settings, "{not valid json");

        ConfigLoadResult result = new ConfigStore(configRoot).LoadWithRecovery();

        Assert.NotNull(result.Recovery);
        Assert.StartsWith("settings.json.broken-", Path.GetFileName(result.Recovery!.BackupPath));
        Assert.Equal("{not valid json", File.ReadAllText(result.Recovery.BackupPath));
        Assert.Equal(AppConfig.ConfigVersion, result.Config.Version);
    }

    [Fact]
    public void LegacyNumpadInputMigratesToCurrentMeaning()
    {
        string configRoot = Path.Combine(root, "Azookey");
        Directory.CreateDirectory(configRoot);
        AppConfig legacy = AppConfig.CreateDefault() with
        {
            Version = "0.1.1",
            General = AppConfig.CreateDefault().General with { NumpadInput = NumpadInputMode.AlwaysHalf }
        };
        File.WriteAllText(Path.Combine(configRoot, "settings.json"), JsonSerializer.Serialize(legacy, AzookeyJson.Options));

        ConfigLoadResult result = new ConfigStore(configRoot).LoadWithRecovery();

        Assert.Equal(AppConfig.ConfigVersion, result.Config.Version);
        Assert.Equal(NumpadInputMode.DirectInput, result.Config.General.NumpadInput);
    }

    [Fact]
    public void LoadWithRecoveryRemovesLegacyRemovedDefaultRomajiRows()
    {
        string configRoot = Path.Combine(root, "Azookey");
        Directory.CreateDirectory(configRoot);
        List<RomajiRule> legacyRows =
        [
            .. CreateLegacyRemovedDefaultRows(),
            new RomajiRule { Input = "qa", Output = "くぁ", NextInput = "" }
        ];
        AppConfig legacy = AppConfig.CreateDefault() with
        {
            Version = "0.1.1",
            RomajiTable = new RomajiTableConfig { Rows = legacyRows }
        };
        string settingsPath = Path.Combine(configRoot, "settings.json");
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(legacy, AzookeyJson.Options));

        ConfigLoadResult result = new ConfigStore(configRoot).LoadWithRecovery();

        Assert.Equal(AppConfig.ConfigVersion, result.Config.Version);
        Assert.DoesNotContain(result.Config.RomajiTable.Rows, IsLegacyRemovedDefaultRow);
        Assert.Contains(result.Config.RomajiTable.Rows, row => row.Input == "qa" && row.Output == "くぁ" && row.NextInput == "");

        AppConfig rewritten = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(settingsPath), AzookeyJson.Options)!;
        Assert.Equal(AppConfig.ConfigVersion, rewritten.Version);
        Assert.DoesNotContain(rewritten.RomajiTable.Rows, IsLegacyRemovedDefaultRow);
        Assert.Contains(rewritten.RomajiTable.Rows, row => row.Input == "qa" && row.Output == "くぁ" && row.NextInput == "");
    }

    [Fact]
    public void WriteLeavesNoTempFile()
    {
        string configRoot = Path.Combine(root, "Azookey");
        var store = new ConfigStore(configRoot);
        AppConfig config = AppConfig.CreateDefault() with { Zenzai = new ZenzaiConfig { Enable = true, Backend = "vulkan", Profile = "" } };

        store.Write(config);

        AppConfig saved = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path.Combine(configRoot, "settings.json")), AzookeyJson.Options)!;
        Assert.True(saved.Zenzai.Enable);
        Assert.Empty(Directory.EnumerateFiles(configRoot, "settings.json.tmp-*"));
    }

    private static List<RomajiRule> CreateLegacyRemovedDefaultRows() =>
    [
        new RomajiRule { Input = "~", Output = "〜", NextInput = "" },
        new RomajiRule { Input = ".", Output = "。", NextInput = "" },
        new RomajiRule { Input = ",", Output = "、", NextInput = "" },
        new RomajiRule { Input = "[", Output = "「", NextInput = "" },
        new RomajiRule { Input = "]", Output = "」", NextInput = "" }
    ];

    private static bool IsLegacyRemovedDefaultRow(RomajiRule row) =>
        row.NextInput.Length == 0 && (row.Input, row.Output) is
            ("~", "〜") or (".", "。") or (",", "、") or ("[", "「") or ("]", "」");
}
