using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// ドライランモードのテスト（ネットワークアクセスなし・実ツイートなしで実行できる）
///
/// テストは仕様ベースで書く方針:
/// 文面のフォーマット（区切り文字・絵文字・並び等）は変更されうるため、
/// 「入力データの内容が文面に反映される」「ツイートされない」という仕様レベルの
/// 不変条件のみを検証し、見た目の詳細には依存させない。
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
    public async Task CreateTweet_ドライラン時は全地区の文面をコンソール出力する()
    {
        // ドライラン時は認証情報を読み込まないため、環境変数・App.configなしでも動作する。
        // 仮に実装が誤って送信経路に入った場合は例外で失敗するため、
        // このテストが例外なく完走すること自体が「ツイートされない」ことの検証を兼ねる
        var service = new TwitterService(dryRun: true);

        string output = await CaptureConsoleOutAsync(() => service.CreateTweet(CreateTestParam()));

        // 全地区の全チーム名が出力されること
        Assert.Contains("Yankees", output);
        Assert.Contains("Red Sox", output);
        Assert.Contains("Diamondbacks", output);
        Assert.Contains("Dodgers", output);
        // 地区の識別情報（地区名）が出力されること
        Assert.Contains("East", output);
        Assert.Contains("West", output);
        // 各地区のタグメッセージが出力されること
        Assert.Contains("#MLB #RepBX #Yankees", output);
        Assert.Contains("#MLB #Dbacks #Diamondbacks", output);
    }

    [Fact]
    public async Task CreateTweet_順位データが空の場合は文面を出力しない()
    {
        var service = new TwitterService(dryRun: true);
        var emptyParam = new Param { TeamsList = new List<ParamByKey>() };

        string output = await CaptureConsoleOutAsync(() => service.CreateTweet(emptyParam));

        Assert.True(string.IsNullOrWhiteSpace(output), $"順位データが空でも出力があった: {output}");
    }

    [Fact]
    public async Task ExecuteTweet_ドライラン時は例外を投げてツイートを拒否する()
    {
        var service = new TwitterService(dryRun: true);

        // 誤投稿防止ガードの確認: ドライラン中の直接送信は必ず失敗する（例外の型は問わない）
        await Assert.ThrowsAnyAsync<Exception>(() => service.ExecuteTweet("テスト文面"));
    }
}
