namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// 1チームの成績（不変の値オブジェクト）。
    /// 勝率・ゲーム差・順位付けといった「成績から導かれるルール」はすべてここに置き、
    /// 順位表（DivisionStanding / WildCardStanding）はこのルールを組み合わせるだけにする
    /// </summary>
    /// <param name="Name">チーム名（例: "Yankees"）</param>
    /// <param name="League">リーグ名（"AL" / "NL"）</param>
    /// <param name="Division">地区名（"East" / "Central" / "West"）</param>
    /// <param name="Wins">勝ち数</param>
    /// <param name="Losses">負け数</param>
    internal record TeamStanding(string Name, string League, string Division, int Wins, int Losses)
    {
        /// <summary>
        /// 勝率。勝敗から一意に決まるため外部データとして持ち回らず算出する
        /// （APIの値は小数3桁に丸められており、丸めた値で同率に見える2チームも正しい勝率で順位付けできる）
        /// </summary>
        public double Percentage => Wins + Losses == 0 ? 0 : (double)Wins / (Wins + Losses);

        /// <summary>
        /// 基準チームとのゲーム差。基準より上位（貯金が多い）なら負の値になる
        /// </summary>
        /// <param name="baseline">基準チーム（地区順位では首位、ワイルドカードではプレーオフ圏ボーダー）</param>
        public float GamesBehind(TeamStanding baseline)
        {
            // ゲーム差の定義: （基準チームの貯金 - 対象チームの貯金）/ 2
            return ((baseline.Wins - baseline.Losses) - (Wins - Losses)) / 2f;
        }

        /// <summary>
        /// 順位付けの規則で並べ替える（勝率降順、同率なら勝ち数降順）。
        /// 地区順位・ワイルドカード順位のどちらもこの規則で決める
        /// </summary>
        public static IEnumerable<TeamStanding> OrderByRank(IEnumerable<TeamStanding> teams)
        {
            return teams
                .OrderByDescending(team => team.Percentage)
                .ThenByDescending(team => team.Wins);
        }
    }
}
