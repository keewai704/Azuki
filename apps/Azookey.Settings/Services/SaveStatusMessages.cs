using System;

namespace Azookey.Settings.Services;

public static class SaveStatusMessages
{
    public static string CreateSaveFailedMessage(Exception error) =>
        $"設定の保存に失敗しました: {error.Message}";
}
