locals {
  # iam.tf（Terraform実行ロールの権限スコープ）からも参照するため名前をlocalsに持つ
  alert_topic_name = "mlbbot-alerts"
  alert_alarm_name = "mlbbot-tweet-failed"
}

# 全件失敗時はLambdaがエラー終了する仕様のため、Errorsメトリクスでツイート全滅を検知できる
module "monitoring" {
  source = "../../modules/monitoring"

  topic_name        = local.alert_topic_name
  alarm_name        = local.alert_alarm_name
  alarm_description = "TwitterMlbBotの実行がエラー終了した（ツイート全滅など）。CloudWatch Logsを確認すること"
  function_name     = module.twitter_mlb_bot.function_name
  # 通知先メールアドレスは環境固有情報のためterraform.tfvars（gitignore対象）で渡す
  alert_email = var.alert_email
}
