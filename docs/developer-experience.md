# 開発者体験（DX）改善提案

現状の開発フロー（VSCode + dotnet CLI、GitHub ActionsでPR時CI + masterマージ時にLambdaデプロイ）を前提にした改善提案。優先度順。

## 1. デプロイのOIDC化（優先度: 中・Terraform化後に対応）

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

事前にIAMリソースの作成が必要:

1. GitHub OIDCプロバイダー（`token.actions.githubusercontent.com`）を作成
2. このリポジトリのmasterブランチに限定した信頼ポリシーを持つIAMロールを作成（権限は `lambda:UpdateFunctionCode` のみに絞る）
3. 切り替え後、旧IAMユーザーのアクセスキーを無効化・削除

**対応時期**: 上記IAMリソースはコード管理下で作るべきなので、**Terraform化（[docs/infrastructure.md](infrastructure.md)）が完了してから**Terraformで作成して対応する。

## 2. ローカル開発のシークレット管理を user-secrets に統一（優先度: 中）

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

`Nullable` / `AnalysisLevel` を全体有効化すると本体プロジェクト（Nullable無効）で警告が大量発生するため、警告の解消とセットで実施する。

## 4. branch protection（優先度: 低・任意）

masterへの直push＝即本番デプロイの構成なので、GitHubのリポジトリ設定でPR必須 + CI必須にすると事故が減る。単独開発のため運用ルール（必ずPRを切る）でも代替可能。設定する場合: Settings → Branches → Add branch protection rule（`master`、Require a pull request / Require status checks: `build-and-test`）。
