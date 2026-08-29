data "aws_caller_identity" "current" {}

locals {
  terraform_role_name = "mlbbot-terraform-execution"
  # ロール自身に付与するポリシーが自分のARNを参照すると循環になるため、名前からARNを組み立てる
  terraform_role_arn = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/${local.terraform_role_name}"
}

module "deploy_role" {
  source = "../../modules/github_oidc_role"

  role_name         = "mlbbot-github-actions-deploy"
  role_description  = "GitHub ActionsからのLambdaコードデプロイ専用ロール"
  github_repository = "shin4488/MlbBot"
  github_branch     = "master"

  # デプロイに必要なのは関数コードの更新のみ
  policy_json = jsonencode({
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

module "terraform_role" {
  source = "../../modules/assumable_role"

  role_name          = local.terraform_role_name
  role_description   = "Terraform実行（plan/apply）専用ロール"
  trusted_user_names = [var.terraform_user_name]

  # Terraformが管理しているリソース群に必要な範囲へ絞った権限
  policy_json = jsonencode({
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
        # 一覧・参照系のアクションはIAM仕様上リソースを絞れないため"*"とする
        Sid      = "MonitoringRead"
        Effect   = "Allow"
        Action   = ["sns:GetTopicAttributes", "sns:GetSubscriptionAttributes", "sns:ListTagsForResource", "sns:ListTopics", "sns:ListSubscriptions", "sns:ListSubscriptionsByTopic", "cloudwatch:DescribeAlarms", "cloudwatch:ListTagsForResource"]
        Resource = "*"
      },
      {
        # 変更系は自分たちのトピック・アラームに限定する（sns末尾の*はサブスクリプションARNを含めるため）
        Sid    = "MonitoringWrite"
        Effect = "Allow"
        Action = ["sns:CreateTopic", "sns:DeleteTopic", "sns:Subscribe", "sns:Unsubscribe", "sns:SetTopicAttributes", "sns:TagResource", "sns:UntagResource", "cloudwatch:PutMetricAlarm", "cloudwatch:DeleteAlarms", "cloudwatch:TagResource", "cloudwatch:UntagResource"]
        Resource = [
          "arn:aws:sns:${var.aws_region}:${data.aws_caller_identity.current.account_id}:${local.alert_topic_name}*",
          "arn:aws:cloudwatch:${var.aws_region}:${data.aws_caller_identity.current.account_id}:alarm:${local.alert_alarm_name}",
        ]
      },
      {
        Sid    = "IamManagedByTerraform"
        Effect = "Allow"
        Action = ["iam:Get*", "iam:List*", "iam:CreateRole", "iam:DeleteRole", "iam:UpdateRole", "iam:UpdateAssumeRolePolicy", "iam:PutRolePolicy", "iam:DeleteRolePolicy", "iam:AttachRolePolicy", "iam:DetachRolePolicy", "iam:TagRole", "iam:UntagRole", "iam:PassRole", "iam:PutUserPolicy", "iam:DeleteUserPolicy"]
        Resource = [
          local.terraform_role_arn,
          module.deploy_role.role_arn,
          module.twitter_mlb_bot.role_arn,
          "arn:aws:iam::${data.aws_caller_identity.current.account_id}:user/${var.terraform_user_name}",
        ]
      },
      {
        Sid      = "OidcProvider"
        Effect   = "Allow"
        Action   = ["iam:GetOpenIDConnectProvider", "iam:CreateOpenIDConnectProvider", "iam:DeleteOpenIDConnectProvider", "iam:TagOpenIDConnectProvider", "iam:UntagOpenIDConnectProvider"]
        Resource = module.deploy_role.oidc_provider_arn
      }
    ]
  })
}
