using System;
using System.Linq;
using System.Threading.Tasks;
using TwitterMlbBot.Authorization;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;

namespace TwitterMlbBot
{
    /// <summary>
    /// エントリーポイント。引数解析と依存関係の組み立てだけを行い、処理本体はBotRunnerに任せる
    /// </summary>
    public class Program
    {
        /// <summary>
        /// エントリーポイント
        /// WebAPI接続の関係で非同期エントリーポイントとしている
        /// </summary>
        /// <param name="args">コマンドライン引数（Lambda経由の実行ではnull）</param>
        /// <returns></returns>
        public static async Task Main(string[]? args)
        {
            string[] arguments = args ?? Array.Empty<string>();

            // --dry-run指定（または環境変数DRY_RUN=true）の場合はツイートせず文面をコンソール出力するのみとする
            // Lambda実行時はargsがnull・DRY_RUN未設定のため、通常どおりツイートされる
            bool dryRun = arguments.Contains("--dry-run")
                || string.Equals(Environment.GetEnvironmentVariable("DRY_RUN"), "true", StringComparison.OrdinalIgnoreCase);

            // コマンドライン引数で西暦年が入力されたらその年を使用、入力されなかったら現在の西暦年を使用
            int year = arguments
                .Select(argument => int.TryParse(argument, out int inputYear) ? inputYear : 0)
                .FirstOrDefault(inputYear => inputYear > 0);
            if (year == 0)
            {
                // Lambda実行環境の時刻はUTCのため、日本時間基準で対象年を決める
                TimeZoneInfo jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
                year = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst).Year;
            }

            // 依存関係の組み立て
            // ドライラン時はDryRunTweetSenderを使い、X API認証情報の読み込み自体を行わない（誤投稿を構造的に防ぐ）
            IStandingsProvider standingsProvider = new MlbApiClient(RequireEnvironmentVariable("MLB_API_KEY"));
            ITweetSender tweetSender = dryRun
                ? new DryRunTweetSender()
                : new TwitterApiSender(new OAuth1(
                    RequireEnvironmentVariable("CONSUMER_KEY"),
                    RequireEnvironmentVariable("CONSUMER_SECRET"),
                    RequireEnvironmentVariable("ACCESS_KEY"),
                    RequireEnvironmentVariable("ACCESS_SECRET")));
            BotRunner runner = new BotRunner(standingsProvider, new TweetComposer(new HashtagProvider()), tweetSender);

            await runner.RunAsync(year);
        }

        /// <summary>
        /// 必須の環境変数を取得する。未設定の場合は原因がわかるメッセージで即座に失敗させる
        /// </summary>
        private static string RequireEnvironmentVariable(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"環境変数 {name} が設定されていません。");
            }
            return value;
        }
    }
}
