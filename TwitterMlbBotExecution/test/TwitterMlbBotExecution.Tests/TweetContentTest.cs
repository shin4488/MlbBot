using TwitterMlbBot.Composing;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ツイート文面（TweetContent）のテスト。文字数上限の境界を検証する
/// </summary>
public class TweetContentTest
{
    [Fact]
    public void ExceedsCharacterLimit_上限ちょうどは超過ではない()
    {
        var content = new TweetContent(new string('a', TweetContent.CharacterLimit));

        Assert.False(content.ExceedsCharacterLimit);
    }

    [Fact]
    public void ExceedsCharacterLimit_上限を1字でも超えたら超過になる()
    {
        var content = new TweetContent(new string('a', TweetContent.CharacterLimit + 1));

        Assert.True(content.ExceedsCharacterLimit);
    }
}
