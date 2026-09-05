data "aws_caller_identity" "current" {}

locals {
  terraform_role_name = "mlbbot-terraform-execution"
  # ロール自身に付与するポリシーが自分のARNを参照すると循環になるため、名前からARNを組み立てる
  terraform_role_arn = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/${local.terraform_role_name}"
  terraform_user_arn = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:user/${var.terraform_user_name}"
}

# Terraform実行ユーザーがIAM系リソースをplan/applyするためのブートストラップ権限
# （管理者が付与したものをimportして管理下に置いている）。
# ⚠️ 誤って削除するとTerraformからIAMを変更できなくなり、管理者による再付与が必要になる
resource "aws_iam_user_policy" "terraform_iam_bootstrap" {
  name = "TerraformIamBootstrap"
  user = var.terraform_user_name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "Roles"
        Effect   = "Allow"
        Action   = ["iam:CreateRole", "iam:DeleteRole", "iam:GetRole", "iam:TagRole", "iam:UntagRole", "iam:UpdateRole", "iam:UpdateAssumeRolePolicy", "iam:PutRolePolicy", "iam:GetRolePolicy", "iam:ListRolePolicies", "iam:DeleteRolePolicy", "iam:AttachRolePolicy", "iam:DetachRolePolicy", "iam:ListAttachedRolePolicies", "iam:ListInstanceProfilesForRole"]
        Resource = [module.twitter_mlb_bot.role_arn, module.deploy_role.role_arn, local.terraform_role_arn]
      },
      {
        Sid      = "Oidc"
        Effect   = "Allow"
        Action   = ["iam:CreateOpenIDConnectProvider", "iam:DeleteOpenIDConnectProvider", "iam:GetOpenIDConnectProvider", "iam:TagOpenIDConnectProvider", "iam:UntagOpenIDConnectProvider"]
        Resource = module.deploy_role.oidc_provider_arn
      },
      {
        Sid      = "SelfUserPolicy"
        Effect   = "Allow"
        Action   = ["iam:PutUserPolicy", "iam:GetUserPolicy", "iam:ListUserPolicies", "iam:DeleteUserPolicy"]
        Resource = local.terraform_user_arn
      }
    ]
  })
}

module "deploy_role" {
  source = "../../modules/github_oidc_role"

  role_name = "mlbbot-github-actions-deploy"
  # IAMロールのdescriptionはASCII/Latin-1のみ許可（日本語不可）
  role_description  = "Deploy role for GitHub Actions to update the Lambda function code"
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

  role_name = local.terraform_role_name
  # IAMロールのdescriptionはASCII/Latin-1のみ許可（日本語不可）
  role_description   = "Terraform execution role for plan/apply of this repository"
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
        Action   = ["lambda:Get*", "lambda:List*", "lambda:UpdateFunctionConfiguration", "lambda:PutFunctionEventInvokeConfig", "lambda:DeleteFunctionEventInvokeConfig", "lambda:AddPermission", "lambda:RemovePermission", "lambda:TagResource", "lambda:UntagResource"]
        Resource = module.twitter_mlb_bot.function_arn
      },
      {
        Sid      = "EventBridge"
        Effect   = "Allow"
        Action   = ["events:Describe*", "events:List*", "events:PutRule", "events:DeleteRule", "events:PutTargets", "events:RemoveTargets", "events:EnableRule", "events:DisableRule", "events:TagResource", "events:UntagResource"]
        Resource = module.twitter_mlb_bot.event_rule_arn
      },
      {
        Sid    = "Logs"
        Effect = "Allow"
        Action = ["logs:DescribeMetricFilters", "logs:CreateLogGroup", "logs:DeleteLogGroup", "logs:PutRetentionPolicy", "logs:DeleteRetentionPolicy", "logs:PutMetricFilter", "logs:DeleteMetricFilter", "logs:TagResource", "logs:UntagResource", "logs:ListTagsForResource"]
        Resource = [
          "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:${module.twitter_mlb_bot.log_group_name}",
          "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:${module.twitter_mlb_bot.log_group_name}:*",
        ]
      },
      {
        # 参照系でも個別リソースに絞れるものは限定する。
        Sid    = "MonitoringRead"
        Effect = "Allow"
        Action = ["sns:GetTopicAttributes", "sns:GetSubscriptionAttributes", "sns:ListTagsForResource", "sns:ListSubscriptionsByTopic", "cloudwatch:DescribeAlarms", "cloudwatch:ListTagsForResource"]
        Resource = [
          "arn:aws:sns:${var.aws_region}:${data.aws_caller_identity.current.account_id}:${local.alert_topic_name}",
          "arn:aws:cloudwatch:${var.aws_region}:${data.aws_caller_identity.current.account_id}:alarm:${local.alert_alarm_name}",
          "arn:aws:cloudwatch:${var.aws_region}:${data.aws_caller_identity.current.account_id}:alarm:${local.error_log_alarm_name}",
        ]
      },
      {
        # これらの一覧APIにはリソース単位の制限がない。
        Sid      = "ResourceDiscovery"
        Effect   = "Allow"
        Action   = ["sns:ListTopics", "sns:ListSubscriptions", "logs:DescribeLogGroups"]
        Resource = "*"
      },
      {
        # SNSの購読操作もIAMではトピックで制限する。同じ接頭辞の別トピックまで許可しない。
        Sid    = "MonitoringWrite"
        Effect = "Allow"
        Action = ["sns:CreateTopic", "sns:DeleteTopic", "sns:Subscribe", "sns:Unsubscribe", "sns:SetTopicAttributes", "sns:TagResource", "sns:UntagResource", "cloudwatch:PutMetricAlarm", "cloudwatch:DeleteAlarms", "cloudwatch:TagResource", "cloudwatch:UntagResource"]
        Resource = [
          "arn:aws:sns:${var.aws_region}:${data.aws_caller_identity.current.account_id}:${local.alert_topic_name}",
          "arn:aws:cloudwatch:${var.aws_region}:${data.aws_caller_identity.current.account_id}:alarm:${local.alert_alarm_name}",
          "arn:aws:cloudwatch:${var.aws_region}:${data.aws_caller_identity.current.account_id}:alarm:${local.error_log_alarm_name}",
        ]
      },
      {
        # 自身の権限もTerraformで管理するため、このロールは自分の許可範囲を変更できる。
        # 権限の上限を強制する場合は、別の管理者が管理する権限境界などが必要になる。
        Sid    = "IamManagedByTerraform"
        Effect = "Allow"
        Action = ["iam:Get*", "iam:List*", "iam:CreateRole", "iam:DeleteRole", "iam:UpdateRole", "iam:UpdateAssumeRolePolicy", "iam:PutRolePolicy", "iam:DeleteRolePolicy", "iam:AttachRolePolicy", "iam:DetachRolePolicy", "iam:TagRole", "iam:UntagRole", "iam:PutUserPolicy", "iam:DeleteUserPolicy"]
        Resource = [
          local.terraform_role_arn,
          module.deploy_role.role_arn,
          module.twitter_mlb_bot.role_arn,
          "arn:aws:iam::${data.aws_caller_identity.current.account_id}:user/${var.terraform_user_name}",
        ]
      },
      {
        # 設定変更時にLambdaへ渡すのは実行ロールだけ。Terraform自身やデプロイ用ロールは渡さない。
        Sid      = "PassLambdaExecutionRole"
        Effect   = "Allow"
        Action   = "iam:PassRole"
        Resource = module.twitter_mlb_bot.role_arn
        Condition = {
          StringEquals = { "iam:PassedToService" = "lambda.amazonaws.com" }
        }
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
