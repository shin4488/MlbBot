# 開発者体験（DX）改善提案

現状の開発フロー（VSCode + dotnet CLI、GitHub ActionsでPR時CI + masterマージ時にLambdaデプロイ）を前提にした改善提案。優先度順。

## 1. デプロイワークフローのOIDC認証への切り替え（優先度: 中）

OIDCプロバイダーとデプロイ用ロール（masterブランチ限定・`lambda:UpdateFunctionCode` のみ）はTerraform（[infra/](../infra/README.md)）で定義済み。残作業はワークフロー側の切り替え:

1. GitHub Secretsに `AWS_DEPLOY_ROLE_ARN`（デプロイ用ロールのARN）を登録
2. lambda_deploy.ymlを長期キー認証からOIDC認証へ変更:

```yaml
permissions:
  id-token: write
  contents: read
steps:
  - uses: aws-actions/configure-aws-credentials@<SHA固定> # 現行規約に合わせcommit hashで指定
    with:
      role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}
      aws-region: ${{ secrets.AWS_REGION }}
```

3. 切り替え後、旧IAMユーザーのアクセスキーをGitHub Secretsから削除し、AWS側でも無効化・削除

## 2. Directory.Build.props による共通設定の一元化（優先度: 低）

リポジトリ直下に `Directory.Build.props` を置き、TFM・Nullable等の全プロジェクト共通設定を一元化する:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

`AnalysisLevel` / `EnforceCodeStyleInBuild` の有効化で警告が発生した場合は、その解消とセットで実施する。
