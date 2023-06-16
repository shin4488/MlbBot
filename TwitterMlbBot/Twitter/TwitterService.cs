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
        // デプロイテスト
        private const int teamNamePadding = 12;
        private const int digitPadding = 2;
        private static readonly string twitterEndpoint = "https://api.twitter.com/2/tweets";
        private static readonly HttpClient client = new HttpClient();

        public TwitterService()
        {
            Dictionary<string, string> apiKeyConfig = ProcessUtility.ReadAppConfig("twitter");
            string consumerKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerKey", "CONSUMER_KEY");
            string consumerSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerSecret", "CONSUMER_SECRET");
            string accessKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessKey", "ACCESS_KEY");
            string accessSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessSecret", "ACCESS_SECRET");
            // twitter apiはOAuth1.0も使用可能
            // ツイート用のAPIに対しては、認証へッダーは同じであるため1度だけ設定すれば良い
            var authorization = new OAuth1(consumerKey, consumerSecret, accessKey, accessSecret);
            string authorizationContent = authorization.CreateAuthorizationData(twitterEndpoint);
            client.DefaultRequestHeaders.Add("Authorization", $"OAuth {authorizationContent}");
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
                teamsByKey.Teams.ForEach(team =>
                {
                    // ツイート文は「<順位> : <チーム名> : <勝ち数> : <負け数> : <ゲーム差>」
                    standingBuffer
                        .Append(team.Ranking.ToString()).Append(". ")
                        .Append(team.Name.PadRight(teamNamePadding)).Append(" : ")
                        .Append(team.Wins.ToString().PadRight(digitPadding)).Append(" : ")
                        .Append(team.Losses.ToString().PadRight(digitPadding)).Append(" : ")
                        .Append(team.GamesBehind.ToString()).Append("\n");
                });

                string tweetMessage =
                    $"{teamsByKey.Key.League} | {teamsByKey.Key.Division} (Win : Loss : Behind)\n" +
                    standingBuffer.ToString() +
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
