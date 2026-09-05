using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

public class TeamStandingTest
{
    [Theory]
    [InlineData("", "AL", "East", 80, 60)]
    [InlineData("Example", " ", "East", 80, 60)]
    [InlineData("Example", "AL", "", 80, 60)]
    [InlineData("Example", "AL", "East", -1, 60)]
    [InlineData("Example", "AL", "East", 80, -1)]
    public void 取得元によらず順位付けできない成績は作れない(string name, string league, string division, int wins, int losses)
    {
        Assert.ThrowsAny<Exception>(() => new TeamStanding(name, league, division, wins, losses));
    }

    [Fact]
    public void 試合前の0勝0敗は有効な成績として扱う()
    {
        var team = new TeamStanding("Example", "AL", "East", 0, 0);

        Assert.Equal(0, team.Wins);
        Assert.Equal(0, team.Losses);
        Assert.Equal(0, team.Percentage);
    }
}
