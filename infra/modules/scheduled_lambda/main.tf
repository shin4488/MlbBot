# 定期実行Lambda一式（Lambda関数 + 実行ロール + ロググループ + EventBridgeスケジュール）を
# まとめて作成する共通モジュール。
# 初回作成後の関数コードのデプロイはTerraformの管理外（このリポジトリではGitHub Actionsが担う）で、
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
  # 初回コードが未指定なら存在し得ないS3参照をダミーとして与える。既存関数の更新では無視され、
  # 万一関数を再作成しようとした場合も必ず失敗して止まる（安全側に倒れる）。
  # バケット名はS3の上限63文字を超えているため、第三者がこの名前のバケットを作ることも不可能
  s3_bucket = var.initial_code != null ? var.initial_code.s3_bucket : "terraform-placeholder-never-used-this-name-exceeds-the-s3-63-character-limit-so-it-cannot-exist"
  s3_key    = var.initial_code != null ? var.initial_code.s3_key : "terraform-placeholder-never-used.zip"

  lifecycle {
    ignore_changes = [
      # 配布済みコードを初回コードやダミー参照へ巻き戻さない
      s3_bucket,
      s3_key,
      # APIキー等の環境変数は値を.tfに書かないため管理しない（Lambda側で直接管理。
      # ignoreを外すと「全環境変数を削除する」差分が出るので注意）
      environment,
    ]
  }
}

# ---- 非同期実行 ----

# 副作用が成功しても応答だけ受け取れない場合に備え、重複実行を避ける。
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

# ---- EventBridge Scheduler（定期実行） ----

data "aws_caller_identity" "current" {}

resource "aws_scheduler_schedule_group" "this" {
  # 初回はTerraform自身の管理権限を追加してから新リソースの作成を始める。
  depends_on = [var.scheduler_management_dependencies]
  name       = var.schedule_group_name
}

resource "aws_iam_role" "scheduler" {
  name = var.scheduler_role_name
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "scheduler.amazonaws.com" }
      Action    = "sts:AssumeRole"
      # Schedulerは個別スケジュールではなく、スケジュールグループのARNを渡す。
      Condition = {
        StringEquals = { "aws:SourceAccount" = data.aws_caller_identity.current.account_id }
        ArnEquals    = { "aws:SourceArn" = aws_scheduler_schedule_group.this.arn }
      }
    }]
  })
}

resource "aws_iam_role_policy" "scheduler" {
  name = "invoke-scheduled-function"
  role = aws_iam_role.scheduler.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = "lambda:InvokeFunction"
      Resource = aws_lambda_function.this.arn
    }]
  })
}

resource "aws_scheduler_schedule" "this" {
  for_each   = var.schedules
  depends_on = [aws_iam_role_policy.scheduler]

  name                         = each.key
  group_name                   = aws_scheduler_schedule_group.this.name
  schedule_expression          = each.value.schedule_expression
  schedule_expression_timezone = each.value.time_zone
  state                        = var.schedules_enabled ? "ENABLED" : "DISABLED"

  flexible_time_window {
    mode = "OFF"
  }

  target {
    arn      = aws_lambda_function.this.arn
    role_arn = aws_iam_role.scheduler.arn
    input    = each.value.input
    retry_policy {
      maximum_retry_attempts = 0
    }
  }
}
