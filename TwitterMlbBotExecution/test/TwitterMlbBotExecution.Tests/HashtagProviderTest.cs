using TwitterMlbBot.Composing;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ハッシュタグ生成（HashtagProvider）のテスト
/// タグマップはテスト用に注入し、実際の公式タグマップの中身（毎シーズン変わりうる）には依存させない
/// </summary>
public class HashtagProviderTest
{
    [Fact]
    public void GetHashtags_公式タグがあるチームは公式タグと元チーム名タグの両方を返す()
    {
        var provider = new HashtagProvider(new Dictionary<string, string> { { "Red Sox", "DirtyWater" } });

        Assert.Equal("#DirtyWater #RedSox", provider.GetHashtags("Red Sox"));
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
