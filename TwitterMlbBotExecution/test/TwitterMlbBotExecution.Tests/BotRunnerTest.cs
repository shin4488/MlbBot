using Microsoft.Extensions.Logging.Abstractions;
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

        public int CallCount { get; private set; }

        public FakeStandingsProvider(List<TeamStanding> standings)
        {
            this.standings = standings;
        }

        public Task<List<TeamStanding>> GetStandingsAsync(int year)
        {
            CallCount++;
            return Task.FromResult(this.standings);
        }
    }

    private class FakeSeasonCalendarProvider : ISeasonCalendarProvider
    {
        private readonly Func<SeasonCalendar> getResult;

        public FakeSeasonCalendarProvider(Func<SeasonCalendar>? getResult = null)
        {
            this.getResult = getResult ?? (() => new SeasonCalendar(seasonEndDate));
        }

        public Task<SeasonCalendar> GetSeasonCalendarAsync(int year)
        {
            return Task.FromResult(this.getResult());
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

    // 7月の日付: ワイルドカード（8月以降のみ）を含まない、地区ツイートだけの基本ケースに使う
    private static readonly DateOnly julyDate = new DateOnly(2026, 7, 15);
    private static readonly DateOnly augustDate = new DateOnly(2026, 8, 30);
    private static readonly DateOnly seasonEndDate = new DateOnly(2026, 9, 27);

    private static BotRunner CreateRunner(
        List<TeamStanding> standings, FakeTweetSender sender, ISeasonCalendarProvider? seasonProvider = null)
    {
        return CreateRunner(new FakeStandingsProvider(standings), sender, seasonProvider);
    }

    private static BotRunner CreateRunner(
        FakeStandingsProvider standingsProvider, FakeTweetSender sender, ISeasonCalendarProvider? seasonProvider = null)
    {
        return new BotRunner(
            seasonProvider ?? new FakeSeasonCalendarProvider(),
            standingsProvider,
            new TweetComposer(new HashtagProvider()),
            sender,
            NullLogger<BotRunner>.Instance);
    }

    [Fact]
    public async Task RunAsync_取得した順位から組み立てた全地区分を送信する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate);

        Assert.Equal(2, sender.SentContents.Count);
        Assert.Contains(sender.SentContents, content => content.Contains("White Sox"));
        Assert.Contains(sender.SentContents, content => content.Contains("Dodgers"));
    }

    [Fact]
    public async Task RunAsync_順位データが空の場合は何も送信せず正常終了する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(new List<TeamStanding>(), sender).RunAsync(2026, julyDate);

        Assert.Empty(sender.SentContents);
    }

    [Fact]
    public async Task RunAsync_全件送信失敗の場合は例外を投げる()
    {
        var sender = new FakeTweetSender(_ => false);

        await Assert.ThrowsAnyAsync<Exception>(
            () => CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate));
    }

    [Fact]
    public async Task RunAsync_一部でも送信成功していれば例外にしない()
    {
        // 重複コンテンツ拒否など、一部失敗は正常系でも起きるため
        var sender = new FakeTweetSender(content => content.Contains("Dodgers"));

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate);

        Assert.Equal(2, sender.SentContents.Count);
    }

    [Fact]
    public async Task RunAsync_8月以降はワイルドカードもあわせて送信する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, augustDate);

        // 地区2件 + 各リーグのワイルドカード2件
        Assert.Equal(4, sender.SentContents.Count);
        Assert.Equal(2, sender.SentContents.Count(content => content.Contains("Wild Card")));
    }

    [Fact]
    public async Task RunAsync_7月まではワイルドカードを送信しない()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate);

        Assert.DoesNotContain(sender.SentContents, content => content.Contains("Wild Card"));
    }

    [Fact]
    public async Task RunAsync_シーズン終了後は順位取得もツイートもしない()
    {
        var sender = new FakeTweetSender();
        var standingsProvider = new FakeStandingsProvider(CreateTwoDivisionStandings());

        await CreateRunner(standingsProvider, sender).RunAsync(2026, seasonEndDate.AddDays(1));

        Assert.Empty(sender.SentContents);
        // オフシーズン中のMLB API呼び出し（クォータ消費）も止める仕様
        Assert.Equal(0, standingsProvider.CallCount);
    }

    [Fact]
    public async Task RunAsync_シーズン最終日の分はツイートする()
    {
        // 最終戦の結果を反映した最終順位はツイートされること（境界）
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, seasonEndDate);

        Assert.NotEmpty(sender.SentContents);
    }

    [Fact]
    public async Task RunAsync_シーズン日程の取得に失敗したら例外を投げてツイートしない()
    {
        // 黙ってスキップ・黙って投稿のどちらもせず異常終了させ、エラーアラームのメール通知につなげる仕様
        var sender = new FakeTweetSender();
        var failingProvider = new FakeSeasonCalendarProvider(
            () => throw new InvalidOperationException("シーズン日程の取得失敗"));

        await Assert.ThrowsAnyAsync<Exception>(
            () => CreateRunner(CreateTwoDivisionStandings(), sender, failingProvider).RunAsync(2026, julyDate));

        Assert.Empty(sender.SentContents);
    }
}
