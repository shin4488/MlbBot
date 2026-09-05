namespace TwitterMlbBotExecution.Tests;

/// <summary>
/// HTTPの境界で固定応答を返す。実ネットワークや本番の認証情報を使わずリクエストを検証する
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return respond(request);
    }
}
