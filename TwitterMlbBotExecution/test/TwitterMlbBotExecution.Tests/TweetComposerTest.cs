using System.Globalization;
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
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    public void Compose_実行環境の言語によらず英語表記の日付と小数を使う(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            string text = Assert.Single(Compose(new List<TeamStanding>
            {
                Teams.Create("AL", "East", "First", 84, 56),
                Teams.Create("AL", "East", "Second", 70, 71),
            })).Text;

            Assert.Contains("8/30", text);
            Assert.Contains("14.5", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static readonly DateOnly testDate = new DateOnly(2026, 8, 30);

    [Fact]
    public void 生成した文面リストは入力や受け取り側の操作で変更されない()
    {
        var source = new List<TeamStanding>
        {
            Teams.Create("AL", "East", "Alpha", 90, 50),
            Teams.Create("AL", "East", "Beta", 80, 60),
        };
        var composer = new TweetComposer(new HashtagProvider());
        var divisions = DivisionStanding.FromStandings(source);
        var results = new[]
        {
            composer.ComposeTweets(source, testDate),
            composer.Compose(divisions, testDate),
            composer.ComposeWildCards(WildCardStanding.FromDivisions(divisions), testDate),
        };
        source.Clear();

        foreach (var tweets in results)
        {
            Assert.Contains(tweets, tweet => tweet.Text.Contains("Beta"));
            var expected = tweets.ToArray();
            if (tweets is IList<TweetContent> writable)
            {
                Assert.ThrowsAny<Exception>(() => writable.Clear());
            }
            Assert.Equal(expected, tweets);
        }
    }

    [Theory]
    [InlineData(7, 31, 2)]
    [InlineData(8, 1, 4)]
    public void ComposeTweets_8月の開始から地区の後にリーグごとのワイルドカードを追加する(int month, int day, int count)
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "East", "Alpha", 90, 50),
            Teams.Create("AL", "East", "Beta", 80, 60),
            Teams.Create("NL", "West", "Gamma", 90, 50),
            Teams.Create("NL", "West", "Delta", 80, 60),
        };

        var tweets = new TweetComposer(new HashtagProvider())
            .ComposeTweets(standings, new DateOnly(2026, month, day));

        Assert.Equal(count, tweets.Count);
        Assert.All(tweets.Take(2), tweet => Assert.DoesNotContain("Wild Card", tweet.Text));
        Assert.All(tweets.Skip(2), tweet => Assert.Contains("Wild Card", tweet.Text));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void ComposeTweets_成績が空なら投稿文面はない(int month)
    {
        var tweets = new TweetComposer(new HashtagProvider())
            .ComposeTweets(Array.Empty<TeamStanding>(), new DateOnly(2026, month, 1));

        Assert.Empty(tweets);
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
            Teams.Create("AL", "Central", "White Sox", 84, 56),
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
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Athletics", 56, 84),
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
            Teams.Create("AL", "Central", "White Sox", 84, 56),
        };

        string text = Assert.Single(Compose(standings)).Text;

        Assert.Contains("W-L (GB)", text);
    }

    [Fact]
    public void Compose_勝敗数がハイフン連結で入る()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
        };

        string text = Assert.Single(Compose(standings)).Text;

        Assert.Contains("84-56", text);
    }

    [Fact]
    public void Compose_ゲーム差は2位以下にのみ表示される()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Astros", 70, 71),
        };

        string text = Assert.Single(Compose(standings)).Text;

        // 2位のゲーム差（首位の貯金28、2位の貯金-1 → 14.5）は表示される
        Assert.Contains("(14.5)", text);
        // 首位の行にはゲーム差を表示しない（"(0)" が現れない）
        Assert.DoesNotContain("(0)", text);
    }

    [Fact]
    public void Compose_地区ごとに1件の文面が生成される()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("NL", "West", "Dodgers", 82, 48),
            Teams.Create("NL", "West", "Rockies", 60, 70),
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
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("AL", "Central", "Athletics", 56, 84),
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
            Teams.Create("AL", "Central", "White Sox", 95, 45),
            Teams.Create("AL", "Central", "Astros", 88, 52),
            Teams.Create("AL", "Central", "Dodgers", 87, 53),
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
            Teams.Create("AL", "Central", "White Sox", 95, 45),
            Teams.Create("AL", "Central", "Astros", 88, 52),
            Teams.Create("AL", "Central", "Dodgers", 87, 53),
            Teams.Create("AL", "Central", "Rockies", 86, 54),
            Teams.Create("AL", "Central", "Cubs", 85, 55),
            Teams.Create("AL", "Central", "Angels", 82, 58),
            Teams.Create("AL", "Central", "Athletics", 80, 60),
            Teams.Create("AL", "Central", "Mets", 78, 62),
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
        // 勝敗は各行の文字数が最大になる組み合わせ（3桁の勝ち数・2桁小数のゲーム差）にする
        var standings = new List<TeamStanding>
        {
            Teams.Create("NL", "West", "Diamondbacks", 100, 62),
            Teams.Create("NL", "West", "Blue Jays", 89, 74),
            Teams.Create("NL", "West", "Guardians", 88, 75),
            Teams.Create("NL", "West", "Nationals", 87, 76),
            Teams.Create("NL", "West", "Twins", 86, 77),
        };

        var tweet = Assert.Single(Compose(standings));

        Assert.False(tweet.ExceedsCharacterLimit, $"文面が上限を超えている: {tweet.CharacterCount}字");
    }
}
