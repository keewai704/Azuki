using Xunit;

namespace Azookey.UI.Tests.Candidate;

public sealed class ReadingDisplayRemovalTests
{
    [Fact]
    public void ReadingDisplayWindowIsNotPartOfWinUiRuntime()
    {
        string uiRoot = GetProjectRoot("Azookey.UI");

        Assert.False(File.Exists(Path.Combine(uiRoot, "Windows", "RubyWindow.xaml")));
        Assert.False(File.Exists(Path.Combine(uiRoot, "Windows", "RubyWindow.xaml.cs")));

        Assert.DoesNotContain("RubyWindow", File.ReadAllText(Path.Combine(uiRoot, "App.xaml.cs")));
        Assert.DoesNotContain("RubyWindow", File.ReadAllText(Path.Combine(uiRoot, "UiWindowCoordinator.cs")));
        Assert.DoesNotContain("ShowRuby", File.ReadAllText(Path.Combine(uiRoot, "WindowRenderPlan.cs")));

        string geometrySource = File.ReadAllText(Path.Combine(uiRoot, "Candidate", "WindowGeometry.cs"));
        Assert.DoesNotContain("RubyWindow", geometrySource);
        Assert.DoesNotContain("CandidateWindowPositionWithRubyClearance", geometrySource);
    }

    private static string GetProjectRoot(string projectName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "apps", projectName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate {projectName} from {AppContext.BaseDirectory}.");
    }
}
