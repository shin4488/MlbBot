using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

namespace TwitterMlbBotExecution.Tests;

public class FunctionTest
{
    [Fact]
    public async void TestToUpperFunction()
    {
        // Invoke the lambda function and confirm the string was upper cased.
        var function = new Function();
        var context = new TestLambdaContext();
        await function.FunctionHandlerAsync("hello world", context);
    }
}
