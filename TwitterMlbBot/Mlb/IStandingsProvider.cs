namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB順位データの取得元
    /// </summary>
    internal interface IStandingsProvider
    {
        /// <summary>
        /// 指定した年の順位データを取得する
        /// </summary>
        /// <param name="year">対象の西暦年</param>
        /// <returns>全チームの順位データ（シーズン開始前など存在しない場合は空）</returns>
        Task<IReadOnlyList<TeamStanding>> GetStandingsAsync(int year);
    }
}
