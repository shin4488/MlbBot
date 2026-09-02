namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB APIレスポンスのチーム順位データ（不変）
    /// 使用するプロパティのみ定義する（未定義の項目はデシリアライズ時に無視される）。
    /// プロパティ名はAPIのJSONキー（PascalCase）と一致させてあり、属性なしでデシリアライズできる
    /// </summary>
    internal record TeamStanding
    {
        /// <summary>
        /// チーム名（例: "Yankees"）
        /// </summary>
        public string Name { get; init; } = "";

        /// <summary>
        /// リーグ名（"AL" / "NL"）
        /// </summary>
        public string League { get; init; } = "";

        /// <summary>
        /// 地区名（"East" / "Central" / "West"。All-Star擬似チームのみリーグ名と同名）
        /// </summary>
        public string Division { get; init; } = "";

        /// <summary>
        /// 勝ち数
        /// </summary>
        public int Wins { get; init; }

        /// <summary>
        /// 負け数
        /// </summary>
        public int Losses { get; init; }

        /// <summary>
        /// 勝率
        /// </summary>
        public double Percentage { get; init; }

        /// <summary>
        /// 1つ上の順位のチームとのゲーム差
        /// </summary>
        public float? GamesBehind { get; init; }

        /// <summary>
        /// All-Star用の擬似チームかどうか（リーグ名と地区名が同一になるのが特徴）
        /// </summary>
        public bool IsAllStarPseudoTeam => League == Division;
    }
}
