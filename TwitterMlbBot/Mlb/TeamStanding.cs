namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB APIレスポンスのチーム順位データ
    /// 使用するプロパティのみ定義する（未定義の項目はデシリアライズ時に無視される。
    /// APIが返す全項目は docs/tweet-content-ideas.md 項目4の表を参照）
    /// </summary>
    internal class TeamStanding
    {
        /// <summary>
        /// チーム名（例: "Yankees"）
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// リーグ名（"AL" / "NL"）
        /// </summary>
        public string League { get; set; } = "";

        /// <summary>
        /// 地区名（"East" / "Central" / "West"。All-Star擬似チームのみリーグ名と同名）
        /// </summary>
        public string Division { get; set; } = "";

        /// <summary>
        /// 勝ち数
        /// </summary>
        public int Wins { get; set; }

        /// <summary>
        /// 負け数
        /// </summary>
        public int Losses { get; set; }

        /// <summary>
        /// 勝率
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 1つ上の順位のチームとのゲーム差
        /// </summary>
        public float? GamesBehind { get; set; }
    }
}
