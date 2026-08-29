using TwitterMlbBot;
using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// 順位データからツイート用データへのマッピング（Program.MapToTwitterParam）のテスト
///
/// テストは仕様ベースで書く方針:
/// 「順位は勝率で決まる」「All-Star擬似チームは除外される」「タグは上位2チーム分」という
/// 仕様レベルの不変条件のみを検証する。公式ハッシュタグマップは毎シーズン変わりうるため、
/// マップに載っていないチーム名を使いマップの中身には依存させない。
/// </summary>
public class ProgramMappingTest
{
    private static DetailResult CreateTeam(
        string league, string division, string name, int wins, int losses, double percentage)
    {
        return new DetailResult
        {
            League = league,
            Division = division,
            Name = name,
            Wins = wins,
            Losses = losses,
            Percentage = percentage,
            GamesBehind = 0,
        };
    }

    [Fact]
    public void MapToTwitterParam_勝率の高い順に順位が付く()
    {
        // 入力はあえて順位順に並べない（APIの並び順に依存しない仕様の検証）
        var teams = new List<DetailResult>
        {
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("AL", "Central", "Athletics", 56, 84, 0.400),
        };

        var result = Program.MapToTwitterParam(teams);

        var division = Assert.Single(result.TeamsList);
        Assert.Equal(new[] { "White Sox", "Astros", "Athletics" }, division.Teams.Select(t => t.Name));
        Assert.Equal(new[] { 1, 2, 3 }, division.Teams.Select(t => t.Ranking));
    }

    [Fact]
    public void MapToTwitterParam_AllStar擬似チームのグループは除外される()
    {
        var teams = new List<DetailResult>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            // All-Star用の擬似チーム: リーグ名と地区名が同一
            CreateTeam("AL", "AL", "AL All-Stars", 0, 0, 0),
            CreateTeam("NL", "NL", "NL All-Stars", 0, 0, 0),
        };

        var result = Program.MapToTwitterParam(teams);

        var division = Assert.Single(result.TeamsList);
        Assert.DoesNotContain(division.Teams, t => t.Name.Contains("All-Stars"));
    }

    [Fact]
    public void MapToTwitterParam_タグには全体タグと上位2チームのタグが入る()
    {
        var teams = new List<DetailResult>
        {
            CreateTeam("AL", "Central", "White Sox", 84, 56, 0.600),
            CreateTeam("AL", "Central", "Astros", 70, 70, 0.500),
            CreateTeam("AL", "Central", "Athletics", 56, 84, 0.400),
        };

        var result = Program.MapToTwitterParam(teams);

        string tagMessage = result.TeamsList[0].TagMessage;
        Assert.Contains("#MLB", tagMessage);
        // チーム名のスペースは除去されてタグになる
        Assert.Contains("#WhiteSox", tagMessage);
        Assert.Contains("#Astros", tagMessage);
        // タグ付けは上位2チームまで
        Assert.DoesNotContain("#Athletics", tagMessage);
    }
}
