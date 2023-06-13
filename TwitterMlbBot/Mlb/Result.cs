using System.Collections.Generic;

namespace TwitterMlbBot.Mlb
{
    class Result
    {
        /// <summary>
        /// WebAPIで取得したチームリスト
        /// </summary>
        public List<DetailResult> ResultTeamList { get; set; }
    }

    /// <summary>
    /// MLBの各チームのWebAPIレスポンス用リザルトクラス
    /// </summary>
    class DetailResult
    {
        /// <summary>
        /// シーズン（西暦年）
        /// </summary>
        public int Season { get; set; }
        public int SeasonType { get; set; }
        /// <summary>
        /// チームID
        /// </summary>
        public int TeamID { get; set; }
        public string Key { get; set; }
        /// <summary>
        /// 本拠地
        /// </summary>
        public string City { get; set; }
        /// <summary>
        /// チーム名
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// リーグ名
        /// </summary>
        public string League { get; set; }
        /// <summary>
        /// 地区名
        /// </summary>
        public string Division { get; set; }
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
        /// 同地区チーム対決での勝ち数
        /// </summary>
        public int DivisionWins { get; set; }
        /// <summary>
        /// 同地区チーム対決での負け数
        /// </summary>
        public int DivisionLosses { get; set; }
        /// <summary>
        /// 1つ上の順位のチームとのゲーム差
        /// </summary>
        public float? GamesBehind { get; set; }
        /// <summary>
        /// 直近10試合での勝ち数
        /// </summary>
        public int LastTenGamesWins { get; set; }
        /// <summary>
        /// 直近10試合での負け数
        /// </summary>
        public int LastTenGamesLosses { get; set; }
        public string Streak { get; set; }
        /// <summary>
        /// ワイルドカードをめぐる順位
        /// </summary>
        public int? WildCardRank { get; set; }
        /// <summary>
        /// ワイルドカードをめぐるゲーム差
        /// </summary>
        public float? WildCardGamesBehind { get; set; }
        /// <summary>
        /// ホームでの勝ち数
        /// </summary>
        public int HomeWins { get; set; }
        /// <summary>
        /// ホームでの負け数
        /// </summary>
        public int HomeLosses { get; set; }
        /// <summary>
        /// アウェイでの勝ち数
        /// </summary>
        public int AwayWins { get; set; }
        /// <summary>
        /// アウェイでの負け数
        /// </summary>
        public int AwayLosses { get; set; }
        /// <summary>
        /// デイゲームでの勝ち数
        /// </summary>
        public int? DayWins { get; set; }
        /// <summary>
        /// デイゲームでの負け数
        /// </summary>
        public int? DayLosses { get; set; }
        /// <summary>
        /// ナイトゲームでの勝ち数
        /// </summary>
        public int? NightWins { get; set; }
        /// <summary>
        /// ナイトゲームでの負け数
        /// </summary>
        public int? NightLosses { get; set; }
        public int RunsScored { get; set; }
        public int RunsAgainst { get; set; }
        public int GlobalTeamID { get; set; }
    }
}
