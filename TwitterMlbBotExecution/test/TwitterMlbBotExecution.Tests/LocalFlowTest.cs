using System.Net;
using System.Text.Json.Nodes;
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

    private static IReadOnlyList<TeamStanding> teams => StandingsFixture.Teams;

    private static string StandingsJson
    {
        get
        {
            // 実際の応答には、30球団に加えてリーグ名と地区名が同一のAll-Star用の擬似チームが含まれる
            JsonArray response = StandingsFixture.CreateResponse();
            response.Add(JsonNode.Parse("""{"Name":"AL All-Stars","League":"AL","Division":"AL","Wins":0,"Losses":0}"""));
            response.Add(JsonNode.Parse("""{"Name":"NL All-Stars","League":"NL","Division":"NL","Wins":0,"Losses":0}"""));
            return response.ToJsonString();
        }
    }

    [Theory]
    [InlineData(7, 31, 2)]
    [InlineData(8, 1, 4)]
    [InlineData(9, 27, 4)]
    [InlineData(9, 28, 0)]
    public async Task 日程と順位を取得して当日分の文面をドライラン出力する(int month, int day, int expectedCount)
    {
        var date = new DateOnly(2026, month, day);
        using var output = new StringWriter();
        var sender = new RecordingDryRunSender(output);
        using var client = CreateClient(calendarJson, StandingsJson, allowStandings: expectedCount > 0);

        await CreateRunner(client, sender).RunAsync(2026, date, PostingGroup.West);

        Assert.Equal(expectedCount, sender.Contents.Count);
        Assert.All(sender.Contents, content => Assert.Contains(content.Text, output.ToString()));
        if (expectedCount == 0)
        {
            Assert.Empty(output.ToString());
            return;
        }
        Assert.All(teams.Where(team => team.Division == "West"), team => Assert.Contains(team.Name, output.ToString()));
        Assert.DoesNotContain("All-Stars", output.ToString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public async Task APIが空順位を返した場合は何も出力しない(int month)
    {
        using var output = new StringWriter();
        using var client = CreateClient(calendarJson, "[]");

        await CreateRunner(client, new DryRunTweetSender(output)).RunAsync(2026, new DateOnly(2026, month, 1), PostingGroup.West);

        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData("null")]
    // 正しいチームが先にあっても、不完全な順位を一部だけ投稿しない。
    [InlineData("""[{"Name":"Example","League":"AL","Division":"East","Wins":80,"Losses":60},{}]""")]
    public async Task 不正な順位応答の場合は何も出力せず失敗する(string standingsJson)
    {
        using var output = new StringWriter();
        using var client = CreateClient(calendarJson, standingsJson);

        await Assert.ThrowsAnyAsync<Exception>(() => CreateRunner(client, new DryRunTweetSender(output))
            .RunAsync(2026, new DateOnly(2026, 8, 1), PostingGroup.West));

        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData("球団欠落")]
    [InlineData("リーグ欠落")]
    [InlineData("球団重複")]
    [InlineData("同数のまま球団重複")]
    [InlineData("未知のリーグ")]
    [InlineData("未知の地区")]
    [InlineData("地区の球団数が不均等")]
    public async Task 球団構成が不完全な順位表は一部も出力しない(string defect)
    {
        JsonArray response = JsonNode.Parse(StandingsJson)!.AsArray();
        switch (defect)
        {
            case "球団欠落": response.RemoveAt(0); break;
            case "リーグ欠落":
                foreach (JsonNode? row in response.Where(row => row!["League"]!.GetValue<string>() == "NL").ToArray())
                {
                    response.Remove(row);
                }
                break;
            case "球団重複": response.Add(response[0]!.DeepClone()); break;
            case "同数のまま球団重複": response[1] = response[0]!.DeepClone(); break;
            case "未知のリーグ": response[0]!["League"] = "UNKNOWN"; break;
            case "未知の地区": response[0]!["Division"] = "UNKNOWN"; break;
            case "地区の球団数が不均等": response[0]!["Division"] = "West"; break;
        }
        using var output = new StringWriter();
        using var client = CreateClient(calendarJson, response.ToJsonString());

        await Assert.ThrowsAnyAsync<Exception>(() => CreateRunner(client, new DryRunTweetSender(output))
            .RunAsync(2026, new DateOnly(2026, 8, 1), PostingGroup.West));

        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData(2, 28, 0)]
    [InlineData(3, 1, 2)]
    [InlineData(10, 31, 4)]
    [InlineData(11, 1, 0)]
    public async Task 日程の通信障害時は時期に応じて続行か見送りを決める(int month, int day, int expectedCount)
    {
        using var output = new StringWriter();
        var sender = new RecordingDryRunSender(output);
        using var client = CreateClient(null, StandingsJson, allowStandings: expectedCount > 0);

        await CreateRunner(client, sender).RunAsync(2026, new DateOnly(2026, month, day), PostingGroup.West);

        Assert.Equal(expectedCount, sender.Contents.Count);
    }

    [Theory]
    [InlineData("""{"seasons":[{"seasonId":"2026","regularSeasonEndDate":"2025-09-27"}]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2025","regularSeasonEndDate":"2026-09-27"}]}""")]
    [InlineData("""{"seasons":[{"seasonId":"2026","regularSeasonEndDate":"2026-06-30"},{"seasonId":"2026","regularSeasonEndDate":"2026-09-27"}]}""")]
    public async Task 対象シーズンの日程が不確かなときは終了済みと誤判定せず取得失敗として扱う(string calendar)
    {
        using var output = new StringWriter();
        using var client = CreateClient(calendar, StandingsJson);

        await CreateRunner(client, new DryRunTweetSender(output)).RunAsync(2026, new DateOnly(2026, 7, 1), PostingGroup.West);

        Assert.All(teams.Where(team => team.Division == "West"), team => Assert.Contains(team.Name, output.ToString()));
    }

    [Theory]
    [InlineData("East", 7, 31, 2)]
    [InlineData("East", 8, 1, 2)]
    [InlineData("Central", 7, 31, 2)]
    [InlineData("Central", 8, 1, 2)]
    [InlineData("West", 7, 31, 2)]
    [InlineData("West", 8, 1, 4)]
    [InlineData("West", 9, 27, 4)]
    [InlineData("West", 9, 28, 0)]
    public async Task 対象地区だけを出力し西部の8月から全地区によるWCを追加する(string group, int month, int day, int count)
    {
        using var output = new StringWriter();
        var sender = new RecordingDryRunSender(output);
        using var client = CreateClient(calendarJson, StandingsJson, allowStandings: count > 0);

        await CreateRunner(client, sender).RunAsync(2026, new DateOnly(2026, month, day), Enum.Parse<PostingGroup>(group));

        Assert.Equal(count, sender.Contents.Count);
        if (count == 0) return;
        // 先頭の2件は対象グループのAL・NLの地区で、その地区の5球団だけを載せる
        string[] divisionTexts = sender.Contents.Take(2).Select(content => content.Text).ToArray();
        foreach (string league in new[] { "AL", "NL" })
        {
            string text = Assert.Single(divisionTexts, text => text.Contains($"{league} {group}"));
            Assert.All(teams, team => Assert.Equal(
                team.League == league && team.Division == group, text.Contains(team.Name)));
        }
        if (count == 2) return;
        // 西部のワイルドカードも全地区から作る。架空の球団は各地区で同じ成績なので、
        // 各地区の首位（-0）を除いた2位（-1）・3位（-2）の6球団が、地区によらず表示される
        string[] wildCardTexts = sender.Contents.Skip(2).Select(content => content.Text).ToArray();
        foreach (string league in new[] { "AL", "NL" })
        {
            string text = Assert.Single(wildCardTexts, text => text.Contains($"{league} Wild Card"));
            Assert.All(teams.Where(team => team.League == league), team => Assert.Equal(
                team.Name.EndsWith("-1") || team.Name.EndsWith("-2"), text.Contains(team.Name)));
        }
    }

    [Theory]
    [InlineData("East")]
    [InlineData("Central")]
    [InlineData("West")]
    public async Task 対象外地区の勝敗欠落でも全投稿を止める(string group)
    {
        var response = JsonNode.Parse(StandingsJson)!.AsArray();
        var other = response.First(row => row!["Division"]!.GetValue<string>() != group)!;
        other.AsObject().Remove("Wins");
        using var output = new StringWriter();
        using var client = CreateClient(calendarJson, response.ToJsonString());
        await Assert.ThrowsAnyAsync<Exception>(() => CreateRunner(client, new DryRunTweetSender(output))
            .RunAsync(2026, new DateOnly(2026, 8, 1), Enum.Parse<PostingGroup>(group)));
        Assert.Empty(output.ToString());
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
