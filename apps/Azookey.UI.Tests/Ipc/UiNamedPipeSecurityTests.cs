using System.IO.Pipes;
using System.Security.AccessControl;
using Azookey.UI.Ipc;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Xunit;

namespace Azookey.UI.Tests.Ipc;

public sealed class UiNamedPipeSecurityTests
{
    [Fact]
    public void CreateGrantsAccessToBuiltInUsers()
    {
        PipeSecurity security = UiNamedPipeSecurity.Create();
        string sddl = security.GetSecurityDescriptorSddlForm(AccessControlSections.All);

        Assert.Contains("(A;;GA;;;BU)", sddl);
    }

    [Fact]
    public async Task CreateServerStreamAllowsLocalClientConnection()
    {
        string pipeName = $"azookey_ui_test_{Guid.NewGuid():N}";
        CreateNamedPipeServerStreamContext context = new()
        {
            NamedPipeEndPoint = new NamedPipeEndPoint(pipeName),
            PipeOptions = PipeOptions.Asynchronous,
        };

        using NamedPipeServerStream server = UiNamedPipeServerStreamFactory.Create(context);
        using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        Task connectServer = server.WaitForConnectionAsync();
        client.Connect(1000);
        Task completedTask = await Task.WhenAny(connectServer, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.Same(connectServer, completedTask);
        Assert.True(server.IsConnected);
        Assert.True(client.IsConnected);
    }
}
