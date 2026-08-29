using System.Globalization;
using System.Text;
using TwitterMlbBot.Mlb;

namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// 地区順位表からツイート文面を組み立てる純粋クラス（ネットワーク・設定に依存しない）
    /// 地区分け・順位付けはDivisionStandingが担い、このクラスは文面の見た目だけに責任を持つ
    /// </summary>
    internal class TweetComposer
    {
        private readonly HashtagProvider hashtagProvider;

        public TweetComposer(HashtagProvider hashtagProvider)
        {
            this.hashtagProvider = hashtagProvider;
        }

        /// <summary>
        /// 地区順位表をツイート文面リストに変換する
        /// </summary>
        /// <param name="divisions">地区ごとの順位表</param>
        /// <param name="date">文面のヘッダに表示する日付。日付が入り毎日本文が変わることで、
        /// 順位が動かない日でもX APIの重複コンテンツ判定（403）を回避できる</param>
        /// <returns>地区ごとのツイート文面（順位表が空の場合は空リスト）</returns>
        public IReadOnlyList<TweetContent> Compose(IReadOnlyList<DivisionStanding> divisions, DateOnly date)
        {
            return divisions
                .Select(division => ComposeDivisionTweet(division, date))
                .ToList();
        }

        /// <summary>
        /// 1地区分のツイート文面を組み立てる
        /// </summary>
        private TweetContent ComposeDivisionTweet(DivisionStanding division, DateOnly date)
        {
            var buffer = new StringBuilder();
            // 凡例（W-L (GB)）をヘッダ行に同居させ、数字の意味を示しつつ行数を抑える。
            // 読者は英語圏想定のため文面は英語で統一
            buffer
                .Append("📅 ")
                .Append(date.ToString("M/d", CultureInfo.InvariantCulture))
                .Append(" ⚾ ")
                .Append(division.League)
                .Append(' ')
                .Append(division.Division)
                .AppendLine(" ⚾ W-L (GB)");

            // Xはプロポーショナルフォント表示のため、空白での桁揃えは効かない。
            // 「<順位>. <チーム名> <勝ち数>-<負け数> (<ゲーム差>)」の区切り文字形式とし、首位のゲーム差は表示しない
            foreach (RankedTeam rankedTeam in division.RankedTeams)
            {
                buffer
                    .Append(rankedTeam.Rank.ToString(CultureInfo.InvariantCulture)).Append(". ")
                    .Append(rankedTeam.Team.Name)
                    .Append(' ')
                    .Append(rankedTeam.Team.Wins.ToString(CultureInfo.InvariantCulture))
                    .Append('-')
                    .Append(rankedTeam.Team.Losses.ToString(CultureInfo.InvariantCulture));
                if (rankedTeam.Rank > 1 && rankedTeam.Team.GamesBehind is float gamesBehind)
                {
                    buffer.Append(" (").Append(gamesBehind.ToString("0.#", CultureInfo.InvariantCulture)).Append(')');
                }
                buffer.AppendLine();
            }

            // 「#MLB #<1位チームタグ> #<2位チームタグ>」をタグ付けメッセージとする
            buffer.Append("#MLB");
            foreach (RankedTeam topTeam in division.RankedTeams.Take(2))
            {
                buffer.Append(' ').Append(this.hashtagProvider.GetHashtags(topTeam.Team.Name));
            }
            return new TweetContent(buffer.ToString());
        }
    }
}
