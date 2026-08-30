using Microsoft.Extensions.Logging;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;

namespace TwitterMlbBot
{
    /// <summary>
    /// 「シーズン判定 → 順位取得 → 地区順位表化 → 文面組み立て → 送信」のオーケストレーションだけを持つクラス
    /// </summary>
    internal class BotRunner
    {
        private readonly ISeasonCalendarProvider seasonCalendarProvider;
        private readonly IStandingsProvider standingsProvider;
        private readonly TweetComposer composer;
        private readonly ITweetSender tweetSender;
        private readonly ILogger<BotRunner> logger;

        public BotRunner(
            ISeasonCalendarProvider seasonCalendarProvider,
            IStandingsProvider standingsProvider,
            TweetComposer composer,
            ITweetSender tweetSender,
            ILogger<BotRunner> logger)
        {
            this.seasonCalendarProvider = seasonCalendarProvider;
            this.standingsProvider = standingsProvider;
            this.composer = composer;
            this.tweetSender = tweetSender;
            this.logger = logger;
        }

        /// <summary>
        /// 指定した年の順位を取得し、地区ごとにツイートする
        /// </summary>
        /// <param name="year">対象の西暦年</param>
        /// <param name="date">ツイート文面に表示する日付</param>
        public async Task RunAsync(int year, DateOnly date)
        {
            // レギュラーシーズン終了後は順位が動かないため、順位取得もツイートもせずに終了する
            // （凍結した順位の投稿と、X API・MLB APIの無駄な消費をオフシーズン中ずっと防ぐ）
            SeasonCalendar season = await this.seasonCalendarProvider.GetSeasonCalendarAsync(year);
            if (season.IsFinished(date))
            {
                this.logger.LogInformation(
                    "レギュラーシーズン終了後のためツイートしません（終了日: {EndDate}）", season.RegularSeasonEndDate);
                return;
            }

            List<TeamStanding> standings = await this.standingsProvider.GetStandingsAsync(year);
            IReadOnlyList<DivisionStanding> divisions = DivisionStanding.FromStandings(standings);
            List<TweetContent> tweetContentList = new(this.composer.Compose(divisions, date));

            // ワイルドカード順位はプレーオフ争いが本格化する8月以降のみツイートする
            // （シーズン序盤は情報価値が薄く、X APIの従量課金も抑える）
            if (date.Month >= 8)
            {
                tweetContentList.AddRange(
                    this.composer.ComposeWildCards(WildCardStanding.FromDivisions(divisions), date));
            }

            // 順位データが存在しない場合（シーズンオフ等）はツイートしない
            if (tweetContentList.Count == 0)
            {
                this.logger.LogInformation("順位データが空のためツイートしません");
                return;
            }

            int successCount = 0;
            foreach (TweetContent tweetContent in tweetContentList)
            {
                if (tweetContent.ExceedsCharacterLimit)
                {
                    // Xの実際の判定は重み付きの独自カウントのため、ここでは送信を止めず警告のみ出す
                    this.logger.LogWarning(
                        "文面が上限（{Limit}字）を超えている可能性があります（{Count}字）",
                        TweetContent.CharacterLimit, tweetContent.CharacterCount);
                }
                if (await this.tweetSender.SendAsync(tweetContent))
                {
                    successCount++;
                }
            }

            this.logger.LogInformation("Tweets sent: {SuccessCount}/{TotalCount}", successCount, tweetContentList.Count);
            if (successCount == 0)
            {
                // 一部失敗は重複コンテンツ拒否など正常系でも起きるため、全件失敗のみエラーにする
                throw new AllTweetsFailedException(tweetContentList.Count);
            }
        }
    }
}
