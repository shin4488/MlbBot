using TwitterMlbBot.Composing;
using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ドライラン用送信先（DryRunTweetSender）のテスト
/// </summary>
public class DryRunTweetSenderTest
{
    [Fact]
    public async Task SendAsync_文面をコンソール出力し成功として返す()
    {
        using var writer = new StringWriter();

        bool result = await new DryRunTweetSender(writer).SendAsync(new TweetContent("テスト文面です"));

        Assert.True(result);
        Assert.Contains("テスト文面です", writer.ToString());
    }
}
