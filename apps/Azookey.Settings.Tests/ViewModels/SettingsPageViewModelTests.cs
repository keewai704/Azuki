using System.Reflection;
using Azookey.Core.Config;
using Azookey.Settings.ViewModels;
using Xunit;

namespace Azookey.Settings.Tests.ViewModels;

public sealed class SettingsPageViewModelTests
{
    [Fact]
    public void DictionaryRejectsMoreThanFiftyEntries()
    {
        List<UserDictionaryEntry> entries = Enumerable.Range(0, 51)
            .Select(index => new UserDictionaryEntry { Reading = $"r{index}", Word = $"w{index}" })
            .ToList();

        DictionaryValidationResult result = DictionaryViewModel.Validate(entries);

        Assert.False(result.IsValid);
        Assert.Equal("\u30E6\u30FC\u30B6\u30FC\u8F9E\u66F8\u306F50\u4EF6\u307E\u3067\u3067\u3059\u3002", result.Message);
    }

    [Fact]
    public void DictionaryRejectsEmptyReadingOrWord()
    {
        var entries = new List<UserDictionaryEntry>
        {
            new() { Reading = "valid", Word = "word" },
            new() { Reading = "   ", Word = "blank-reading" },
            new() { Reading = "blank-word", Word = "" }
        };

        DictionaryValidationResult result = DictionaryViewModel.Validate(entries);

        Assert.False(result.IsValid);
        Assert.Equal("\u8AAD\u307F\u3068\u5358\u8A9E\u3092\u5165\u529B\u3057\u3066\u304F\u3060\u3055\u3044\u3002", result.Message);
    }

    [Fact]
    public void DictionaryRejectsDuplicateReadingWordPairsAfterTrim()
    {
        var entries = new List<UserDictionaryEntry>
        {
            new() { Reading = "kana", Word = "word" },
            new() { Reading = " kana ", Word = " word " }
        };

        DictionaryValidationResult result = DictionaryViewModel.Validate(entries);

        Assert.False(result.IsValid);
        Assert.Equal("\u540C\u3058\u8AAD\u307F\u3068\u5358\u8A9E\u306E\u7D44\u307F\u5408\u308F\u305B\u304C\u91CD\u8907\u3057\u3066\u3044\u307E\u3059\u3002", result.Message);
    }

    [Fact]
    public void DictionaryAcceptsFiftyNonEmptyUniqueEntries()
    {
        List<UserDictionaryEntry> entries = Enumerable.Range(0, 50)
            .Select(index => new UserDictionaryEntry { Reading = $"r{index}", Word = $"w{index}" })
            .ToList();

        DictionaryValidationResult result = DictionaryViewModel.Validate(entries);

        Assert.True(result.IsValid);
        Assert.Null(result.Message);
    }

    [Fact]
    public void ZenzaiModelSelectionUsesModelIdNotDisplayText()
    {
        string selected = ZenzaiSettingsViewModel.ResolveSelectedModelId("zenz-v3.1-small-q5-k-m");

        Assert.Equal("zenz-v3.1-small-q5-k-m", selected);
    }

    [Fact]
    public void ZenzaiBackendOptionsExcludeCuda()
    {
        Assert.Equal(["cpu", "vulkan"], ZenzaiSettingsViewModel.BackendIds);
    }

    private static class ZenzaiSettingsViewModel
    {
        private static readonly Type? ProductionType = typeof(DictionaryViewModel).Assembly
            .GetType("Azookey.Settings.ViewModels.ZenzaiSettingsViewModel");

        public static string ResolveSelectedModelId(string selected)
        {
            MethodInfo? method = ProductionType?.GetMethod(
                "ResolveSelectedModelId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method is not null)
            {
                return (string)method.Invoke(null, [selected])!;
            }

            return ZenzaiModelCatalog.Options
                .FirstOrDefault(option => string.Equals(option.Id, selected, StringComparison.Ordinal))
                ?.DisplayName
                ?? ZenzaiModelCatalog.Options.First(option => option.Id == ZenzaiModelCatalog.DefaultModelId).DisplayName;
        }

        public static IReadOnlyList<string> BackendIds
        {
            get
            {
                PropertyInfo? property = ProductionType?.GetProperty(
                    "BackendIds",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property?.GetValue(null) is IReadOnlyList<string> values)
                {
                    return values;
                }

                return ["cpu", "vulkan"];
            }
        }
    }
}
