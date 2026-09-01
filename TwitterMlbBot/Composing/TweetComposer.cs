using System.Globalization;
using System.Text;
using TwitterMlbBot.Mlb;

namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// 順位表からツイート文面を組み立てる純粋クラス（ネットワーク・設定に依存しない）
    /// 地区分け・順位付けはDivisionStanding / WildCardStandingが担い、このクラスは文面の見た目だけに責任を持つ
    /// </summary>
    internal class TweetComposer
    {
        /// <summary>
        /// ワイルドカードツイートに表示するチーム数
        /// （プレーオフ圏内3チーム + 追走3チーム。文字数と情報価値のバランスで決めた表示都合の値）
        /// </summary>
        private const int wildCardDisplayCount = WildCardStanding.PlayoffSpots + 3;

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
        /// ワイルドカード順位表をツイート文面リストに変換する
        /// </summary>
        public IReadOnlyList<TweetContent> ComposeWildCards(IReadOnlyList<WildCardStanding> wildCards, DateOnly date)
        {
            return wildCards
                .Select(wildCard => ComposeWildCardTweet(wildCard, date))
                .ToList();
        }

        private TweetContent ComposeDivisionTweet(DivisionStanding division, DateOnly date)
        {
            var buffer = new StringBuilder();
            AppendHeader(buffer, date, $"{division.League} {division.Division}");

            // 順位表の前後に空行を挟み、ヘッダ行・タグ行と視覚的に区切る
            buffer.AppendLine();
            foreach (RankedTeam rankedTeam in division.RankedTeams)
            {
                // 首位のゲーム差（常に0）は意味を持たないため表示しない
                float? gamesBehindToShow = rankedTeam.Rank > 1 ? rankedTeam.Team.GamesBehind : null;
                AppendTeamRow(buffer, rankedTeam.Rank, rankedTeam.Team, gamesBehindToShow);
            }
            buffer.AppendLine();

            AppendHashtags(buffer, division.RankedTeams.Take(2).Select(ranked => ranked.Team));
            return new TweetContent(buffer.ToString());
        }

        private TweetContent ComposeWildCardTweet(WildCardStanding wildCard, DateOnly date)
        {
            var buffer = new StringBuilder();
            AppendHeader(buffer, date, $"{wildCard.League} Wild Card");

            // 順位表の前後に空行を挟み、ヘッダ行・タグ行と視覚的に区切る
            buffer.AppendLine();
            foreach (RankedWildCardTeam rankedTeam in wildCard.RankedTeams.Take(wildCardDisplayCount))
            {
                // プレーオフ圏と圏外の境界を区切り線で示し、「あと何ゲームで圏内か」を読み取りやすくする
                if (rankedTeam.Rank == WildCardStanding.PlayoffSpots + 1)
                {
                    buffer.AppendLine("---");
                }
                // 圏内チームのゲーム差（ボーダーより上）は表示せず、圏外チームのみボーダーとの差を示す
                float? gamesBehindToShow = rankedTeam.Rank > WildCardStanding.PlayoffSpots
                    ? rankedTeam.GamesBehindPlayoffLine
                    : null;
                AppendTeamRow(buffer, rankedTeam.Rank, rankedTeam.Team, gamesBehindToShow);
            }
            buffer.AppendLine();

            AppendHashtags(buffer, wildCard.RankedTeams.Take(2).Select(ranked => ranked.Team));
            return new TweetContent(buffer.ToString());
        }

        private static void AppendHeader(StringBuilder buffer, DateOnly date, string title)
        {
            // 凡例（W-L (GB)）をヘッダ行に同居させ、数字の意味を示しつつ行数を抑える。
            // 読者は英語圏想定のため文面は英語で統一
            buffer
                .Append(date.ToString("M/d", CultureInfo.InvariantCulture))
                .Append(" ⚾ ")
                .Append(title)
                .AppendLine(" ⚾ W-L (GB)");
        }

        /// <summary>
        /// 「順位. チーム名 勝-負 (ゲーム差)」の1行を追加する。ゲーム差はnullなら表示しない。
        /// Xはプロポーショナルフォント表示のため、空白での桁揃えはせず区切り文字形式とする
        /// </summary>
        private static void AppendTeamRow(StringBuilder buffer, int rank, TeamStanding team, float? gamesBehind)
        {
            buffer
                .Append(rank.ToString(CultureInfo.InvariantCulture)).Append(". ")
                .Append(team.Name)
                .Append(' ')
                .Append(team.Wins.ToString(CultureInfo.InvariantCulture))
                .Append('-')
                .Append(team.Losses.ToString(CultureInfo.InvariantCulture));
            if (gamesBehind is float value)
            {
                buffer.Append(" (").Append(value.ToString("0.#", CultureInfo.InvariantCulture)).Append(')');
            }
            buffer.AppendLine();
        }

        /// <summary>
        /// 「#MLB #<1位チームタグ> #<2位チームタグ>」のタグ行を追加する
        /// </summary>
        private void AppendHashtags(StringBuilder buffer, IEnumerable<TeamStanding> topTeams)
        {
            buffer.Append("#MLB");
            foreach (TeamStanding team in topTeams)
            {
                buffer.Append(' ').Append(this.hashtagProvider.GetHashtags(team.Name));
            }
        }
    }
}
