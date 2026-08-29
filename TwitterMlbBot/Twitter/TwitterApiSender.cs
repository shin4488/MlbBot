using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TwitterMlbBot.Authorization;
using TwitterMlbBot.Composing;

namespace TwitterMlbBot.Twitter
{
    /// <summary>
    /// X API v2（OAuth1.0a署名）でツイートを投稿する送信先
    /// </summary>
    internal class TwitterApiSender : ITweetSender
    {
        private static readonly string twitterEndpoint = "https://api.twitter.com/2/tweets";
        private static readonly HttpClient client = new HttpClient()
        {
            // Lambdaタイムアウト（15秒）より先に打ち切り、原因を特定しやすくする
            Timeout = TimeSpan.FromSeconds(10),
        };
        // X APIの503エラー（短時間での連続POSTによる制限）を防ぐための送信後インターバル
        // （Lambdaの15秒タイムアウトに引っかからないよう1秒とする）
        private static readonly TimeSpan postInterval = TimeSpan.FromSeconds(1);
        private readonly OAuth1 authorization;
        private readonly ILogger<TwitterApiSender> logger;

        public TwitterApiSender(OAuth1 authorization, ILogger<TwitterApiSender> logger)
        {
            this.authorization = authorization;
            this.logger = logger;
        }

        public async Task<bool> SendAsync(TweetContent tweetContent)
        {
            // 各リクエストごとに新しいタイムスタンプとnonceを含んだOAuth署名を生成する
            string authorizationContent = this.authorization.CreateAuthorizationData(twitterEndpoint);
            string requestBody = JsonSerializer.Serialize(new { text = tweetContent.Text });
            using var request = new HttpRequestMessage(HttpMethod.Post, twitterEndpoint);
            request.Headers.ExpectContinue = false; // X API側の503対策として `Expect: 100-continue` を無効化
            request.Headers.Add("Authorization", $"OAuth {authorizationContent}");
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                this.logger.LogWarning("Tweet failed: {StatusCode} - {ResponseBody}", response.StatusCode, responseContent);
            }
            await Task.Delay(postInterval);
            return response.IsSuccessStatusCode;
        }
    }
}
