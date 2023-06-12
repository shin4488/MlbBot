using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace TwitterMlbBot.Twitter
{
    class TwitterService
    {
        private const int _teamNamePadding = 12;
        private const int _digitPadding = 2;
        private readonly string consumerKey;
        private readonly string consumerSecret;
        private readonly string accessKey;
        private readonly string accessSecret;
        private static readonly string twitterEndpoint = "https://api.twitter.com/2/tweets";
        private static readonly HttpClient client = new HttpClient();

        public TwitterService()
        {
            // WebAPI認証用データ取得
            Dictionary<string, string> apiKeyConfig = ProcessUtility.ReadAppConfig("twitter");
            // AWSのlambda関数使用時はApp.configの値がnullとなるためnullチェックを入れる
            this.consumerKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerKey", "CONSUMER_KEY");
            this.consumerSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerSecret", "CONSUMER_SECRET");
            this.accessKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessKey", "ACCESS_KEY");
            this.accessSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessSecret", "ACCESS_SECRET");
            string authorizationContent = CreateAuthorizationData();
            client.DefaultRequestHeaders.Add("Authorization", $"OAuth {authorizationContent}");
        }

        private string CreateAuthorizationData() {
            var timstamp = CreateTimestamp();
            var nonce = CreateNonce();
            var signatureBase64 = CreateSignature(twitterEndpoint, "POST", nonce, timstamp);
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

            var sigBaseString = CombineQueryParams(parameters);
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

        /// <summary>
        /// データからツイート文を作成し、ツイート実行
        /// </summary>
        /// <param name="param">データ</param>
        public async Task CreateTweet(Param param)
        {
            List<string> targetTweetContentList = new List<string>();

            // 最初に今日の日付のみツイートする
            string todayDate = DateTime.Now.ToShortDateString();
            targetTweetContentList.Add(todayDate);

            foreach (ParamByKey teamsByKey in param.TeamsList)
            {
                // 各チームのデータからツイート文を作成
                List<string> messagesByTeam = teamsByKey.Teams
                    .Select(team =>
                    {
                        // ツイート文は「<順位> : <チーム名> : <勝ち数> : <負け数> : <ゲーム差>」
                        string teamTweetMessage =
                            team.Ranking.ToString() + ". " +
                            team.Name.PadRight(_teamNamePadding) + " : " +
                            team.Wins.ToString().PadRight(_digitPadding) + " : " +
                            team.Losses.ToString().PadRight(_digitPadding) + " : " +
                            team.GamesBehind.ToString();
                        return teamTweetMessage;
                    }).ToList();

                string tweetMessage =
                    $"{teamsByKey.Key.League} | {teamsByKey.Key.Division} (Win : Loss : Behind)\n" +
                    // チーム順位
                    $"{string.Join('\n', messagesByTeam)}\n" +
                    // タグ付けメッセージ
                    teamsByKey.TagMessage;

                // 地区ごとにツイート対象を保持
                targetTweetContentList.Add(tweetMessage);
            }

            // 順位データが存在する場合のみ、一斉にツイート実行
            // 順位データが存在しない場合は、日付データのみリストに保持されているため、要素数1となる
            if (targetTweetContentList.Count <= 1)
            {
                return;
            }

            foreach (string tweetContent in targetTweetContentList)
            {
                await Task.Run(async () =>
                {
                    await ExecuteTweet(tweetContent);
                });
            }
        }

        /// <summary>
        /// ツイートの実行
        /// </summary>
        /// <param name="tweetMessage">ツイート文</param>
        /// <returns></returns>
        public async Task ExecuteTweet(string tweetMessage)
        {
            string requestBody = JsonSerializer.Serialize(new { text = tweetMessage });
            await Task.Run(async () => {
                await client.PostAsync(
                    new Uri(twitterEndpoint),
                    new StringContent(requestBody, Encoding.UTF8, "application/json")
                );
            });
        }
    }
}
