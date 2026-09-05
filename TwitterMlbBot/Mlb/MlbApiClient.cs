using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// sportsdata.io のMLB APIから順位データを取得するクライアント。
    /// レスポンスの形（フィールド名・All-Star用の擬似チームなどAPI固有の事情）はこのクラス内に閉じ込め、
    /// ドメインにはTeamStandingへ変換したものだけを渡す
    /// </summary>
    internal class MlbApiClient : IStandingsProvider
    {
        private static readonly HttpClient client = new HttpClient()
        {
            // Lambdaタイムアウトより先に打ち切り、原因を特定しやすくする
            Timeout = TimeSpan.FromSeconds(10),
        };
        private static readonly string endpoint = "https://api.sportsdata.io/v3/mlb/scores/json/Standings/";
        private readonly string apiKey;
        private readonly ILogger<MlbApiClient> logger;

        public MlbApiClient(string apiKey, ILogger<MlbApiClient> logger)
        {
            this.apiKey = apiKey;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<TeamStanding>> GetStandingsAsync(int year)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint + year);
            // APIキーはURIに含めずヘッダーで渡す（URIがログや例外メッセージに出てもキーが漏れないようにする）
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

            using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new MlbApiException(response.StatusCode, responseBody);
            }

            IReadOnlyList<TeamStanding> standings = ParseStandings(responseBody);
            // レスポンス全文はログに出さず、運用確認に必要な件数のみ出力する
            logger.LogInformation("MLB standings fetched: {TeamCount} teams for {Year}", standings.Count, year);
            return standings;
        }

        /// <summary>
        /// APIレスポンスからチーム成績を取り出す。All-Star用の擬似チームはここで除外し、ドメインには渡さない
        /// </summary>
        internal static IReadOnlyList<TeamStanding> ParseStandings(string responseBody)
        {
            List<StandingResponse> parsed =
                JsonSerializer.Deserialize<List<StandingResponse>>(responseBody) ?? new List<StandingResponse>();
            return parsed
                .Where(standing => !standing.IsAllStarPseudoTeam)
                .Select(standing => new TeamStanding(
                    standing.Name ?? "", standing.League ?? "", standing.Division ?? "", standing.Wins, standing.Losses))
                .ToList();
        }

        // APIレスポンスの形（このクライアント内だけの転送用の型。使うフィールドのみ定義し、未定義の項目は無視される）
        private sealed record StandingResponse(
            [property: JsonPropertyName("Name")] string? Name,
            [property: JsonPropertyName("League")] string? League,
            [property: JsonPropertyName("Division")] string? Division,
            [property: JsonPropertyName("Wins")] int Wins,
            [property: JsonPropertyName("Losses")] int Losses)
        {
            /// <summary>
            /// All-Star用の擬似チーム（"AL All-Stars" 等）かどうか。
            /// レスポンスには実在の30球団に加えてこの擬似チームが混ざっており、リーグ名と地区名が同一（"AL"/"AL"）になるのが特徴
            /// </summary>
            public bool IsAllStarPseudoTeam => League is "AL" or "NL" && League == Division;
        }
    }
}
