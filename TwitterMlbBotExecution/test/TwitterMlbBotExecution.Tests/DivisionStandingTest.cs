using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// 地区順位表（DivisionStanding）のテスト
/// 「RankedTeamsは常に順位順」「All-Star擬似チームは含まれない」という型の不変条件を検証する
/// </summary>
public class DivisionStandingTest
{
    private static TeamStanding CreateTeam(
        string league, string division, string name, int wins, int losses, double percentage)
    {
        return new TeamStanding
        {
            League = league,
            Division = division,
            Name = name,
            Wins = wins,
            Losses = losses,
            Percentage = percentage,
        };
    }

    [Fact]
    public void FromStandings_RankedTeamsは勝率降順で並ぶ()
    {
        // 入力はあえて順位順に並べない
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("AL", "Central", "Athletics", 56, 84, 0.400),
        };

        var divisions = DivisionStanding.FromStandings(standings);

        var division = Assert.Single(divisions);
        Assert.Equal(new[] { "White Sox", "Astros", "Athletics" }, division.RankedTeams.Select(ranked => ranked.Team.Name));
        // 順位は1位から連番で振られること
        Assert.Equal(new[] { 1, 2, 3 }, division.RankedTeams.Select(ranked => ranked.Rank));
    }

    [Fact]
    public void FromStandings_勝率が同率の場合は勝ち数の多い順に並ぶ()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            CreateTeam("AL", "Central", "White Sox", 75, 75, 0.500),
        };

        var divisions = DivisionStanding.FromStandings(standings);

        Assert.Equal("White Sox", Assert.Single(divisions).RankedTeams[0].Team.Name);
    }

    [Fact]
    public void FromStandings_リーグと地区ごとにグループ化される()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("NL", "West", "Dodgers", 82, 48, 0.630),
            // リーグが違えば同じ地区名でも別グループ
            CreateTeam("AL", "West", "Astros", 70, 70, 0.500),
        };

        var divisions = DivisionStanding.FromStandings(standings);

        Assert.Equal(3, divisions.Count);
    }

    [Fact]
    public void FromStandings_AllStar擬似チームは含まれない()
    {
        var standings = new List<TeamStanding>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            // All-Star用の擬似チーム: リーグ名と地区名が同一
            CreateTeam("AL", "AL", "AL All-Stars", 0, 0, 0),
            CreateTeam("NL", "NL", "NL All-Stars", 0, 0, 0),
        };

        var divisions = DivisionStanding.FromStandings(standings);

        var division = Assert.Single(divisions);
        Assert.DoesNotContain(division.RankedTeams, ranked => ranked.Team.IsAllStarPseudoTeam);
    }
}
