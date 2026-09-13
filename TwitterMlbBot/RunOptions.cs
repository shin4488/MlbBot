using System.Globalization;

namespace TwitterMlbBot;

/// <summary>代表タイムゾーンの前日を表示対象日とする実行オプション。</summary>
internal record RunOptions(bool DryRun, int Year, DateOnly Date, PostingGroup Group)
{
    private const string InvalidArgumentsMessage = "起動引数が不正です。--dry-run、--group East|Central|West、対象年を確認してください。";

    public static IReadOnlyList<RunOptions> Parse(string[]? args, string? dryRunEnvironmentValue, DateTime utcNow)
    {
        string[] arguments = args ?? [];
        bool dryRun = string.Equals(dryRunEnvironmentValue, "true", StringComparison.OrdinalIgnoreCase);
        PostingGroup? group = null;
        int? year = null;
        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            if (argument == "--dry-run")
            {
                dryRun = true;
                continue;
            }

            bool canReadGroupValue = argument == "--group" && group is null && index + 1 < arguments.Length;
            if (canReadGroupValue)
            {
                group = ParseGroup(arguments[++index]);
                continue;
            }

            if (year is not null)
            {
                throw new ArgumentException(InvalidArgumentsMessage);
            }
            year = ParseYear(argument);
        }

        // 投稿先の指定漏れによる意図しない全地区投稿を防ぐため、通常投稿ではグループ指定を必須にする。
        // 全地区の文面をまとめて確認できるよう、送信しないドライランでは未指定を許可する。
        if (group is null && !dryRun)
        {
            throw new ArgumentException("投稿グループが未指定のため起動できません。--group East|Central|Westを指定してください。");
        }

        // 全地区の確認でも、日付は各グループの代表タイムゾーンで個別に算出する。
        PostingGroup[] groups = group is { } selected ? [selected] : [PostingGroup.East, PostingGroup.Central, PostingGroup.West];
        return groups.Select(selected => CreateForGroup(dryRun, year, utcNow, selected)).ToList().AsReadOnly();
    }

    private static PostingGroup ParseGroup(string argument) => argument switch
    {
        "East" => PostingGroup.East,
        "Central" => PostingGroup.Central,
        "West" => PostingGroup.West,
        _ => throw new ArgumentException("投稿グループにはEast・Central・Westのいずれかを指定してください。"),
    };

    private static int ParseYear(string argument)
    {
        bool isValidYear = int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out int year)
            && year is >= 1 and <= 9999;
        if (!isValidYear)
        {
            // 入力値自体はログに残さず、指定ミスで投稿範囲が広がることを防ぐ。
            throw new ArgumentException(InvalidArgumentsMessage);
        }
        return year;
    }

    private static RunOptions CreateForGroup(bool dryRun, int? year, DateTime utcNow, PostingGroup group)
    {
        string zoneId = group switch
        {
            PostingGroup.East => "America/New_York",
            PostingGroup.Central => "America/Chicago",
            _ => "America/Los_Angeles",
        };
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        DateTime localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        DateOnly date = DateOnly.FromDateTime(localNow).AddDays(-1);
        return new RunOptions(dryRun, year ?? date.Year, date, group);
    }
}
