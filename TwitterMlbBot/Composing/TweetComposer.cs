using System.Collections.Generic;
using System.Linq;
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
        private const int teamNamePadding = 12;
        private const int digitPadding = 2;
        private readonly HashtagProvider hashtagProvider;

        public TweetComposer(HashtagProvider hashtagProvider)
        {
            this.hashtagProvider = hashtagProvider;
        }

        /// <summary>
        /// 地区順位表をツイート文面リストに変換する
        /// </summary>
        /// <param name="divisions">地区ごとの順位表</param>
        /// <returns>地区ごとのツイート文面（順位表が空の場合は空リスト）</returns>
        public IReadOnlyList<TweetContent> Compose(IReadOnlyList<DivisionStanding> divisions)
        {
            return divisions
                .Select(ComposeDivisionTweet)
                .ToList();
        }

        /// <summary>
        /// 1地区分のツイート文面を組み立てる
        /// </summary>
        private TweetContent ComposeDivisionTweet(DivisionStanding division)
        {
            var buffer = new StringBuilder();
            buffer
                .Append("⚾ ")
                .Append(division.League)
                .Append(" | ")
                .Append(division.Division)
                .Append(" ⚾️ ")
                .AppendLine("Win : Loss : Behind");

            // ツイート文は「<順位>. <チーム名> : <勝ち数> : <負け数> : <ゲーム差>」
            foreach (RankedTeam rankedTeam in division.RankedTeams)
            {
                buffer
                    .Append(rankedTeam.Rank.ToString()).Append(". ")
                    .Append(rankedTeam.Team.Name.PadRight(teamNamePadding)).Append(" : ")
                    .Append(rankedTeam.Team.Wins.ToString().PadRight(digitPadding)).Append(" : ")
                    .Append(rankedTeam.Team.Losses.ToString().PadRight(digitPadding)).Append(" : ")
                    .AppendLine(rankedTeam.Team.GamesBehind.ToString());
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
