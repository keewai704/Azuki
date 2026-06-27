using System;
using System.Collections.Generic;
using System.Linq;
using Azookey.Core.Config;

namespace Azookey.Settings.ViewModels;

public sealed record DictionaryValidationResult(bool IsValid, string? Message)
{
    public static DictionaryValidationResult Valid { get; } = new(true, null);
}

public static class DictionaryViewModel
{
    public static DictionaryValidationResult Validate(IReadOnlyList<UserDictionaryEntry> entries)
    {
        if (entries.Count > 50)
        {
            return new DictionaryValidationResult(false, "ユーザー辞書は50件までです。");
        }

        if (entries.Any(entry => string.IsNullOrWhiteSpace(entry.Reading) || string.IsNullOrWhiteSpace(entry.Word)))
        {
            return new DictionaryValidationResult(false, "読みと単語を入力してください。");
        }

        bool hasDuplicate = entries
            .GroupBy(entry => (Reading: entry.Reading.Trim(), Word: entry.Word.Trim()))
            .Any(group => group.Count() > 1);

        return hasDuplicate
            ? new DictionaryValidationResult(false, "同じ読みと単語の組み合わせが重複しています。")
            : DictionaryValidationResult.Valid;
    }
}

public static class ZenzaiSettingsViewModel
{
    public static IReadOnlyList<string> BackendIds { get; } = ["cpu", "vulkan"];

    public static string ResolveSelectedModelId(string? selected)
    {
        if (!string.IsNullOrWhiteSpace(selected) &&
            ZenzaiModelCatalog.Options.Any(model => string.Equals(model.Id, selected, StringComparison.Ordinal)))
        {
            return selected;
        }

        return ZenzaiModelCatalog.DefaultModelId;
    }

    public static string ResolveSelectedBackendId(string? selected)
    {
        return BackendIds.FirstOrDefault(id => string.Equals(id, selected, StringComparison.OrdinalIgnoreCase))
            ?? "cpu";
    }
}
