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

variable "schedule_expression" {
  description = "定期実行スケジュール（例: cron(0 6 * * ? *)）"
  type        = string
}

variable "rule_name" {
  description = "EventBridgeルール名"
  type        = string
}

variable "rule_description" {
  description = "EventBridgeルールの説明"
  type        = string
  default     = ""
}

variable "rule_state" {
  description = "EventBridgeルールの状態（ENABLED / DISABLED）"
  type        = string
  default     = "ENABLED"
}

variable "event_target_id" {
  description = "EventBridgeターゲットID（既存リソースをインポートする場合に指定。省略時は自動生成）"
  type        = string
  default     = null
}

variable "permission_statement_id" {
  description = "Lambda起動許可のステートメントID（既存リソースをインポートする場合に指定。省略時は自動生成）"
  type        = string
  default     = null
}

variable "log_retention_days" {
  description = "CloudWatch Logsの保持日数（nullで無期限）"
  type        = number
  default     = null
}
