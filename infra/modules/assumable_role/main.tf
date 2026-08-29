# 同一アカウント内の指定IAMユーザーがAssumeRoleできるIAMロールを作成する汎用モジュール。
# 長期アクセスキーを増やさずに、作業内容ごとの権限分離とCloudTrailでの追跡を可能にする

data "aws_caller_identity" "current" {}

resource "aws_iam_role" "this" {
  name        = var.role_name
  description = var.role_description

  # 信頼ポリシーは同一アカウントに開き、実際に誰が引き受けられるかは
  # 各ユーザー側のsts:AssumeRole権限（下のaws_iam_user_policy）で制御する
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root" }
        Action    = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_role_policy" "this" {
  name   = "main"
  role   = aws_iam_role.this.id
  policy = var.policy_json
}

resource "aws_iam_user_policy" "assume" {
  for_each = toset(var.trusted_user_names)

  name = "assume-${var.role_name}"
  user = each.value

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = "sts:AssumeRole"
        Resource = aws_iam_role.this.arn
      }
    ]
  })
}
