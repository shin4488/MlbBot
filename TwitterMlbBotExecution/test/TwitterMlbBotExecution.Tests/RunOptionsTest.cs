using TwitterMlbBot;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// 実行オプション解析（RunOptions.Parse）のテスト
/// 純粋関数のため、Lambda相当（argsなし）・年またぎなどの境界も固定入力で検証できる
/// </summary>
public class RunOptionsTest
{
    private static readonly DateTime seasonUtcNow = new DateTime(2026, 8, 29, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_dryRun引数でドライランになる()
    {
        var options = RunOptions.Parse(new[] { "--dry-run" }, null, seasonUtcNow);

        Assert.True(options.DryRun);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    public void Parse_環境変数DRY_RUNがtrueならドライランになる(string environmentValue)
    {
        var options = RunOptions.Parse(null, environmentValue, seasonUtcNow);

        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_Lambda相当の入力では通常送信になる()
    {
        // Lambda経由の実行: argsはnull・DRY_RUN未設定
        var options = RunOptions.Parse(null, null, seasonUtcNow);

        Assert.False(options.DryRun);
    }

    [Fact]
    public void Parse_数値の引数があればその年を対象とする()
    {
        var options = RunOptions.Parse(new[] { "--dry-run", "2025" }, null, seasonUtcNow);

        Assert.Equal(2025, options.Year);
    }

    [Fact]
    public void Parse_年の指定がなければ日本時間の現在年を対象とする()
    {
        var options = RunOptions.Parse(null, null, seasonUtcNow);

        Assert.Equal(2026, options.Year);
    }

    [Fact]
    public void Parse_表示用の日付は直近の試合日_日本時間の前日_になる()
    {
        // UTC 2026-08-29 21:00 = JST 2026-08-30 06:00 → 試合日はその前日
        var eveningUtc = new DateTime(2026, 8, 29, 21, 0, 0, DateTimeKind.Utc);

        var options = RunOptions.Parse(null, null, eveningUtc);

        Assert.Equal(new DateOnly(2026, 8, 29), options.Date);
    }

    [Fact]
    public void Parse_年の境界は日本時間基準で判定される()
    {
        // UTCではまだ大晦日だが、日本時間では年が明けている時刻
        var newYearEveUtc = new DateTime(2026, 12, 31, 16, 0, 0, DateTimeKind.Utc);

        var options = RunOptions.Parse(null, null, newYearEveUtc);

        Assert.Equal(2027, options.Year);
        // 表示用の日付は試合日（日本時間の前日）
        Assert.Equal(new DateOnly(2026, 12, 31), options.Date);
    }
}
