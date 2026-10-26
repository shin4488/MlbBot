using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using TwitterMlbBot.Mlb;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// sportsdata.io の順位取得（MlbApiClient）のテスト
/// HTTP応答だけを差し替え、実際のAPIレスポンスと同じ構造のJSONから「何がドメインに渡るか」を検証する
/// </summary>
public class MlbApiClientTest
{
    [Theory]
    [InlineData("null", "届いていない")]
    [InlineData("[null]", "成績が欠けている")]
    [InlineData("[{}]", "勝ち数")]
    [InlineData("[{\"Wins\":1}]", "負け数")]
    [InlineData("[{\"Name\":\"Example\",\"League\":\"AL\",\"Division\":\"East\",\"Wins\":1,\"Losses\":0}]", "構成と一致しない")]
    public async Task GetStandingsAsync_データ不備は例外を連鎖させず対象年と理由を伝える(string responseBody, string reason)
    {
        Exception exception = await Assert.ThrowsAnyAsync<Exception>(() => GetStandingsAsync(2025, responseBody));

        Assert.Null(exception.InnerException);
        Assert.Contains("2025年", exception.Message);
        Assert.Contains(reason, exception.Message);
    }

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
    public async Task GetStandingsAsync_不正な応答を空順位や架空の成績として扱わない(string responseBody)
    {
        // チーム情報の検証は、残りの球団がそろった応答でも不正な1件を拒否することを確かめる。
        if (responseBody.StartsWith('['))
        {
            JsonArray response = StandingsFixture.CreateResponse();
            response[0] = JsonNode.Parse(responseBody)![0]?.DeepClone();
            responseBody = response.ToJsonString();
        }
        // 正常な空配列と、取得したデータの欠落・破損は区別する
        await Assert.ThrowsAnyAsync<Exception>(() => GetStandingsAsync(2026, responseBody));
    }

    [Fact]
    public async Task GetStandingsAsync_チーム名_リーグ_地区_勝敗が取り出せる()
    {
        // 実レスポンスの抜粋（未使用フィールドが混ざっていても解析できることも兼ねて検証）
        string responseBody = """
            [{"Season":2026,"SeasonType":1,"TeamID":10,"Key":"NYY","City":"New York","Name":"Yankees","League":"AL","Division":"East","Wins":82,"Losses":53,"Percentage":0.607,"GamesBehind":0.0,"Streak":"W2"}]
            """;

        JsonArray response = StandingsFixture.CreateResponse();
        response[0] = JsonNode.Parse(responseBody)![0]!.DeepClone();
        TeamStanding team = Assert.Single(await GetStandingsAsync(2026, response.ToJsonString()), team => team.Name == "Yankees");

        Assert.Equal("AL", team.League);
        Assert.Equal("East", team.Division);
        Assert.Equal(82, team.Wins);
        Assert.Equal(53, team.Losses);
    }

    [Fact]
    public async Task GetStandingsAsync_地区優勝とワイルドカード進出の確定フラグを取り込む()
    {
        JsonArray response = StandingsFixture.CreateResponse();
        response[0]!["ClinchedDivision"] = true;
        response[0]!["ClinchedWildCard"] = false;
        response[1]!["ClinchedDivision"] = false;
        response[1]!["ClinchedWildCard"] = true;
        response[2]!.AsObject().Remove("ClinchedDivision");
        response[2]!.AsObject().Remove("ClinchedWildCard");
        string divisionWinner = response[0]!["Name"]!.GetValue<string>();
        string wildCardWinner = response[1]!["Name"]!.GetValue<string>();
        string withoutFlags = response[2]!["Name"]!.GetValue<string>();

        IReadOnlyList<TeamStanding> teams = await GetStandingsAsync(2026, response.ToJsonString());

        // 返却順は仕様にないため、チーム名で対応する成績を探す
        TeamStanding Find(string name) => Assert.Single(teams, team => team.Name == name);
        Assert.True(Find(divisionWinner).ClinchedDivision);
        Assert.False(Find(divisionWinner).ClinchedWildCard);
        Assert.False(Find(wildCardWinner).ClinchedDivision);
        Assert.True(Find(wildCardWinner).ClinchedWildCard);
        // 項目がない場合は確定していないものとして扱う
        Assert.False(Find(withoutFlags).ClinchedDivision);
        Assert.False(Find(withoutFlags).ClinchedWildCard);
    }

    [Fact]
    public async Task GetStandingsAsync_AllStar擬似チームは含まれない()
    {
        // レスポンスにはリーグ名と地区名が同一の擬似チーム（"AL"/"AL"）が混ざる
        JsonArray response = StandingsFixture.CreateResponse();
        response.Add(JsonNode.Parse("""{"Name":"AL All-Stars","League":"AL","Division":"AL"}"""));
        response.Add(JsonNode.Parse("""{"Name":"NL All-Stars","League":"NL","Division":"NL"}"""));

        IReadOnlyList<TeamStanding> teams = await GetStandingsAsync(2026, response.ToJsonString());

        // 返却順は仕様にないため、チーム名順にそろえて比べる
        Assert.Equal(
            StandingsFixture.Teams.OrderBy(team => team.Name, StringComparer.Ordinal),
            teams.OrderBy(team => team.Name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetStandingsAsync_空の配列なら空リストを返す()
    {
        // シーズン開始前はAPIが空配列を返す（実測済み）
        Assert.Empty(await GetStandingsAsync(2026, "[]"));
    }

    private static async Task<IReadOnlyList<TeamStanding>> GetStandingsAsync(int year, string responseBody)
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody) })));
        return await new MlbApiClient(client, "dummy-mlb-key", NullLogger<MlbApiClient>.Instance).GetStandingsAsync(year);
    }
}
