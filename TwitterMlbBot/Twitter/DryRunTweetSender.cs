using TwitterMlbBot.Composing;

namespace TwitterMlbBot.Twitter
{
    /// <summary>
    /// ツイートせず文面をコンソール出力するだけのドライラン用送信先
    /// </summary>
    internal class DryRunTweetSender : ITweetSender
    {
        private readonly TextWriter output;

        public DryRunTweetSender(TextWriter output)
        {
            // 出力先の選択と寿命は呼び出し側に任せ、プロセス全体のConsole.Outを変更しない
            this.output = output;
        }

        public Task<bool> SendAsync(TweetContent tweetContent)
        {
            output.WriteLine($"----- dry-run: 以下はツイートされません（{tweetContent.CharacterCount}文字） -----");
            output.WriteLine(tweetContent.Text);
            return Task.FromResult(true);
        }
    }
}
