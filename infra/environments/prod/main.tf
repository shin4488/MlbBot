locals {
  scheduler_role_name = "mlbbot-scheduler-execution"
}

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

  schedule_group_name = "mlbbot-standings"
  scheduler_role_name = local.scheduler_role_name
  scheduler_management_dependencies = [
    module.terraform_role.policy_id,
    aws_iam_user_policy.terraform_iam_bootstrap.id,
  ]
  # 投稿履歴による重複防止はないため、有効化前に最初の投稿対象日が投稿済みでないことを確認する。
  # 3グループが同じ対象日から始まるよう、東部の実行時刻より十分前に有効化する。
  schedules_enabled = true
  schedules = {
    East    = { schedule_expression = "cron(0 8 * * ? *)", time_zone = "America/New_York", input = jsonencode({ group = "East" }) }
    Central = { schedule_expression = "cron(0 8 * * ? *)", time_zone = "America/Chicago", input = jsonencode({ group = "Central" }) }
    West    = { schedule_expression = "cron(0 8 * * ? *)", time_zone = "America/Los_Angeles", input = jsonencode({ group = "West" }) }
  }

  # ログの無限成長を防ぐ（運用調査には90日あれば十分）
  log_retention_days = 90
}
