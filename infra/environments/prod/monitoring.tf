locals {
  # iam.tf（Terraform実行ロールの権限スコープ）からも参照するため名前をlocalsに持つ
  alert_topic_name     = "mlbbot-alerts"
  alert_alarm_name     = "mlbbot-tweet-failed"
  error_log_alarm_name = "mlbbot-error-logged"
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

  # 投稿を続行しつつエラーログを出すケース（シーズン日程の取得失敗など）は実行が正常終了し
  # Errorsメトリクスに現れないため、エラーレベルのログ（SimpleConsoleの "fail:" 行）でも通知する
  error_log_alarm = {
    log_group_name    = module.twitter_mlb_bot.log_group_name
    filter_pattern    = "\"fail:\""
    metric_namespace  = "TwitterMlbBot"
    metric_name       = "ErrorLogCount"
    alarm_name        = local.error_log_alarm_name
    alarm_description = "TwitterMlbBotがエラーログを出力した（シーズン日程の取得に失敗し投稿を続行した場合など）。CloudWatch Logsを確認すること"
  }

  # メトリクスフィルタ等の作成権限（terraform_roleのポリシー更新）が先に適用されるようにする
  depends_on = [module.terraform_role]
}
