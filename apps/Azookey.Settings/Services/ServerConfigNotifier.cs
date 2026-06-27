using Azookey;
using Azookey.Core.Ipc;
using Grpc.Net.Client;

namespace Azookey.Settings.Services;

public sealed class ServerConfigNotifier : IServerConfigNotifier
{
    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        using GrpcChannel channel = NamedPipeGrpcClientFactory.CreateChannel("azookey_server");
        var client = new AzookeyService.AzookeyServiceClient(channel);
        await client.UpdateConfigAsync(
            new UpdateConfigRequest { RequestId = 0 },
            cancellationToken: cancellationToken);
    }
}
