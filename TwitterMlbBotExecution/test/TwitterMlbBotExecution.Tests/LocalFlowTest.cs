using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TwitterMlbBot;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// HTTP通信だけを差し替え、実際の取得クライアントからドライラン出力までを通す。
/// HttpClientにはスタブしか接続しないため、認証情報や外部ネットワークを使わない。
/// </summary>
public class LocalFlowTest
{
    private const string calendarJson = """{"seasons":[{"seasonId":"2026","regularSeasonEndDate":"2026-09-27"}]}""";

    private static readonly TeamStanding[] teams = (
        from league in new[] { "AL", "NL" }
        from division in new[] { "East", "Central", "West" }
        from index in Enumerable.Range(0, 5)
        select new TeamStanding($"{league}-{division}-{index}", league, division, 90 - index, 50 + index)
    ).ToArray();

    private static string StandingsJson => JsonSerializer.Serialize(teams.Concat(new[]
    {
        new TeamStanding("AL All-Stars", "AL", "AL", 0, 0),
        new TeamStanding("NL All-Stars", "NL", "NL", 0, 0),
    }));

    [Theory]
    [InlineData(7, 31, 6)]
    [InlineData(8, 1, 8)]
    [InlineData(9, 27, 8)]
    [InlineData(9, 28, 0)]
    public async Task 日程と順位を取得して当日分の文面をドライラン出力する(int month, int day, int expectedCount)
    {
        var date = new DateOnly(2026, month, day);
        using var output = new StringWriter();
        var sender = new RecordingDryRunSender(output);
        using var client = CreateClient(calendarJson, StandingsJson, allowStandings: expectedCount > 0);

        await CreateRunner(client, sender).RunAsync(2026, date);

        Assert.Equal(expectedCount, sender.Contents.Count);
        Assert.All(sender.Contents, content => Assert.Contains(content.Text, output.ToString()));
        if (expectedCount == 0)
        {
            Assert.Empty(output.ToString());
            return;
        }
        Assert.All(teams, team => Assert.Contains(team.Name, output.ToString()));
        Assert.DoesNotContain("All-Stars", output.ToString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public async Task APIが空順位を返した場合は何も出力しない(int month)
    {
        using var output = new StringWriter();
        using var client = CreateClient(calendarJson, "[]");

        await CreateRunner(client, new DryRunTweetSender(output)).RunAsync(2026, new DateOnly(2026, month, 1));

        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData(2, 28, 0)]
    [InlineData(3, 1, 6)]
    [InlineData(10, 31, 8)]
    [InlineData(11, 1, 0)]
    public async Task 日程の通信障害時は時期に応じて続行か見送りを決める(int month, int day, int expectedCount)
    {
        using var output = new StringWriter();
        var sender = new RecordingDryRunSender(output);
        using var client = CreateClient(null, StandingsJson, allowStandings: expectedCount > 0);

        await CreateRunner(client, sender).RunAsync(2026, new DateOnly(2026, month, day));

        Assert.Equal(expectedCount, sender.Contents.Count);
    }

    private static BotRunner CreateRunner(HttpClient client, ITweetSender sender) => new(
        new MlbStatsApiClient(client, NullLogger<MlbStatsApiClient>.Instance),
        new MlbApiClient(client, "dummy-mlb-key", NullLogger<MlbApiClient>.Instance),
        new TweetComposer(new HashtagProvider()), sender, NullLogger<BotRunner>.Instance);

    private static HttpClient CreateClient(string? calendar, string standings, bool allowStandings = true) =>
        new(new StubHttpMessageHandler(async request =>
        {
            // await後の例外も、同期的な例外と同じ方針で扱われることを確認する。
            await Task.Yield();
            Assert.Equal(HttpMethod.Get, request.Method);
            switch (request.RequestUri!.Host)
            {
                case "statsapi.mlb.com":
                    Assert.False(request.Headers.Contains("Ocp-Apim-Subscription-Key"));
                    if (calendar is null)
                    {
                        throw new HttpRequestException("テスト用の日程通信障害");
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(calendar) };
                case "api.sportsdata.io":
                    Assert.True(allowStandings, "投稿を見送る日は順位APIを消費しない");
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(standings) };
                default:
                    throw new InvalidOperationException("テストで想定していない接続先です。");
            }
        }));

    private sealed class RecordingDryRunSender(TextWriter output) : ITweetSender
    {
        private readonly DryRunTweetSender sender = new(output);
        public List<TweetContent> Contents { get; } = new();

        public Task<bool> SendAsync(TweetContent content)
        {
            Contents.Add(content);
            return sender.SendAsync(content);
        }
    }
}
