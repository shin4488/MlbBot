namespace TwitterMlbBot
{
    /// <summary>
    /// 外部APIに使う接続設定を統一する。接続の寿命は呼び出し側が管理する。
    /// </summary>
    internal static class ApiHttpClientFactory
    {
        public static HttpClient Create() => new(new HttpClientHandler
        {
            // 自動転送では独自のAPIキーヘッダーが転送先にも送られる。転送応答は取得失敗として扱う。
            AllowAutoRedirect = false,
            // 認証はリクエストのヘッダーで完結するため、共有クライアントにCookieを保持させない。
            UseCookies = false,
        })
        {
            // 通信が停滞しても、Lambdaの制限時間を使い切る前に失敗を処理できるようにする。
            Timeout = TimeSpan.FromSeconds(10),
            // 順位・日程の小さな応答を想定し、異常な巨大応答によるメモリ消費を制限する。
            MaxResponseContentBufferSize = 1024 * 1024,
        };
    }
}
