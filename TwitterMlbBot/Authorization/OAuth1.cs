using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace TwitterMlbBot.Authorization
{
    public class OAuth1
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

        public string CreateAuthorizationData(string endpoint)
        {
            string timstamp = this.timestampProvider();
            string nonce = this.nonceProvider();
            string signatureBase64 = this.CreateSignature(endpoint, "POST", nonce, timstamp);
            return $@"oauth_consumer_key=""{Uri.EscapeDataString(this.consumerKey)}""" +
                    $@",oauth_token=""{Uri.EscapeDataString(this.accessKey)}""" +
                    $@",oauth_signature_method=""HMAC-SHA1""" +
                    $@",oauth_timestamp=""{Uri.EscapeDataString(timstamp)}""" +
                    $@",oauth_nonce=""{Uri.EscapeDataString(nonce)}""" +
                    $@",oauth_version=""1.0""" +
                    $@",oauth_signature=""{Uri.EscapeDataString(signatureBase64)}""";
        }

        private string CreateSignature(string url, string method, string nonce, string timestamp)
        {
            var parameters = new Dictionary<string, string>();
            parameters.Add("oauth_consumer_key", this.consumerKey);
            parameters.Add("oauth_nonce", nonce);
            parameters.Add("oauth_signature_method", "HMAC-SHA1");
            parameters.Add("oauth_timestamp", timestamp);
            parameters.Add("oauth_token", this.accessKey);
            parameters.Add("oauth_version", "1.0");

            var sigBaseString = this.CombineQueryParams(parameters);
            var signatureBaseString =
                method.ToString() + "&" +
                Uri.EscapeDataString(url) + "&" +
                Uri.EscapeDataString(sigBaseString.ToString());
            var compositeKey =
                Uri.EscapeDataString(this.consumerSecret) + "&" +
                Uri.EscapeDataString(this.accessSecret);
            using (var hasher = new HMACSHA1(Encoding.ASCII.GetBytes(compositeKey)))
            {
                return Convert.ToBase64String(hasher.ComputeHash(
                    Encoding.ASCII.GetBytes(signatureBaseString)));
            }
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

        public string CombineQueryParams(Dictionary<string, string>? parameters)
        {
            if (parameters == null || !parameters.Any())
            {
                return string.Empty;
            }

            var buffer = new StringBuilder();
            // OAuth 1.0a仕様では署名ベース文字列のパラメータをキーの辞書順に並べる必要がある
            foreach (var param in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                buffer
                    .Append(param.Key)
                    .Append("=")
                    .Append(Uri.EscapeDataString(param.Value))
                    .Append("&");
            }

            // 末尾の&以降は、その後に続くパラメータが存在しないため不要
            return buffer.ToString().TrimEnd('&');
        }
    }
}
