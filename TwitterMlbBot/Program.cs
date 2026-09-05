using Microsoft.Extensions.Logging;
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
        // Lambdaのウォーム起動でも接続プールを再利用する。通信の寿命・設定はここだけで管理し、
        // 各クライアントは注入された接続を借りる（破棄しない）。認証情報は各リクエストだけに付ける
        private static readonly HttpClient httpClient = ApiHttpClientFactory.Create();

        /// <summary>
        /// エントリーポイント
        /// WebAPI接続の関係で非同期エントリーポイントとしている
        /// </summary>
        /// <param name="args">コマンドライン引数（Lambda経由の実行ではnull）</param>
        /// <returns></returns>
        public static async Task Main(string[]? args)
        {
            RunOptions options = RunOptions.Parse(args, Environment.GetEnvironmentVariable("DRY_RUN"), DateTime.UtcNow);

            // Lambda環境ではコンソール出力がそのままCloudWatch Logsに流れる
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                builder.AddSimpleConsole(console => console.SingleLine = true));

            // 依存関係の組み立て
            // ドライラン時はDryRunTweetSenderを使い、X API認証情報の読み込み自体を行わない（誤投稿を構造的に防ぐ）
            IStandingsProvider standingsProvider = new MlbApiClient(
                httpClient,
                RequireEnvironmentVariable("MLB_API_KEY"),
                loggerFactory.CreateLogger<MlbApiClient>());
            ITweetSender tweetSender = options.DryRun
                ? new DryRunTweetSender(Console.Out)
                : new TwitterApiSender(
                    httpClient,
                    new OAuth1(
                        RequireEnvironmentVariable("CONSUMER_KEY"),
                        RequireEnvironmentVariable("CONSUMER_SECRET"),
                        RequireEnvironmentVariable("ACCESS_KEY"),
                        RequireEnvironmentVariable("ACCESS_SECRET")),
                    loggerFactory.CreateLogger<TwitterApiSender>());
            BotRunner runner = new BotRunner(
                new MlbStatsApiClient(httpClient, loggerFactory.CreateLogger<MlbStatsApiClient>()),
                standingsProvider,
                new TweetComposer(new HashtagProvider()),
                tweetSender,
                loggerFactory.CreateLogger<BotRunner>());

            await runner.RunAsync(options.Year, options.Date);
        }

        /// <summary>
        /// 必須の環境変数を取得する。未設定の場合は原因がわかるメッセージで即座に失敗させる
        /// </summary>
        private static string RequireEnvironmentVariable(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException($"認証情報が未設定のため、ボットを起動できません。環境変数 {name} を設定してください。");
            }
            return value;
        }
    }
}
