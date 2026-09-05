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
  validation {
    condition     = length(var.trusted_user_names) > 0 && alltrue([for name in var.trusted_user_names : can(regex("^[A-Za-z0-9_+=,.@-]+$", name))])
    error_message = "引き受けを許可するIAMユーザー名を1件以上指定してください。ワイルドカードやARNは指定できません。"
  }
}

variable "policy_json" {
  description = "ロールに付与する権限（IAMポリシーJSON）"
  type        = string
}
