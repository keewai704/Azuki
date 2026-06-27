using System.Text.Json;

namespace Azookey.Core.Config;

public sealed record ConfigRecovery(string OriginalPath, string BackupPath);

public sealed record ConfigLoadResult(AppConfig Config, ConfigRecovery? Recovery, Exception? RewriteError);

public sealed class ConfigStore : IConfigStore
{
    public const string SettingsFileName = "settings.json";

    private readonly string configRoot;

    public ConfigStore(string configRoot) => this.configRoot = configRoot;

    public static ConfigStore FromAppData()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException("APPDATA is not set");
        }

        return new ConfigStore(Path.Combine(appData, "Azookey"));
    }

    public ConfigLoadResult LoadWithRecovery()
    {
        Directory.CreateDirectory(configRoot);
        string configPath = Path.Combine(configRoot, SettingsFileName);
        AppConfig config;
        ConfigRecovery? recovery = null;

        if (!File.Exists(configPath))
        {
            config = AppConfig.CreateDefault();
        }
        else
        {
            try
            {
                config = AppConfig.Deserialize(File.ReadAllText(configPath));
            }
            catch (JsonException)
            {
                string backupPath = BackupCorruptedConfig(configPath);
                recovery = new ConfigRecovery(configPath, backupPath);
                config = AppConfig.CreateDefault();
            }
        }

        Exception? rewriteError = null;
        try
        {
            Write(config);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            rewriteError = error;
        }

        return new ConfigLoadResult(config, recovery, rewriteError);
    }

    public void Write(AppConfig config)
    {
        Directory.CreateDirectory(configRoot);
        string configPath = Path.Combine(configRoot, SettingsFileName);
        string tempPath = Path.Combine(
            configRoot,
            $"{SettingsFileName}.tmp-{Environment.ProcessId}-{DateTime.Now:yyyyMMddHHmmssffffff}");

        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(config, AzookeyJson.Options));
            File.Move(tempPath, configPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string BackupCorruptedConfig(string configPath)
    {
        string parent = Path.GetDirectoryName(configPath)!;
        string baseName = $"{SettingsFileName}.broken-{DateTime.Now:yyyyMMddHHmmss}";

        for (int index = 0; index < 1000; index++)
        {
            string suffix = index == 0 ? "" : $"-{index}";
            string candidate = Path.Combine(parent, baseName + suffix);
            if (File.Exists(candidate))
            {
                continue;
            }

            File.Move(configPath, candidate);
            return candidate;
        }

        string overflow = Path.Combine(parent, baseName + "-overflow");
        File.Move(configPath, overflow);
        return overflow;
    }
}
