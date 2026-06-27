using Microsoft.Win32;
using System.Security;

namespace Azookey.Core.Runtime;

public static class WindowsAppRuntimeChecker
{
    private const string WindowsAppRuntime22RegistryPath = @"SOFTWARE\Microsoft\WindowsAppRuntime\2.2";

    public static bool IsRuntimeLikelyInstalled() =>
        IsRuntimeLikelyInstalled(RegistryKeyExists);

    internal static bool IsRuntimeLikelyInstalled(
        Func<string, bool> registryKeyExists,
        Func<string> localAppData,
        Func<string, bool> directoryExists,
        Func<bool> windowsVersionIsNewEnough)
    {
        _ = localAppData;
        _ = directoryExists;
        _ = windowsVersionIsNewEnough;
        return IsRuntimeLikelyInstalled(registryKeyExists);
    }

    internal static bool IsRuntimeLikelyInstalled(Func<string, bool> registryKeyExists)
    {
        ArgumentNullException.ThrowIfNull(registryKeyExists);

        return registryKeyExists(WindowsAppRuntime22RegistryPath);
    }

    private static bool RegistryKeyExists(string subKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return RegistryKeyExists(subKey, RegistryView.Registry64)
            || RegistryKeyExists(subKey, RegistryView.Registry32);
    }

    private static bool RegistryKeyExists(string subKey, RegistryView view)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using RegistryKey? runtimeKey = baseKey.OpenSubKey(subKey);
            return runtimeKey is not null;
        }
        catch (Exception error) when (
            error is ArgumentException
            or IOException
            or SecurityException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
