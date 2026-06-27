using System.IO;

namespace Azookey.UI;

internal readonly record struct UiHostStartupError(string Message);

internal static class UiHostStartupErrors
{
    private const string PipeName = "azookey_ui";
    private const string AddressInUse = "address already in use";

    public static UiHostStartupError? Classify(Exception exception)
    {
        IOException? ioException = FindIOException(exception);
        if (ioException is null)
        {
            return null;
        }

        if (IsPipeConflict(exception))
        {
            return new UiHostStartupError($"Failed to start ui.exe host because named pipe '{PipeName}' is already in use.");
        }

        return new UiHostStartupError($"Failed to start ui.exe host: {ioException.Message}");
    }

    private static IOException? FindIOException(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is IOException ioException)
            {
                return ioException;
            }
        }

        return null;
    }

    private static bool IsPipeConflict(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().FullName == "Microsoft.AspNetCore.Connections.AddressInUseException")
            {
                return true;
            }

            if (current.Message.Contains(AddressInUse, StringComparison.OrdinalIgnoreCase) ||
                (current.Message.Contains(PipeName, StringComparison.OrdinalIgnoreCase) &&
                 current.Message.Contains("already in use", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
