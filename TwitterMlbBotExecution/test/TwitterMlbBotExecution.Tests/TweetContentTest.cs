using TwitterMlbBot.Composing;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ツイート文面（TweetContent）のテスト。
/// Xの文字数ルール（ラテン文字等は1、CJK文字・絵文字等は2として数え、上限280）の境界を検証する
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

    [Fact]
    public void CharacterCount_日本語は1文字を2として数える()
    {
        // Xの重み付きルール: CJK文字は英数字の2倍（半分の文字数で上限に達する）
        var content = new TweetContent(new string('あ', TweetContent.CharacterLimit / 2));

        Assert.Equal(TweetContent.CharacterLimit, content.CharacterCount);
        Assert.False(content.ExceedsCharacterLimit);
        Assert.True(new TweetContent(content.Text + "あ").ExceedsCharacterLimit);
    }

    [Fact]
    public void CharacterCount_絵文字は2として数える()
    {
        // ⚾（BMP内）も🔥（サロゲートペア）も、string.Lengthではなく1絵文字=2として数える
        Assert.Equal(2, new TweetContent("⚾").CharacterCount);
        Assert.Equal(2, new TweetContent("🔥").CharacterCount);
    }
}
