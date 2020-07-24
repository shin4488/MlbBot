using CoreTweet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitterMlbBot.Twitter
{
    class TwitterService
    {
        private const int _paddingWidth = 13;
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
            _consumerKey = apiKeyConfig["consumerKey"];
            _consumerSecret = apiKeyConfig["consumerSecret"];
            _accessKey = apiKeyConfig["accessKey"];
            _accessSecret = apiKeyConfig["accessSecret"];
            // Twitter認証
            _tokens = Tokens.Create(_consumerKey, _consumerSecret, _accessKey, _accessSecret);
        }

        /// <summary>
        /// データからツイート文を作成し、ツイート実行
        /// </summary>
        /// <param name="param">データ</param>
        public void CreateTweet(Param param)
        {
            // 最初に今日の日付のみツイートする
            string todayDate = DateTime.Now.ToShortDateString();
            Status dateTweetStatus = ExecuteTweet(todayDate);

            foreach (ParamByKey teamsByKey in param.TeamsList)
            {
                // 各チームのデータからツイート文を作成
                List<string> messagesByTeam = teamsByKey.Teams
                    .Select(team =>
                    {
                        // ツイート文は「<順位> : <チーム名> : <ゲーム差>」
                        string teamTweetMessage =
                            team.Ranking.ToString() + "  : " +
                            team.Name.PadRight(_paddingWidth) + ": " +
                            team.GamesBehind.ToString();
                        return teamTweetMessage;
                    }).ToList();

                string tweetMessage =
                    // リーグ名 : 地区名
                    teamsByKey.Key.League + " : " +
                    teamsByKey.Key.Division + "\n" +
                    // チーム順位
                    string.Join('\n', messagesByTeam) + "\n" +
                    // タグ付けメッセージ
                    teamsByKey.TagMessage;

                // 地区ごとにツイート
                Status standingsTweetStatus = ExecuteTweet(tweetMessage);
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
