module "twitter_mlb_bot" {
  source = "../../modules/scheduled_lambda"

  function_name = "TwitterMlbBot"
  runtime       = "dotnet10"
  handler       = "TwitterMlbBotExecution::TwitterMlbBotExecution.Function::FunctionHandlerAsync"
  memory_size   = 512
  # API待ち時間と投稿間隔を含む実行時間を確保する。関数エラー時には自動再投稿しない。
  timeout                = 60
  maximum_retry_attempts = 0

  role_name        = "SuLambdaRole"
  role_description = "Allows Lambda functions to call AWS services on your behalf."
  # ログ書き込みはモジュール内で専用グループに限定。広域のAWS管理ポリシーは付けない。

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
