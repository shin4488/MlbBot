using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// MLBチーム名からXのハッシュタグ文字列を生成する
    /// </summary>
    internal class HashtagProvider
    {
        /// <summary>
        /// MLB公式チームハッシュタグマップ（チーム名と公式タグが異なるもののみ定義）
        /// 毎シーズン変更の可能性があるため、ここで一元管理する
        /// </summary>
        private static readonly Dictionary<string, string> OfficialHashtagMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Diamondbacks", "Dbacks" },
                { "Braves",       "BravesCountry" },
                { "Orioles",      "Birdland" },
                { "Red Sox",      "DirtyWater" },
                { "Reds",         "ATOBTTR" },
                { "Guardians",    "GuardsBall" },
                { "Tigers",       "DNMW" },
                { "Phillies",     "RingTheBell" },
                { "Royals",       "FountainsUp" },
                { "Angels",       "RepTheHalo" },
                { "Marlins",      "FightinFish" },
                { "Brewers",      "ThisIsMyCrew" },
                { "Twins",        "NoPlaceLikeHERE" },
                { "Mets",         "LGM" },
                { "Yankees",      "RepBX" },
                { "Pirates",      "LetsGoBucs" },
                { "Padres",       "ForTheFaithful" },
                { "Mariners",     "TridentsUp" },
                { "Giants",       "SFGiants" },
                { "Cardinals",    "STLCards" },
                { "Rays",         "RaysUp" },
                { "Rangers",      "AllForTX" },
                { "Blue Jays",    "BlueJays50" },
                { "Nationals",    "Natitude" },
            };

        private readonly Dictionary<string, string> officialHashtagMap;

        public HashtagProvider() : this(OfficialHashtagMap)
        {
        }

        /// <summary>
        /// テスト用にタグマップを差し替えられるコンストラクタ
        /// </summary>
        internal HashtagProvider(Dictionary<string, string> officialHashtagMap)
        {
            this.officialHashtagMap = officialHashtagMap;
        }

        /// <summary>
        /// チーム名からハッシュタグ文字列を生成する。
        /// 公式タグがチーム名と異なる場合は、公式タグと元チーム名の両方を返す。
        /// </summary>
        /// <param name="teamName">チーム名（例: "Diamondbacks"）</param>
        /// <returns>ハッシュタグ文字列（例: "#Dbacks #Diamondbacks"）</returns>
        public string GetHashtags(string teamName)
        {
            string nameNoSpace = Regex.Replace(teamName, @"\s", "");
            return this.officialHashtagMap.TryGetValue(teamName, out string? officialTag)
                // 公式タグ + 元チーム名タグの両方を付ける
                ? $"#{officialTag} #{nameNoSpace}"
                // チーム名と公式タグが同じ場合はそのまま使用
                : $"#{nameNoSpace}";
        }
    }
}
