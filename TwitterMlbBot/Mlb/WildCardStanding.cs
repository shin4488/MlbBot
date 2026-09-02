namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// リーグ単位のワイルドカード順位表
    /// 「地区首位を除いたチームの順位順」であることを型として保証する。
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
        /// ワイルドカード順位順（1位から）のチームリスト。ゲーム差はプレーオフ圏ボーダー（最終枠のチーム）基準
        /// </summary>
        public IReadOnlyList<RankedTeam> RankedTeams { get; }

        private WildCardStanding(string league, IReadOnlyList<RankedTeam> rankedTeams)
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
                    List<TeamStanding> contenders = TeamStanding.OrderByRank(league
                        .SelectMany(division => division.RankedTeams)
                        // 地区首位はワイルドカード争いの対象外（地区優勝枠でプレーオフに進む）
                        .Where(ranked => ranked.Rank > 1)
                        .Select(ranked => ranked.Team))
                        .ToList();

                    // ゲーム差はプレーオフ圏ボーダー（最終枠のチーム）を基準に計算する。
                    // 圏内チームの値は負になるが、表示側が圏外のみ表示するため問題にしない。
                    // 対象チームがボーダーの位置まで存在しない場合（データ欠け等）はゲーム差を0とする
                    TeamStanding? playoffLine = contenders.Count >= PlayoffSpots ? contenders[PlayoffSpots - 1] : null;
                    List<RankedTeam> rankedTeams = contenders
                        .Select((team, index) => new RankedTeam(
                            index + 1, team, playoffLine == null ? 0 : team.GamesBehind(playoffLine)))
                        .ToList();
                    return new WildCardStanding(league.Key, rankedTeams);
                })
                .Where(wildCard => wildCard.RankedTeams.Count > 0)
                .ToList();
        }

        // ワイルドカード順位に情報価値が出るのはプレーオフ争いが本格化する8月以降
        // （シーズン序盤から出すと読む価値が薄いうえ、X APIの従量課金も無駄になる）
        private const int PlayoffRaceStartMonth = 8;

        /// <summary>
        /// ワイルドカード順位をツイートする価値のある時期（プレーオフ争いの本格化以降）か
        /// </summary>
        public static bool IsPlayoffRacePeriod(DateOnly gameDate)
        {
            return gameDate.Month >= PlayoffRaceStartMonth;
        }
    }
}
