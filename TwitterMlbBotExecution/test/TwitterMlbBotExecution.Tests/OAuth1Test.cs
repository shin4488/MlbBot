using TwitterMlbBot.Authorization;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// OAuth 1.0a署名ベース文字列のパラメータ結合のテスト
/// OAuth 1.0a仕様（キーの辞書順・URLエンコード・&連結）に基づく検証で、実装の内部構造には依存しない
/// </summary>
public class OAuth1Test
{
    [Fact]
    public void CombineQueryParams_パラメータが空なら空文字列を返す()
    {
        var oauth = new OAuth1("ck", "cs", "ak", "as");

        Assert.Equal(string.Empty, oauth.CombineQueryParams(new Dictionary<string, string>()));
        Assert.Equal(string.Empty, oauth.CombineQueryParams(null));
    }

    [Fact]
    public void CombineQueryParams_キーの辞書順にURLエンコードして連結する()
    {
        var oauth = new OAuth1("ck", "cs", "ak", "as");
        // あえて辞書順と逆の順序で渡す
        var parameters = new Dictionary<string, string>
        {
            { "oauth_token", "token value" },
            { "oauth_nonce", "abc123" },
        };

        string combined = oauth.CombineQueryParams(parameters);

        // 辞書順に並び、値はURLエンコード（スペース→%20）され、&で連結される
        Assert.Equal("oauth_nonce=abc123&oauth_token=token%20value", combined);
    }
}
