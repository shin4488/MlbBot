output "role_arn" {
  description = "作成したロールのARN"
  value       = aws_iam_role.this.arn
}
