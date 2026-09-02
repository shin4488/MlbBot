using TwitterMlbBot.Mlb;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// テスト用のチーム成績データ生成。
/// 各テストが独自のヘルパーを持つとTeamStandingの形が変わるたびに全ファイルを直すことになるため、ここに1つだけ置く
/// </summary>
internal static class Teams
{
    public static TeamStanding Create(string league, string division, string name, int wins, int losses)
    {
        return new TeamStanding(name, league, division, wins, losses);
    }
}
