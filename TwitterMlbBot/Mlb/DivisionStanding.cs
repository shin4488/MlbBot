namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// 1地区分の順位表
    /// 「RankedTeamsは常に順位順」であることを型として保証する。
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
        /// 順位順（1位から）のチームリスト。ゲーム差は首位基準
        /// </summary>
        public IReadOnlyList<RankedTeam> RankedTeams { get; }

        private DivisionStanding(string league, string division, IReadOnlyList<RankedTeam> rankedTeams)
        {
            League = league;
            Division = division;
            RankedTeams = rankedTeams;
        }

        /// <summary>
        /// 全チームの成績を地区ごとの順位表に変換する
        /// </summary>
        /// <param name="standings">全チームの成績（並び順は問わない）</param>
        /// <returns>地区ごとの順位表</returns>
        public static IReadOnlyList<DivisionStanding> FromStandings(IEnumerable<TeamStanding> standings)
        {
            return standings
                .GroupBy(team => new { team.League, team.Division })
                .Select(teams =>
                {
                    // APIレスポンスの並び順には依存せず、自前の順位付け規則で並べる
                    List<TeamStanding> ordered = TeamStanding.OrderByRank(teams).ToList();
                    TeamStanding leader = ordered[0];
                    return new DivisionStanding(
                        teams.Key.League,
                        teams.Key.Division,
                        ordered
                            .Select((team, index) => new RankedTeam(index + 1, team, team.GamesBehind(leader)))
                            .ToList());
                })
                .ToList();
        }
    }
}
