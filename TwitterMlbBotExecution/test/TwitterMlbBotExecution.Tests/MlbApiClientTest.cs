using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// sportsdata.io レスポンス解析（MlbApiClient）のテスト
/// 実際のAPIレスポンスと同じ構造のJSONを使い、ネットワークなしで「何がドメインに渡るか」を検証する
/// </summary>
public class MlbApiClientTest
{
    [Fact]
    public void ParseStandings_チーム名_リーグ_地区_勝敗が取り出せる()
    {
        // 実レスポンスの抜粋（未使用フィールドが混ざっていても解析できることも兼ねて検証）
        string responseBody = """
            [{"Season":2026,"SeasonType":1,"TeamID":10,"Key":"NYY","City":"New York","Name":"Yankees","League":"AL","Division":"East","Wins":82,"Losses":53,"Percentage":0.607,"GamesBehind":0.0,"Streak":"W2"}]
            """;

        TeamStanding team = Assert.Single(MlbApiClient.ParseStandings(responseBody));

        Assert.Equal("Yankees", team.Name);
        Assert.Equal("AL", team.League);
        Assert.Equal("East", team.Division);
        Assert.Equal(82, team.Wins);
        Assert.Equal(53, team.Losses);
    }

    [Fact]
    public void ParseStandings_AllStar擬似チームは含まれない()
    {
        // レスポンスにはリーグ名と地区名が同一の擬似チーム（"AL"/"AL"）が混ざる
        string responseBody = """
            [{"Name":"Yankees","League":"AL","Division":"East","Wins":82,"Losses":53},
             {"Name":"AL All-Stars","League":"AL","Division":"AL","Wins":0,"Losses":0},
             {"Name":"NL All-Stars","League":"NL","Division":"NL","Wins":0,"Losses":0}]
            """;

        TeamStanding team = Assert.Single(MlbApiClient.ParseStandings(responseBody));

        Assert.Equal("Yankees", team.Name);
    }

    [Fact]
    public void ParseStandings_空の配列なら空リストを返す()
    {
        // シーズン開始前はAPIが空配列を返す（実測済み）
        Assert.Empty(MlbApiClient.ParseStandings("[]"));
    }
}
