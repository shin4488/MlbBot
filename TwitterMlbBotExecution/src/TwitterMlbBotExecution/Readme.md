# how to deploy to AWS lambda
https://stackoverflow.com/questions/73049998/net-binaries-missing-aws-lambda
https://edit-anything.com/blog/401-unauthorized.html
https://sk13g.com/twitter-api-suspended/
https://php-mania.com/memo/68
> プロジェクトを作成すると、Twitter APIのv2（バージョン2）が利用出来るようになります。
> ちなみにスタンドアロンアプリは、バージョン1のみで、v2（バージョン2）は利用できないみたいです。
https://research-labo.com/howto-twitterapi-getkeys/
Twitter API v2仕様
https://developer.twitter.com/en/docs/twitter-api/tweets/manage-tweets/api-reference/post-tweets
Twitter API v2をOAuth1.0で認証するときの認証ヘッダーの仕様
https://developer.twitter.com/en/docs/authentication/oauth-1-0a
Twitter API v2をOAuth1.0で認証するときの認証ヘッダーの値作成の実装方法
https://liamhunt.blog/posts/c-twitter-api-post-tweet-oauth-1-0a/
ConfigureAwait(false)の代わりにawait Task.Run(async () => {})を使う
https://dev.to/noseratio/why-i-no-longer-use-configureawait-false-3pne
lambda functionをlocalで実行する方法として、PackageReferenceを使用する場合は、参照される方のプロジェクトは`<PropertyGroup>`で`<OutputType>Library</OutputType>`指定としてクラスライブラリとする（Exe指定だと動かない）
https://github.com/aws/aws-lambda-dotnet/issues/1235#issuecomment-1181045785

# AWS Lambda Empty Function Project

This starter project consists of:
* Function.cs - class file containing a class with a single function handler method
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

The generated function handler is a simple method accepting a string argument that returns the uppercase equivalent of the input string. Replace the body of this method, and parameters, to suit your needs.

## Here are some steps to follow from Visual Studio:

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed function, for example to have your function invoked when an object is created in an Amazon S3 bucket, use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can deploy your application using the [Amazon.Lambda.Tools Global Tool](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) from the command line.

Install Amazon.Lambda.Tools Global Tools if not already installed.
```
    dotnet tool install -g Amazon.Lambda.Tools
```

If already installed check if new version is available.
```
    dotnet tool update -g Amazon.Lambda.Tools
```

Execute unit tests
```
    cd "TwitterMlbBotExecution/test/TwitterMlbBotExecution.Tests"
    dotnet test
```

Deploy function to AWS Lambda
```
    cd "TwitterMlbBotExecution/src/TwitterMlbBotExecution"
    dotnet lambda deploy-function
```
