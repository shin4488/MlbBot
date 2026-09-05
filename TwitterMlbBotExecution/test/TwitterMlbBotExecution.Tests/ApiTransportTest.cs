using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TwitterMlbBot.Authorization;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

public class ApiTransportTest
{
    [Fact]
    public async Task 順位取得は対象年を指定しキーをURIではなくリクエストヘッダーに含める()
    {
        const string dummyKey = "dummy-mlb-key";
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith("/Standings/2026", request.RequestUri!.AbsolutePath);
            Assert.DoesNotContain(dummyKey, request.RequestUri.AbsoluteUri);
            Assert.Equal(dummyKey, Assert.Single(request.Headers.GetValues("Ocp-Apim-Subscription-Key")));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(StandingsFixture.CreateResponse().ToJsonString()),
            });
        }));
        var provider = new MlbApiClient(client, dummyKey, NullLogger<MlbApiClient>.Instance);

        var teams = await provider.GetStandingsAsync(2026);

        Assert.Equal(StandingsFixture.Teams, teams);
        // 接続を他APIとも共有できるよう、認証情報を共通ヘッダーに残さない
        Assert.False(client.DefaultRequestHeaders.Contains("Ocp-Apim-Subscription-Key"));
    }

    [Fact]
    public async Task 日程取得は対象年とMLBの競技を指定し終了日を返す()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.EndsWith("/seasons/2026", request.RequestUri!.AbsolutePath);
            Assert.Contains("sportId=1", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"seasons":[{"seasonId":"2026","regularSeasonEndDate":"2026-09-27"}]}"""),
            });
        }));
        var provider = new MlbStatsApiClient(client, NullLogger<MlbStatsApiClient>.Instance);

        var calendar = await provider.GetSeasonCalendarAsync(2026);

        Assert.Equal(new DateOnly(2026, 9, 27), calendar.RegularSeasonEndDate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 取得APIのHTTPエラーは空データとして扱わず失敗を伝える(bool standings)
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        if (standings)
        {
            var provider = new MlbApiClient(client, "dummy-mlb-key", NullLogger<MlbApiClient>.Instance);
            await Assert.ThrowsAnyAsync<Exception>(() => provider.GetStandingsAsync(2026));
        }
        else
        {
            var provider = new MlbStatsApiClient(client, NullLogger<MlbStatsApiClient>.Instance);
            await Assert.ThrowsAnyAsync<Exception>(() => provider.GetSeasonCalendarAsync(2026));
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task 投稿は署名付きJSONで文面を送りHTTP応答に応じた成否を返す(HttpStatusCode status, bool expected)
    {
        const string text = "テスト文面\n\"quoted\" ⚾";
        int postedCount = 0;
        using var client = new HttpClient(new StubHttpMessageHandler(async request =>
        {
            postedCount++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.twitter.com/2/tweets", request.RequestUri!.AbsoluteUri);
            Assert.Equal("OAuth", request.Headers.Authorization?.Scheme);
            Assert.Contains("oauth_signature=", request.Headers.Authorization!.Parameter!);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            Assert.Equal(text, json.RootElement.GetProperty("text").GetString());
            return new HttpResponseMessage(status);
        }));
        var sender = new TwitterApiSender(client,
            new OAuth1("consumer-key", "consumer-secret", "access-key", "access-secret"),
            NullLogger<TwitterApiSender>.Instance);

        bool result = await sender.SendAsync(new TweetContent(text));

        Assert.Equal(expected, result);
        // 失敗応答でも自動再投稿はしない（重複投稿と追加課金を避ける仕様）
        Assert.Equal(1, postedCount);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }
}
