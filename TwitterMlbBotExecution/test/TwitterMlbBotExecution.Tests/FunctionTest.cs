using Xunit;
using Amazon.Lambda.TestUtilities;

namespace TwitterMlbBotExecution.Tests;

public class FunctionTest
{
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"source\":\"aws.events\"}")]
    [InlineData("{\"group\":null}")]
    [InlineData("{\"group\":\"east\"}")]
    [InlineData("{\"group\":\"\"}")]
    [InlineData("{\"group\":\"--dry-run\"}")]
    public async Task 不正イベントと旧イベントは外部接続前に拒否する(string json)
    {
        var input = DeserializeEvent(json);
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            new Function().FunctionHandlerAsync(input, new TestLambdaContext()));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"group\":0}")]
    [InlineData("{\"group\":[]}")]
    public void JSONの型が異なるイベントは受け取れない(string json)
    {
        Assert.ThrowsAny<Exception>(() => DeserializeEvent(json));
    }

    [Theory]
    [InlineData("East")]
    [InlineData("Central")]
    [InlineData("West")]
    public void SchedulerのJSONから対象グループを受け取る(string group)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new { group });
        Assert.Equal(group, DeserializeEvent(json)!.Group);
    }

    private static ScheduledEvent? DeserializeEvent(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()
            .Deserialize<ScheduledEvent?>(stream);
    }

    // 本テストはモックではなく本番のProgram.Mainをそのまま実行する（MLB APIコール + 実ツイート投稿）。
    // 認証情報が設定された環境で一括実行すると実際にツイートされてしまうため、Skip指定でCI・ローカルの
    // dotnet test から恒久的に除外する。手動で疎通確認したい場合のみSkipを外して単体実行すること。
    [Fact(Skip = "本番のProgram.Mainを直接実行するため（実ツイートが投稿される）。手動疎通確認専用。")]
    public async Task RunProductionFlow()
    {
        var function = new Function();
        var context = new TestLambdaContext();
        await function.FunctionHandlerAsync(new ScheduledEvent("East"), context);
    }
}
