# Lambda関数の実行エラーをメール通知する監視一式（SNSトピック + メール購読 + アラーム）

resource "aws_sns_topic" "this" {
  name = var.topic_name
}

# apply後にAWSから届く確認メールの「Confirm subscription」を踏むまで通知は有効にならない
resource "aws_sns_topic_subscription" "email" {
  topic_arn = aws_sns_topic.this.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

resource "aws_cloudwatch_metric_alarm" "lambda_errors" {
  alarm_name        = var.alarm_name
  alarm_description = var.alarm_description

  namespace   = "AWS/Lambda"
  metric_name = "Errors"
  dimensions = {
    FunctionName = var.function_name
  }

  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 1
  comparison_operator = "GreaterThanOrEqualToThreshold"
  # 定期実行の合間はデータが無いのが常態のため、欠落はアラーム対象にしない
  treat_missing_data = "notBreaching"

  alarm_actions = [aws_sns_topic.this.arn]
}

# 処理を続行しつつエラーログだけを出す障害は、Lambdaのエラーメトリクスに現れないため
# ログからメトリクスを起こして通知する
resource "aws_cloudwatch_log_metric_filter" "error_log" {
  count = var.error_log_alarm == null ? 0 : 1

  name           = var.error_log_alarm.metric_name
  log_group_name = var.error_log_alarm.log_group_name
  pattern        = var.error_log_alarm.filter_pattern

  metric_transformation {
    name      = var.error_log_alarm.metric_name
    namespace = var.error_log_alarm.metric_namespace
    value     = "1"
  }
}

resource "aws_cloudwatch_metric_alarm" "error_log" {
  count = var.error_log_alarm == null ? 0 : 1

  alarm_name        = var.error_log_alarm.alarm_name
  alarm_description = var.error_log_alarm.alarm_description

  namespace   = var.error_log_alarm.metric_namespace
  metric_name = var.error_log_alarm.metric_name

  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 1
  comparison_operator = "GreaterThanOrEqualToThreshold"
  # 定期実行の合間はデータが無いのが常態のため、欠落はアラーム対象にしない
  treat_missing_data = "notBreaching"

  alarm_actions = [aws_sns_topic.this.arn]
}
