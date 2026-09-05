using System.Collections.Frozen;

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
        internal static readonly IReadOnlyDictionary<string, string> OfficialHashtagMap =
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
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private readonly IReadOnlyDictionary<string, string> officialHashtagMap;

        public HashtagProvider() : this(OfficialHashtagMap)
        {
        }

        /// <summary>
        /// タグマップを受け取り、このインスタンスで使う対応表を確定する
        /// </summary>
        internal HashtagProvider(IReadOnlyDictionary<string, string> officialHashtagMap)
        {
            // IReadOnlyDictionaryでも呼び出し側の辞書は変更できるため、不変のスナップショットを持つ。
            // 公式マップも不変なので、共有しても別の実行のタグが書き換わることはない
            this.officialHashtagMap = officialHashtagMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// チーム名からハッシュタグ文字列を生成する。
        /// 公式タグがチーム名と異なる場合は、公式タグと元チーム名の両方を返す。
        /// </summary>
        /// <param name="teamName">チーム名（例: "Diamondbacks"）</param>
        /// <returns>ハッシュタグ文字列（例: "#Dbacks #Diamondbacks"）</returns>
        public string GetHashtags(string teamName)
        {
            // ハッシュタグに空白は使えないため除去する（"Red Sox" → "RedSox"）
            string nameNoSpace = teamName.Replace(" ", "");
            bool shouldAddOfficialTag = officialHashtagMap.TryGetValue(teamName, out string? officialTag)
                && !string.Equals(officialTag, nameNoSpace, StringComparison.OrdinalIgnoreCase);
            return shouldAddOfficialTag
                // 公式タグ + 元チーム名タグの両方を付ける
                ? $"#{officialTag} #{nameNoSpace}"
                // 公式タグがチーム名と同じ場合（およびマップ未定義の場合）はチーム名タグのみ使用
                : $"#{nameNoSpace}";
        }
    }
}
