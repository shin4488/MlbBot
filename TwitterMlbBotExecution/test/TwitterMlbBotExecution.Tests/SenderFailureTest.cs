using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TwitterMlbBot;
using TwitterMlbBot.Authorization;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

public class SenderFailureTest
{
    [Theory]
    [InlineData("http", false)]
    [InlineData("network", false)]
    [InlineData("timeout", false)]
    [InlineData("http", true)]
    [InlineData("network", true)]
    [InlineData("timeout", true)]
    public async Task 送信に失敗しても再送せず次の文面へ進み全件失敗だけエラーにする(string failure, bool allFail)
    {
        var sentTexts = new List<string>();
        using var client = new HttpClient(new StubHttpMessageHandler(async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            sentTexts.Add(body.RootElement.GetProperty("text").GetString()!);
            await Task.Yield();
            bool shouldSucceedAfterFirstFailure = sentTexts.Count > 1 && !allFail;
            if (shouldSucceedAfterFirstFailure)
            {
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            return failure switch
            {
                "network" => throw new HttpRequestException("テスト用の通信障害"),
                "timeout" => throw new TaskCanceledException("テスト用のタイムアウト"),
                _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            };
        }));
        var runner = new BotRunner(new FixedCalendar(), new FixedStandings(),
            new TweetComposer(new HashtagProvider()), CreateSender(client), NullLogger<BotRunner>.Instance);

        if (allFail)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => runner.RunAsync(2026, new DateOnly(2026, 7, 1)));
        }
        else
        {
            await runner.RunAsync(2026, new DateOnly(2026, 7, 1));
        }

        Assert.Equal(2, sentTexts.Count);
        Assert.Contains(sentTexts, text => text.Contains("First"));
        Assert.Contains(sentTexts, text => text.Contains("Second"));
    }

    [Fact]
    public async Task 失敗後の次の投稿にも間隔を空け新しい認証署名を使う()
    {
        var authorizations = new List<string>();
        TimeSpan interval = TimeSpan.Zero;
        var elapsedSinceFailure = new Stopwatch();
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            interval = elapsedSinceFailure.Elapsed;
            authorizations.Add(request.Headers.Authorization!.Parameter!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }));
        var sender = CreateSender(client);

        Assert.False(await sender.SendAsync(new TweetContent("First")));
        elapsedSinceFailure.Start();
        Assert.False(await sender.SendAsync(new TweetContent("Second")));

        // 上限時間は制約しない。CIが遅い場合でも、連続POSTを避ける仕様だけを検証する。
        Assert.True(interval >= TimeSpan.FromMilliseconds(900), "次のHTTP投稿まで約1秒空ける");
        Assert.Equal(2, authorizations.Distinct().Count());
    }

    private static TwitterApiSender CreateSender(HttpClient client) => new(client,
        new OAuth1("dummy-consumer", "dummy-secret", "dummy-access", "dummy-access-secret"),
        NullLogger<TwitterApiSender>.Instance);

    private sealed class FixedCalendar : ISeasonCalendarProvider
    {
        public Task<SeasonCalendar> GetSeasonCalendarAsync(int year) =>
            Task.FromResult(new SeasonCalendar(new DateOnly(2026, 9, 27)));
    }

    private sealed class FixedStandings : IStandingsProvider
    {
        public Task<IReadOnlyList<TeamStanding>> GetStandingsAsync(int year) =>
            Task.FromResult<IReadOnlyList<TeamStanding>>(new[]
            {
                new TeamStanding("First", "AL", "East", 80, 60),
                new TeamStanding("Second", "NL", "West", 90, 50),
            });
    }
}
