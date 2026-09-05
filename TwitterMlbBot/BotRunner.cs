using Microsoft.Extensions.Logging;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;

namespace TwitterMlbBot
{
    /// <summary>
    /// 「シーズン判定 → 順位取得 → 文面組み立て → 送信」の流れと失敗時の方針を持つ。
    /// 接続先はinterfaceで差し替え、外部I/Oのない文面生成はTweetComposerを直接使う。
    /// 実装が1つの純粋な処理まで抽象化せず、読み進める際の行き来を減らす
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
        /// 指定した年の順位を取得し、その日に必要な文面を順に送信する
        /// </summary>
        /// <param name="year">対象の西暦年</param>
        /// <param name="date">ツイート文面に表示する日付</param>
        public async Task RunAsync(int year, DateOnly date)
        {
            if (await ShouldSkipForOffSeasonAsync(year, date))
            {
                return;
            }

            IReadOnlyList<TeamStanding> standings = await standingsProvider.GetStandingsAsync(year);
            IReadOnlyList<TweetContent> tweets = composer.ComposeTweets(standings, date);

            // 順位データが存在しない場合（シーズンオフ等）はツイートしない
            if (tweets.Count == 0)
            {
                logger.LogInformation("順位データが空のためツイートしません");
                return;
            }

            await SendTweetsAsync(tweets);
        }

        private async Task SendTweetsAsync(IReadOnlyList<TweetContent> tweets)
        {
            int successCount = 0;
            foreach (TweetContent tweetContent in tweets)
            {
                if (tweetContent.ExceedsCharacterLimit)
                {
                    // 文字数はXの重み付きルールで数えているが、絵文字の結合シーケンス等は安全側に多く数えるため、
                    // 超過と判定しても送信を止めず警告にとどめる（実際に超過していればX APIが拒否し、送信失敗として記録される）
                    logger.LogWarning(
                        "投稿文面がXの文字数上限（{Limit}字）を超えている可能性があります（{Count}字）。投稿は試みますが、Xに拒否される場合があります",
                        TweetContent.CharacterLimit, tweetContent.CharacterCount);
                }
                if (await TrySendAsync(tweetContent))
                {
                    successCount++;
                }
            }

            logger.LogInformation("Tweets sent: {SuccessCount}/{TotalCount}", successCount, tweets.Count);
            if (successCount == 0)
            {
                // 一部失敗は重複コンテンツ拒否など正常系でも起きるため、全件失敗のみエラーにする
                throw new AllTweetsFailedException(tweets.Count);
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
                return await tweetSender.SendAsync(tweetContent);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "この文面の投稿に失敗しました。残りの文面の投稿は続けます");
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
                season = await seasonCalendarProvider.GetSeasonCalendarAsync(year);
            }
            catch (Exception exception)
            {
                if (SeasonCalendar.IsClearlyOffSeason(date))
                {
                    // 明らかにシーズン外は日程が不明でも投稿対象がなく実害ゼロのため、メール通知にはせず静かに見送る
                    logger.LogWarning(exception, "シーズン日程の取得に失敗しましたが、明らかにシーズン外のためツイートせず終了します");
                    return true;
                }
                // シーズン中でありうる期間は、日程が不明でもツイートを止めない
                // （このエラーログはログ監視アラームが拾い、メール通知される）
                logger.LogError(exception, "シーズン日程の取得に失敗しましたが、シーズン中の可能性があるため投稿を続行します");
                return false;
            }

            if (season.IsFinished(date))
            {
                logger.LogInformation(
                    "レギュラーシーズン終了後のためツイートしません（終了日: {EndDate}）", season.RegularSeasonEndDate);
                return true;
            }
            return false;
        }
    }
}
