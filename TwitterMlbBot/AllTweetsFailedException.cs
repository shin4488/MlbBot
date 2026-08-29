using System;

namespace TwitterMlbBot
{
    /// <summary>
    /// 全ツイートの送信に失敗したことを表す例外
    /// Lambda実行をエラー終了させ、CloudWatchのエラーメトリクスで検知できるようにするために投げる
    /// </summary>
    internal class AllTweetsFailedException : Exception
    {
        public AllTweetsFailedException(int attemptedCount)
            : base($"ツイートが全{attemptedCount}件失敗しました。失敗理由は直前のログを参照。")
        {
        }
    }
}
