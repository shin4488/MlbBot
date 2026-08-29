using System;
using System.Net;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB APIの呼び出し失敗を表す例外
    /// リクエストURIは含めない（メッセージはステータスコードとレスポンスボディのみとし、機密情報が漏れる余地を作らない）
    /// </summary>
    class MlbApiException : Exception
    {
        public MlbApiException(HttpStatusCode statusCode, string responseBody)
            : base($"MLB API returned {(int)statusCode} ({statusCode}): {responseBody}")
        {
        }
    }
}
