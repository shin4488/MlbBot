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
    }
}
