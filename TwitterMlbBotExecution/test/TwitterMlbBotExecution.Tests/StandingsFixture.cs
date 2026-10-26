using System.Text.Json.Nodes;
using TwitterMlbBot.Mlb;

namespace TwitterMlbBotExecution.Tests;

internal static class StandingsFixture
{
    // MLBの地区構成を持つ架空の球団。実在の球団名・成績の更新でテストを直す必要をなくす。
    public static IReadOnlyList<TeamStanding> Teams { get; } = (
        from league in new[] { "AL", "NL" }
        from division in new[] { "East", "Central", "West" }
        from index in Enumerable.Range(0, 5)
        select new TeamStanding($"{league}-{division}-{index}", league, division, 90 - index, 50 + index)
    ).ToList().AsReadOnly();

    /// <summary>
    /// Teamsと同じ成績を、sportsdata.ioの順位APIの項目名で表した応答。
    /// ドメインの型をそのまま直列化せず、型の名前を変えても配信元の形式が変わらないようにする
    /// </summary>
    public static JsonArray CreateResponse() => new(Teams.Select(team => (JsonNode)new JsonObject
    {
        ["Name"] = team.Name,
        ["League"] = team.League,
        ["Division"] = team.Division,
        ["Wins"] = team.Wins,
        ["Losses"] = team.Losses,
        ["ClinchedDivision"] = team.ClinchedDivision,
        ["ClinchedWildCard"] = team.ClinchedWildCard,
    }).ToArray());
}
