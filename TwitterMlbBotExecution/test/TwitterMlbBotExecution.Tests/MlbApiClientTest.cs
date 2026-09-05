using System.Text.Json.Nodes;
using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// sportsdata.io レスポンス解析（MlbApiClient）のテスト
/// 実際のAPIレスポンスと同じ構造のJSONを使い、ネットワークなしで「何がドメインに渡るか」を検証する
/// </summary>
public class MlbApiClientTest
{
    [Theory]
    [InlineData("null")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    [InlineData("""[{"League":"AL","Division":"East","Wins":80,"Losses":60}]""")]
    [InlineData("""[{"Name":"Example","League":"AL","Wins":80,"Losses":60}]""")]
    [InlineData("""[{"Name":"Example","League":"AL","Division":"East","Losses":60}]""")]
    [InlineData("""[{"Name":"Example","League":"AL","Division":"East","Wins":80}]""")]
    [InlineData("""[{"Name":"Example","League":"AL","Division":"East","Wins":-1,"Losses":60}]""")]
    [InlineData("""[{"Name":" ","League":"AL","Division":"East","Wins":80,"Losses":60}]""")]
    [InlineData("not-json")]
    public void ParseStandings_不正な応答を空順位や架空の成績として扱わない(string responseBody)
    {
        // チーム情報の検証は、残りの球団がそろった応答でも不正な1件を拒否することを確かめる。
        if (responseBody.StartsWith('['))
        {
            JsonArray response = StandingsFixture.CreateResponse();
            response[0] = JsonNode.Parse(responseBody)![0]?.DeepClone();
            responseBody = response.ToJsonString();
        }
        // 正常な空配列と、取得したデータの欠落・破損は区別する
        Assert.ThrowsAny<Exception>(() => MlbApiClient.ParseStandings(responseBody));
    }

    [Fact]
    public void ParseStandings_チーム名_リーグ_地区_勝敗が取り出せる()
    {
        // 実レスポンスの抜粋（未使用フィールドが混ざっていても解析できることも兼ねて検証）
        string responseBody = """
            [{"Season":2026,"SeasonType":1,"TeamID":10,"Key":"NYY","City":"New York","Name":"Yankees","League":"AL","Division":"East","Wins":82,"Losses":53,"Percentage":0.607,"GamesBehind":0.0,"Streak":"W2"}]
            """;

        JsonArray response = StandingsFixture.CreateResponse();
        response[0] = JsonNode.Parse(responseBody)![0]!.DeepClone();
        TeamStanding team = Assert.Single(MlbApiClient.ParseStandings(response.ToJsonString()), team => team.Name == "Yankees");

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
        JsonArray response = StandingsFixture.CreateResponse();
        response.Add(JsonNode.Parse("""{"Name":"AL All-Stars","League":"AL","Division":"AL"}"""));
        response.Add(JsonNode.Parse("""{"Name":"NL All-Stars","League":"NL","Division":"NL"}"""));

        IReadOnlyList<TeamStanding> teams = MlbApiClient.ParseStandings(response.ToJsonString());

        Assert.Equal(StandingsFixture.Teams, teams);
    }

    [Fact]
    public void ParseStandings_空の配列なら空リストを返す()
    {
        // シーズン開始前はAPIが空配列を返す（実測済み）
        Assert.Empty(MlbApiClient.ParseStandings("[]"));
    }
}
