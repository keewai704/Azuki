using System.Xml.Linq;
using Xunit;

namespace Azookey.Core.Tests.Installer;

public sealed class StartupTaskTests
{
    [Fact]
    public void StartupTaskRunsAtLeastPrivilege()
    {
        string repositoryRoot = FindRepositoryRoot();
        string taskPath = Path.Combine(repositoryRoot, "installer", "Azookey Startup.xml");
        XDocument document = XDocument.Parse(File.ReadAllText(taskPath));
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        string? runLevel = document.Root?
            .Element(ns + "Principals")?
            .Element(ns + "Principal")?
            .Element(ns + "RunLevel")?
            .Value;

        Assert.Equal("LeastPrivilege", runLevel);
    }

    [Fact]
    public void InstallerDeletesRemovedReadingWindowResourceOnUpgrade()
    {
        string repositoryRoot = FindRepositoryRoot();
        string installerPath = Path.Combine(repositoryRoot, "installer", "Installer.iss");
        string installerScript = File.ReadAllText(installerPath);

        Assert.Contains(@"Type: files; Name: ""{app}\Windows\RubyWindow.xbf""", installerScript);
    }

    [Fact]
    public void PostBuildDoesNotCopyBundledGguf()
    {
        string repositoryRoot = FindRepositoryRoot();
        string postBuildPath = Path.Combine(repositoryRoot, "scripts", "post-build.ps1");
        string postBuildScript = File.ReadAllText(postBuildPath);

        Assert.DoesNotContain("zenz.gguf", postBuildScript);
    }

    [Fact]
    public void InstallerDeletesBundledRootGgufOnUpgrade()
    {
        string repositoryRoot = FindRepositoryRoot();
        string installerPath = Path.Combine(repositoryRoot, "installer", "Installer.iss");
        string installerScript = File.ReadAllText(installerPath);

        Assert.Contains(@"Type: files; Name: ""{app}\zenz.gguf""", installerScript);
    }

    [Fact]
    public void InstallerBuildPayloadUsesExplicitAllowlist()
    {
        string repositoryRoot = FindRepositoryRoot();
        string installerPath = Path.Combine(repositoryRoot, "installer", "Installer.iss");
        string installerScript = File.ReadAllText(installerPath);

        Assert.DoesNotContain(@"Source: ""../build/*""", installerScript);
        Assert.Contains(@"Source: ""../build/*.exe""", installerScript);
        Assert.Contains(@"Source: ""../build/EngineRuntime/*""", installerScript);
        Assert.Contains(@"#define MySettingsAppName ""settings.exe""", installerScript);
    }

    [Fact]
    public void InstallerChainsDotNetDesktopRuntime10()
    {
        string repositoryRoot = FindRepositoryRoot();
        string installerPath = Path.Combine(repositoryRoot, "installer", "Installer.iss");
        string dependenciesPath = Path.Combine(repositoryRoot, "installer", "CodeDependencies.iss");

        string installerScript = File.ReadAllText(installerPath);
        string dependenciesScript = File.ReadAllText(dependenciesPath);

        Assert.Contains("Dependency_AddDotNet100Desktop;", installerScript);
        Assert.Contains("Microsoft.WindowsDesktop.App', 10, 0, 9", dependenciesScript);
        Assert.Contains("windowsdesktop-runtime-10.0.9-win-x64.exe", dependenciesScript);
    }

    [Fact]
    public void BuildInstallerFindsUserLocalInnoSetupCompiler()
    {
        string repositoryRoot = FindRepositoryRoot();
        string buildCommonPath = Path.Combine(repositoryRoot, "scripts", "build-common.ps1");
        string buildCommonScript = File.ReadAllText(buildCommonPath);

        Assert.Contains("Get-Command iscc", buildCommonScript);
        Assert.Contains("Inno Setup 6", buildCommonScript);
        Assert.Contains("ISCC.exe", buildCommonScript);
    }

    [Fact]
    public void BuildScriptsShareNativeCommandAndInnoSetupHelpers()
    {
        string repositoryRoot = FindRepositoryRoot();
        string commonPath = Path.Combine(repositoryRoot, "scripts", "build-common.ps1");
        string commonScript = File.ReadAllText(commonPath);

        Assert.Contains("function Invoke-Native", commonScript);
        Assert.Contains("function Resolve-InnoSetupCompiler", commonScript);

        foreach (string scriptName in new[] { "build-winui.ps1", "post-build.ps1", "build-installer.ps1" })
        {
            string script = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", scriptName));

            Assert.Contains(". $PSScriptRoot/build-common.ps1", script);
        }
    }

    [Fact]
    public void WorkflowFetchesSwiftModulemapThroughGitHubApi()
    {
        string repositoryRoot = FindRepositoryRoot();
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "actions.yml");
        string workflow = File.ReadAllText(workflowPath);

        Assert.Contains("https://api.github.com/gists/ef8be2217082302b291f2b8d4178194a", workflow);
        Assert.Contains("files.'ucrt.modulemap'.content", workflow);
        Assert.DoesNotContain("gist.githubusercontent.com/fkunn1326/ef8be2217082302b291f2b8d4178194a", workflow);
    }

    [Fact]
    public void RetiredTauriFrontendIsNotInstalledInCiOrVmBuild()
    {
        string repositoryRoot = FindRepositoryRoot();
        string workflowPath = Path.Combine(repositoryRoot, ".github", "workflows", "actions.yml");
        string vmBuildPath = Path.Combine(repositoryRoot, "scripts", "vm_build.sh");

        string workflow = File.ReadAllText(workflowPath);
        string vmBuildScript = File.ReadAllText(vmBuildPath);

        Assert.DoesNotContain("Install frontend dependencies", workflow);
        Assert.DoesNotContain("frontend/package-lock.json", workflow);
        Assert.DoesNotContain("npm ci", workflow);
        Assert.DoesNotContain("npm ci", vmBuildScript);
        Assert.DoesNotContain("Set-Location (Join-Path $SourceDir \"frontend\")", vmBuildScript);
    }

    [Fact]
    public void InstallerDoesNotCarryRetiredWebView2Dependency()
    {
        string repositoryRoot = FindRepositoryRoot();
        string installerPath = Path.Combine(repositoryRoot, "installer", "Installer.iss");
        string dependenciesPath = Path.Combine(repositoryRoot, "installer", "CodeDependencies.iss");

        string installerScript = File.ReadAllText(installerPath);
        string dependenciesScript = File.ReadAllText(dependenciesPath);

        Assert.DoesNotContain("Dependency_AddWebView2", installerScript);
        Assert.DoesNotContain("Dependency_AddWebView2", dependenciesScript);
        Assert.DoesNotContain("MicrosoftEdgeWebview2Setup", dependenciesScript);
    }

    [Fact]
    public void ManualInstallStagingDoesNotRequireBundledGguf()
    {
        string repositoryRoot = FindRepositoryRoot();
        string stageScriptPath = Path.Combine(repositoryRoot, "scripts", "vm_stage_for_manual_test.sh");
        string stageScript = File.ReadAllText(stageScriptPath);

        Assert.DoesNotContain("\"zenz.gguf\"", stageScript);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "apps", "Azookey.WinUI.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
