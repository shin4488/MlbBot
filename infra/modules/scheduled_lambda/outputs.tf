output "function_arn" {
  description = "Lambda関数のARN"
  value       = aws_lambda_function.this.arn
}

output "function_name" {
  description = "Lambda関数名"
  value       = aws_lambda_function.this.function_name
}

output "role_arn" {
  description = "Lambda実行ロールのARN"
  value       = aws_iam_role.this.arn
}

output "log_group_name" {
  description = "CloudWatch Logsロググループ名"
  value       = aws_cloudwatch_log_group.this.name
}

output "schedules" {
  description = "作成するスケジュールの実行設定"
  value = {
    for name, schedule in aws_scheduler_schedule.this : name => {
      schedule_expression = schedule.schedule_expression
      time_zone           = schedule.schedule_expression_timezone
      input               = schedule.target[0].input
      state               = schedule.state
    }
  }
}
