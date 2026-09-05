using System.Text.Json;
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

    public static JsonArray CreateResponse() => JsonSerializer.SerializeToNode(Teams)!.AsArray();
}
