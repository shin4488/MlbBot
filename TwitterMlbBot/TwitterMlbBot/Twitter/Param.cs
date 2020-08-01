using System.Collections.Generic;

namespace TwitterMlbBot.Twitter
{
    class Param
    {
        public List<ParamByKey> TeamsList { get; set; }
    }

    /// <summary>
    /// キーごとにまとめたパラメータクラス
    /// </summary>
    public class ParamByKey
    {
        /// <summary>
        /// チームをグループ化するためのキー
        /// </summary>
        public GroupKey Key { get; set; }

        /// <summary>
        /// ツイート時のタグ付けメッセージ
        /// </summary>
        public string TagMessage { get; set; }

        /// <summary>
        /// 同リーグ・同地区のチームリスト
        /// </summary>
        public List<DetailParam> Teams { get; set; }
    }

    /// <summary>
    /// チームをグループ化する際のキークラス
    /// </summary>
    public class GroupKey
    {
        /// <summary>
        /// リーグ名
        /// </summary>
        public string League { get; set; }

        /// <summary>
        /// 地区名
        /// </summary>
        public string Division { get; set; }
    }

    /// <summary>
    /// MLBの各チームのデータ
    /// </summary>
    public class DetailParam
    {
        /// <summary>
        /// 順位
        /// </summary>
        public int Ranking { get; set; }

        /// <summary>
        /// チーム名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 勝ち数
        /// </summary>
        public int? Wins { get; set; }

        /// <summary>
        /// 負け数
        /// </summary>
        public int? Losses { get; set; }

        /// <summary>
        /// 1つ上の順位のチームとのゲーム差
        /// </summary>
        public float? GamesBehind { get; set; }
    }
}
