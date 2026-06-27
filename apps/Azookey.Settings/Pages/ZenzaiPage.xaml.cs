using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using Azookey.Core.Config;
using Azookey.Settings.Services;
using Azookey.Settings.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Azookey.Settings.Pages;

public sealed partial class ZenzaiPage : SettingsPageBase
{
    public ZenzaiPage()
    {
        InitializeComponent();
    }

    protected override void LoadFromState()
    {
        EnsureOptions(
            zenzaiModelBox,
            ZenzaiModelCatalog.Options
                .Select(model => CreateOption(model.DisplayName, model.Id))
                .ToList());
        EnsureOptions(
            zenzaiBackendBox,
            ZenzaiSettingsViewModel.BackendIds
                .Select(id => CreateOption(id == "cpu" ? "CPU" : "Vulkan", id))
                .ToList());

        zenzaiEnableSwitch.IsOn = State.Config.Zenzai.Enable;
        SelectComboTag(zenzaiModelBox, ZenzaiSettingsViewModel.ResolveSelectedModelId(State.Config.Zenzai.ModelId));
        SelectComboTag(zenzaiBackendBox, ZenzaiSettingsViewModel.ResolveSelectedBackendId(State.Config.Zenzai.Backend));
        zenzaiProfileBox.Text = State.Config.Zenzai.Profile;

        cpuAvailabilityText.Text = Availability(X86Base.IsSupported && Avx.IsSupported);
        vulkanAvailabilityText.Text = Availability(HasDll("vulkan-1.dll"));
    }

    private async void OnZenzaiSettingChanged(object sender, RoutedEventArgs e)
    {
        await SaveZenzaiAsync();
    }

    private async void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await SaveZenzaiAsync();
    }

    private async void OnBackendSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await SaveZenzaiAsync();
    }

    private async void OnProfileLostFocus(object sender, RoutedEventArgs e)
    {
        await SaveZenzaiAsync();
    }

    private async Task SaveZenzaiAsync()
    {
        if (IsLoading)
        {
            return;
        }

        AppConfig previous = State.Config;
        AppConfig next = previous with
        {
            Zenzai = previous.Zenzai with
            {
                Enable = zenzaiEnableSwitch.IsOn,
                Profile = zenzaiProfileBox.Text,
                Backend = ZenzaiSettingsViewModel.ResolveSelectedBackendId(GetSelectedTag(zenzaiBackendBox, previous.Zenzai.Backend)),
                ModelId = GetSelectedTag(zenzaiModelBox, previous.Zenzai.ModelId)
            }
        };

        var result = await SaveConfigAsync(_ => next);
        bool restartRequired = result?.Saved == true &&
            (!string.Equals(previous.Zenzai.ModelId, next.Zenzai.ModelId, StringComparison.Ordinal) ||
                !string.Equals(previous.Zenzai.Backend, next.Zenzai.Backend, StringComparison.Ordinal));
        if (restartRequired)
        {
            await RestartServerAsync();
        }
    }

    private static bool HasDll(string name)
    {
        return GetSearchDirectories().Any(directory => File.Exists(Path.Combine(directory, name)));
    }

    private static IEnumerable<string> GetSearchDirectories()
    {
        yield return AppContext.BaseDirectory;

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return directory.Trim();
        }
    }

    private static string Availability(bool available) => available ? "利用可能" : "利用不可";
}
