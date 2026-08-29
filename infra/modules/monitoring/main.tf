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
