# 定期実行Lambda一式（Lambda関数 + 実行ロール + ロググループ + EventBridgeスケジュール）を
# まとめて作成する共通モジュール。
# 関数コードのデプロイはTerraformの管理外（このリポジトリではGitHub Actionsが担う）で、
# Terraformはインフラ設定のみを管理する。

# ---- IAM ----

resource "aws_iam_role" "this" {
  name        = var.role_name
  description = var.role_description

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { Service = "lambda.amazonaws.com" }
        Action    = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "this" {
  for_each = toset(var.policy_arns)

  role       = aws_iam_role.this.name
  policy_arn = each.value
}

# AWSLambdaBasicExecutionRoleは全ロググループに書き込めるため、専用グループだけに許可する。
# グループ作成・保持期間の変更はTerraformが担当し、Lambdaには与えない。
resource "aws_iam_role_policy" "logs" {
  name = "write-function-logs"
  role = aws_iam_role.this.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["logs:CreateLogStream", "logs:PutLogEvents"]
      Resource = "${aws_cloudwatch_log_group.this.arn}:*"
    }]
  })
}

# ---- Lambda ----

resource "aws_lambda_function" "this" {
  # ロールの参照だけではログ権限の付与を待たないため、権限の設定後に関数を作成・更新する。
  depends_on    = [aws_iam_role_policy.logs]
  function_name = var.function_name
  role          = aws_iam_role.this.arn
  runtime       = var.runtime
  handler       = var.handler
  architectures = var.architectures
  memory_size   = var.memory_size
  timeout       = var.timeout

  # aws_lambda_functionはコード指定（filename/s3_bucket/image_uri）が構文上必須のため、
  # 存在し得ないS3参照をダミーとして与える。既存関数の更新ではignore_changesにより無視され、
  # 万一関数を再作成しようとした場合も必ず失敗して止まる（安全側に倒れる）。
  # バケット名はS3の上限63文字を超えているため、第三者がこの名前のバケットを作ることも不可能
  s3_bucket = "terraform-placeholder-never-used-this-name-exceeds-the-s3-63-character-limit-so-it-cannot-exist"
  s3_key    = "terraform-placeholder-never-used.zip"

  lifecycle {
    ignore_changes = [
      # 上のダミーS3参照は実態と常に不一致のため、差分と誤ったコード更新を抑止する
      s3_bucket,
      s3_key,
      # APIキー等の環境変数は値を.tfに書かないため管理しない（Lambda側で直接管理。
      # ignoreを外すと「全環境変数を削除する」差分が出るので注意）
      environment,
    ]
  }
}

# ---- 非同期実行 ----

# 投稿が成功しても通信障害で応答を受け取れない場合に備え、重複投稿を避ける。
# そのため、関数エラーによる全体の自動再実行は既定で行わない。
# Lambdaの重複配信全般を防ぐものではなく、実行ごとの冪等性はアプリ側で別途考慮する。
resource "aws_lambda_function_event_invoke_config" "this" {
  function_name          = aws_lambda_function.this.function_name
  maximum_retry_attempts = var.maximum_retry_attempts
}

# ---- CloudWatch Logs ----

resource "aws_cloudwatch_log_group" "this" {
  name              = "/aws/lambda/${var.function_name}"
  retention_in_days = var.log_retention_days
}

# ---- EventBridge（定期実行） ----

resource "aws_cloudwatch_event_rule" "this" {
  name                = var.rule_name
  description         = var.rule_description
  schedule_expression = var.schedule_expression
  state               = var.rule_state
}

resource "aws_cloudwatch_event_target" "this" {
  rule      = aws_cloudwatch_event_rule.this.name
  target_id = var.event_target_id
  arn       = aws_lambda_function.this.arn
}

resource "aws_lambda_permission" "this" {
  statement_id  = var.permission_statement_id
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.this.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.this.arn
}
