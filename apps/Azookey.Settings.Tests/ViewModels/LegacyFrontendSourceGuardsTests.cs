using Xunit;

namespace Azookey.Settings.Tests.ViewModels;

public sealed class LegacyFrontendSourceGuardsTests
{
    [Fact]
    public void LegacyFrontendSourcesDoNotExposeForbiddenSettings()
    {
        string source = string.Join(
            Environment.NewLine,
            GetFrontendSourcePaths().Select(File.ReadAllText));

        string lower = string.Concat("cu", "da");
        string upper = string.Concat("CU", "DA");

        foreach (string forbidden in new[]
        {
            string.Concat("capability", ".", lower),
            string.Concat("show_", "live_", "conversion_", "reading"),
            string.Concat("live_", "conversion_", "reading_", "vertical_", "adjustment"),
            string.Concat(upper, " ", "("),
            string.Concat(upper, " ", "Tool", "kit"),
            string.Concat("cu", "dart64", "_12"),
            string.Concat("cu", "blas64", "_12"),
            string.Concat("\u30E9\u30A4\u30D6\u5909\u63DB\u4E2D", "\u306E\u8AAD\u307F"),
            string.Concat("\u8AAD\u307F\u8868\u793A", "\u306E\u9AD8\u3055")
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> GetFrontendSourcePaths()
    {
        string root = GetRepositoryRoot();

        yield return Path.Combine(root, "frontend", "src", "pages", "zenzai.tsx");
        yield return Path.Combine(root, "frontend", "src", "pages", "general.tsx");
        yield return Path.Combine(root, "frontend", "src-tauri", "src", "lib.rs");
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "frontend");
            if (Directory.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate the repository root from {AppContext.BaseDirectory}.");
    }
}
