using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

public class StandingImmutabilityTest
{
    [Fact]
    public void 並べ替えた成績は列挙する前に入力が変わっても作成時の順位を保つ()
    {
        var source = new List<TeamStanding>
        {
            Teams.Create("AL", "East", "Contender", 80, 60),
            Teams.Create("AL", "East", "Leader", 90, 50),
        };

        var ordered = TeamStanding.OrderByRank(source);
        source.Clear();

        Assert.Equal(new[] { "Leader", "Contender" }, ordered.Select(team => team.Name));
    }

    [Fact]
    public void 公開する成績や順位表はリスト自体も書き換えられない()
    {
        var source = new[]
        {
            Teams.Create("AL", "East", "Leader", 90, 50),
            Teams.Create("AL", "East", "Contender", 80, 60),
        };
        var divisions = DivisionStanding.FromStandings(source);

        AssertCannotChange(TeamStanding.OrderByRank(source));
        AssertCannotChange(divisions);
        AssertCannotChange(WildCardStanding.FromDivisions(divisions));
    }

    private static void AssertCannotChange<T>(IReadOnlyList<T> items)
    {
        Assert.NotEmpty(items);
        var expected = items.ToArray();
        if (items is IList<T> writable)
        {
            Assert.ThrowsAny<Exception>(() => writable.Clear());
        }
        Assert.Equal(expected, items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 作成した順位表は入力や公開コレクション経由で書き換えられない(bool wildCard)
    {
        var source = new List<TeamStanding>
        {
            Teams.Create("AL", "East", "Leader", 90, 50),
            Teams.Create("AL", "East", "Contender", 80, 60),
        };
        var divisions = DivisionStanding.FromStandings(source);
        var rankedTeams = wildCard
            ? Assert.Single(WildCardStanding.FromDivisions(divisions)).RankedTeams
            : Assert.Single(divisions).RankedTeams;
        var expected = rankedTeams.ToArray();

        source.Clear();
        // コレクションの具体型には依存せず、書き込みAPIを公開している場合も変更を受け付けないことを確認する
        if (rankedTeams is IList<RankedTeam> writable)
        {
            Assert.ThrowsAny<Exception>(() => writable[0] = expected[0] with { Rank = 99 });
        }

        Assert.Equal(expected, rankedTeams);
    }
}
