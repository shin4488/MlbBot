using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ドライランモードのテスト（ネットワークアクセスなし・実ツイートなしで実行できる）
/// </summary>
public class TwitterServiceDryRunTest
{
    /// <summary>
    /// 2地区分のダミー順位データを作成する
    /// </summary>
    private static Param CreateTestParam()
    {
        return new Param
        {
            TeamsList = new List<ParamByKey>
            {
                new ParamByKey
                {
                    Key = new GroupKey { League = "AL", Division = "East" },
                    TagMessage = "#MLB #RepBX #Yankees",
                    Teams = new List<DetailParam>
                    {
                        new DetailParam { Ranking = 1, Name = "Yankees", Wins = 80, Losses = 50, GamesBehind = 0 },
                        new DetailParam { Ranking = 2, Name = "Red Sox", Wins = 75, Losses = 55, GamesBehind = 5 },
                    },
                },
                new ParamByKey
                {
                    Key = new GroupKey { League = "NL", Division = "West" },
                    TagMessage = "#MLB #Dbacks #Diamondbacks",
                    Teams = new List<DetailParam>
                    {
                        new DetailParam { Ranking = 1, Name = "Diamondbacks", Wins = 82, Losses = 48, GamesBehind = 0 },
                        new DetailParam { Ranking = 2, Name = "Dodgers", Wins = 70, Losses = 60, GamesBehind = 12 },
                    },
                },
            },
        };
    }

    /// <summary>
    /// Console出力をキャプチャしてactionを実行し、出力文字列を返す
    /// </summary>
    private static async Task<string> CaptureConsoleOutAsync(Func<Task> action)
    {
        TextWriter originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            await action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        return writer.ToString();
    }

    [Fact]
    public async Task CreateTweet_ドライラン時はツイートせず文面をコンソール出力する()
    {
        // ドライラン時は認証情報を読み込まないため、環境変数・App.configなしでも動作する
        var service = new TwitterService(dryRun: true);

        string output = await CaptureConsoleOutAsync(() => service.CreateTweet(CreateTestParam()));

        // 地区ごとに1件、計2件のドライラン出力があること
        Assert.Equal(2, output.Split("dry-run").Length - 1);
        // 順位表・タグの内容が文面に含まれること
        Assert.Contains("AL | East", output);
        Assert.Contains("Yankees", output);
        Assert.Contains("#MLB #Dbacks #Diamondbacks", output);
    }

    [Fact]
    public async Task CreateTweet_順位データが空の場合は何も出力しない()
    {
        var service = new TwitterService(dryRun: true);
        var emptyParam = new Param { TeamsList = new List<ParamByKey>() };

        string output = await CaptureConsoleOutAsync(() => service.CreateTweet(emptyParam));

        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public async Task ExecuteTweet_ドライラン時は例外を投げてツイートを拒否する()
    {
        var service = new TwitterService(dryRun: true);

        // 誤投稿防止ガードの確認: ドライラン中の直接呼び出しはHTTPリクエスト前に必ず失敗する
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteTweet("テスト文面"));
    }
}
