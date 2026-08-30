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

        /// <summary>
        /// 明らかにシーズン外の時期（11〜2月）か。
        /// MLBのレギュラーシーズンは3〜10月の間でしか行われないため、
        /// その年の日程が取得できなくてもこの期間は「シーズン中でない」と断定できる
        /// </summary>
        public static bool IsClearlyOffSeason(DateOnly date)
        {
            return date.Month >= 11 || date.Month <= 2;
        }
    }
}
