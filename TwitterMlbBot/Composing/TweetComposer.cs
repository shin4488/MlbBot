using System.Collections.Generic;
using System.Linq;
using System.Text;
using TwitterMlbBot.Mlb;

namespace TwitterMlbBot.Composing
{
    /// <summary>
    /// 順位データから地区ごとのツイート文面を組み立てる純粋クラス（ネットワーク・設定に依存しない）
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
            return standings
                .GroupBy(team => new { team.League, team.Division })
                // All-Star用の擬似チームは「リーグ: AL, 地区: AL」のようにリーグ名と地区名が同一になるため除外する
                .Where(teams => teams.Key.League != teams.Key.Division)
                .Select(teams => ComposeDivisionTweet(teams.Key.League, teams.Key.Division, teams))
                .ToList();
        }

        /// <summary>
        /// 1地区分のツイート文面を組み立てる
        /// </summary>
        private string ComposeDivisionTweet(string league, string division, IEnumerable<TeamStanding> teams)
        {
            // APIレスポンスの並び順には依存せず、勝率降順（同率なら勝ち数降順）で順位を決める
            List<TeamStanding> rankedTeams = teams
                .OrderByDescending(team => team.Percentage)
                .ThenByDescending(team => team.Wins)
                .ToList();

            var buffer = new StringBuilder();
            buffer
                .Append("⚾ ")
                .Append(league)
                .Append(" | ")
                .Append(division)
                .Append(" ⚾️ ")
                .AppendLine("Win : Loss : Behind");

            // ツイート文は「<順位>. <チーム名> : <勝ち数> : <負け数> : <ゲーム差>」
            int ranking = 0;
            foreach (TeamStanding team in rankedTeams)
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
            foreach (TeamStanding topTeam in rankedTeams.Take(2))
            {
                buffer.Append(' ').Append(this.hashtagProvider.GetHashtags(topTeam.Name));
            }
            return buffer.ToString();
        }
    }
}
