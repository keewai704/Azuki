using System.Text;

namespace Azookey.Settings.Services;

internal static class StartupDiagnostics
{
    private const string LogFileName = "settings-ui-startup.log";

    public static void Log(string message) => Write(message, null);

    public static void LogException(string message, Exception exception) => Write(message, exception);

    private static void Write(string message, Exception? exception)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                return;
            }

            string logDirectory = Path.Combine(appData, "Azookey", "logs");
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, LogFileName);

            var builder = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(' ')
                .Append(message);

            if (exception is not null)
            {
                builder
                    .AppendLine()
                    .Append(exception);
            }

            File.AppendAllText(logPath, builder.AppendLine().ToString());
        }
        catch
        {
            // Startup diagnostics must never make UI startup less reliable.
        }
    }
}
