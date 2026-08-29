module "twitter_mlb_bot" {
  source = "../../modules/scheduled_lambda"

  function_name = "TwitterMlbBot"
  runtime       = "dotnet10"
  handler       = "TwitterMlbBotExecution::TwitterMlbBotExecution.Function::FunctionHandlerAsync"
  architectures = ["x86_64"]
  memory_size   = 512
  timeout       = 15

  role_name        = "SuLambdaRole"
  role_description = "Allows Lambda functions to call AWS services on your behalf."
  # 最小権限: このボットが必要とするAWS権限はCloudWatch Logsへのログ書き込みのみ
  policy_arns = ["arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"]

  # 毎日06:00 UTC（15:00 JST）に実行
  schedule_expression = "cron(0 6 * * ? *)"
  rule_name           = "CronTweetMlbStandings"
  rule_description    = "1日1回指定の時間に、TwitterでMLBの順位を地区ごとにツイートする"

  # 既存リソースをインポートしたため、作成時に採番されていたIDをそのまま指定している
  event_target_id         = "aaf74d02-ecf9-4792-b28d-a81f5b4e59b8"
  permission_statement_id = "lambda-1e02f0e0-9ffc-46fd-9545-f8d41f212d28"

  # ログの無限成長を防ぐ（運用調査には90日あれば十分）
  log_retention_days = 90
}
