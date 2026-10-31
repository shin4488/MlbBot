using TwitterMlbBot;
using System.Globalization;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// 文面組み立て（TweetComposer）のテスト
///
/// テストは仕様ベースで書く方針:
/// BotRunnerと同じく全チームの成績・表示日・グループを渡し、
/// 「日付と順位・勝敗・ゲーム差が文面に反映される」「タグは上位2チーム分」といった
/// 仕様レベルの不変条件を検証する。ハッシュタグの検証には公式タグマップに
/// 載っていないチーム名を使い、シーズンごとのマップ変更に依存させない。
/// </summary>
public class TweetComposerTest
{
    // 8月の日付。Centralグループは地区の文面だけ、Westグループはワイルドカードの文面も作る
    private static readonly DateOnly testDate = new DateOnly(2026, 8, 30);

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    public void ComposeTweets_実行環境の言語によらず英語表記の日付と小数を使う(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            string text = Assert.Single(ComposeCentralTweets(new List<TeamStanding>
            {
                Teams.Create("AL", "Central", "First", 84, 56),
                Teams.Create("AL", "Central", "Second", 70, 71),
            })).Text;

            Assert.Contains("8/30", text);
            Assert.Contains("14.5", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ComposeTweets_生成した文面リストは入力や受け取り側の操作で変更されない()
    {
        var source = new List<TeamStanding>
        {
            Teams.Create("AL", "West", "Alpha", 90, 50),
            Teams.Create("AL", "West", "Beta", 80, 60),
        };

        var tweets = new TweetComposer(new HashtagProvider()).ComposeTweets(source, testDate, PostingGroup.West);
        source.Clear();

        // 地区とワイルドカードの両方の文面が、入力を消した後も残る
        Assert.Equal(2, tweets.Count);
        Assert.All(tweets, tweet => Assert.Contains("Beta", tweet.Text));
        var expected = tweets.ToArray();
        if (tweets is IList<TweetContent> writable)
        {
            Assert.ThrowsAny<Exception>(() => writable.Clear());
        }
        Assert.Equal(expected, tweets);
    }

    [Theory]
    [InlineData(7, 31, 2)]
    [InlineData(8, 1, 4)]
    public void ComposeTweets_8月の開始から地区の後にリーグごとのワイルドカードを追加する(int month, int day, int count)
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "West", "Alpha", 90, 50),
            Teams.Create("AL", "West", "Beta", 80, 60),
            Teams.Create("NL", "West", "Gamma", 90, 50),
            Teams.Create("NL", "West", "Delta", 80, 60),
        };

        var tweets = new TweetComposer(new HashtagProvider())
            .ComposeTweets(standings, new DateOnly(2026, month, day), PostingGroup.West);

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
            .ComposeTweets(Array.Empty<TeamStanding>(), new DateOnly(2026, month, 1), PostingGroup.West);

