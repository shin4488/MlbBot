using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// 文面組み立て（TweetComposer）のテスト
///
/// テストは仕様ベースで書く方針:
/// 「日付と順位・勝敗・ゲーム差が文面に反映される」「タグは上位2チーム分」といった
/// 仕様レベルの不変条件を検証する。ハッシュタグの検証には公式タグマップに
/// 載っていないチーム名を使い、シーズンごとのマップ変更に依存させない。
/// </summary>
public class TweetComposerTest
{
    private static readonly DateOnly testDate = new DateOnly(2026, 8, 30);

    private static TeamStanding CreateTeam(
        string league, string division, string name, int wins, int losses, double percentage, float? gamesBehind = 0)
    {
        return new TeamStanding
        {
            League = league,
            Division = division,
            Name = name,
            Wins = wins,
            Losses = losses,
            Percentage = percentage,
            GamesBehind = gamesBehind,
        };
    }

    private static IReadOnlyList<TweetContent> Compose(List<TeamStanding> standings)
    {
        return new TweetComposer(new HashtagProvider())
            .Compose(DivisionStanding.FromStandings(standings), testDate);
    }

    [Fact]
    public void Compose_日付がヘッダに入る()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
        };

        string text = Assert.Single(Compose(standings)).Text;

        Assert.Contains("8/30", text);
    }

    [Fact]
    public void Compose_勝率の高い順に列挙される()
    {
        // 入力はあえて順位順に並べない（APIの並び順に依存しない仕様の検証）
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("AL", "Central", "Athletics", 56, 84, 0.400),
        };

        string text = Assert.Single(Compose(standings)).Text;

        Assert.True(text.IndexOf("White Sox") < text.IndexOf("Astros"), "勝率1位のチームが先に出力されること");
        Assert.True(text.IndexOf("Astros") < text.IndexOf("Athletics"), "勝率2位のチームが3位より先に出力されること");
    }

    [Fact]
    public void Compose_数字の意味がわかる凡例が入る()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
        };

        string text = Assert.Single(Compose(standings)).Text;

        Assert.Contains("W-L (GB)", text);
    }

    [Fact]
    public void Compose_勝敗数がハイフン連結で入る()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
        };

        string text = Assert.Single(Compose(standings)).Text;

        Assert.Contains("84-56", text);
    }

    [Fact]
    public void Compose_ゲーム差は2位以下にのみ表示される()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600, gamesBehind: 0),
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500, gamesBehind: 14.5f),
        };

        string text = Assert.Single(Compose(standings)).Text;

        // 2位のゲーム差は表示される
        Assert.Contains("(14.5)", text);
        // 首位の行にはゲーム差を表示しない（"(0)" が現れない）
        Assert.DoesNotContain("(0)", text);
    }

    [Fact]
    public void Compose_地区ごとに1件の文面が生成される()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            CreateTeam("NL", "West", "Dodgers", 82, 48, 0.630),
            CreateTeam("NL", "West", "Rockies", 60, 70, 0.460),
        };

        var tweets = Compose(standings);

        Assert.Equal(2, tweets.Count);
        var alTweet = Assert.Single(tweets, tweet => tweet.Text.Contains("White Sox"));
        Assert.DoesNotContain("Dodgers", alTweet.Text);
    }

    [Fact]
    public void Compose_順位表が空なら空リストを返す()
    {
        Assert.Empty(Compose(new List<TeamStanding>()));
    }

    [Fact]
    public void Compose_タグには全体タグと上位2チームのタグが入る()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            CreateTeam("AL", "Central", "Athletics", 56, 84, 0.400),
        };

        string text = Assert.Single(Compose(standings)).Text;

        Assert.Contains("#MLB", text);
        // チーム名のスペースは除去されてタグになる
        Assert.Contains("#WhiteSox", text);
        Assert.Contains("#Astros", text);
        // タグ付けは上位2チームまで
        Assert.DoesNotContain("#Athletics", text);
    }

    [Fact]
    public void ComposeWildCards_ヘッダと日付と凡例が入る()
    {
        string text = ComposeWildCard().Text;

        Assert.Contains("Wild Card", text);
        Assert.Contains("8/30", text);
        Assert.Contains("W-L (GB)", text);
    }

    [Fact]
    public void ComposeWildCards_プレーオフ圏内はゲーム差非表示_圏外は境界線の後に表示される()
    {
        string text = ComposeWildCard().Text;
        string[] lines = text.Split('\n');

        // 圏内1〜3位の行にはゲーム差の括弧が付かない
        Assert.All(
            lines.Where(line => line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.")),
            line => Assert.DoesNotContain("(", line));
        // 圏内と圏外の間に境界線があり、圏外チームにはボーダーとのゲーム差が表示される
        Assert.Contains("---", text);
        Assert.Contains(lines, line => line.StartsWith("4.") && line.Contains("(1)"));
    }

    [Fact]
    public void ComposeWildCards_全チームが圏内なら境界線を表示しない()
    {
        // 圏外チームがいない（データ欠け等で対象チームが少ない）場合に無意味な区切り線を出さない
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 95, 45, 0.679),
            CreateTeam("AL", "Central", "Astros", 88, 52, 0.629),
            CreateTeam("AL", "Central", "Dodgers", 87, 53, 0.621),
        };
        var wildCards = WildCardStanding.FromDivisions(DivisionStanding.FromStandings(standings));

        var tweet = Assert.Single(new TweetComposer(new HashtagProvider()).ComposeWildCards(wildCards, testDate));

        Assert.DoesNotContain("---", tweet.Text);
    }

    [Fact]
    public void ComposeWildCards_表示は6チームまで()
    {
        string text = ComposeWildCard().Text;

        Assert.Contains("6. ", text);
        Assert.DoesNotContain("7. ", text);
    }

    /// <summary>
    /// 1リーグ・首位1チーム＋ワイルドカード7チームのデータでWCツイートを1件生成する
    /// （7番目のチームは表示上限の検証用）
    /// </summary>
    private static TweetContent ComposeWildCard()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 95, 45, 0.679),
            CreateTeam("AL", "Central", "Astros", 88, 52, 0.629),
            CreateTeam("AL", "Central", "Dodgers", 87, 53, 0.621),
            CreateTeam("AL", "Central", "Rockies", 86, 54, 0.614),
            CreateTeam("AL", "Central", "Cubs", 85, 55, 0.607),
            CreateTeam("AL", "Central", "Angels", 82, 58, 0.586),
            CreateTeam("AL", "Central", "Athletics", 80, 60, 0.571),
            CreateTeam("AL", "Central", "Mets", 78, 62, 0.557),
        };
        var wildCards = WildCardStanding.FromDivisions(DivisionStanding.FromStandings(standings));
        return Assert.Single(new TweetComposer(new HashtagProvider()).ComposeWildCards(wildCards, testDate));
    }

    [Fact]
    public void Compose_文面はXの文字数上限280字以内に収まる()
    {
        // 実在の公式タグマップを意図的に使う数少ないテスト:
        // 「長いチーム名 + 長い公式タグ」の組み合わせが上限を超えないことの回帰検証で、
        // タグマップの変更（毎シーズンありうる）で文字数超過が起きたらここで検出する
        var standings = new List<TeamStanding>
        {
            CreateTeam("NL", "West", "Diamondbacks", 100, 62, 0.617),
            CreateTeam("NL", "West", "Blue Jays", 99, 63, 0.611),
            CreateTeam("NL", "West", "Guardians", 98, 64, 0.605),
            CreateTeam("NL", "West", "Nationals", 97, 65, 0.599),
            CreateTeam("NL", "West", "Twins", 96, 66, 0.593),
        }.Select(team => team with { GamesBehind = 10.5f }).ToList();

        var tweet = Assert.Single(Compose(standings));

        Assert.False(tweet.ExceedsCharacterLimit, $"文面が上限を超えている: {tweet.CharacterCount}字");
    }
}
