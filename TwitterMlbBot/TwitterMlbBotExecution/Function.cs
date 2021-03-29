using System.Threading.Tasks;
using TwitterMlbBot;

using Amazon.Lambda.Core;
using System;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TwitterMlbBotExecution
{
    public class Function
    {
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandlerAsync(object input, ILambdaContext context)
        {
            Console.WriteLine(input);
            Console.WriteLine(context);
            // TwitterMlbBot側のプログラムを呼び出して、ツイート実行
            // 年は指定しないためnullを引数で渡す
            await Program.Main(null);
        }
    }
}
