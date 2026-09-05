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
        private readonly HttpClient client;
        // X APIは短時間の連続POSTを503で拒否するため、投稿と投稿の間に空ける最低間隔
        private static readonly TimeSpan postInterval = TimeSpan.FromSeconds(1);
        private readonly OAuth1 authorization;
        private readonly ILogger<TwitterApiSender> logger;
        private bool hasSentBefore;

        public TwitterApiSender(HttpClient client, OAuth1 authorization, ILogger<TwitterApiSender> logger)
        {
            this.client = client;
            this.authorization = authorization;
            this.logger = logger;
        }

        public async Task<bool> SendAsync(TweetContent tweetContent)
        {
            if (hasSentBefore)
            {
                // 間隔は「送信後」ではなく「次の送信前」に空ける。最後の1件の後に待つ必要はなく、
                // Lambdaの実行時間（課金）を1秒無駄にしないため
                await Task.Delay(postInterval);
            }
            hasSentBefore = true;

            // 各リクエストごとに新しいタイムスタンプとnonceを含んだOAuth署名を生成する
            string authorizationContent = authorization.CreateAuthorizationData(twitterEndpoint);
            string requestBody = JsonSerializer.Serialize(new { text = tweetContent.Text });
            using var request = new HttpRequestMessage(HttpMethod.Post, twitterEndpoint);
            request.Headers.ExpectContinue = false; // X API側の503対策として `Expect: 100-continue` を無効化
            request.Headers.Add("Authorization", $"OAuth {authorizationContent}");
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // 応答を受け取れなくても投稿済みの可能性があるので、ここで自動再送はしない。
            // 次の文面への続行と全件失敗の判定はBotRunnerが担う
            using HttpResponseMessage response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                // 応答には認証情報や投稿文面が含まれうる。調査用の応答コードだけをログに残す。
                logger.LogWarning("Xへの投稿が受け付けられませんでした（応答コード: {StatusCode}）。",
                    response.StatusCode);
            }
            return response.IsSuccessStatusCode;
        }
    }
}
