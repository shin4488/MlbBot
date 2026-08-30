using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// Stats APIレスポンス解析（MlbStatsApiClient）のテスト
/// 実際のAPIレスポンスと同じ構造のJSONを使い、ネットワークなしで解析仕様を検証する
/// </summary>
public class MlbStatsApiClientTest
{
    [Fact]
    public void ParseSeasonCalendar_レギュラーシーズン終了日が取り出せる()
    {
        // 実レスポンスの抜粋（公開データ。未使用フィールドが混ざっていても解析できることも兼ねて検証）
        string responseBody = """
            {"copyright":"Copyright 2026 MLB","seasons":[{"seasonId":"2026","regularSeasonStartDate":"2026-03-25","regularSeasonEndDate":"2026-09-27","postSeasonEndDate":"2026-10-31"}]}
            """;

        SeasonCalendar calendar = MlbStatsApiClient.ParseSeasonCalendar(responseBody, 2026);

        Assert.Equal(new DateOnly(2026, 9, 27), calendar.RegularSeasonEndDate);
    }

    [Theory]
    [InlineData("""{"seasons":[]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2026"}]}""")]
    public void ParseSeasonCalendar_シーズン情報が欠けていたら例外を投げる(string responseBody)
    {
        // シーズンなし・終了日フィールド欠落のどちらでも、判定不能のまま投稿可否を決めない仕様
        Assert.ThrowsAny<Exception>(
            () => MlbStatsApiClient.ParseSeasonCalendar(responseBody, 2026));
    }
}
