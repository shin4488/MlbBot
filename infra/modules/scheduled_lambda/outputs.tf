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

output "event_rule_arn" {
  description = "定期実行ルールのARN"
  value       = aws_cloudwatch_event_rule.this.arn
}

output "log_group_name" {
  description = "CloudWatch Logsロググループ名"
  value       = aws_cloudwatch_log_group.this.name
}
