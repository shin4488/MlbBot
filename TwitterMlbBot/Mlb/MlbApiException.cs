using System.Net;

namespace TwitterMlbBot.Mlb
{
    /// <summary>
    /// MLB APIから情報を取得できなかったこと（HTTPエラー応答、日程データの不正）を表す例外。
    /// 実装上の不具合による例外と区別するために使う。
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
