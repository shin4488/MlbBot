using System;
using System.Linq;

namespace TwitterMlbBot
{
    /// <summary>
    /// 実行オプション（値オブジェクト）
    /// </summary>
    /// <param name="DryRun">ツイートせず文面をコンソール出力するのみとするか</param>
    /// <param name="Year">順位データの対象年（西暦）</param>
    /// <param name="Date">ツイート文面に表示する日付（直近の試合日 = アメリカの日付 = 日本時間の前日）</param>
    internal record RunOptions(bool DryRun, int Year, DateOnly Date)
    {
        private static readonly TimeZoneInfo jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        /// <summary>
        /// コマンドライン引数・環境変数・現在時刻から実行オプションを組み立てる純粋関数
        /// </summary>
        /// <param name="args">
        /// コマンドライン引数（Lambda経由の実行ではnull）。
        /// --dry-run でドライラン、数値の引数があればその年を対象とする
        /// </param>
        /// <param name="dryRunEnvironmentValue">環境変数DRY_RUNの値（"true"でドライラン。Lambdaでは通常未設定）</param>
        /// <param name="utcNow">現在時刻（UTC）。年・日付は日本時間に換算して決める</param>
        public static RunOptions Parse(string[]? args, string? dryRunEnvironmentValue, DateTime utcNow)
        {
            string[] arguments = args ?? Array.Empty<string>();

            bool dryRun = arguments.Contains("--dry-run")
                || string.Equals(dryRunEnvironmentValue, "true", StringComparison.OrdinalIgnoreCase);

            DateOnly jstToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, jst));
            // 表示する順位は前夜（アメリカ時間）の試合結果のため、日付も試合日＝日本時間の前日に合わせる
            DateOnly gameDate = jstToday.AddDays(-1);

            int year = arguments
                .Select(argument => int.TryParse(argument, out int inputYear) ? inputYear : 0)
                .FirstOrDefault(inputYear => inputYear > 0);
            if (year == 0)
            {
                year = jstToday.Year;
            }

            return new RunOptions(dryRun, year, gameDate);
        }
    }
}
