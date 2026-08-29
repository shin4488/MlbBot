# 改善候補

残っている改善タスク。優先度順。現在の構造は [README](../README.md) のアーキテクチャ図、インフラの使い方は [infra/README.md](../infra/README.md) を参照。

## 1. リトライの導入（優先度: 中）

### 現状の問題

- X API・MLB APIの一時的エラー（429/503等）に対するリトライがなく、単発の失敗がそのままツイート欠落になる
- ツイートの部分失敗はログ出力のみで検知手段がない（全件失敗の場合のみLambda実行がエラー終了し、アラームでメール通知される）

### 提案

1. 一時的エラーにリトライを入れる。手書きでもよいが `Polly` を使うと簡潔（`WaitAndRetryAsync(3回, 指数バックオフ)`）。
2. リトライ分の実行時間を確保するため、Lambdaタイムアウト（現状15秒）を60秒程度へ引き上げる（[infra/](../infra/README.md) の `timeout` 変更。applyは人間が実行）。

## 2. デプロイ用の旧アクセスキーの廃止（優先度: 中）

デプロイはOIDC認証（GitHub OIDC + masterブランチ限定のIAMロール）に切り替え済みのため、長期アクセスキーは不要になっている。OIDCでのデプロイ成功を1回確認したうえで:

1. GitHub Secretsから `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` を削除
2. AWS側（IAMユーザーのセキュリティ認証情報）で該当アクセスキーを無効化→削除

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
