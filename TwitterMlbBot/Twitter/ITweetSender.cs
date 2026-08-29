using System.Threading.Tasks;
using TwitterMlbBot.Composing;

namespace TwitterMlbBot.Twitter
{
    /// <summary>
    /// ツイートの送信先
    /// </summary>
    internal interface ITweetSender
    {
        /// <summary>
        /// ツイートを1件送信する
        /// </summary>
        /// <param name="tweetContent">ツイート文面</param>
        /// <returns>送信に成功した場合はtrue</returns>
        Task<bool> SendAsync(TweetContent tweetContent);
    }
}
