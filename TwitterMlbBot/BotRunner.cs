using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;

namespace TwitterMlbBot
{
    /// <summary>
    /// 「順位取得 → 地区順位表化 → 文面組み立て → 送信」のオーケストレーションだけを持つクラス
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
            IReadOnlyList<DivisionStanding> divisions = DivisionStanding.FromStandings(standings);
            IReadOnlyList<TweetContent> tweetContentList = this.composer.Compose(divisions);

            // 順位データが存在しない場合（シーズンオフ等）はツイートしない
            if (tweetContentList.Count == 0)
            {
                return;
            }

            int successCount = 0;
            foreach (TweetContent tweetContent in tweetContentList)
            {
                if (tweetContent.ExceedsCharacterLimit)
                {
                    // Xの実際の判定は重み付きの独自カウントのため、ここでは送信を止めず警告のみ出す
                    Console.WriteLine(
                        $"警告: 文面が上限（{TweetContent.CharacterLimit}字）を超えている可能性があります（{tweetContent.CharacterCount}字）");
                }
                if (await this.tweetSender.SendAsync(tweetContent))
                {
                    successCount++;
                }
            }

            Console.WriteLine($"Tweets sent: {successCount}/{tweetContentList.Count}");
            if (successCount == 0)
            {
                // 一部失敗は重複コンテンツ拒否など正常系でも起きるため、全件失敗のみエラーにする
                throw new AllTweetsFailedException(tweetContentList.Count);
            }
        }
    }
}
