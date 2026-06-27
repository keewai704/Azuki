using Azookey.Settings.Services;
using Xunit;

namespace Azookey.Settings.Tests.Services;

public sealed class SaveStatusMessagesTests
{
    [Fact]
    public void CreateSaveFailedMessageIncludesWriteFailureReason()
    {
        var error = new InvalidOperationException("write failed");

        string message = SaveStatusMessages.CreateSaveFailedMessage(error);

        Assert.Equal("設定の保存に失敗しました: write failed", message);
    }
}
