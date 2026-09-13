variable "function_name" {
  description = "Lambda関数名"
  type        = string
}

variable "runtime" {
  description = "Lambdaランタイム（例: dotnet10）"
  type        = string
}

variable "handler" {
  description = "Lambdaハンドラ"
  type        = string
}

variable "architectures" {
  description = "CPUアーキテクチャ"
  type        = list(string)
  default     = ["x86_64"]
}

variable "memory_size" {
  description = "メモリサイズ（MB）"
  type        = number
}

variable "timeout" {
  description = "タイムアウト（秒）"
  type        = number
}

variable "maximum_retry_attempts" {
  description = "関数エラー時の非同期再試行回数。副作用のある定期処理では再実行を避ける"
  type        = number
  default     = 0
  validation {
    condition     = contains([0, 1, 2], var.maximum_retry_attempts)
    error_message = "非同期再試行回数は0・1・2のいずれかを指定してください。"
  }
}

variable "role_name" {
  description = "Lambda実行ロール名"
  type        = string
}

variable "role_description" {
  description = "Lambda実行ロールの説明"
  type        = string
  default     = ""
}

variable "policy_arns" {
  description = "実行ロールにアタッチする管理ポリシーARNのリスト"
  type        = list(string)
  default     = []
}

variable "schedule_group_name" {
  description = "専用Schedulerグループ名"
  type        = string
}

variable "scheduler_role_name" {
  description = "Schedulerが対象Lambdaだけを起動するロール名"
  type        = string
}

variable "schedules_enabled" {
  description = "定期実行を有効にするか。意図しない起動を避けるため既定は無効"
  type        = bool
  default     = false
}

variable "schedules" {
  description = "スケジュール名ごとの実行式・タイムゾーン・Lambdaへ渡すJSON"
  type = map(object({
    schedule_expression = string
    time_zone           = optional(string, "UTC")
    input               = optional(string)
  }))
  validation {
    condition = alltrue([
      for schedule in var.schedules : schedule.input == null ? true : can(jsondecode(schedule.input))
    ])
    error_message = "inputを指定する場合は有効なJSON文字列を渡してください。"
  }
}

variable "initial_code" {
  description = "新規Lambdaの初回コードを置くS3バケットとキー。省略時は既存関数の管理専用とし、再作成を失敗させる"
  type = object({
    s3_bucket = string
    s3_key    = string
  })
  default = null
}

variable "log_retention_days" {
  description = "CloudWatch Logsの保持日数（nullで無期限）"
  type        = number
  default     = null
}

variable "scheduler_management_dependencies" {
  description = "Scheduler作成前に反映が必要な管理用IAMポリシーのID"
  type        = list(string)
  default     = []
}
