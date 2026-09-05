# 同一アカウント内の指定IAMユーザーがAssumeRoleできるIAMロールを作成する汎用モジュール。
# 長期アクセスキーを増やさずに、作業内容ごとの権限分離とCloudTrailでの追跡を可能にする

data "aws_caller_identity" "current" {}

data "aws_iam_user" "trusted" {
  for_each  = toset(var.trusted_user_names)
  user_name = each.value
}

resource "aws_iam_role" "this" {
  name        = var.role_name
  description = var.role_description

  # アカウント全体への信頼だけでは、別のユーザー・ロールの広いsts:AssumeRole権限でも入れてしまう。
  # 引き受け元のARNも限定する。IAMパスを持つユーザーでも正しいARNになるよう実体を参照する。
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root" }
        Action    = "sts:AssumeRole"
        Condition = {
          ArnEquals = {
            "aws:PrincipalArn" = [for user in data.aws_iam_user.trusted : user.arn]
          }
        }
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
