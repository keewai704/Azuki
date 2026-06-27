using Azookey.Settings.Pages;
using Xunit;

namespace Azookey.Settings.Tests.ViewModels;

public sealed class SettingsInstallDirectoryTests
{
    [Fact]
    public void ResolveInstallDirectoryKeepsSettingsAppDirectoryAsInstallRoot()
    {
        string installDirectory = Path.Combine("C:", "Users", "test", "AppData", "Roaming", "Azookey");
        string baseDirectory = Path.Combine(installDirectory, "settings-app");

        string resolved = SettingsPageBase.ResolveInstallDirectory(baseDirectory);

        Assert.Equal(baseDirectory, resolved);
    }
}
