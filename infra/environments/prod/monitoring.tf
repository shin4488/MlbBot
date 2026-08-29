# ツイート全滅の検知: 全件失敗時はLambdaがエラー終了する仕様のため、
# Errorsメトリクスのアラームでメール通知できる

resource "aws_sns_topic" "bot_alerts" {
  name = "mlbbot-alerts"
}

# 通知先メールアドレスは環境固有情報のためterraform.tfvars（gitignore対象）で渡す。
# apply後にAWSから届く確認メールの「Confirm subscription」を踏むまで通知は有効にならない
resource "aws_sns_topic_subscription" "bot_alerts_email" {
  topic_arn = aws_sns_topic.bot_alerts.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

resource "aws_cloudwatch_metric_alarm" "lambda_errors" {
  alarm_name        = "mlbbot-tweet-failed"
  alarm_description = "TwitterMlbBotの実行がエラー終了した（ツイート全滅など）。CloudWatch Logsを確認すること"

  namespace   = "AWS/Lambda"
  metric_name = "Errors"
  dimensions = {
    FunctionName = module.twitter_mlb_bot.function_name
  }

  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 1
  comparison_operator = "GreaterThanOrEqualToThreshold"
  # 1日1回実行のためデータ欠落が常態。欠落はアラーム対象にしない
  treat_missing_data = "notBreaching"

  alarm_actions = [aws_sns_topic.bot_alerts.arn]
}
