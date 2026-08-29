output "topic_arn" {
  description = "通知用SNSトピックのARN"
  value       = aws_sns_topic.this.arn
}
