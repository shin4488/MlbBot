using Amazon.Lambda.Core;
using TwitterMlbBot;

// Lambdaランタイムが呼び出しイベント（EventBridgeのJSON）をハンドラの引数へ変換するためのシリアライザ指定
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TwitterMlbBotExecution;

/// <summary>
/// Lambdaハンドラ。EventBridgeの定期実行から呼ばれ、本体（Program.Main）へ処理を委ねるだけの薄いラッパー
/// </summary>
public class Function
{
    /// <summary>
    /// ハンドラ本体。イベント内容（スケジュールルールの情報）は処理に使わないため参照しない
    /// （引数はLambdaの呼び出し規約に合わせたもの。呼び出しIDなどはランタイムがSTART/END行として自動でログ出力する）
    /// </summary>
    public async Task FunctionHandlerAsync(object input, ILambdaContext context)
    {
        await Program.Main(null);
    }
}
