# 値はすべてgitignore対象のterraform.tfvarsで渡す（環境固有・機密情報はコミットしない。
# 雛形はterraform.tfvars.example参照）

variable "aws_region" {
  description = "AWSリージョン"
  type        = string
}

variable "alert_email" {
  description = "アラーム通知先メールアドレス"
  type        = string
}

variable "state_bucket_name" {
  description = "tfstateを保存しているS3バケット名（Terraform実行用ロールの権限付与に使用）"
  type        = string
}

variable "terraform_user_name" {
  description = "Terraform実行用ロールへのAssumeRoleを許可するIAMユーザー名"
  type        = string
}
