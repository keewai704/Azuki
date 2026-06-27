using Azookey.Core.Runtime;
using Xunit;

namespace Azookey.Core.Tests.Runtime;

public sealed class WindowsAppRuntimeCheckerTests
{
    [Fact]
    public void RuntimeIsLikelyInstalledWhenWindowsAppRuntime22RegistryKeyExists()
    {
        Assert.True(WindowsAppRuntimeChecker.IsRuntimeLikelyInstalled(
            registryKeyExists: path => path == @"SOFTWARE\Microsoft\WindowsAppRuntime\2.2",
            localAppData: () => "C:\\Users\\test\\AppData\\Local",
            directoryExists: _ => false,
            windowsVersionIsNewEnough: () => false));
    }

    [Fact]
    public void RuntimeIsNotLikelyInstalledJustBecauseWindowsAppsDirectoryExists()
    {
        Assert.False(WindowsAppRuntimeChecker.IsRuntimeLikelyInstalled(
            registryKeyExists: _ => false,
            localAppData: () => "C:\\Users\\test\\AppData\\Local",
            directoryExists: path => path == "C:\\Users\\test\\AppData\\Local\\Microsoft\\WindowsApps",
            windowsVersionIsNewEnough: () => false));
    }

    [Fact]
    public void RuntimeIsNotLikelyInstalledJustBecauseWindowsVersionIsNewEnough()
    {
        Assert.False(WindowsAppRuntimeChecker.IsRuntimeLikelyInstalled(
            registryKeyExists: _ => false,
            localAppData: () => "C:\\Users\\test\\AppData\\Local",
            directoryExists: _ => false,
            windowsVersionIsNewEnough: () => true));
    }

    [Fact]
    public void RuntimeIsNotLikelyInstalledWhenRegistryKeyIsMissing()
    {
        Assert.False(WindowsAppRuntimeChecker.IsRuntimeLikelyInstalled(
            registryKeyExists: _ => false,
            localAppData: () => "C:\\Users\\test\\AppData\\Local",
            directoryExists: _ => false,
            windowsVersionIsNewEnough: () => false));
    }
}
