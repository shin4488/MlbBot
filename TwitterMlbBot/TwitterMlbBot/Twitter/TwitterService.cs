using CoreTweet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitterMlbBot.Twitter
{
    class TwitterService
    {
        private const int _teamNamePadding = 12;
        private const int _digitPadding = 2;
        private readonly string _consumerKey;
        private readonly string _consumerSecret;
        private readonly string _accessKey;
        private readonly string _accessSecret;
        /// <summary>
        /// Twitter認証で得られるトークン
        /// </summary>
        private readonly Tokens _tokens;

        public TwitterService()
        {
            // WebAPI認証用データ取得
            Dictionary<string, string> apiKeyConfig = ProcessUtility.ReadAppConfig("twitter");
            // AWSのlambda関数使用時はApp.configの値がnullとなるためnullチェックを入れる
            _consumerKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerKey", "CONSUMER_KEY");
            _consumerSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "consumerSecret", "CONSUMER_SECRET");
            _accessKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessKey", "ACCESS_KEY");
            _accessSecret = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "accessSecret", "ACCESS_SECRET");
            // Twitter認証
            _tokens = Tokens.Create(_consumerKey, _consumerSecret, _accessKey, _accessSecret);
        }

        /// <summary>
        /// データからツイート文を作成し、ツイート実行
        /// </summary>
        /// <param name="param">データ</param>
        public void CreateTweet(Param param)
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
                            team.Ranking.ToString() + "  : " +
                            team.Name.PadRight(_teamNamePadding) + " : " +
                            team.Wins.ToString().PadRight(_digitPadding) + " : " +
                            team.Losses.ToString().PadRight(_digitPadding) + " : " +
                            team.GamesBehind.ToString();
                        return teamTweetMessage;
                    }).ToList();

                string tweetMessage =
                    // リーグ名 : 地区名 : Wins : Losses : GamesBehind
                    teamsByKey.Key.League + " : " +
                    teamsByKey.Key.Division + " (Win : Loss : Behind)\n" +
                    // チーム順位
                    string.Join('\n', messagesByTeam) + "\n" +
                    // タグ付けメッセージ
                    teamsByKey.TagMessage;

                // 地区ごとにツイート対象を保持
                targetTweetContentList.Add(tweetMessage);
            }

            // 順位データが存在する場合のみ、一斉にツイート実行
            // 順位データが存在しない場合は、日付データのみリストに保持されているため、要素数1となる
            if (targetTweetContentList.Count > 1)
            {
                targetTweetContentList.ForEach(x => {
                    Status standingsTweetStatus = ExecuteTweet(x);
                });
            }
        }

        /// <summary>
        /// ツイートの実行
        /// </summary>
        /// <param name="tweetMessage">ツイート文</param>
        /// <returns></returns>
        public Status ExecuteTweet(string tweetMessage)
        {
            return _tokens.Statuses.Update(
                new { status = tweetMessage }
            );
        }
    }
}
