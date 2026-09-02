namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// 順位表の1行（順位が確定したチーム）
    /// </summary>
    /// <param name="Rank">順位（1位から）</param>
    /// <param name="Team">チームの成績</param>
    /// <param name="GamesBehind">
    /// 順位表の基準チームとのゲーム差。基準は順位表の種類で異なる
    /// （地区順位: 首位、ワイルドカード順位: プレーオフ圏ボーダー）。基準より上位なら負の値
    /// </param>
    internal record RankedTeam(int Rank, TeamStanding Team, float GamesBehind);
}
