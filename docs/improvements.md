# 改善候補

残っている改善タスク。優先度順。現在の構造は [README](../README.md) のアーキテクチャ図、インフラの使い方は [infra/README.md](../infra/README.md) を参照。

## 1. リトライの導入（優先度: 中）

### 現状の問題

- X API・MLB APIの一時的エラー（429/503等）に対するリトライがなく、単発の失敗がそのままツイート欠落になる
- ツイートの部分失敗はログ出力のみで検知手段がない（全件失敗の場合のみLambda実行がエラー終了し、アラームでメール通知される）

### 提案

1. 一時的エラーにリトライを入れる。手書きでもよいが `Polly` を使うと簡潔（`WaitAndRetryAsync(3回, 指数バックオフ)`）。
2. リトライ分の実行時間を確保するため、Lambdaタイムアウト（現状15秒）を60秒程度へ引き上げる（[infra/](../infra/README.md) の `timeout` 変更。applyは人間が実行）。

## 2. デプロイワークフローのOIDC認証への切り替え（優先度: 中）

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

## 3. Directory.Build.props による共通設定の一元化（優先度: 低）

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

`Nullable` / `AnalysisLevel` の全体有効化は本体プロジェクト（Nullable無効）で警告が発生するため、警告の解消とセットで実施する。
