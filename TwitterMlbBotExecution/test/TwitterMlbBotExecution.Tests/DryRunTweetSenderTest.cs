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
        TextWriter originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        bool result;
        try
        {
            result = await new DryRunTweetSender().SendAsync("テスト文面です");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.True(result);
        Assert.Contains("テスト文面です", writer.ToString());
    }
}
