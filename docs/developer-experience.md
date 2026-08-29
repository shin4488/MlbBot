# 開発者体験（DX）改善提案

現状の開発フロー（VSCode + dotnet CLI、GitHub ActionsでPR時CI + masterマージ時にLambdaデプロイ）を前提にした改善提案。優先度順。

## 1. .NET 6 → .NET 10 への更新（優先度: 高・期限あり）

- .NET 6は2024年11月にサポート終了済み。AWS Lambdaの `dotnet6` ランタイムは既に非推奨で、**2027-02-01以降は関数コードの更新がブロックされる**（デプロイパイプラインが動かなくなる）
- `dotnet8` ランタイムも2026-11-10に非推奨開始のため、移行先は `dotnet10` 一択
- 前提: ローカルに .NET 10 SDK のインストールが必要（`brew install --cask dotnet-sdk`）

具体的な手順・パッケージの追随更新・破壊的変更と対処法は **[docs/dependency-upgrades.md](dependency-upgrades.md)** にまとめてある。そちらを参照。

## 2. デプロイのOIDC化（優先度: 中）

現状のデプロイは長期AWSキー（`AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY` のGitHub Secrets）を使っており、キー漏洩リスクと定期ローテーションの手間がある。GitHub OIDC + IAMロールに移行する:

```yaml
permissions:
  id-token: write
  contents: read
steps:
  - uses: aws-actions/configure-aws-credentials@<SHA固定> # 現行規約に合わせcommit hashで指定
    with:
      role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}
      aws-region: ap-northeast-1
```

事前にIAM側の作業が必要（AWSコンソール/CLIでの手動作業）:

1. IAMにGitHub OIDCプロバイダー（`token.actions.githubusercontent.com`）を作成
2. このリポジトリのmasterブランチに限定した信頼ポリシーを持つIAMロールを作成（権限は `lambda:UpdateFunctionCode` のみに絞る）
3. 切り替え後、旧IAMユーザーのアクセスキーを無効化・削除

## 3. ローカル開発のシークレット管理を user-secrets に統一（優先度: 中）

現状はApp.config（gitignore対象、Dummy.configがテンプレート）にJSON文字列を書く方式で、初回セットアップ手順が分かりにくく、JSONのクォートミスもしやすい。

.NET標準の user-secrets へ移行（[docs/code-improvements.md](code-improvements.md) 項目2の設定管理刷新とセットで行う）:

```bash
cd TwitterMlbBot
dotnet user-secrets init
dotnet user-secrets set "Mlb:ApiKey" "xxx"
dotnet user-secrets set "Twitter:ConsumerKey" "xxx"
dotnet user-secrets set "Twitter:ConsumerSecret" "xxx"
dotnet user-secrets set "Twitter:AccessKey" "xxx"
dotnet user-secrets set "Twitter:AccessSecret" "xxx"
```

- シークレットはリポジトリ外（`~/.microsoft/usersecrets/`）に保存されるため、誤コミットが構造的に起きない
- `Dummy.config` と App.config 運用は廃止

## 4. Directory.Build.props による共通設定の一元化（優先度: 低）

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

**.NET 10移行PR（項目1）と同時に実施すること**。現状のnet6.0のまま `Nullable`/`AnalysisLevel` を全体有効化すると本体プロジェクト（Nullable無効）で警告が大量発生するため、TFM更新・警告対応とセットが安全。

## 5. branch protection（優先度: 低・任意）

masterへの直push＝即本番デプロイの構成なので、GitHubのリポジトリ設定でPR必須 + CI必須にすると事故が減る。単独開発のため運用ルール（必ずPRを切る）でも代替可能。設定する場合: Settings → Branches → Add branch protection rule（`master`、Require a pull request / Require status checks: `build-and-test`）。
