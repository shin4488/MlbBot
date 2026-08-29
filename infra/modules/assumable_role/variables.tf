variable "role_name" {
  description = "作成するロール名"
  type        = string
}

variable "role_description" {
  description = "ロールの説明"
  type        = string
  default     = ""
}

variable "trusted_user_names" {
  description = "このロールへのAssumeRoleを許可するIAMユーザー名のリスト"
  type        = list(string)
}

variable "policy_json" {
  description = "ロールに付与する権限（IAMポリシーJSON）"
  type        = string
}
