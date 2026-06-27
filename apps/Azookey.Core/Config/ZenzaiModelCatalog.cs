namespace Azookey.Core.Config;

public sealed record ZenzaiModelOption(
    string Id,
    string DisplayName,
    string Repository,
    string FileName,
    long ExpectedSizeBytes,
    string Sha256)
{
    public string Url => $"https://huggingface.co/{Repository}/resolve/main/{FileName}";
}

public static class ZenzaiModelCatalog
{
    public const string DefaultModelId = "zenz-v3.2-small-q5-k-m";

    public static IReadOnlyList<ZenzaiModelOption> Options { get; } =
    [
        new(
            "zenz-v3.2-small-q5-k-m",
            "Zenz v3.2 small (Q5_K_M)",
            "Miwa-Keita/zenz-v3.2-small-gguf",
            "ggml-model-Q5_K_M.gguf",
            73_871_936,
            "29c223d4c23327b80fd13ebb5ab2555057a46317997d5da391584ffbef0db673"),
        new(
            "zenz-v3.1-small-q5-k-m",
            "Zenz v3.1 small (Q5_K_M)",
            "Miwa-Keita/zenz-v3.1-small-gguf",
            "ggml-model-Q5_K_M.gguf",
            73_871_968,
            "4de930c06bef8c263aa1aa40684af206db4ce1b96375b3b8ed0ea508e0b14f6c"),
        new(
            "zenz-v3-small-q5-k-m",
            "Zenz v3 small (Q5_K_M)",
            "Miwa-Keita/zenz-v3-small-gguf",
            "ggml-model-Q5_K_M.gguf",
            72_298_816,
            "501f605d088f5b988791a00ae19ed46985ed7c48144f364b2f3f1f951c9b2083"),
        new(
            "zenz-v2-q5-k-m",
            "Zenz v2 (Q5_K_M)",
            "Miwa-Keita/zenz-v2-gguf",
            "zenz-v2-Q5_K_M.gguf",
            72_298_816,
            "22b8d8190bba8c9fec075ffb5b323b0f0d65c7c5f5ff82011799a0c3049d9662")
    ];

    public static string ResolveModelId(string? modelId) =>
        Options.Any(option => string.Equals(option.Id, modelId, StringComparison.Ordinal))
            ? modelId!
            : DefaultModelId;

    public static string? ResolveExistingModelPath(string configRoot, string? modelId)
    {
        string resolvedId = ResolveModelId(modelId);
        ZenzaiModelOption model = Options.First(option => option.Id == resolvedId);
        string path = Path.Combine(configRoot, "models", model.Id, model.FileName);
        return File.Exists(path) ? path : null;
    }
}
