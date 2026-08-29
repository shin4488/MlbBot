namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// 地区内の順位が確定したチーム（値オブジェクト）
    /// </summary>
    /// <param name="Rank">地区内順位（1位から）</param>
    /// <param name="Team">チームの順位データ</param>
    internal record RankedTeam(int Rank, TeamStanding Team);
}
