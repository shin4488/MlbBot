using Xunit;
using Amazon.Lambda.TestUtilities;

namespace TwitterMlbBotExecution.Tests;

public class FunctionTest
{
    // 本テストはモックではなく本番のProgram.Mainをそのまま実行する（MLB APIコール + 実ツイート投稿）。
    // 認証情報が設定された環境で一括実行すると実際にツイートされてしまうため、Skip指定でCI・ローカルの
    // dotnet test から恒久的に除外する。手動で疎通確認したい場合のみSkipを外して単体実行すること。
    [Fact(Skip = "本番のProgram.Mainを直接実行するため（実ツイートが投稿される）。手動疎通確認専用。")]
    public async Task RunProductionFlow()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        await function.FunctionHandlerAsync("manual smoke test", context);
    }
}
