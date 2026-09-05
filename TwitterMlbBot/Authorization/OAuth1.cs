using System.Security.Cryptography;
using System.Text;

namespace TwitterMlbBot.Authorization
{
    /// <summary>
    /// OAuth 1.0a（HMAC-SHA1）のAuthorizationヘッダ値を生成する。
    /// X API v2の投稿にはユーザーコンテキスト認証が必要で、OAuth 1.0aのアクセストークンは失効しないため、
    /// トークン更新の仕組みを持たない無人ボットに向く
    /// </summary>
    internal class OAuth1
    {
        private readonly string consumerKey;
        private readonly string consumerSecret;
        private readonly string accessKey;
        private readonly string accessSecret;
        private readonly Func<string> timestampProvider;
        private readonly Func<string> nonceProvider;

        public OAuth1(string consumerKey, string consumerSecret, string accessKey, string accessSecret)
            : this(consumerKey, consumerSecret, accessKey, accessSecret, CreateTimestamp, CreateNonce)
        {
        }

        /// <summary>
        /// テスト用にタイムスタンプ・nonceの生成を差し替えられるコンストラクタ
        /// （署名は両値に依存するため、固定しないと出力を検証できない）
        /// </summary>
        internal OAuth1(
            string consumerKey, string consumerSecret, string accessKey, string accessSecret,
            Func<string> timestampProvider, Func<string> nonceProvider)
        {
            this.consumerKey = consumerKey;
            this.consumerSecret = consumerSecret;
            this.accessKey = accessKey;
            this.accessSecret = accessSecret;
            this.timestampProvider = timestampProvider;
            this.nonceProvider = nonceProvider;
        }

        /// <summary>
        /// POSTリクエスト用のAuthorizationヘッダ値（"OAuth " に続く部分）を生成する
        /// </summary>
        /// <param name="endpoint">リクエスト先URL（クエリ文字列なし）</param>
        public string CreateAuthorizationData(string endpoint)
        {
            string timestamp = timestampProvider();
            string nonce = nonceProvider();
            string signatureBase64 = CreateSignature(endpoint, "POST", nonce, timestamp);
            return $@"oauth_consumer_key=""{Uri.EscapeDataString(consumerKey)}""" +
                    $@",oauth_token=""{Uri.EscapeDataString(accessKey)}""" +
                    $@",oauth_signature_method=""HMAC-SHA1""" +
                    $@",oauth_timestamp=""{Uri.EscapeDataString(timestamp)}""" +
                    $@",oauth_nonce=""{Uri.EscapeDataString(nonce)}""" +
                    $@",oauth_version=""1.0""" +
                    $@",oauth_signature=""{Uri.EscapeDataString(signatureBase64)}""";
        }

        private string CreateSignature(string url, string method, string nonce, string timestamp)
        {
            // 署名対象はOAuthパラメータのみ。X API v2はJSONボディで投稿するため、ボディの内容は署名に含めない
            // （OAuth 1.0aの署名対象になるのは application/x-www-form-urlencoded のボディだけ）
            var parameters = new Dictionary<string, string>
            {
                { "oauth_consumer_key", consumerKey },
                { "oauth_nonce", nonce },
                { "oauth_signature_method", "HMAC-SHA1" },
                { "oauth_timestamp", timestamp },
                { "oauth_token", accessKey },
                { "oauth_version", "1.0" },
            };

            string signatureBaseString =
                method + "&" +
                Uri.EscapeDataString(url) + "&" +
                Uri.EscapeDataString(CombineQueryParams(parameters));
            string compositeKey =
                Uri.EscapeDataString(consumerSecret) + "&" +
                Uri.EscapeDataString(accessSecret);
            using var hasher = new HMACSHA1(Encoding.ASCII.GetBytes(compositeKey));
            return Convert.ToBase64String(hasher.ComputeHash(Encoding.ASCII.GetBytes(signatureBaseString)));
        }

        private static string CreateTimestamp()
        {
            // OAuth 1.0a では時刻をUNIXタイムスタンプ（切り捨て）で指定する必要がある
            // X APIは未来のタイムスタンプを不正なリクエストとして弾くため、確実に時刻の切り捨てが行われるようにする
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        }

        private static string CreateNonce()
        {
            // 同一ミリ秒での衝突が理論上ありうるTicksではなく、一意性が保証されるGUIDを使う
            return Guid.NewGuid().ToString("N");
        }

        private static string CombineQueryParams(IReadOnlyDictionary<string, string> parameters)
        {
            // OAuth 1.0a仕様では署名ベース文字列のパラメータをキーの辞書順に並べ、"key=value" を & で連結する
            return string.Join("&", parameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => parameter.Key + "=" + Uri.EscapeDataString(parameter.Value)));
        }
    }
}
