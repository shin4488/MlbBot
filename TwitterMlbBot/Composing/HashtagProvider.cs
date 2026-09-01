using System.Text.RegularExpressions;

namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// MLBチーム名からXのハッシュタグ文字列を生成する
    /// </summary>
    internal class HashtagProvider
    {
        /// <summary>
        /// MLB公式チームハッシュタグマップ（全30球団）
        /// 公式タグがチーム名と同じ球団も、考慮漏れと区別がつくよう省略せず定義する
        /// （全30球団が定義されていることはテストが検証する）
        /// 毎シーズン変更の可能性があるため、ここで一元管理する。
        /// 並びは公式発表のタグ一覧と突き合わせやすいよう球団略号のアルファベット順
        /// </summary>
        internal static readonly Dictionary<string, string> OfficialHashtagMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Diamondbacks", "Dbacks" },
                { "Athletics",    "Athletics" },
                { "Braves",       "BravesCountry" },
                { "Orioles",      "Birdland" },
                { "Red Sox",      "DirtyWater" },
                { "Cubs",         "Cubs" },
                { "White Sox",    "WhiteSox" },
                { "Reds",         "ATOBTTR" },
                { "Guardians",    "GuardsBall" },
                { "Rockies",      "Rockies" },
                { "Tigers",       "DNMW" },
                { "Astros",       "ChaseTheFight" },
                { "Royals",       "FountainsUp" },
                { "Angels",       "RepTheHalo" },
                { "Dodgers",      "Dodgers" },
                { "Marlins",      "FightinFish" },
                { "Brewers",      "ThisIsMyCrew" },
                { "Twins",        "NoPlaceLikeHERE" },
                { "Mets",         "LGM" },
                { "Yankees",      "RepBX" },
                { "Phillies",     "RingTheBell" },
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
                    && !string.Equals(officialTag, nameNoSpace, StringComparison.OrdinalIgnoreCase)
                // 公式タグ + 元チーム名タグの両方を付ける
                ? $"#{officialTag} #{nameNoSpace}"
                // 公式タグがチーム名と同じ場合（およびマップ未定義の場合）はチーム名タグのみ使用
                : $"#{nameNoSpace}";
        }
    }
}
