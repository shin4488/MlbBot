output "role_arn" {
  description = "作成したロールのARN"
  value       = aws_iam_role.this.arn
}

output "policy_id" {
  description = "管理権限の付与完了に依存するリソース向けのポリシーID"
  value       = aws_iam_role_policy.this.id
}
