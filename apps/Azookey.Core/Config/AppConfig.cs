using System.Runtime.Serialization;
using System.Text.Json;

namespace Azookey.Core.Config;

public enum WidthMode
{
    Half,
    Full
}

public enum PunctuationStyle
{
    ToutenKuten,
    FullwidthCommaFullwidthPeriod,
    ToutenFullwidthPeriod,
    FullwidthCommaKuten
}

public enum SymbolStyle
{
    CornerBracketMiddleDot,
    SquareBracketBackslash,
    CornerBracketBackslash,
    SquareBracketMiddleDot
}

public enum SpaceInputMode
{
    AlwaysHalf,
    [EnumMember(Value = "always_full")]
    FollowInputMode
}

public enum NumpadInputMode
{
    DirectInput,
    AlwaysHalf,
    FollowInputMode
}

public sealed record CharacterWidthGroups
{
    public WidthMode Alphabet { get; init; } = WidthMode.Half;
    public WidthMode Number { get; init; } = WidthMode.Half;
    public WidthMode Bracket { get; init; } = WidthMode.Full;
    public WidthMode CommaPeriod { get; init; } = WidthMode.Full;
    public WidthMode MiddleDotCornerBracket { get; init; } = WidthMode.Full;
    public WidthMode Quote { get; init; } = WidthMode.Full;
    public WidthMode ColonSemicolon { get; init; } = WidthMode.Full;
    public WidthMode HashGroup { get; init; } = WidthMode.Half;
    public WidthMode Tilde { get; init; } = WidthMode.Full;
    public WidthMode MathSymbol { get; init; } = WidthMode.Full;
    public WidthMode QuestionExclamation { get; init; } = WidthMode.Full;
}

public sealed record GeneralConfig
{
    public PunctuationStyle PunctuationStyle { get; init; } = PunctuationStyle.ToutenKuten;
    public SymbolStyle SymbolStyle { get; init; } = SymbolStyle.CornerBracketMiddleDot;
    public SpaceInputMode SpaceInput { get; init; } = SpaceInputMode.AlwaysHalf;
    public NumpadInputMode NumpadInput { get; init; } = NumpadInputMode.DirectInput;
    public bool PunctuationCommit { get; init; }
    public bool PunctuationCommitPunctuation { get; init; } = true;
    public bool PunctuationCommitExclamation { get; init; } = true;
    public bool PunctuationCommitQuestion { get; init; } = true;
    public bool ShowCandidateWindowAfterSpace { get; init; }
    public bool ShowLiveConversionReading { get; init; } = true;
    public int LiveConversionReadingVerticalAdjustment { get; init; } = 4;
}

public sealed record RomajiRule
{
    public string Input { get; init; } = "";
    public string Output { get; init; } = "";
    public string NextInput { get; init; } = "";
}

public sealed record RomajiTableConfig
{
    public List<RomajiRule> Rows { get; init; } = DefaultRomajiTable.Load();
}

public sealed record ZenzaiConfig
{
    public bool Enable { get; init; }
    public string Profile { get; init; } = "";
    public string Backend { get; init; } = "cpu";
    public string ModelId { get; init; } = ZenzaiModelCatalog.DefaultModelId;
}

public sealed record ShortcutConfig
{
    public bool CtrlSpaceToggle { get; init; } = true;
    public bool AltBackquoteToggle { get; init; } = true;
    public bool EisuToggle { get; init; }
}

public sealed record DebugConfig
{
    public bool ServerLogEnabled { get; init; }
    public string ServerLogLevel { get; init; } = "warn";
    public bool ServerCrashTraceEnabled { get; init; } = true;
}

public sealed record CharacterWidthConfig
{
    public Dictionary<string, bool> SymbolFullwidth { get; init; } = CharacterWidthDefaults.CreateSymbolMap();
    public CharacterWidthGroups Groups { get; init; } = new();
}

public sealed record UserDictionaryEntry
{
    public string Reading { get; init; } = "";
    public string Word { get; init; } = "";
}

public sealed record UserDictionaryConfig
{
    public List<UserDictionaryEntry> Entries { get; init; } = [];
}

public sealed record AppConfig
{
    public const string ConfigVersion = "0.1.3";

    public string Version { get; init; } = ConfigVersion;
    public DebugConfig Debug { get; init; } = new();
    public ZenzaiConfig Zenzai { get; init; } = new();
    public ShortcutConfig Shortcuts { get; init; } = new();
    public GeneralConfig General { get; init; } = new();
    public RomajiTableConfig RomajiTable { get; init; } = new();
    public CharacterWidthConfig CharacterWidth { get; init; } = new();
    public UserDictionaryConfig UserDictionary { get; init; } = new();

    public static AppConfig CreateDefault() => new();

    public static AppConfig Deserialize(string json)
    {
        AppConfig config = JsonSerializer.Deserialize<AppConfig>(json, AzookeyJson.Options)
            ?? throw new JsonException("Failed to deserialize app config.");

        config = config with
        {
            Zenzai = NormalizeZenzai(config.Zenzai)
        };

        return config.Version == ConfigVersion
            ? config
            : config with
            {
                Version = ConfigVersion,
                General = config.General with
                {
                    NumpadInput = MigrateLegacyNumpadInput(config.General.NumpadInput)
                },
                RomajiTable = config.RomajiTable with
                {
                    Rows = MigrateLegacyRomajiRows(config.RomajiTable.Rows)
                }
            };
    }

    private static ZenzaiConfig NormalizeZenzai(ZenzaiConfig zenzai) =>
        zenzai with
        {
            Backend = NormalizeZenzaiBackend(zenzai.Backend),
            ModelId = ZenzaiModelCatalog.ResolveModelId(zenzai.ModelId)
        };

    public static string NormalizeZenzaiBackend(string? backend) =>
        string.Equals(backend, "vulkan", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(backend, "cuda", StringComparison.OrdinalIgnoreCase)
            ? "vulkan"
            : "cpu";

    private static NumpadInputMode MigrateLegacyNumpadInput(NumpadInputMode legacyValue) =>
        legacyValue switch
        {
            NumpadInputMode.AlwaysHalf => NumpadInputMode.DirectInput,
            NumpadInputMode.FollowInputMode => NumpadInputMode.AlwaysHalf,
            _ => NumpadInputMode.DirectInput
        };

    private static List<RomajiRule> MigrateLegacyRomajiRows(List<RomajiRule> rows) =>
        rows.Where(row => !IsLegacyRemovedDefaultRow(row)).ToList();

    private static bool IsLegacyRemovedDefaultRow(RomajiRule row) =>
        row.NextInput.Length == 0 && (row.Input, row.Output) is
            ("~", "〜") or (".", "。") or (",", "、") or ("[", "「") or ("]", "」");
}
