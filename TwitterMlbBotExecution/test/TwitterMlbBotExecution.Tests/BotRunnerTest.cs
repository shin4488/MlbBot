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

        public Task<IReadOnlyList<TeamStanding>> GetStandingsAsync(int year)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<TeamStanding>>(standings);
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
            return Task.FromResult(getResult());
        }
    }

    private class FakeTweetSender : ITweetSender
    {
        private readonly Func<string, bool> sendResult;

        public List<string> SentContents { get; } = new();

        /// <param name="sendResult">文面ごとの送信結果（false=失敗）。例外を投げると送信先の障害を模擬できる</param>
        public FakeTweetSender(Func<string, bool>? sendResult = null)
        {
            this.sendResult = sendResult ?? (_ => true);
        }

        public Task<bool> SendAsync(TweetContent tweetContent)
        {
            SentContents.Add(tweetContent.Text);
            return Task.FromResult(sendResult(tweetContent.Text));
        }
    }

    private static List<TeamStanding> CreateTwoDivisionStandings()
    {
        return new List<TeamStanding>
        {
            Teams.Create("AL", "West", "Mariners", 84, 56),
            Teams.Create("AL", "West", "Astros", 70, 70),
            Teams.Create("NL", "West", "Dodgers", 82, 48),
            Teams.Create("NL", "West", "Rockies", 60, 70),
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
    public async Task RunAsync_取得した順位から組み立てた対象グループの両リーグ分を送信する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate, PostingGroup.West);

        Assert.Equal(2, sender.SentContents.Count);
        Assert.Contains(sender.SentContents, content => content.Contains("Mariners"));
        Assert.Contains(sender.SentContents, content => content.Contains("Dodgers"));
    }

    [Fact]
    public async Task RunAsync_順位データが空の場合は何も送信せず正常終了する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(new List<TeamStanding>(), sender).RunAsync(2026, julyDate, PostingGroup.West);

        Assert.Empty(sender.SentContents);
    }

    [Fact]
    public async Task RunAsync_全件送信失敗の場合は例外を投げる()
    {
        var sender = new FakeTweetSender(_ => false);

        await Assert.ThrowsAnyAsync<Exception>(
            () => CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate, PostingGroup.West));
    }

    [Fact]
    public async Task RunAsync_一部でも送信成功していれば例外にしない()
    {
        // 重複コンテンツ拒否など、一部失敗は正常系でも起きるため
        var sender = new FakeTweetSender(content => content.Contains("Dodgers"));

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate, PostingGroup.West);

        Assert.Equal(2, sender.SentContents.Count);
    }

    [Fact]
    public async Task RunAsync_1件の送信で例外が起きても残りのツイートは送信する()
    {
        // タイムアウト等で送信先が例外を投げても、その1件の失敗にとどめて他の地区は投稿する仕様
        var sender = new FakeTweetSender(content =>
            content.Contains("Mariners") ? throw new HttpRequestException("送信失敗") : true);

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate, PostingGroup.West);

        Assert.Equal(2, sender.SentContents.Count);
        Assert.Contains(sender.SentContents, content => content.Contains("Dodgers"));
    }

    [Fact]
    public async Task RunAsync_全件の送信で例外が起きた場合は例外を投げる()
    {
        // 例外による失敗も「失敗」として数え、全滅ならエラー終了（アラーム通知）につなげる
        var sender = new FakeTweetSender(_ => throw new HttpRequestException("送信失敗"));

        await Assert.ThrowsAnyAsync<Exception>(
            () => CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate, PostingGroup.West));
    }

    [Theory]
    [InlineData("bug", false)]
    [InlineData("bug", true)]
    [InlineData("argument", false)]
    [InlineData("argument", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    [InlineData("task-cancel", false)]
    [InlineData("task-cancel", true)]
    public async Task RunAsync_送信の不具合とキャンセルは成功済み投稿の有無によらずそのまま伝える(string failure, bool afterSuccess)
    {
        Exception original = CreateUnexpectedFailure(failure);
        int attempts = 0;
        var sender = new FakeTweetSender(_ =>
        {
            if (++attempts == 1 && afterSuccess) return true;
            throw original;
        });

        Exception actual = await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate, PostingGroup.West));

        Assert.Same(original, actual);
        Assert.Equal(afterSuccess ? 2 : 1, sender.SentContents.Count);
    }

    [Theory]
    [InlineData(1, "bug")]
    [InlineData(7, "bug")]
    [InlineData(1, "argument")]
    [InlineData(7, "argument")]
    [InlineData(1, "cancel")]
    [InlineData(7, "cancel")]
    [InlineData(1, "task-cancel")]
    [InlineData(7, "task-cancel")]
    public async Task RunAsync_日程取得の不具合とキャンセルは季節によらずそのまま伝える(int month, string failure)
    {
        Exception original = CreateUnexpectedFailure(failure);
        var sender = new FakeTweetSender();
        var standings = new FakeStandingsProvider(CreateTwoDivisionStandings());
        var calendar = new FakeSeasonCalendarProvider(() => throw original);

        Exception actual = await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateRunner(standings, sender, calendar).RunAsync(2026, new DateOnly(2026, month, 15), PostingGroup.West));

        Assert.Same(original, actual);
        Assert.Equal(0, standings.CallCount);
        Assert.Empty(sender.SentContents);
    }

    private static Exception CreateUnexpectedFailure(string failure) => failure switch
    {
        "bug" => new InvalidOperationException("テスト用の実装不具合"),
        "argument" => new ArgumentException("テスト用の不正な引数"),
        "cancel" => new OperationCanceledException(),
        "task-cancel" => new TaskCanceledException(),
        _ => throw new ArgumentException("未定義のテスト条件"),
    };

    [Theory]
    [InlineData(1, false)]
    [InlineData(7, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    public async Task RunAsync_日程の通信障害とHTTPタイムアウトは季節に応じて回復する(int month, bool timeout)
    {
        Exception failure = timeout
            ? new TaskCanceledException("テスト用のタイムアウト", new TimeoutException())
            : new HttpRequestException("テスト用の通信障害");
        var sender = new FakeTweetSender();
        var calendar = new FakeSeasonCalendarProvider(() => throw failure);

        await CreateRunner(CreateTwoDivisionStandings(), sender, calendar)
            .RunAsync(2026, new DateOnly(2026, month, 15), PostingGroup.West);

        Assert.Equal(month == 7 ? 2 : 0, sender.SentContents.Count);
    }

    [Fact]
    public async Task RunAsync_8月以降はワイルドカードもあわせて送信する()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, augustDate, PostingGroup.West);

        // 地区2件 + 各リーグのワイルドカード2件
        Assert.Equal(4, sender.SentContents.Count);
        Assert.Equal(2, sender.SentContents.Count(content => content.Contains("Wild Card")));
    }

    [Fact]
    public async Task RunAsync_7月まではワイルドカードを送信しない()
    {
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, julyDate, PostingGroup.West);

        Assert.DoesNotContain(sender.SentContents, content => content.Contains("Wild Card"));
    }

    [Fact]
    public async Task RunAsync_シーズン終了後は順位取得もツイートもしない()
    {
        var sender = new FakeTweetSender();
        var standingsProvider = new FakeStandingsProvider(CreateTwoDivisionStandings());

        await CreateRunner(standingsProvider, sender).RunAsync(2026, seasonEndDate.AddDays(1), PostingGroup.West);

        Assert.Empty(sender.SentContents);
        // オフシーズン中のMLB API呼び出し（クォータ消費）も止める仕様
        Assert.Equal(0, standingsProvider.CallCount);
    }

    [Fact]
    public async Task RunAsync_シーズン最終日の分はツイートする()
    {
        // 最終戦の結果を反映した最終順位はツイートされること（境界）
        var sender = new FakeTweetSender();

        await CreateRunner(CreateTwoDivisionStandings(), sender).RunAsync(2026, seasonEndDate, PostingGroup.West);

        Assert.NotEmpty(sender.SentContents);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(10)]
    public async Task RunAsync_シーズン中でありうる期間は日程取得に失敗してもツイートを続行する(int month)
    {
        // statsapiの障害でツイートを止めない仕様（エラーログはログ監視アラームが拾いメール通知される）
        var sender = new FakeTweetSender();
        var failingProvider = new FakeSeasonCalendarProvider(
            () => throw new MlbApiException("シーズン日程の取得失敗"));

        await CreateRunner(CreateTwoDivisionStandings(), sender, failingProvider)
            .RunAsync(2026, new DateOnly(2026, month, 15), PostingGroup.West);

        Assert.NotEmpty(sender.SentContents);
    }

    [Fact]
    public async Task RunAsync_文字数上限を超える可能性のある文面でも送信は試みる()
    {
        // Xの実際の判定は重み付きの独自カウントのため、近似カウントの超過では送信を止めない仕様（警告のみ）
        var sender = new FakeTweetSender();
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "West", new string('A', 300), 84, 56),
        };

        await CreateRunner(standings, sender).RunAsync(2026, julyDate, PostingGroup.West);

        string sent = Assert.Single(sender.SentContents);
        Assert.True(sent.Length > TweetContent.CharacterLimit, "上限超過の文面が題材になっていること");
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RunAsync_明らかなシーズン外は日程取得に失敗しても静かに正常終了する(int month)
    {
        // 明らかなシーズン外（11〜2月）はどのみち投稿対象がなく実害ゼロのため、例外・メール通知にはしない仕様
        var sender = new FakeTweetSender();
        var standingsProvider = new FakeStandingsProvider(CreateTwoDivisionStandings());
        var failingProvider = new FakeSeasonCalendarProvider(
            () => throw new MlbApiException("シーズン日程の取得失敗"));

        await CreateRunner(standingsProvider, sender, failingProvider)
            .RunAsync(2026, new DateOnly(2026, month, 15), PostingGroup.West);

        Assert.Empty(sender.SentContents);
        Assert.Equal(0, standingsProvider.CallCount);
    }
}
