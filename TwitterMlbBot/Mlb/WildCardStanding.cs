namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// リーグ単位のワイルドカード順位表
    /// 「地区首位を除いたチームの勝率順」であることを型として保証する。
    /// sportsdata.ioにもワイルドカード順位のフィールドはあるが、地区首位の扱いが
    /// 実データ上不明瞭だったため、外部仕様に依存せず地区順位表から自前で導出する
    /// </summary>
    internal class WildCardStanding
    {
        /// <summary>
        /// MLBのワイルドカード枠数（プレーオフ圏ボーダーの位置）
        /// </summary>
        public const int PlayoffSpots = 3;

        /// <summary>
        /// リーグ名（"AL" / "NL"）
        /// </summary>
        public string League { get; }

        /// <summary>
        /// ワイルドカード順位順（1位から）のチームリスト
        /// </summary>
        public IReadOnlyList<RankedWildCardTeam> RankedTeams { get; }

        private WildCardStanding(string league, IReadOnlyList<RankedWildCardTeam> rankedTeams)
        {
            League = league;
            RankedTeams = rankedTeams;
        }

        /// <summary>
        /// 地区順位表からリーグごとのワイルドカード順位表を導出する
        /// </summary>
        public static IReadOnlyList<WildCardStanding> FromDivisions(IReadOnlyList<DivisionStanding> divisions)
        {
            return divisions
                .GroupBy(division => division.League)
                .Select(league =>
                {
                    List<TeamStanding> contenders = league
                        .SelectMany(division => division.RankedTeams)
                        // 地区首位はワイルドカード争いの対象外（地区優勝枠でプレーオフに進む）
                        .Where(ranked => ranked.Rank > 1)
                        .Select(ranked => ranked.Team)
                        .OrderByDescending(team => team.Percentage)
                        .ThenByDescending(team => team.Wins)
                        .ToList();

                    // ゲーム差はプレーオフ圏ボーダー（最終枠のチーム）を基準に計算する。
                    // 圏内チームの値は負になるが、表示側が圏外のみ表示するため問題にしない
                    TeamStanding? playoffLine = contenders.Count >= PlayoffSpots ? contenders[PlayoffSpots - 1] : null;
                    List<RankedWildCardTeam> rankedTeams = contenders
                        .Select((team, index) => new RankedWildCardTeam(
                            index + 1, team, GamesBehind(playoffLine, team)))
                        .ToList();
                    return new WildCardStanding(league.Key, rankedTeams);
                })
                .Where(wildCard => wildCard.RankedTeams.Count > 0)
                .ToList();
        }

        private static float GamesBehind(TeamStanding? baseline, TeamStanding team)
        {
            if (baseline == null)
            {
                return 0;
            }
            // ゲーム差の定義: （基準チームの貯金 - 対象チームの貯金）/ 2
            return ((baseline.Wins - baseline.Losses) - (team.Wins - team.Losses)) / 2f;
        }
    }
}
