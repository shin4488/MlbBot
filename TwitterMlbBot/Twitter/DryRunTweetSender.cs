using System;
using System.Threading.Tasks;

namespace TwitterMlbBot.Twitter
{
    /// <summary>
    /// ツイートせず文面をコンソール出力するだけのドライラン用送信先
    /// </summary>
    internal class DryRunTweetSender : ITweetSender
    {
        public Task<bool> SendAsync(string tweetContent)
        {
            Console.WriteLine($"----- dry-run: 以下はツイートされません（{tweetContent.Length}文字） -----");
            Console.WriteLine(tweetContent);
            return Task.FromResult(true);
        }
    }
}
