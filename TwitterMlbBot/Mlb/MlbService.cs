using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace TwitterMlbBot.Mlb
{
    class MlbService
    {
        /// <summary>
        /// WebAPIコールアウト用
        /// </summary>
        private static readonly HttpClient _client = new HttpClient();
        private readonly string _endpoint = "https://api.sportsdata.io/v3/mlb/scores/json/Standings/";
        private readonly string _apiKey;

        public MlbService()
        {
            // WebAPI認証用データ取得
            Dictionary<string, string> apiKeyConfig = ProcessUtility.ReadAppConfig("mlb");
            // AWSのlambda関数使用時はApp.configの値がnullとなるためnullチェックを入れる
            _apiKey = ProcessUtility.GetEnvVarByKey(apiKeyConfig, "apiKey", "MLB_API_KEY");
        }

        /// <summary>
        /// WebAPI接続によるMLBのチームデータ取得
        /// </summary>
        /// <returns>リーグごと・地区ごとのチームデータ</returns>
        public async Task<Result> GetStandingData(Param param)
        {
            string uri = _endpoint + param.Year;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Ocp-Apim-Subscription-Key", _apiKey }
            };

            // WebAPIコールアウト
            HttpResponseMessage response =
                await ProcessUtility.CalloutAsync(_client, "GET", uri, headers, null);
            if (!response.IsSuccessStatusCode)
            {
                // WebAPIレスポンスが200台:OK以外の場合は例外発生
                throw new Exception();
            }

            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Result result = new Result
            {
                ResultTeamList = JsonSerializer.Deserialize<List<DetailResult>>(responseBody)
            };
            return result;
        }
    }
}
