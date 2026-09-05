# AWSへ接続せずplanだけを実行する。ARNのアカウント部分はテスト専用のダミー。
mock_provider "aws" {
  override_during = plan
  mock_data "aws_caller_identity" {
    defaults = { account_id = "000000000000" }
  }
  mock_data "aws_iam_user" {
    defaults = { arn = "arn:aws:iam::000000000000:user/operators/test-user" }
  }
}

variables {
  role_name          = "test-role"
  trusted_user_names = ["test-user"]
  policy_json        = "{\"Version\":\"2012-10-17\",\"Statement\":[]}"
}

run "trust_only_named_user" {
  command = plan
  assert {
    condition     = jsondecode(aws_iam_role.this.assume_role_policy).Statement[0].Condition.ArnEquals["aws:PrincipalArn"] == ["arn:aws:iam::000000000000:user/operators/test-user"]
    error_message = "引き受け元は指定ユーザーの実際のARNに限定する必要があります。"
  }
}

run "reject_empty_trust" {
  command = plan
  variables { trusted_user_names = [] }
  expect_failures = [var.trusted_user_names]
}

run "reject_wildcard_trust" {
  command = plan
  variables { trusted_user_names = ["*"] }
  expect_failures = [var.trusted_user_names]
}
