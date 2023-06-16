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

        public OAuth1(string consumerKey, string consumerSecret, string accessKey, string accessSecret)
        {
            this.consumerKey = consumerKey;
            this.consumerSecret = consumerSecret;
            this.accessKey = accessKey;
            this.accessSecret = accessSecret;
        }

        public string CreateAuthorizationData(string endpoint) {
            string timstamp = this.CreateTimestamp();
            string nonce = this.CreateNonce();
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

        private string CreateTimestamp()
        {
            var totalSeconds = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc))
                .TotalSeconds;
            return Convert.ToInt64(totalSeconds).ToString();
        }

        private string CreateNonce()
        {
            return Convert.ToBase64String(
                new ASCIIEncoding().GetBytes(
                    DateTime.Now.Ticks.ToString()));
        }

        public string CombineQueryParams(Dictionary<string, string> parameters)
        {
            if (parameters == null || !parameters.Any())
            {
                return string.Empty;
            }

            var buffer = new StringBuilder();
            foreach (var param in parameters)
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