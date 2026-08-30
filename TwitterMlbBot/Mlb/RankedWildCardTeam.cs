namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// ワイルドカード順位が確定したチーム（値オブジェクト）
    /// </summary>
    /// <param name="Rank">リーグ内のワイルドカード順位（1位から）</param>
    /// <param name="Team">チームの順位データ</param>
    /// <param name="GamesBehindPlayoffLine">
    /// プレーオフ圏ボーダー（ワイルドカード最終枠のチーム）とのゲーム差。
    /// 地区順位のゲーム差（首位との差）とは基準が異なるため別に持つ
    /// </param>
    internal record RankedWildCardTeam(int Rank, TeamStanding Team, float GamesBehindPlayoffLine);
}
