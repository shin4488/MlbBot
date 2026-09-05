using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TwitterMlbBot;
using TwitterMlbBot.Authorization;
using TwitterMlbBot.Composing;
using TwitterMlbBot.Mlb;
using TwitterMlbBot.Twitter;
using Xunit;

namespace TwitterMlbBotExecution.Tests;

public class ApiSafetyTest
{
    [Fact]
    public async Task 転送先に認証情報を送らない()
    {
        // 実際のHTTPハンドラによる自動転送を検証する。接続先はループバックだけ。
        await using var server = new LoopbackServer(redirect: true);
        using var client = ApiHttpClientFactory.Create();
        using var request = new HttpRequestMessage(HttpMethod.Get, server.Address);
        request.Headers.Add("Ocp-Apim-Subscription-Key", "dummy-key");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, server.RequestCount);
    }

    [Fact]
    public async Task 巨大な応答は全体をメモリに読み込まず失敗する()
    {
        await using var server = new LoopbackServer(redirect: false);
        using var client = ApiHttpClientFactory.Create();

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync(server.Address));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 順位と日程のHTTPエラーは応答本文を例外に含めない(bool standings)
    {
        const string privateContent = "dummy-private-response-content";
        using var client = new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(privateContent) })));

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            if (standings)
                await new MlbApiClient(client, "dummy-key", NullLogger<MlbApiClient>.Instance).GetStandingsAsync(2026);
            else
                await new MlbStatsApiClient(client, NullLogger<MlbStatsApiClient>.Instance).GetSeasonCalendarAsync(2026);
        });

        Assert.DoesNotContain(privateContent, exception.ToString());
    }

    [Fact]
    public async Task 投稿失敗のログに応答本文を含めない()
    {
        const string privateContent = "dummy-private-response-content";
        using var client = new HttpClient(new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent(privateContent) })));
        var logger = new RecordingLogger<TwitterApiSender>();
        var sender = new TwitterApiSender(client, new OAuth1("dummy", "dummy", "dummy", "dummy"), logger);

        Assert.False(await sender.SendAsync(new TweetContent("test")));

        Assert.NotEmpty(logger.Messages);
        Assert.All(logger.Messages, message => Assert.DoesNotContain(privateContent, message));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void JSON解析エラーの内部例外からも応答内容を漏らさない(bool standings)
    {
        const string privateContent = "dummy-private-property";
        string malformed = "{\"" + privateContent + "\": tru}";
        Exception exception = Assert.ThrowsAny<Exception>(() =>
        {
            if (standings) MlbApiClient.ParseStandings("[" + malformed + "]");
            else MlbStatsApiClient.ParseSeasonCalendar(malformed, 2026);
        });

        Assert.DoesNotContain(privateContent, exception.ToString());
    }

    [Fact]
    public void 大きな勝敗でも勝率とゲーム差の符号が反転しない()
    {
        var tied = new TeamStanding("Tied", "AL", "East", int.MaxValue, int.MaxValue);
        var leader = new TeamStanding("Leader", "AL", "East", int.MaxValue, 0);
        var trailer = new TeamStanding("Trailer", "AL", "East", 0, int.MaxValue);

        Assert.Equal(0.5, tied.Percentage);
        Assert.True(trailer.GamesBehind(leader) > 0);
        Assert.True(leader.GamesBehind(trailer) < 0);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class LoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(15));
        private readonly Task serving;
        public Uri Address { get; }
        public int RequestCount { get; private set; }

        public LoopbackServer(bool redirect)
        {
            listener.Start();
            Address = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
            serving = ServeAsync(redirect);
        }

        private async Task ServeAsync(bool redirect)
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using TcpClient connection = await listener.AcceptTcpClientAsync(stop.Token);
                    using NetworkStream stream = connection.GetStream();
                    using var reader = new StreamReader(stream, leaveOpen: true);
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync(stop.Token))) { }
                    RequestCount++;
                    string response = redirect
                        ? $"HTTP/1.1 302 Found\r\nLocation: {Address}next\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        // 上限がなければ正常に読み込める大きさの本文を返す。
                        : "HTTP/1.1 200 OK\r\nContent-Length: 2097152\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), stop.Token);
                    if (!redirect)
                    {
                        try { await stream.WriteAsync(new byte[2097152], stop.Token); }
                        catch (IOException) { /* 上限で拒否したクライアントは途中で接続を閉じる。 */ }
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            listener.Stop();
            await serving;
            stop.Dispose();
        }
    }
}
