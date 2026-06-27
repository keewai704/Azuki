namespace Azookey.Core.Config;

public interface IConfigStore
{
    ConfigLoadResult LoadWithRecovery();

    void Write(AppConfig config);
}
