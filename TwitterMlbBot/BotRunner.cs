using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;

namespace TwitterMlbBot
{
    /// <summary>
    /// 「順位取得 → 文面組み立て → 送信」のオーケストレーションだけを持つクラス
    /// </summary>
    internal class BotRunner
    {
        private readonly IStandingsProvider standingsProvider;
        private readonly TweetComposer composer;
        private readonly ITweetSender tweetSender;

        public BotRunner(IStandingsProvider standingsProvider, TweetComposer composer, ITweetSender tweetSender)
        {
            this.standingsProvider = standingsProvider;
            this.composer = composer;
            this.tweetSender = tweetSender;
        }

        /// <summary>
        /// 指定した年の順位を取得し、地区ごとにツイートする
        /// </summary>
        /// <param name="year">対象の西暦年</param>
        public async Task RunAsync(int year)
        {
            List<TeamStanding> standings = await this.standingsProvider.GetStandingsAsync(year);
            IReadOnlyList<string> tweetContentList = this.composer.Compose(standings);

            // 順位データが存在しない場合（シーズンオフ等）はツイートしない
            if (tweetContentList.Count == 0)
            {
                return;
            }

            int successCount = 0;
            foreach (string tweetContent in tweetContentList)
            {
                if (await this.tweetSender.SendAsync(tweetContent))
                {
                    successCount++;
                }
            }

            Console.WriteLine($"Tweets sent: {successCount}/{tweetContentList.Count}");
            if (successCount == 0)
            {
                // 全件失敗はLambda実行をエラーで終わらせ、CloudWatchのエラーメトリクスで検知できるようにする
                // （一部失敗は重複コンテンツ拒否など正常系でも起きるため、エラーにしない）
                throw new Exception($"ツイートが全{tweetContentList.Count}件失敗しました。失敗理由は直前のログを参照。");
            }
        }
    }
}
