using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using TwitterMlbBot;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TwitterMlbBotExecution;

/// <summary>Schedulerから受け取る投稿対象。</summary>
public sealed record ScheduledEvent([property: JsonPropertyName("group")] string? Group);

/// <summary>Schedulerのグループ指定を本体へ渡すLambdaハンドラ。</summary>
public class Function
{
    public async Task FunctionHandlerAsync(ScheduledEvent? input, ILambdaContext context)
    {
        // 許可するグループ名の規則は、通常の起動引数と共通にする。
        // イベントの指定漏れを全地区ドライランとして扱わないよう、空値でもグループ指定を渡す。
        await Program.Main(["--group", input?.Group ?? ""]);
    }
}
