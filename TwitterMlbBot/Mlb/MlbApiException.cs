using System.Net;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB APIの呼び出し失敗を表す例外
    /// 取得できなかった情報を示し、原因の調査用に応答コードと配信元の回答を添える。リクエストURIや認証情報は加えない
    /// </summary>
    internal sealed class MlbApiException : Exception
    {
        public MlbApiException(string informationName, HttpStatusCode statusCode, string responseBody)
            : base($"{informationName}を取得できませんでした。配信元がエラーを返しました（HTTP {(int)statusCode}）。配信元の回答: {responseBody}")
        {
        }
    }
}
