using System.Text.Json;
using Azookey.Core.Config;
using Xunit;

namespace Azookey.Core.Tests.Config;

public sealed class AppConfigTests
{
    [Fact]
    public void DefaultConfigMatchesRustDefaults()
    {
        AppConfig config = AppConfig.CreateDefault();

        Assert.Equal("0.1.3", config.Version);
        Assert.False(config.General.PunctuationCommit);
        Assert.True(config.General.PunctuationCommitPunctuation);
        Assert.True(config.General.PunctuationCommitExclamation);
        Assert.True(config.General.PunctuationCommitQuestion);
        Assert.False(config.General.ShowCandidateWindowAfterSpace);
        Assert.True(config.General.ShowLiveConversionReading);
        Assert.Equal(4, config.General.LiveConversionReadingVerticalAdjustment);
        Assert.Equal(NumpadInputMode.DirectInput, config.General.NumpadInput);
        Assert.Equal(SpaceInputMode.AlwaysHalf, config.General.SpaceInput);
        Assert.True(config.Shortcuts.CtrlSpaceToggle);
        Assert.True(config.Shortcuts.AltBackquoteToggle);
        Assert.False(config.Shortcuts.EisuToggle);
        Assert.False(config.Zenzai.Enable);
        Assert.Equal("", config.Zenzai.Profile);
        Assert.Equal("cpu", config.Zenzai.Backend);
        Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
        Assert.False(config.Debug.ServerLogEnabled);
        Assert.Equal("warn", config.Debug.ServerLogLevel);
        Assert.True(config.Debug.ServerCrashTraceEnabled);
        Assert.Empty(config.UserDictionary.Entries);
        Assert.True(config.RomajiTable.Rows.Count > 100);
    }

    [Fact]
    public void DefaultConfigIncludesDefaultZenzaiModel()
    {
        AppConfig config = AppConfig.CreateDefault();

        Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
    }

    [Fact]
    public void LegacyZenzaiConfigWithoutModelIdUsesDefaultModel()
    {
        const string json = """
        {
          "version": "0.1.3",
          "zenzai": { "enable": false, "profile": "", "backend": "cpu" }
        }
        """;

        AppConfig config = AppConfig.Deserialize(json);

        Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
    }

    [Fact]
    public void UnknownZenzaiModelIdNormalizesToDefaultModel()
    {
        const string json = """
        {
          "version": "0.1.3",
          "zenzai": {
            "enable": false,
            "profile": "",
            "backend": "cpu",
            "model_id": "missing-model"
          }
        }
        """;

        AppConfig config = AppConfig.Deserialize(json);

        Assert.Equal(ZenzaiModelCatalog.DefaultModelId, config.Zenzai.ModelId);
    }

    [Fact]
    public void DeserializeMigratesLegacyCudaBackendToVulkan()
    {
        const string json = """
        {
          "version": "0.1.3",
          "zenzai": { "enable": true, "profile": "", "backend": "cuda" }
        }
        """;

        AppConfig config = AppConfig.Deserialize(json);

        Assert.Equal("vulkan", config.Zenzai.Backend);
    }

    [Fact]
    public void JsonUsesSnakeCaseEnumValuesAndPropertyNames()
    {
        AppConfig config = AppConfig.CreateDefault();
        string json = JsonSerializer.Serialize(config, AzookeyJson.Options);

        Assert.Contains("\"punctuation_style\"", json);
        Assert.Contains("\"numpad_input\": \"direct_input\"", json);
        Assert.Contains("\"space_input\": \"always_half\"", json);
        Assert.DoesNotContain("PunctuationStyle", json);
    }

    [Fact]
    public void SpaceInputAcceptsLegacyAlwaysFullAlias()
    {
        const string json = """
        {
          "version": "0.1.2",
          "zenzai": { "enable": false, "profile": "", "backend": "cpu" },
          "general": { "space_input": "always_full" }
        }
        """;

        AppConfig config = JsonSerializer.Deserialize<AppConfig>(json, AzookeyJson.Options)!;

        Assert.Equal(SpaceInputMode.FollowInputMode, config.General.SpaceInput);
    }

    [Theory]
    [InlineData("always_half", NumpadInputMode.DirectInput)]
    [InlineData("follow_input_mode", NumpadInputMode.AlwaysHalf)]
    [InlineData("direct_input", NumpadInputMode.DirectInput)]
    public void DeserializeMigratesLegacyNumpadInputValues(string legacyValue, NumpadInputMode expected)
    {
        string json = $$"""
        {
          "version": "0.1.1",
          "zenzai": { "enable": false, "profile": "", "backend": "cpu" },
          "general": { "numpad_input": "{{legacyValue}}" }
        }
        """;

        AppConfig config = AppConfig.Deserialize(json);

        Assert.Equal(AppConfig.ConfigVersion, config.Version);
        Assert.Equal(expected, config.General.NumpadInput);
    }

    [Fact]
    public void ParseRowsSkipsBlankCommentAndMalformedLines()
    {
        List<RomajiRule> rows = DefaultRomajiTable.ParseRows(
        [
            "",
            "   ",
            "# comment",
            "invalid",
            "\tmissing-input",
            "missing-output\t ",
            "ka\tか",
            "kk\tっ\tk"
        ]);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("ka", row.Input);
                Assert.Equal("か", row.Output);
                Assert.Equal("", row.NextInput);
            },
            row =>
            {
                Assert.Equal("kk", row.Input);
                Assert.Equal("っ", row.Output);
                Assert.Equal("k", row.NextInput);
            });
    }
}
