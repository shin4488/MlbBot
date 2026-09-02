using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// sportsdata.io のMLB APIから順位データを取得するクライアント
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
            request.Headers.Add("Ocp-Apim-Subscription-Key", this.apiKey);

            using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new MlbApiException(response.StatusCode, responseBody);
            }

            List<TeamStanding> standings =
                JsonSerializer.Deserialize<List<TeamStanding>>(responseBody) ?? new List<TeamStanding>();
            // レスポンス全文はログに出さず、運用確認に必要な件数のみ出力する
            this.logger.LogInformation("MLB standings fetched: {TeamCount} teams for {Year}", standings.Count, year);
            return standings;
        }
    }
}
