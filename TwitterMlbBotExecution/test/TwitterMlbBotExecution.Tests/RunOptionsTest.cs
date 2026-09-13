using System.Globalization;
using TwitterMlbBot;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

public class RunOptionsTest
{
    private static readonly DateTime now = new(2026, 8, 2, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("East", "2026-07-01T12:00:00Z", "2026-06-30")]
    [InlineData("Central", "2026-07-01T13:00:00Z", "2026-06-30")]
    [InlineData("West", "2026-07-01T15:00:00Z", "2026-06-30")]
    [InlineData("East", "2026-01-01T13:00:00Z", "2025-12-31")]
    [InlineData("Central", "2026-01-01T14:00:00Z", "2025-12-31")]
    [InlineData("West", "2026-01-01T16:00:00Z", "2025-12-31")]
    [InlineData("East", "2026-03-08T12:00:00Z", "2026-03-07")]
    [InlineData("Central", "2026-03-08T13:00:00Z", "2026-03-07")]
    [InlineData("West", "2026-03-08T15:00:00Z", "2026-03-07")]
    [InlineData("East", "2026-11-01T13:00:00Z", "2026-10-31")]
    [InlineData("Central", "2026-11-01T14:00:00Z", "2026-10-31")]
    [InlineData("West", "2026-11-01T16:00:00Z", "2026-10-31")]
    [InlineData("West", "2028-03-01T16:00:00Z", "2028-02-29")]
    [InlineData("East", "2026-08-02T03:59:59Z", "2026-07-31")]
    [InlineData("East", "2026-08-02T04:00:00Z", "2026-08-01")]
    [InlineData("Central", "2026-08-02T04:59:59Z", "2026-07-31")]
    [InlineData("Central", "2026-08-02T05:00:00Z", "2026-08-01")]
    [InlineData("West", "2026-08-02T06:59:59Z", "2026-07-31")]
    [InlineData("West", "2026-08-02T07:00:00Z", "2026-08-01")]
    public void グループの現地前日とその年を使用する(string group, string instant, string expected)
    {
        var option = Assert.Single(RunOptions.Parse(["--group", group], null,
            DateTime.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)));
        var date = DateOnly.ParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(date, option.Date);
        Assert.Equal(date.Year, option.Year);
        Assert.Equal(group, option.Group.ToString());
        Assert.False(option.DryRun);
    }

    [Fact]
    public void 全地区ドライランも各現地日付を使う()
    {
        var options = RunOptions.Parse(["--dry-run"], null, new DateTime(2026, 8, 2, 5, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new[] { PostingGroup.East, PostingGroup.Central, PostingGroup.West }, options.Select(option => option.Group));
        Assert.Equal(new[] { new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 31) }, options.Select(option => option.Date));
        Assert.All(options, option => Assert.True(option.DryRun));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    public void 環境変数だけでも全地区ドライランになる(string value)
    {
        Assert.All(RunOptions.Parse(null, value, now), option => Assert.True(option.DryRun));
    }

    [Fact]
    public void 明示したドライランと対象年を優先する()
    {
        var option = Assert.Single(RunOptions.Parse(["--dry-run", "2025", "--group", "West"], "false", now));
        Assert.True(option.DryRun);
        Assert.Equal(2025, option.Year);
        Assert.Equal(new DateOnly(2026, 8, 1), option.Date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--group")]
    [InlineData("--group east")]
    [InlineData("--group All")]
    [InlineData("--group 0")]
    [InlineData("--group East,West")]
    [InlineData("--group East --group West")]
    [InlineData("--dry-run --group unknown")]
    [InlineData("--dry-rnu --group East")]
    [InlineData("--dry-run 0")]
    [InlineData("--dry-run 10000")]
    [InlineData("--dry-run 2025 2026")]
    public void 不正入力は投稿対象を補完せず拒否する(string arguments)
    {
        Assert.ThrowsAny<ArgumentException>(() => RunOptions.Parse(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries), null, now));
    }
}
