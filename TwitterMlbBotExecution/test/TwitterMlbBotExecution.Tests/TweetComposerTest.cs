using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// 文面組み立て（TweetComposer）のテスト
///
/// テストは仕様ベースで書く方針:
/// 文面のフォーマット（区切り文字・絵文字・並び等）は変更されうるため、
/// 「順位順に列挙される」「タグは上位2チーム分」といった仕様レベルの不変条件のみを検証する。
/// ハッシュタグの検証には公式タグマップに載っていないチーム名を使い、
/// シーズンごとのマップ変更に依存させない。
/// </summary>
public class TweetComposerTest
{
    private static TeamStanding CreateTeam(
        string league, string division, string name, int wins, int losses, double percentage)
    {
        return new TeamStanding
        {
            League = league,
            Division = division,
            Name = name,
            Wins = wins,
            Losses = losses,
            Percentage = percentage,
            GamesBehind = 0,
        };
    }

    private static IReadOnlyList<TweetContent> Compose(List<TeamStanding> standings)
    {
        return new TweetComposer(new HashtagProvider()).Compose(DivisionStanding.FromStandings(standings));
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

        var tweets = Compose(standings);

        string text = Assert.Single(tweets).Text;
        Assert.True(text.IndexOf("White Sox") < text.IndexOf("Astros"), "勝率1位のチームが先に出力されること");
        Assert.True(text.IndexOf("Astros") < text.IndexOf("Athletics"), "勝率2位のチームが3位より先に出力されること");
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
        // 各地区の文面には自地区のチームのみ含まれること
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

        var tweets = Compose(standings);

        string text = Assert.Single(tweets).Text;
        Assert.Contains("#MLB", text);
        // チーム名のスペースは除去されてタグになる
        Assert.Contains("#WhiteSox", text);
        Assert.Contains("#Astros", text);
        // タグ付けは上位2チームまで
        Assert.DoesNotContain("#Athletics", text);
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

        var tweets = Compose(standings);

        var tweet = Assert.Single(tweets);
        Assert.False(tweet.ExceedsCharacterLimit, $"文面が上限を超えている: {tweet.CharacterCount}字");
    }
}
