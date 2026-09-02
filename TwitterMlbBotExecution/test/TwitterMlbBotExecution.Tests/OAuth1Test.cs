using TwitterMlbBot.Authorization;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// OAuth 1.0a署名生成（OAuth1.CreateAuthorizationData）のテスト
/// タイムスタンプ・nonceを固定入力に差し替え、署名値を検証する。
/// 期待値は実装とは独立に、OAuth 1.0a仕様（RFC 5849）どおり
/// 「POST&url&ソート済みパラメータ をキー consumer_secret&access_secret でHMAC-SHA1」
/// をPythonで計算して導出したもの。パラメータの辞書順ソートやURLエンコードが崩れれば
/// 署名値が変わるため、内部の結合処理を個別にテストせずこの1本で仕様を担保する
/// </summary>
public class OAuth1Test
{
    [Fact]
    public void CreateAuthorizationData_固定入力に対する署名が仕様どおり生成される()
    {
        var oauth = new OAuth1(
            "consumer-key", "consumer-secret", "access-key", "access-secret",
            timestampProvider: () => "1700000000",
            nonceProvider: () => "testnonce");

        string authorization = oauth.CreateAuthorizationData("https://api.twitter.com/2/tweets");

        // 独立に導出した期待署名（URLエンコード済み）。ダミーの鍵から計算した値であり機密ではない
        // （gitleaksが高エントロピー文字列として誤検出するため除外指定）
        Assert.Contains(@"oauth_signature=""2hYaKhmOvXjqImlN1KdJPLDQ1Zg%3D""", authorization); // gitleaks:allow
        // ヘッダに必要なパラメータが揃っていること
        Assert.Contains(@"oauth_consumer_key=""consumer-key""", authorization);
        Assert.Contains(@"oauth_token=""access-key""", authorization);
        Assert.Contains(@"oauth_signature_method=""HMAC-SHA1""", authorization);
        Assert.Contains(@"oauth_timestamp=""1700000000""", authorization);
        Assert.Contains(@"oauth_nonce=""testnonce""", authorization);
        Assert.Contains(@"oauth_version=""1.0""", authorization);
    }
}
