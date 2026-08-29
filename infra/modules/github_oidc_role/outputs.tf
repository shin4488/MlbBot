output "role_arn" {
  description = "作成したロールのARN"
  value       = aws_iam_role.this.arn
}

output "oidc_provider_arn" {
  description = "GitHub OIDCプロバイダーのARN"
  value       = aws_iam_openid_connect_provider.github.arn
}
