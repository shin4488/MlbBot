using System.Net;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB APIの呼び出し失敗を表す例外
    /// HTTP応答やデータ不正による取得失敗を、実装上の不具合と区別する。
    /// 応答本文は認証情報を含む可能性があるため保持しない。
    /// </summary>
    internal sealed class MlbApiException : Exception
    {
        // クライアントで検証した失敗理由だけを受け取り、応答本文や元のJSON例外は渡さない。
        public MlbApiException(string message) : base(message)
        {
        }

        public MlbApiException(string informationName, HttpStatusCode statusCode)
            : base($"{informationName}を取得できませんでした。配信元がエラーを返しました（HTTP {(int)statusCode}）。")
        {
        }
    }
}
