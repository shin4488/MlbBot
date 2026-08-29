using TwitterMlbBot;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// オーケストレーション（BotRunner）のテスト
/// 取得元・送信先をフェイクに差し替え、ネットワークアクセスなし・実ツイートなしで
/// 「取得→組み立て→送信」の流れと失敗時の仕様を検証する
/// </summary>
public class BotRunnerTest
{
    private class FakeStandingsProvider : IStandingsProvider
    {
        private readonly List<TeamStanding> standings;

        public FakeStandingsProvider(List<TeamStanding> standings)
        {
            this.standings = standings;
        }

        public Task<List<TeamStanding>> GetStandingsAsync(int year)
        {
            return Task.FromResult(this.standings);
        }
    }

    private class FakeTweetSender : ITweetSender
    {
        private readonly Func<string, bool> sendResult;

        public List<string> SentContents { get; } = new();

        public FakeTweetSender(Func<string, bool>? sendResult = null)
        {
            this.sendResult = sendResult ?? (_ => true);
        }

        public Task<bool> SendAsync(TweetContent tweetContent)
        {
            SentContents.Add(tweetContent.Text);
            return Task.FromResult(this.sendResult(tweetContent.Text));
        }
    }

    private static List<TeamStanding> CreateTwoDivisionStandings()
    {
        return new List<TeamStanding>
        {
            new TeamStanding { League = "AL", Division = "Central", Name = "White Sox", Wins = 84, Losses = 56, Percentage = 0.600 },
            new TeamStanding { League = "AL", Division = "Central", Name = "Astros", Wins = 70, Losses = 70, Percentage = 0.500 },
            new TeamStanding { League = "NL", Division = "West", Name = "Dodgers", Wins = 82, Losses = 48, Percentage = 0.630 },
            new TeamStanding { League = "NL", Division = "West", Name = "Rockies", Wins = 60, Losses = 70, Percentage = 0.460 },
        };
    }

    private static BotRunner CreateRunner(List<TeamStanding> standings, FakeTweetSender sender)
    {
        return new BotRunner(
            new FakeStandingsProvider(standings),
            new TweetComposer(new HashtagProvider()),
            sender);
    }

    [Fact]
    public async Task RunAsync_取得した順位から組み立てた全地区分を送信する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026);

        Assert.Equal(2, sender.SentContents.Count);
        Assert.Contains(sender.SentContents, content => content.Contains("White Sox"));
        Assert.Contains(sender.SentContents, content => content.Contains("Dodgers"));
    }

    [Fact]
    public async Task RunAsync_順位データが空の場合は何も送信せず正常終了する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(new List<TeamStanding>(), sender).RunAsync(2026);

        Assert.Empty(sender.SentContents);
    }

    [Fact]
    public async Task RunAsync_全件送信失敗の場合は例外を投げる()
    {
        var sender = new FakeTweetSender(_ => false);

        await Assert.ThrowsAnyAsync<Exception>(
            () => CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026));
    }

    [Fact]
    public async Task RunAsync_一部でも送信成功していれば例外にしない()
    {
        // 重複コンテンツ拒否など、一部失敗は正常系でも起きるため
        var sender = new FakeTweetSender(content => content.Contains("Dodgers"));

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026);

        Assert.Equal(2, sender.SentContents.Count);
    }
}
