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
            if (await this.ShouldSkipForOffSeasonAsync(year, date))
            {
                return;
            }

            IReadOnlyList<TeamStanding> standings = await this.standingsProvider.GetStandingsAsync(year);
            IReadOnlyList<DivisionStanding> divisions = DivisionStanding.FromStandings(standings);
            List<TweetContent> tweetContentList = new(this.composer.Compose(divisions, date));

            if (WildCardStanding.IsPlayoffRacePeriod(date))
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
                    // 文字数はXの重み付きルールで数えているが、絵文字の結合シーケンス等は安全側に多く数えるため、
                    // 超過と判定しても送信を止めず警告にとどめる（実際に超過していればX APIが拒否し、送信失敗として記録される）
                    this.logger.LogWarning(
                        "文面が上限（{Limit}字）を超えている可能性があります（{Count}字）",
                        TweetContent.CharacterLimit, tweetContent.CharacterCount);
                }
                if (await this.TrySendAsync(tweetContent))
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

        /// <summary>
        /// ツイートを1件送信し、成否を返す。
        /// 送信先が例外を投げた場合（タイムアウト・ネットワーク障害等）も「その1件の失敗」として扱い、
        /// 残りの地区のツイートまで道連れにしない。全件失敗した場合の扱いは呼び出し側が決める
        /// </summary>
        private async Task<bool> TrySendAsync(TweetContent tweetContent)
        {
            try
            {
                return await this.tweetSender.SendAsync(tweetContent);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "ツイートの送信中に例外が発生しました。残りのツイートは続行します");
                return false;
            }
        }

        /// <summary>
        /// シーズン状況からツイートを見送るべきかを判定する。
        /// レギュラーシーズン終了後は順位が動かないため、凍結した順位の投稿と
        /// X API・MLB APIの無駄な消費をオフシーズン中ずっと防ぐ
        /// </summary>
        private async Task<bool> ShouldSkipForOffSeasonAsync(int year, DateOnly date)
        {
            SeasonCalendar season;
            try
            {
                season = await this.seasonCalendarProvider.GetSeasonCalendarAsync(year);
            }
            catch (Exception exception)
            {
                if (SeasonCalendar.IsClearlyOffSeason(date))
                {
                    // 明らかにシーズン外は日程が不明でも投稿対象がなく実害ゼロのため、メール通知にはせず静かに見送る
                    this.logger.LogWarning(exception, "シーズン日程の取得に失敗しましたが、明らかにシーズン外のためツイートせず終了します");
                    return true;
                }
                // シーズン中でありうる期間は、日程が不明でもツイートを止めない
                // （このエラーログはログ監視アラームが拾い、メール通知される）
                this.logger.LogError(exception, "シーズン日程の取得に失敗しましたが、シーズン中の可能性があるため投稿を続行します");
                return false;
            }

            if (season.IsFinished(date))
            {
                this.logger.LogInformation(
                    "レギュラーシーズン終了後のためツイートしません（終了日: {EndDate}）", season.RegularSeasonEndDate);
                return true;
            }
            return false;
        }
    }
}