        Assert.Empty(tweets);
    }

    /// <summary>
    /// Centralグループの文面を作る。Centralグループはワイルドカードを投稿しないため、地区の文面だけを検証できる
    /// </summary>
    private static IReadOnlyList<TweetContent> ComposeCentralTweets(IReadOnlyList<TeamStanding> standings)
    {
        return new TweetComposer(new HashtagProvider()).ComposeTweets(standings, testDate, PostingGroup.Central);
    }

    [Fact]
    public void ComposeTweets_地区の文面のヘッダに日付が入る()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
        };

        string text = Assert.Single(ComposeCentralTweets(standings)).Text;

        Assert.Contains("8/30", text);
    }

    [Fact]
    public void ComposeTweets_地区の文面は勝率の高い順に列挙される()
    {
        // 入力はあえて順位順に並べない（APIの並び順に依存しない仕様の検証）
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Athletics", 56, 84),
        };

        string text = Assert.Single(ComposeCentralTweets(standings)).Text;

        Assert.True(text.IndexOf("White Sox") < text.IndexOf("Astros"), "勝率1位のチームが先に出力されること");
        Assert.True(text.IndexOf("Astros") < text.IndexOf("Athletics"), "勝率2位のチームが3位より先に出力されること");
    }

    [Fact]
    public void ComposeTweets_地区の文面に数字の意味がわかる凡例が入る()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
        };

        string text = Assert.Single(ComposeCentralTweets(standings)).Text;

        Assert.Contains("W-L (GB)", text);
    }

    [Fact]
    public void ComposeTweets_地区優勝が確定した球団の行で直接示す()
    {
        var standings = new List<TeamStanding>
        {
            new("Mariners", "AL", "Central", 94, 65, clinchedDivision: true),
            new("Astros", "AL", "Central", 86, 73),
        };

        string text = Assert.Single(ComposeCentralTweets(standings)).Text;

        Assert.Contains("1. Mariners 94-65 🏆", text);
        Assert.DoesNotContain("Astros 86-73 🏆", text);
        Assert.DoesNotContain("Division Champs", text);
    }

    [Fact]
    public void ComposeTweets_勝敗数がハイフン連結で入る()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
        };

        string text = Assert.Single(ComposeCentralTweets(standings)).Text;

        Assert.Contains("84-56", text);
    }

    [Fact]
    public void ComposeTweets_地区のゲーム差は2位以下にのみ表示される()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Astros", 70, 71),
        };

        string text = Assert.Single(ComposeCentralTweets(standings)).Text;

        // 2位のゲーム差（首位の貯金28、2位の貯金-1 → 14.5）は表示される
        Assert.Contains("(14.5)", text);
        // 首位の行にはゲーム差を表示しない（"(0)" が現れない）
        Assert.DoesNotContain("(0)", text);
    }

    [Fact]
    public void ComposeTweets_対象グループの地区ごとに1件の文面が生成される()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("NL", "Central", "Dodgers", 82, 48),
            Teams.Create("NL", "Central", "Rockies", 60, 70),
        };

        var tweets = ComposeCentralTweets(standings);

        Assert.Equal(2, tweets.Count);
        var alTweet = Assert.Single(tweets, tweet => tweet.Text.Contains("White Sox"));
        Assert.DoesNotContain("Dodgers", alTweet.Text);
    }

    [Fact]
    public void ComposeTweets_地区のタグには全体タグと上位2チームのタグが入る()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("AL", "Central", "Athletics", 56, 84),
        };

        string text = Assert.Single(ComposeCentralTweets(standings)).Text;

        Assert.Contains("#MLB", text);
        // チーム名のスペースは除去されてタグになる
        Assert.Contains("#WhiteSox", text);
        Assert.Contains("#Astros", text);
        // タグ付けは上位2チームまで
        Assert.DoesNotContain("#Athletics", text);
    }

    [Fact]
    public void ComposeTweets_ワイルドカードの文面にヘッダと日付と凡例が入る()
    {
        string text = ComposeWildCardTweet(CreateWildCardRace()).Text;

        Assert.Contains("Wild Card", text);
        Assert.Contains("8/30", text);
        Assert.Contains("W-L (GB)", text);
    }

    [Fact]
    public void ComposeTweets_ワイルドカードのプレーオフ圏内はゲーム差非表示_圏外は境界線の後に表示される()
    {
        string text = ComposeWildCardTweet(CreateWildCardRace()).Text;
        string[] lines = text.Split('\n', StringSplitOptions.TrimEntries);

        // 圏内1〜3位の行にはゲーム差の括弧が付かない
        Assert.All(
            lines.Where(line => line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.")),
            line => Assert.DoesNotContain("(", line));
        // 圏内の3位と圏外の4位の間に境界線があり、圏外チームにはボーダーとのゲーム差が表示される
        int borderIndex = Array.IndexOf(lines, "---");
        Assert.StartsWith("3.", lines[borderIndex - 1]);
        Assert.StartsWith("4.", lines[borderIndex + 1]);
        Assert.Contains("(1)", lines[borderIndex + 1]);
    }

    [Fact]
    public void ComposeTweets_ワイルドカード進出が確定した球団だけを暫定順位と区別して示す()
    {
        var standings = new List<TeamStanding>
        {
            new("Yankees", "AL", "East", 96, 66, clinchedDivision: true),
            new("Astros", "AL", "East", 90, 72, clinchedWildCard: true),
            new("Red Sox", "AL", "East", 89, 73),
            new("Tigers", "AL", "East", 86, 76),
            new("Rangers", "AL", "East", 85, 77),
        };

        string text = ComposeWildCardTweet(standings).Text;

        Assert.Contains("Astros 90-72 ✅", text);
        Assert.DoesNotContain("Red Sox 89-73 ✅", text);
        Assert.DoesNotContain("WC clinched", text);
        Assert.Contains("---", text);
        Assert.DoesNotContain("Yankees", text);
    }

    [Fact]
    public void ComposeTweets_ワイルドカードの全チームが圏内なら境界線を表示しない()
    {
        // 圏外チームがいない（データ欠け等で対象チームが少ない）場合に無意味な区切り線を出さない
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 95, 45),
            Teams.Create("AL", "Central", "Astros", 88, 52),
            Teams.Create("AL", "Central", "Dodgers", 87, 53),
        };

        var tweet = ComposeWildCardTweet(standings);

        Assert.DoesNotContain("---", tweet.Text);
    }

    [Fact]
    public void ComposeTweets_ワイルドカードの表示は6チームまで()
    {
        string text = ComposeWildCardTweet(CreateWildCardRace()).Text;

        Assert.Contains("6. ", text);
        Assert.DoesNotContain("7. ", text);
    }

    /// <summary>
    /// 1リーグ・首位1チーム＋ワイルドカード7チームの成績
    /// （7番目のチームは表示上限の検証用。4位のCubsはボーダーの3位Rockiesと1ゲーム差）
    /// </summary>
    private static List<TeamStanding> CreateWildCardRace()
    {
        return new List<TeamStanding>
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
    }

    /// <summary>
    /// Westグループの8月の文面から、ALのワイルドカードの文面を1件取り出す。
    /// ワイルドカードは対象グループ以外の地区も含めた全地区から作られる
    /// </summary>
    private static TweetContent ComposeWildCardTweet(IReadOnlyList<TeamStanding> standings)
    {
        var tweets = new TweetComposer(new HashtagProvider()).ComposeTweets(standings, testDate, PostingGroup.West);
        return Assert.Single(tweets, tweet => tweet.Text.Contains("AL Wild Card"));
    }

    [Fact]
    public void ComposeTweets_地区の文面はXの文字数上限280字以内に収まる()
    {
        // 勝敗は、首位を3桁の勝ち数、2位以下を11.5のような2桁で小数付きのゲーム差にする
        string[] names = LongestTeamNames(5);
        var standings = new List<TeamStanding>
        {
            new TeamStanding(names[0], "NL", "Central", 100, 62, clinchedDivision: true),
            Teams.Create("NL", "Central", names[1], 89, 74),
            Teams.Create("NL", "Central", names[2], 88, 75),
            Teams.Create("NL", "Central", names[3], 87, 76),
            Teams.Create("NL", "Central", names[4], 86, 77),
        };

        var tweet = Assert.Single(ComposeCentralTweets(standings));

        Assert.False(tweet.ExceedsCharacterLimit, $"文面が上限を超えている: {tweet.CharacterCount}字");
    }

    [Fact]
    public void ComposeTweets_進出確定表示を含むワイルドカードの文面はXの文字数上限内に収まる()
    {
        string[] names = LongestTeamNames(6);
        var standings = new List<TeamStanding>
        {
            // 地区首位はワイルドカードの文面に載らない
            new("Leader", "AL", "East", 110, 52, clinchedDivision: true),
            new(names[0], "AL", "East", 100, 62, clinchedWildCard: true),
            new(names[1], "AL", "East", 99, 63, clinchedWildCard: true),
            new(names[2], "AL", "East", 98, 64, clinchedWildCard: true),
            new(names[3], "AL", "East", 97, 65),
            new(names[4], "AL", "East", 96, 66),
            new(names[5], "AL", "East", 95, 67),
        };

        TweetContent tweet = ComposeWildCardTweet(standings);

        Assert.False(tweet.ExceedsCharacterLimit, $"文面が上限を超えている: {tweet.CharacterCount}字");
    }

    /// <summary>
    /// 実際の公式タグ表の30球団から、文面が最も長くなる球団名を上位から順に返す。
    /// タグが付く上位2球団は球団名とタグを合わせて最も長い球団、残りは球団名が最も長い球団にする。
    /// タグ表の変更（毎シーズンありうる）で文面が上限を超えたら、文字数のテストで検出できる
    /// </summary>
    private static string[] LongestTeamNames(int count)
    {
        var hashtagProvider = new HashtagProvider();
        string[] taggedTeams = HashtagProvider.OfficialHashtagMap.Keys
            .OrderByDescending(name => name.Length + hashtagProvider.GetHashtags(name).Length)
            .ThenBy(name => name, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        IEnumerable<string> otherTeams = HashtagProvider.OfficialHashtagMap.Keys
            .Except(taggedTeams)
            .OrderByDescending(name => name.Length)
            .ThenBy(name => name, StringComparer.Ordinal)
            .Take(count - taggedTeams.Length);
        return [.. taggedTeams, .. otherTeams];
    }
}
