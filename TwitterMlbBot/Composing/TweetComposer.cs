using System.Collections.Generic;
using System.Linq;
using System.Text;
using TwitterMlbBot.Mlb;

namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// 順位表からツイート文面を組み立てる純粋クラス（ネットワーク・設定に依存しない）
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
        /// 順位データを地区ごとのツイート文面リストに変換する
        /// </summary>
        /// <param name="standings">全チームの順位データ</param>
        /// <returns>地区ごとのツイート文面（順位データが空の場合は空リスト）</returns>
        public IReadOnlyList<string> Compose(IReadOnlyList<TeamStanding> standings)
        {
            return DivisionStanding.FromStandings(standings)
                .Select(ComposeDivisionTweet)
                .ToList();
        }

        /// <summary>
        /// 1地区分のツイート文面を組み立てる
        /// </summary>
        private string ComposeDivisionTweet(DivisionStanding division)
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
            int ranking = 0;
            foreach (TeamStanding team in division.RankedTeams)
            {
                ranking++;
                buffer
                    .Append(ranking.ToString()).Append(". ")
                    .Append(team.Name.PadRight(teamNamePadding)).Append(" : ")
                    .Append(team.Wins.ToString().PadRight(digitPadding)).Append(" : ")
                    .Append(team.Losses.ToString().PadRight(digitPadding)).Append(" : ")
                    .AppendLine(team.GamesBehind.ToString());
            }

            // 「#MLB #<1位チームタグ> #<2位チームタグ>」をタグ付けメッセージとする
            buffer.Append("#MLB");
            foreach (TeamStanding topTeam in division.RankedTeams.Take(2))
            {
                buffer.Append(' ').Append(this.hashtagProvider.GetHashtags(topTeam.Name));
            }
            return buffer.ToString();
        }
    }
}
