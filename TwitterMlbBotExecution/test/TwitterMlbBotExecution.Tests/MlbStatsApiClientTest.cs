using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// Stats APIの日程取得（MlbStatsApiClient）のテスト
/// HTTP応答だけを差し替え、実際のAPIレスポンスと同じ構造のJSONから解析仕様を検証する。
/// 日程を使えない応答はMlbApiExceptionとして伝え、BotRunnerが日程の取得失敗として時期に応じて続行か見送りを決める
/// </summary>
public class MlbStatsApiClientTest
{
    [Fact]
    public async Task GetSeasonCalendarAsync_応答順によらず対象シーズンの日程を選ぶ()
    {
        const string responseBody = """
            {"seasons":[
                {"seasonId":"2025","regularSeasonEndDate":"2025-09-28"},
                {"seasonId":"2026","regularSeasonEndDate":"2026-09-27"}
            ]}
            """;

        SeasonCalendar calendar = await GetSeasonCalendarAsync(2026, responseBody);

        Assert.Equal(new DateOnly(2026, 9, 27), calendar.RegularSeasonEndDate);
    }

    [Theory]
    [InlineData("""{"seasons":[{"regularSeasonEndDate":"2026-09-27"}]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2025","regularSeasonEndDate":"2026-09-27"}]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2026","regularSeasonEndDate":"2026-09-27"},{"seasonId":"2026","regularSeasonEndDate":"2026-10-01"}]}""")]
    public async Task GetSeasonCalendarAsync_対象シーズンを特定できない日程は使わない(string responseBody)
    {
        await Assert.ThrowsAsync<MlbApiException>(() => GetSeasonCalendarAsync(2026, responseBody));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"seasons":null}""")]
    [InlineData("""{"seasons":[null]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2026","regularSeasonEndDate":"2026-02-30"}]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2026","regularSeasonEndDate":"2025-09-27"}]}""")]
    [InlineData("not-json")]
    public async Task GetSeasonCalendarAsync_不正な日程や対象年と異なる日程は使わない(string responseBody)
    {
        await Assert.ThrowsAsync<MlbApiException>(() => GetSeasonCalendarAsync(2026, responseBody));
    }

    [Fact]
    public async Task GetSeasonCalendarAsync_レギュラーシーズン終了日が取り出せる()
    {
        // 実レスポンスの抜粋（公開データ。未使用フィールドが混ざっていても解析できることも兼ねて検証）
        string responseBody = """
            {"copyright":"Copyright 2026 MLB","seasons":[{"seasonId":"2026","regularSeasonStartDate":"2026-03-25","regularSeasonEndDate":"2026-09-27","postSeasonEndDate":"2026-10-31"}]}
            """;

        SeasonCalendar calendar = await GetSeasonCalendarAsync(2026, responseBody);

        Assert.Equal(new DateOnly(2026, 9, 27), calendar.RegularSeasonEndDate);
    }

    [Theory]
    [InlineData("""{"seasons":[]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2026"}]}""")]
    public async Task GetSeasonCalendarAsync_シーズン情報が欠けていたら例外を投げる(string responseBody)
    {
        // シーズンなし・終了日フィールド欠落のどちらでも、判定不能のまま投稿可否を決めない仕様
        await Assert.ThrowsAsync<MlbApiException>(() => GetSeasonCalendarAsync(2026, responseBody));
    }

    private static async Task<SeasonCalendar> GetSeasonCalendarAsync(int year, string responseBody)
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody) })));
        return await new MlbStatsApiClient(client, NullLogger<MlbStatsApiClient>.Instance).GetSeasonCalendarAsync(year);
    }
}
