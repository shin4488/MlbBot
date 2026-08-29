variable "role_name" {
  description = "作成するロール名"
  type        = string
}

variable "role_description" {
  description = "ロールの説明"
  type        = string
  default     = ""
}

variable "github_repository" {
  description = "AssumeRoleを許可するGitHubリポジトリ（owner/repo形式）"
  type        = string
}

variable "github_branch" {
  description = "AssumeRoleを許可するブランチ"
  type        = string
  default     = "master"
}

variable "policy_json" {
  description = "ロールに付与する権限（IAMポリシーJSON）"
  type        = string
}
