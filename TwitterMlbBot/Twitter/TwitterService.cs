using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TwitterMlbBot.Authorization;

namespace TwitterMlbBot.Twitter
{
    class TwitterService
    {
        private const int teamNamePadding = 12;
        private const int digitPadding = 2;
        private static readonly string twitterEndpoint = "https://api.twitter.com/2/tweets";
        private static readonly HttpClient client = new HttpClient();
        private readonly OAuth1 authorization;

        public TwitterService()
        {
            Dictionary<string, string> apiKeyConfig = ProcessUtility.ReadAppConfig("twitter");
            string consumerKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerKey", "CONSUMER_KEY");
            string consumerSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerSecret", "CONSUMER_SECRET");
            string accessKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessKey", "ACCESS_KEY");
            string accessSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessSecret", "ACCESS_SECRET");

            // 認証情報はインスタンス変数に保持し、リクエストごとに署名（nonceやtimestamp）を生成するようにする
            this.authorization = new OAuth1(consumerKey, consumerSecret, accessKey, accessSecret);
        }

        /// <summary>
        /// データからツイート文を作成し、ツイート実行
        /// </summary>
        /// <param name="param">データ</param>
        public async Task CreateTweet(Param param)
        {
            // 最初に今日の日付のみツイートする
            string todayDate = DateTime.Now.ToShortDateString();
            List<string> targetTweetContentList = new List<string> { todayDate };

            foreach (ParamByKey teamsByKey in param.TeamsList)
            {
                // 各チームのデータからツイート文を作成
                var standingBuffer = new StringBuilder();
                standingBuffer
                    .Append("⚾ ")
                    .Append(teamsByKey.Key.League)
                    .Append(" | ")
                    .Append(teamsByKey.Key.Division)
                    .Append(" ⚾️ ")
                    .AppendLine("Win : Loss : Behind");
                teamsByKey.Teams.ForEach(team =>
                {
                    // ツイート文は「<順位> : <チーム名> : <勝ち数> : <負け数> : <ゲーム差>」
                    standingBuffer
                        .Append(team.Ranking.ToString()).Append(". ")
                        .Append(team.Name.PadRight(teamNamePadding)).Append(" : ")
                        .Append(team.Wins.ToString().PadRight(digitPadding)).Append(" : ")
                        .Append(team.Losses.ToString().PadRight(digitPadding)).Append(" : ")
                        .AppendLine(team.GamesBehind.ToString());
                });
                standingBuffer.Append(teamsByKey.TagMessage);

                // 地区ごとにツイート対象を保持
                targetTweetContentList.Add(standingBuffer.ToString());
            }

            // 順位データが存在する場合のみ、一斉にツイート実行
            // 順位データが存在しない場合は、日付データのみリストに保持されているため、要素数1となる
            if (targetTweetContentList.Count <= 1)
            {
                return;
            }

            foreach (string tweetContent in targetTweetContentList)
            {
                await ExecuteTweet(tweetContent);
            }
        }

        /// <summary>
        /// ツイートの実行
        /// </summary>
        /// <param name="tweetMessage">ツイート文</param>
        /// <returns></returns>
        public async Task ExecuteTweet(string tweetMessage)
        {
            // 各リクエストごとに新しいタイムスタンプとnonceを含んだOAuth署名を生成する
            string authorizationContent = this.authorization.CreateAuthorizationData(twitterEndpoint);
            string requestBody = JsonSerializer.Serialize(new { text = tweetMessage });
            var request = new HttpRequestMessage(HttpMethod.Post, twitterEndpoint);
            request.Headers.Add("Authorization", $"OAuth {authorizationContent}");
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            // Lambda等でエラー原因が分かるようにログを出力する
            if (!response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Tweet failed: {response.StatusCode} - {responseContent}");
            }
        }
    }
}
