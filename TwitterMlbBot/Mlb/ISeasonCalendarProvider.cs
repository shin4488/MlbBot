namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// シーズン日程の取得元
    /// </summary>
    internal interface ISeasonCalendarProvider
    {
        /// <summary>
        /// 指定した年のレギュラーシーズン日程を取得する
        /// </summary>
        /// <param name="year">対象の西暦年</param>
        Task<SeasonCalendar> GetSeasonCalendarAsync(int year);
    }
}
