using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace TwitterMlbBot.Mlb
{
    class MlbService
    {
        private static readonly HttpClient client = new HttpClient()
        {
            // Lambdaタイムアウト（15秒）より先に打ち切り、原因を特定しやすくする
            Timeout = TimeSpan.FromSeconds(10),
        };
        private static readonly string endpoint = "https://api.sportsdata.io/v3/mlb/scores/json/Standings/";
        private readonly string apiKey;

        public MlbService()
        {
            // WebAPI認証用データ取得
            Dictionary<string, string> apiKeyConfig = ProcessUtility.ReadAppConfig("mlb");
            // AWSのlambda関数使用時はApp.configの値がnullとなるためnullチェックを入れる
            this.apiKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "apiKey", "MLB_API_KEY");
        }

        /// <summary>
        /// WebAPI接続によるMLBのチームデータ取得
        /// </summary>
        /// <returns>リーグごと・地区ごとのチームデータ</returns>
        public async Task<Result> GetStandingData(Param param)
        {
            string uri = endpoint + param.Year;
            // APIキーはURIに含めずヘッダーで渡す（URIがログや例外メッセージに出てもキーが漏れないようにする）
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Ocp-Apim-Subscription-Key", this.apiKey },
            };

            // WebAPIコールアウト
            HttpResponseMessage response =
                await ProcessUtility.CalloutAsync(client, "GET", uri, headers, null);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new MlbApiException(response.StatusCode, responseBody);
            }

            Result result = new Result
            {
                ResultTeamList = JsonSerializer.Deserialize<List<DetailResult>>(responseBody)
            };
            // レスポンス全文はログに出さず、運用確認に必要な件数のみ出力する
            Console.WriteLine($"MLB standings fetched: {result.ResultTeamList.Count} teams for {param.Year}");
            return result;
        }
    }
}
