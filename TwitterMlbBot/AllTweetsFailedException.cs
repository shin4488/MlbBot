namespace TwitterMlbBot
{
    /// <summary>
    /// 全ツイートの送信に失敗したことを表す例外
    /// Lambda実行をエラー終了させ、CloudWatchのエラーメトリクスで検知できるようにするために投げる
    /// </summary>
    internal class AllTweetsFailedException : Exception
    {
        public AllTweetsFailedException(int attemptedCount)
            : base($"投稿を試みた{attemptedCount}件すべてに失敗しました。各投稿の失敗理由は直前のログを確認してください。")
        {
        }
    }
}
