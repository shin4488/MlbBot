# モックのplanのみ。実在しないテスト用ARNを使い、AWSの認証情報は不要。
mock_provider "aws" {
  override_during = plan
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
  rule_name           = "test-rule"
  schedule_expression = "rate(1 day)"
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
