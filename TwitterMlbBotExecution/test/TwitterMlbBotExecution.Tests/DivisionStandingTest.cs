using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// 地区順位表（DivisionStanding）のテスト
/// 「RankedTeamsは常に順位順」「ゲーム差は首位基準」という型の不変条件を検証する
/// </summary>
public class DivisionStandingTest
{
    [Fact]
    public void FromStandings_RankedTeamsは勝率降順で並ぶ()
    {
        // 入力はあえて順位順に並べない
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Athletics", 56, 84),
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
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("AL", "Central", "White Sox", 75, 75),
        };

        var divisions = DivisionStanding.FromStandings(standings);

        Assert.Equal("White Sox", Assert.Single(divisions).RankedTeams[0].Team.Name);
    }

    [Fact]
    public void FromStandings_ゲーム差は首位基準で計算される()
    {
        // 首位の貯金28、2位の貯金0 → 2位のゲーム差は14。3位は1つ上ではなく首位との差（貯金-28 → 28）
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("AL", "Central", "Astros", 70, 70),
            Teams.Create("AL", "Central", "Athletics", 56, 84),
        };

        var division = Assert.Single(DivisionStanding.FromStandings(standings));

        Assert.Equal(0f, division.RankedTeams[0].GamesBehind);
        Assert.Equal(14f, division.RankedTeams[1].GamesBehind);
        Assert.Equal(28f, division.RankedTeams[2].GamesBehind);
    }

    [Fact]
    public void FromStandings_リーグと地区ごとにグループ化される()
    {
        var standings = new List<TeamStanding>
        {
            Teams.Create("AL", "Central", "White Sox", 84, 56),
            Teams.Create("NL", "West", "Dodgers", 82, 48),
            // リーグが違えば同じ地区名でも別グループ
            Teams.Create("AL", "West", "Astros", 70, 70),
        };

        var divisions = DivisionStanding.FromStandings(standings);

        Assert.Equal(3, divisions.Count);
    }
}
