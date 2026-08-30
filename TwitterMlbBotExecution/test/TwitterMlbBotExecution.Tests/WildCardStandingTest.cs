using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ワイルドカード順位表（WildCardStanding）のテスト
/// 「地区首位は含まれない」「地区をまたいで勝率順」「ゲーム差はプレーオフ圏ボーダー基準」
/// という導出仕様を検証する
/// </summary>
public class WildCardStandingTest
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

    private static IReadOnlyList<WildCardStanding> FromTeams(params TeamStanding[] teams)
    {
        return WildCardStanding.FromDivisions(DivisionStanding.FromStandings(teams.ToList()));
    }

    [Fact]
    public void FromDivisions_地区首位は含まれない()
    {
        var wildCards = FromTeams(
            CreateTeam("AL", "East", "Yankees", 90, 50, 0.643),
            CreateTeam("AL", "East", "Red Sox", 80, 60, 0.571));

        var wildCard = Assert.Single(wildCards);
        Assert.DoesNotContain(wildCard.RankedTeams, ranked => ranked.Team.Name == "Yankees");
        Assert.Contains(wildCard.RankedTeams, ranked => ranked.Team.Name == "Red Sox");
    }

    [Fact]
    public void FromDivisions_地区をまたいで勝率順に並ぶ()
    {
        // East2位（勝率.571）よりCentral2位（勝率.600）が上位になること
        var wildCards = FromTeams(
            CreateTeam("AL", "East", "Yankees", 90, 50, 0.643),
            CreateTeam("AL", "East", "Red Sox", 80, 60, 0.571),
            CreateTeam("AL", "Central", "White Sox", 91, 49, 0.650),
            CreateTeam("AL", "Central", "Guardians", 84, 56, 0.600));

        var wildCard = Assert.Single(wildCards);
        Assert.Equal(new[] { "Guardians", "Red Sox" }, wildCard.RankedTeams.Select(ranked => ranked.Team.Name));
        Assert.Equal(new[] { 1, 2 }, wildCard.RankedTeams.Select(ranked => ranked.Rank));
    }

    [Fact]
    public void FromDivisions_リーグごとに分かれる()
    {
        var wildCards = FromTeams(
            CreateTeam("AL", "East", "Yankees", 90, 50, 0.643),
            CreateTeam("AL", "East", "Red Sox", 80, 60, 0.571),
            CreateTeam("NL", "West", "Dodgers", 92, 48, 0.657),
            CreateTeam("NL", "West", "Padres", 85, 55, 0.607));

        Assert.Equal(2, wildCards.Count);
        Assert.All(wildCards, wildCard => Assert.Single(wildCard.RankedTeams));
    }

    [Fact]
    public void FromDivisions_ゲーム差はプレーオフ圏ボーダー基準で計算される()
    {
        // 圏内3位（ボーダー）が貯金10、4位が貯金8 → 4位のゲーム差は1
        var wildCards = FromTeams(
            CreateTeam("AL", "East", "Yankees", 95, 45, 0.679),
            CreateTeam("AL", "East", "Red Sox", 88, 52, 0.629),
            CreateTeam("AL", "East", "Rays", 87, 53, 0.621),
            CreateTeam("AL", "Central", "White Sox", 94, 46, 0.671),
            CreateTeam("AL", "Central", "Guardians", 75, 65, 0.536),
            CreateTeam("AL", "West", "Astros", 93, 47, 0.664),
            CreateTeam("AL", "West", "Mariners", 74, 66, 0.529));

        var wildCard = Assert.Single(wildCards);
        // 首位3チームを除く4チーム: Red Sox(1位), Rays(2位), Guardians(3位=ボーダー), Mariners(4位)
        var fourth = wildCard.RankedTeams[3];
        Assert.Equal("Mariners", fourth.Team.Name);
        Assert.Equal(1f, fourth.GamesBehindPlayoffLine);
    }

    [Fact]
    public void FromDivisions_順位データが空なら空リストを返す()
    {
        Assert.Empty(WildCardStanding.FromDivisions(new List<DivisionStanding>()));
    }
}
