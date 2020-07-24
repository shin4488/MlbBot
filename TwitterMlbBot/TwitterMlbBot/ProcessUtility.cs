using Newtonsoft.Json;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;

namespace TwitterMlbBot
{
    class ProcessUtility
    {
        /// <summary>
        /// WebAPIコールアウト
        /// </summary>
        /// <param name="client">HttpClientオブジェクト</param>
        /// <param name="method">リクエストメソッド</param>
        /// <param name="uri">リクエストURI</param>
        /// <param name="headers">リクエストヘッダ</param>
        /// <param name="body">リクエストボディ</param>
        /// <returns>レスポンスオブジェクト</returns>
        public static async Task<HttpResponseMessage> CalloutAsync(
            HttpClient client, string method, string uri,
            Dictionary<string, string> headers, object body
            )
        {
            // Httpメソッド、URIの設定
            HttpMethod httpMethod = new HttpMethod(method);
            using HttpRequestMessage request = new HttpRequestMessage(httpMethod, uri);
            
            if (headers != null)
            {
                // リクエストヘッダの設定
                foreach (KeyValuePair<string, string> header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (body != null)
            {
                // リクエストボディの設定
                string requestBody = JsonConvert.SerializeObject(body);
                StringContent bodyContent = new StringContent(requestBody);
                request.Content = bodyContent;
            }

            return await client.SendAsync(request).ConfigureAwait(false);
        }

        /// <summary>
        /// アプリケーション構成ファイルから設定データ読み取り
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public static Dictionary<string, string> ReadAppConfig(string identifier)
        {
            string config = ConfigurationManager.AppSettings[identifier];
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(config);
        }
    }
}
