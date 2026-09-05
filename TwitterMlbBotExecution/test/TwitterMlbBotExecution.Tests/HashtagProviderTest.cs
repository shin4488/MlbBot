using TwitterMlbBot.Composing;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ハッシュタグ生成（HashtagProvider）のテスト
/// GetHashtagsの挙動はタグマップをテスト用に注入して検証し、実際の公式タグの中身（毎シーズン変わりうる）には依存させない。
/// 実際のマップに対しては「全30球団が定義されていること」のみを検証し、タグ更新時の考慮漏れを検出する
/// </summary>
public class HashtagProviderTest
{
    /// <summary>
    /// MLB全30球団のチーム名（sportsdata.ioが返す表記）。球団名の変更・移転・拡張の時のみ更新する
    /// </summary>
    private static readonly string[] AllMlbTeamNames =
    {
        "Angels", "Astros", "Athletics", "Blue Jays", "Braves", "Brewers",
        "Cardinals", "Cubs", "Diamondbacks", "Dodgers", "Giants", "Guardians",
        "Mariners", "Marlins", "Mets", "Nationals", "Orioles", "Padres",
        "Phillies", "Pirates", "Rangers", "Rays", "Red Sox", "Reds",
        "Rockies", "Royals", "Tigers", "Twins", "White Sox", "Yankees",
    };

    [Fact]
    public void 公式タグマップは全30球団を漏れなく定義している()
    {
        Assert.Equal(
            AllMlbTeamNames.Order(StringComparer.Ordinal),
            HashtagProvider.OfficialHashtagMap.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void GetHashtags_渡した辞書を後で書き換えてもタグは変わらない()
    {
        var source = new Dictionary<string, string> { { "Example", "OriginalTag" } };
        var provider = new HashtagProvider(source);

        source["Example"] = "ChangedTag";
        source.Clear();

        Assert.Equal("#OriginalTag #Example", provider.GetHashtags("Example"));
    }

    [Fact]
    public void 共有する公式タグマップは書き込み用のAPIからも変更できない()
    {
        // 共有データを変更する試み自体が他テストへ影響しないよう、書き込み可否だけを確認する
        if (HashtagProvider.OfficialHashtagMap is IDictionary<string, string> map)
        {
            Assert.True(map.IsReadOnly);
        }
    }

    [Fact]
    public void GetHashtags_チーム名の大文字小文字で公式タグの対応が変わらない()
    {
        var provider = new HashtagProvider(new Dictionary<string, string> { { "Example", "OfficialTag" } });

        Assert.Contains("#OfficialTag", provider.GetHashtags("example"));
    }

    [Fact]
    public void GetHashtags_公式タグがあるチームは公式タグと元チーム名タグの両方を返す()
    {
        var provider = new HashtagProvider(new Dictionary<string, string> { { "Red Sox", "DirtyWater" } });

        Assert.Equal("#DirtyWater #RedSox", provider.GetHashtags("Red Sox"));
    }

    [Fact]
    public void GetHashtags_公式タグがチーム名と同じチームはタグを重複させない()
    {
        var provider = new HashtagProvider(new Dictionary<string, string> { { "White Sox", "WhiteSox" } });

        Assert.Equal("#WhiteSox", provider.GetHashtags("White Sox"));
    }

    [Fact]
    public void GetHashtags_公式タグがないチームはチーム名タグのみを返す()
    {
        var provider = new HashtagProvider(new Dictionary<string, string>());

        Assert.Equal("#Athletics", provider.GetHashtags("Athletics"));
    }

    [Fact]
    public void GetHashtags_チーム名のスペースは除去される()
    {
        var provider = new HashtagProvider(new Dictionary<string, string>());

        Assert.Equal("#WhiteSox", provider.GetHashtags("White Sox"));
    }
}
