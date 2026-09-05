using System.Net;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB APIの呼び出し失敗を表す例外
    /// 取得できなかった情報と応答コードを残す。応答本文は認証情報を含む可能性があるため保持しない。
    /// </summary>
    internal sealed class MlbApiException : Exception
    {
        public MlbApiException(string informationName, HttpStatusCode statusCode)
            : base($"{informationName}を取得できませんでした。配信元がエラーを返しました（HTTP {(int)statusCode}）。")
        {
        }
    }
}
