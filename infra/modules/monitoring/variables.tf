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
