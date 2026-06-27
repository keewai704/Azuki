using System.IO;
using Azookey.UI;
using Xunit;

namespace Azookey.UI.Tests.Ipc;

public sealed class UiHostStartupErrorsTests
{
    [Fact]
    public void ClassifyReturnsPipeConflictMessageForAddressInUseIoFailure()
    {
        IOException error = new("Failed to bind to address http://pipe:/azookey_ui: address already in use.");

        UiHostStartupError? classified = UiHostStartupErrors.Classify(error);

        Assert.Equal("Failed to start ui.exe host because named pipe 'azookey_ui' is already in use.", classified?.Message);
    }

    [Fact]
    public void ClassifyReturnsGenericIoMessageForOtherIoFailures()
    {
        IOException error = new("The pipe transport failed.");

        UiHostStartupError? classified = UiHostStartupErrors.Classify(error);

        Assert.Equal("Failed to start ui.exe host: The pipe transport failed.", classified?.Message);
    }

    [Fact]
    public void ClassifyReturnsNullForNonIoFailures()
    {
        UiHostStartupError? classified = UiHostStartupErrors.Classify(new InvalidOperationException("boom"));

        Assert.Null(classified);
    }
}
