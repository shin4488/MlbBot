# MLB固有の投稿仕様は呼び出し側で確認する。全providerをモック化し、AWSへ接続しない。
mock_provider "aws" {
  override_during = plan
  mock_resource "aws_lambda_function" {
    defaults = { arn = "arn:aws:lambda:us-east-1:000000000000:function:test-function" }
  }
  mock_resource "aws_iam_role" {
    defaults = { arn = "arn:aws:iam::000000000000:role/test-role" }
  }
  mock_resource "aws_sns_topic" {
    defaults = { arn = "arn:aws:sns:us-east-1:000000000000:test-topic" }
  }
  mock_resource "aws_scheduler_schedule_group" {
    defaults = { arn = "arn:aws:scheduler:us-east-1:000000000000:schedule-group/test-schedules" }
  }
  mock_resource "aws_cloudwatch_log_group" {
    defaults = { arn = "arn:aws:logs:us-east-1:000000000000:log-group:/aws/lambda/test-function" }
  }
  mock_resource "aws_iam_openid_connect_provider" {
    defaults = { arn = "arn:aws:iam::000000000000:oidc-provider/token.actions.githubusercontent.com" }
  }
  mock_data "aws_caller_identity" {
    defaults = { account_id = "000000000000" }
  }
  mock_data "aws_iam_user" {
    defaults = { arn = "arn:aws:iam::000000000000:user/test-user" }
  }
}

variables {
  aws_region          = "us-east-1"
  alert_email         = "test@example.com"
  state_bucket_name   = "example-test-state"
  terraform_user_name = "test-user"
}

run "three_enabled_morning_groups" {
  command = plan
  assert {
    condition = toset(keys(module.twitter_mlb_bot.schedules)) == toset(["East", "Central", "West"]) && alltrue([
      for name, schedule in module.twitter_mlb_bot.schedules :
      schedule.state == "ENABLED" && schedule.schedule_expression == "cron(0 8 * * ? *)" &&
      schedule.time_zone == lookup({ East = "America/New_York", Central = "America/Chicago", West = "America/Los_Angeles" }, name, "") &&
      jsondecode(schedule.input) == { group = name }
    ])
    error_message = "東・中・西の現地朝8時に対象グループを渡す3件が有効である必要があります。"
  }
}
