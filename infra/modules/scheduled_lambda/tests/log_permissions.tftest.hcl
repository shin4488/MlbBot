# モックのplanのみ。実在しないテスト用ARNを使い、AWSの認証情報は不要。
mock_provider "aws" {
  override_during = plan
  mock_resource "aws_lambda_function" {
    defaults = { arn = "arn:aws:lambda:us-east-1:000000000000:function:test-function" }
  }
  mock_resource "aws_scheduler_schedule_group" {
    defaults = { arn = "arn:aws:scheduler:us-east-1:000000000000:schedule-group/test-schedules" }
  }
  mock_data "aws_caller_identity" {
    defaults = { account_id = "000000000000" }
  }
  mock_resource "aws_cloudwatch_log_group" {
    defaults = { arn = "arn:aws:logs:us-east-1:000000000000:log-group:/aws/lambda/test-function" }
  }
  mock_resource "aws_iam_role" {
    defaults = { arn = "arn:aws:iam::000000000000:role/test-role" }
  }
}

variables {
  function_name       = "test-function"
  runtime             = "dotnet10"
  handler             = "Example::Example.Function::Handler"
  memory_size         = 512
  timeout             = 60
  role_name           = "test-role"
  schedule_group_name = "test-schedules"
  scheduler_role_name = "test-scheduler-role"
  schedules = {
    cleanup = { schedule_expression = "rate(2 hours)", input = jsonencode({ task = "cleanup", limit = 25 }) }
  }
}

run "write_only_own_logs" {
  command = plan
  assert {
    condition     = jsondecode(aws_iam_role_policy.logs.policy).Statement[0].Resource == "arn:aws:logs:us-east-1:000000000000:log-group:/aws/lambda/test-function:*"
    error_message = "ログ書き込みは自分の関数のロググループに限定する必要があります。"
  }
  assert {
    condition     = toset(jsondecode(aws_iam_role_policy.logs.policy).Statement[0].Action) == toset(["logs:CreateLogStream", "logs:PutLogEvents"])
    error_message = "実行ロールにロググループの作成・削除などの管理権限を与えないでください。"
  }
  assert {
    condition     = length(aws_iam_role_policy_attachment.this) == 0
    error_message = "追加指定がない場合、広域の管理ポリシーを付けないでください。"
  }
}

run "do_not_retry_side_effects_by_default" {
  command = plan
  assert {
    condition     = aws_lambda_function_event_invoke_config.this.maximum_retry_attempts == 0
    error_message = "関数エラー後に投稿済みか不明な処理を自動再実行しないでください。"
  }
}

run "single_schedule_with_arbitrary_payload" {
  command = plan
  assert {
    condition = (length(aws_scheduler_schedule.this) == 1 &&
      aws_scheduler_schedule.this["cleanup"].schedule_expression == "rate(2 hours)" &&
      aws_scheduler_schedule.this["cleanup"].schedule_expression_timezone == "UTC" &&
      aws_scheduler_schedule.this["cleanup"].state == "DISABLED" &&
      jsondecode(aws_scheduler_schedule.this["cleanup"].target[0].input) == { task = "cleanup", limit = 25 } &&
      aws_scheduler_schedule.this["cleanup"].flexible_time_window[0].mode == "OFF" &&
    aws_scheduler_schedule.this["cleanup"].target[0].retry_policy[0].maximum_retry_attempts == 0)
    error_message = "用途によらず実行式とJSONをそのまま渡し、既定ではUTC・無効・再試行なしにしてください。"
  }
  assert {
    condition     = jsondecode(aws_iam_role_policy.scheduler.policy).Statement[0].Action == "lambda:InvokeFunction" && jsondecode(aws_iam_role_policy.scheduler.policy).Statement[0].Resource == aws_lambda_function.this.arn
    error_message = "Schedulerには対象Lambdaの起動だけを許可してください。"
  }
  assert {
    condition     = jsondecode(aws_iam_role.scheduler.assume_role_policy).Statement[0].Principal.Service == "scheduler.amazonaws.com" && jsondecode(aws_iam_role.scheduler.assume_role_policy).Statement[0].Condition.ArnEquals["aws:SourceArn"] == aws_scheduler_schedule_group.this.arn && jsondecode(aws_iam_role.scheduler.assume_role_policy).Statement[0].Condition.StringEquals["aws:SourceAccount"] == data.aws_caller_identity.current.account_id
    error_message = "Schedulerの信頼先を同一アカウントの専用グループに限定してください。"
  }
}

run "enable_after_deployment" {
  command = plan
  variables { schedules_enabled = true }
  assert {
    condition     = alltrue([for schedule in aws_scheduler_schedule.this : schedule.state == "ENABLED"])
    error_message = "有効化は既存スケジュールの状態変更で行ってください。"
  }
}

run "multiple_schedules_with_different_times" {
  command = plan
  variables {
    schedules = {
      report = { schedule_expression = "cron(30 9 ? * MON *)", time_zone = "Asia/Tokyo" }
      backup = { schedule_expression = "cron(15 23 * * ? *)", time_zone = "Europe/London", input = "{\"mode\":\"backup\"}" }
    }
  }
  assert {
    condition = (length(aws_scheduler_schedule.this) == 2 &&
      aws_scheduler_schedule.this["report"].schedule_expression == "cron(30 9 ? * MON *)" &&
      aws_scheduler_schedule.this["report"].schedule_expression_timezone == "Asia/Tokyo" &&
      aws_scheduler_schedule.this["report"].target[0].input == null &&
      aws_scheduler_schedule.this["backup"].schedule_expression == "cron(15 23 * * ? *)" &&
    aws_scheduler_schedule.this["backup"].schedule_expression_timezone == "Europe/London")
    error_message = "件数や地区に制約を設けず、それぞれの時刻・タイムゾーンを使用してください。"
  }
}

run "reject_invalid_json" {
  command = plan
  variables {
    schedules = { invalid = { schedule_expression = "rate(1 day)", input = "not-json" } }
  }
  expect_failures = [var.schedules]
}

run "explicit_initial_code_for_new_function" {
  command = plan
  variables {
    initial_code = { s3_bucket = "example-test-artifacts", s3_key = "initial.zip" }
  }
  assert {
    condition     = aws_lambda_function.this.s3_bucket == "example-test-artifacts" && aws_lambda_function.this.s3_key == "initial.zip"
    error_message = "新規作成時には明示されたコードを使用してください。"
  }
}

run "prevent_accidental_recreation_without_code" {
  command = plan
  assert {
    condition     = length(aws_lambda_function.this.s3_bucket) > 63
    error_message = "コード未指定では存在し得ないS3参照を使用し、既存関数の誤再作成を防いでください。"
  }
}
