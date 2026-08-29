# デプロイとTerraform実行のためのIAMリソース。
# アカウントIDはハードコードせず、実行時にdata sourceから解決する
data "aws_caller_identity" "current" {}

# ---- GitHub Actions デプロイ用（OIDC・長期アクセスキーの廃止） ----

resource "aws_iam_openid_connect_provider" "github_actions" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]
  # GitHub OIDCの既知のサムプリント（公開情報。現在AWSは信頼CAで検証するため実質未使用だが、API上必須）
  thumbprint_list = [
    "6938fd4d98bab03faadb97b34396831e3780aea1",
    "1c58a3a8518e8759bf075b76b750d4f2df264fcd",
  ]
}

# masterブランチからのデプロイのみを許可し、権限は関数コード更新だけに絞る
resource "aws_iam_role" "github_actions_deploy" {
  name        = "mlbbot-github-actions-deploy"
  description = "GitHub Actions（masterブランチ）からのLambdaコードデプロイ専用ロール"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { Federated = aws_iam_openid_connect_provider.github_actions.arn }
        Action    = "sts:AssumeRoleWithWebIdentity"
        Condition = {
          StringEquals = {
            "token.actions.githubusercontent.com:aud" = "sts.amazonaws.com"
            "token.actions.githubusercontent.com:sub" = "repo:shin4488/MlbBot:ref:refs/heads/master"
          }
        }
      }
    ]
  })
}

resource "aws_iam_role_policy" "github_actions_deploy" {
  name = "deploy-function-code"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["lambda:UpdateFunctionCode"]
        Resource = module.twitter_mlb_bot.function_arn
      }
    ]
  })
}

# ---- Terraform実行用ロール（AssumeRole方式・長期キーを増やさない） ----

resource "aws_iam_role" "terraform_execution" {
  name        = "mlbbot-terraform-execution"
  description = "このリポジトリのTerraform実行（plan/apply）専用ロール"

  # 同一アカウント内のIAMアイデンティティからのAssumeRoleを許可する
  # （実際に誰が引き受けられるかは、各アイデンティティ側のsts:AssumeRole権限で制御する）
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

# Terraformが管理しているリソース群に必要な範囲の権限
resource "aws_iam_role_policy" "terraform_execution" {
  name = "manage-mlbbot-stack"
  role = aws_iam_role.terraform_execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "TfState"
        Effect   = "Allow"
        Action   = ["s3:ListBucket", "s3:GetObject", "s3:PutObject", "s3:DeleteObject"]
        Resource = ["arn:aws:s3:::${var.state_bucket_name}", "arn:aws:s3:::${var.state_bucket_name}/*"]
      },
      {
        Sid      = "LambdaConfig"
        Effect   = "Allow"
        Action   = ["lambda:Get*", "lambda:List*", "lambda:UpdateFunctionConfiguration", "lambda:AddPermission", "lambda:RemovePermission", "lambda:TagResource", "lambda:UntagResource"]
        Resource = module.twitter_mlb_bot.function_arn
      },
      {
        Sid      = "EventBridge"
        Effect   = "Allow"
        Action   = ["events:Describe*", "events:List*", "events:PutRule", "events:DeleteRule", "events:PutTargets", "events:RemoveTargets", "events:EnableRule", "events:DisableRule", "events:TagResource", "events:UntagResource"]
        Resource = module.twitter_mlb_bot.event_rule_arn
      },
      {
        Sid      = "Logs"
        Effect   = "Allow"
        Action   = ["logs:Describe*", "logs:List*", "logs:CreateLogGroup", "logs:DeleteLogGroup", "logs:PutRetentionPolicy", "logs:DeleteRetentionPolicy", "logs:TagResource", "logs:UntagResource", "logs:ListTagsForResource"]
        Resource = "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:${module.twitter_mlb_bot.log_group_name}*"
      },
      {
        Sid      = "Monitoring"
        Effect   = "Allow"
        Action   = ["sns:Get*", "sns:List*", "sns:CreateTopic", "sns:DeleteTopic", "sns:Subscribe", "sns:Unsubscribe", "sns:SetTopicAttributes", "sns:TagResource", "sns:UntagResource", "cloudwatch:Describe*", "cloudwatch:List*", "cloudwatch:PutMetricAlarm", "cloudwatch:DeleteAlarms", "cloudwatch:TagResource", "cloudwatch:UntagResource"]
        Resource = "*"
      },
      {
        Sid    = "IamManagedByTerraform"
        Effect = "Allow"
        Action = ["iam:Get*", "iam:List*", "iam:CreateRole", "iam:DeleteRole", "iam:UpdateRole", "iam:UpdateAssumeRolePolicy", "iam:PutRolePolicy", "iam:DeleteRolePolicy", "iam:AttachRolePolicy", "iam:DetachRolePolicy", "iam:TagRole", "iam:UntagRole", "iam:PassRole"]
        Resource = [
          aws_iam_role.terraform_execution.arn,
          aws_iam_role.github_actions_deploy.arn,
          module.twitter_mlb_bot.role_arn,
        ]
      },
      {
        Sid      = "OidcProvider"
        Effect   = "Allow"
        Action   = ["iam:GetOpenIDConnectProvider", "iam:CreateOpenIDConnectProvider", "iam:DeleteOpenIDConnectProvider", "iam:TagOpenIDConnectProvider", "iam:UntagOpenIDConnectProvider"]
        Resource = aws_iam_openid_connect_provider.github_actions.arn
      }
    ]
  })
}

# Terraform実行者（IAMユーザー）に上記ロールへのAssumeRoleを許可する
# （ユーザー名は環境固有情報のためterraform.tfvarsで渡す）
resource "aws_iam_user_policy" "terraform_user_assume" {
  name = "assume-mlbbot-terraform-execution"
  user = var.terraform_user_name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = "sts:AssumeRole"
        Resource = aws_iam_role.terraform_execution.arn
      }
    ]
  })
}
