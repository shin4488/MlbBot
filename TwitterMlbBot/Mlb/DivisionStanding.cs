namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// 1地区分の順位表
    /// 「RankedTeamsは常に順位順（勝率降順、同率なら勝ち数降順）」であることを型として保証する。
    /// 生成はFromStandings経由のみ可能で、順不同のインスタンスは作れない
    /// </summary>
    internal class DivisionStanding
    {
        /// <summary>
        /// リーグ名（"AL" / "NL"）
        /// </summary>
        public string League { get; }

        /// <summary>
        /// 地区名（"East" / "Central" / "West"）
        /// </summary>
        public string Division { get; }

        /// <summary>
        /// 順位順（1位から）のチームリスト
        /// </summary>
        public IReadOnlyList<RankedTeam> RankedTeams { get; }

        private DivisionStanding(string league, string division, IReadOnlyList<RankedTeam> rankedTeams)
        {
            League = league;
            Division = division;
            RankedTeams = rankedTeams;
        }

        /// <summary>
        /// 全チームの順位データを地区ごとの順位表に変換する（All-Star擬似チームは除外）
        /// </summary>
        /// <param name="standings">全チームの順位データ（並び順は問わない）</param>
        /// <returns>地区ごとの順位表</returns>
        public static IReadOnlyList<DivisionStanding> FromStandings(IEnumerable<TeamStanding> standings)
        {
            return standings
                .Where(team => !team.IsAllStarPseudoTeam)
                .GroupBy(team => new { team.League, team.Division })
                .Select(teams => new DivisionStanding(
                    teams.Key.League,
                    teams.Key.Division,
                    // APIレスポンスの並び順には依存せず、勝率降順（同率なら勝ち数降順）で順位を決める
                    teams
                        .OrderByDescending(team => team.Percentage)
                        .ThenByDescending(team => team.Wins)
                        .Select((team, index) => new RankedTeam(index + 1, team))
                        .ToList()))
                .ToList();
        }
    }
}
