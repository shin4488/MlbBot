variable "topic_name" {
  description = "通知用SNSトピック名"
  type        = string
}

variable "alert_email" {
  description = "通知先メールアドレス"
  type        = string
}

variable "alarm_name" {
  description = "アラーム名"
  type        = string
}

variable "alarm_description" {
  description = "アラームの説明（通知メールに表示される）"
  type        = string
  default     = ""
}

variable "function_name" {
  description = "監視対象のLambda関数名"
  type        = string
}

variable "error_log_alarm" {
  description = "エラーログ監視（実行としては正常終了するがエラーログが出る障害の検知）。nullなら作成しない"
  type = object({
    log_group_name    = string
    filter_pattern    = string
    metric_namespace  = string
    metric_name       = string
    alarm_name        = string
    alarm_description = optional(string, "")
  })
  default = null
}
