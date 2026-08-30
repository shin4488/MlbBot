namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// レギュラーシーズンの日程。「いつまで順位をツイートする価値があるか」の判断はこのデータ自身が持つ
    /// </summary>
    internal record SeasonCalendar(DateOnly RegularSeasonEndDate)
    {
        /// <summary>
        /// 指定した試合日の時点でレギュラーシーズンが終了しているか。
        /// 最終戦当日（=終了日）は最終順位をツイートする価値があるため「終了後」に含めない
        /// </summary>
        public bool IsFinished(DateOnly gameDate)
        {
            return gameDate > this.RegularSeasonEndDate;
        }

        // MLBのレギュラーシーズンが行われうる期間（開幕は最も早くて3月、最終戦は最も遅くて10月）
        private const int EarliestSeasonMonth = 3;
        private const int LatestSeasonMonth = 10;

        /// <summary>
        /// 明らかにシーズン外の時期（11〜2月）か。
        /// その年の日程が取得できなくても、この期間は「シーズン中でない」と断定できる
        /// </summary>
        public static bool IsClearlyOffSeason(DateOnly date)
        {
            return date.Month < EarliestSeasonMonth || date.Month > LatestSeasonMonth;
        }
    }
}
