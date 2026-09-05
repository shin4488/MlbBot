namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// 1チームの成績（不変の値オブジェクト）。
    /// 勝率・ゲーム差・順位付けといった「成績から導かれるルール」はすべてここに置き、
    /// 順位表（DivisionStanding / WildCardStanding）はこのルールを組み合わせるだけにする
    /// </summary>
    internal record TeamStanding
    {
        public string Name { get; }
        public string League { get; }
        public string Division { get; }
        public int Wins { get; }
        public int Losses { get; }

        /// <summary>
        /// 順位付けに使える成績を作る。作成後に勝敗などを書き換え、検証を迂回することはできない
        /// </summary>
        /// <param name="name">チーム名（例: "Yankees"）</param>
        /// <param name="league">リーグ名（"AL" / "NL"）</param>
        /// <param name="division">地区名（"East" / "Central" / "West"）</param>
        /// <param name="wins">勝ち数</param>
        /// <param name="losses">負け数</param>
        public TeamStanding(string name, string league, string division, int wins, int losses)
        {
            bool hasTeamIdentity = !string.IsNullOrWhiteSpace(name)
                && !string.IsNullOrWhiteSpace(league)
                && !string.IsNullOrWhiteSpace(division);
            bool hasValidWinLossRecord = wins >= 0 && losses >= 0;
            bool canRankTeam = hasTeamIdentity && hasValidWinLossRecord;

            // API以外から作る場合も同じ規則を守る。未消化の0勝・0敗は有効だが、負の勝敗は使えない。
            if (!canRankTeam)
            {
                throw new ArgumentException("チーム名・所属リーグ・地区・勝敗に不足や誤りがあるため、順位表を作成できません。");
            }

            Name = name;
            League = league;
            Division = division;
            Wins = wins;
            Losses = losses;
        }

        /// <summary>
        /// 勝率。勝敗から一意に決まるため外部データとして持ち回らず算出する
        /// （APIの値は小数3桁に丸められており、丸めた値で同率に見える2チームも正しい勝率で順位付けできる）
        /// </summary>
        public double Percentage => Wins + (long)Losses == 0 ? 0 : (double)Wins / (Wins + (long)Losses);

        /// <summary>
        /// 基準チームとのゲーム差。基準より上位（貯金が多い）なら負の値になる
        /// </summary>
        /// <param name="baseline">基準チーム（地区順位では首位、ワイルドカードではプレーオフ圏ボーダー）</param>
        public float GamesBehind(TeamStanding baseline)
        {
            // ゲーム差の定義: （基準チームの貯金 - 対象チームの貯金）/ 2
            // 外部入力がintの上限付近でも、加減算の途中で符号が反転しないようlongで計算する。
            return ((baseline.Wins - (long)baseline.Losses) - (Wins - (long)Losses)) / 2f;
        }

        /// <summary>
        /// 順位付けの規則で並べ替える（勝率降順、同率なら勝ち数降順）。
        /// 地区順位・ワイルドカード順位のどちらもこの規則で決める
        /// </summary>
        public static IReadOnlyList<TeamStanding> OrderByRank(IEnumerable<TeamStanding> teams)
        {
            // 遅延評価のまま返すと、入力リストの後の変更で順位が変わる。
            // この時点の結果を確定して返す（各要素は不変recordなので要素の複製は不要）
            return teams
                .OrderByDescending(team => team.Percentage)
                .ThenByDescending(team => team.Wins)
                .ToList().AsReadOnly();
        }
    }
}
