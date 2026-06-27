using Xunit;

namespace Azookey.Settings.Tests.ViewModels;

public sealed class SettingsWindowTextTests
{
    private static readonly string[] MojibakeMarkers =
    [
        "\u7E3A",
        "\u7E5D",
        "\u873F",
        "\u8373",
        "\u96AA",
        "\u8B41",
        "\u879F",
        "\u90B1",
        "\u9B06",
        "\u9015",
        "\u7E67",
        "\u90E2"
    ];

    [Fact]
    public void NavigationUsesReadableJapaneseLabels()
    {
        string xaml = File.ReadAllText(GetSourcePath("MainWindow.xaml"));
        string source = GetCombinedSettingsSource();
        string forbiddenUpper = string.Concat("CU", "DA");
        string forbiddenLower = string.Concat("cu", "da");

        Assert.Contains("<NavigationView", xaml);
        Assert.Contains("x:Name=\"StatusInfoBar\"", xaml);
        Assert.Contains("x:Name=\"ContentFrame\"", xaml);
        Assert.Contains("Content=\"\u4E00\u822C\"", xaml);
        Assert.Contains("Content=\"\u5165\u529B\"", xaml);
        Assert.Contains("Content=\"\u5019\u88DC\"", xaml);
        Assert.Contains("Content=\"Zenzai\"", xaml);
        Assert.Contains("Content=\"\u30E6\u30FC\u30B6\u30FC\u8F9E\u66F8\"", xaml);
        Assert.Contains("Content=\"\u30C7\u30D0\u30C3\u30B0\"", xaml);
        Assert.Contains("Content=\"\u60C5\u5831\"", xaml);

        foreach (string marker in MojibakeMarkers)
        {
            Assert.DoesNotContain(marker, xaml);
            Assert.DoesNotContain(marker, source);
        }

        Assert.DoesNotContain(forbiddenUpper, source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("llama_", forbiddenLower), source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsPagesExistAsDedicatedPageFiles()
    {
        string pagesDirectory = GetDirectoryPath("Pages");

        foreach (string pageName in new[]
        {
            "GeneralPage",
            "InputPage",
            "CandidatePage",
            "ZenzaiPage",
            "UserDictionaryPage",
            "DebugPage",
            "InfoPage"
        })
        {
            Assert.True(
                File.Exists(Path.Combine(pagesDirectory, $"{pageName}.xaml")),
                $"{pageName}.xaml should exist.");
            Assert.True(
                File.Exists(Path.Combine(pagesDirectory, $"{pageName}.xaml.cs")),
                $"{pageName}.xaml.cs should exist.");
        }
    }

    [Fact]
    public void GeneralPageDoesNotExposeLiveReadingDisplayControls()
    {
        string source = GetCombinedSettingsSource();

        Assert.DoesNotContain("showLiveConversionReadingSwitch", source);
        Assert.DoesNotContain("readingAdjustmentSlider", source);
        Assert.DoesNotContain("ShowLiveConversionReading", source);
        Assert.DoesNotContain("LiveConversionReadingVerticalAdjustment", source);
        Assert.DoesNotContain("OnReadingAdjustmentChanged", source);
    }

    [Fact]
    public void GeneralPageUsesJapaneseShortcutLabels()
    {
        string source = File.ReadAllText(GetSourcePath(Path.Combine("Pages", "GeneralPage.xaml")));

        Assert.Contains("Header=\"\u30B3\u30F3\u30C8\u30ED\u30FC\u30EB + \u30B9\u30DA\u30FC\u30B9\"", source);
        Assert.Contains("Header=\"\u30AA\u30EB\u30C8 + \u30D0\u30C3\u30AF\u30AF\u30A9\u30FC\u30C8\"", source);
        Assert.Contains("Header=\"\u82F1\u6570\u30AD\u30FC\"", source);
        Assert.DoesNotContain("Header=\"Ctrl + Space\"", source);
        Assert.DoesNotContain("Header=\"Alt + `\"", source);
    }

    [Fact]
    public void ZenzaiPageShowsJapaneseModelSelector()
    {
        string source = GetCombinedSettingsSource();

        Assert.Contains("zenzaiModelBox", source);
        Assert.Contains("\"\u30E2\u30C7\u30EB\"", source);
        Assert.Contains("ZenzaiModelCatalog.Options", source);
        Assert.Contains("CreateOption(model.DisplayName, model.Id)", source);
        Assert.Contains("ModelId = GetSelectedTag(zenzaiModelBox", source);
    }

    [Fact]
    public void ZenzaiPageDoesNotExposeCudaBackendOrCapability()
    {
        string source = GetCombinedSettingsSource();
        string forbiddenLower = string.Concat("cu", "da");

        Assert.DoesNotContain(forbiddenLower, source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZenzaiModelOrBackendSelectionRequestsServerRestart()
    {
        string source = GetCombinedSettingsSource();

        Assert.Contains("protected async Task RestartServerAsync()", source);
        Assert.Contains("bool restartRequired =", source);
        Assert.Contains("previous.Zenzai.ModelId", source);
        Assert.Contains("previous.Zenzai.Backend", source);
        Assert.Contains("await RestartServerAsync();", source);
    }

    [Fact]
    public void MainWindowUsesInfoBarStatusMethod()
    {
        string source = File.ReadAllText(GetSourcePath("MainWindow.xaml.cs"));

        Assert.Contains("public void ShowStatus(string message)", source);
        Assert.Contains("StatusInfoBar.Message = message;", source);
        Assert.Contains("StatusInfoBar.IsOpen = true;", source);
    }

    [Fact]
    public void WinUiBuildCopiesAppPriResourcesWithXamlArtifacts()
    {
        string script = File.ReadAllText(GetRepositoryPath(Path.Combine("scripts", "build-winui.ps1")));

        Assert.Contains("\"*.xbf\"", script);
        Assert.Contains("\"*.pri\"", script);
    }

    [Fact]
    public void AppMergesWinUiControlsResources()
    {
        string xaml = File.ReadAllText(GetSourcePath("SettingsApp.xaml"));

        Assert.Contains("ResourceDictionary.MergedDictionaries", xaml);
        Assert.Contains("XamlControlsResources", xaml);
    }

    private static string GetSourcePath(string fileName)
    {
        string settingsDirectory = GetDirectoryPath("");
        return Path.Combine(settingsDirectory, fileName);
    }

    private static string GetCombinedSettingsSource()
    {
        return string.Join(
            Environment.NewLine,
            EnumerateRelevantSettingsFiles().Select(File.ReadAllText));
    }

    private static IEnumerable<string> EnumerateRelevantSettingsFiles()
    {
        string settingsDirectory = GetDirectoryPath("");

        foreach (string path in Directory.EnumerateFiles(settingsDirectory, "*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            yield return path;
        }
    }

    private static string GetDirectoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "apps", "Azookey.Settings", relativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate apps/Azookey.Settings/{relativePath} from {AppContext.BaseDirectory}.");
    }

    private static string GetRepositoryPath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from {AppContext.BaseDirectory}.");
    }
}
