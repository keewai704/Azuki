using System.Text;
using Azookey.Core.Process;
using Xunit;

namespace Azookey.Core.Tests.Process;

public sealed class LauncherClientTests
{
    [Theory]
    [InlineData("ok", true)]
    [InlineData("OK\r\n", true)]
    [InlineData("error: restart failed", false)]
    [InlineData("unexpected", false)]
    [InlineData("", false)]
    public void ParsesLauncherRestartResponse(string response, bool expected)
    {
        Assert.Equal(expected, LauncherClient.LauncherRestartSucceeded(response));
    }

    [Fact]
    public void UsesExistingLauncherPipeContract()
    {
        Assert.Equal("azookey_launcher", LauncherClient.PipeName);
        Assert.Equal("restart-server\n", Encoding.UTF8.GetString(LauncherClient.RestartCommandBytes));
    }
}
