# 開発者体験（DX）改善提案

現状の開発フロー（VSCode + dotnet CLI、GitHub Actionsでmasterプッシュ時にLambdaデプロイ）を前提にした改善提案。優先度順。

## 1. .NET 6 → .NET 10 への更新（優先度: 高・期限あり）

### 背景
- .NET 6は2024年11月にサポート終了済み。AWS Lambdaの `dotnet6` ランタイムは既に非推奨で、**2027-02-01以降は関数コードの更新がブロックされる**（現行デプロイパイプラインが動かなくなる）
- `dotnet8` ランタイムも2026-11-10に非推奨開始のため、移行先は `dotnet10` 一択
- READMEには「.NET Core 3.1」と書かれており実態（net6.0）とも乖離している

具体的な手順・パッケージの追随更新・破壊的変更と対処法は **[docs/dependency-upgrades.md](dependency-upgrades.md)** にまとめてある。そちらを参照。

## 2. PR/push時のCI（ビルド+テスト）追加（優先度: 高）

現状、CIは「masterへのpush時にデプロイ」しかなく、**ビルドが通るかどうかの検証なしにデプロイまで走る**。

`.github/workflows/ci.yml` を新設:

```yaml
name: CI
on:
  pull_request:
  push:
    branches: [master]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build
```

注意: 現状の `FunctionTest.cs` は本番処理（実API呼び出し）をそのまま実行するため、CIに乗せる前に純粋な単体テストへ置き換えが必要（docs/code-improvements.md 項目3参照）。

## 3. デプロイパイプラインの是正（優先度: 高）

[lambda_deploy.yml](../.github/workflows/lambda_deploy.yml) の問題点と修正:

1. **Debugビルドをデプロイしている**: `dotnet build`（デフォルトDebug）の出力をzipしている。最適化なしのバイナリが本番に上がる状態。
   ```yaml
   - name: Build
     run: dotnet publish TwitterMlbBotExecution/src/TwitterMlbBotExecution -c Release -r linux-x64 --self-contained false -o publish
   - name: Package
     run: (cd publish && zip -r ../TwitterMlbBotExecution.zip .)
   ```
   `publish` を使うと `PublishReadyToRun`（csprojに設定済みだが現状buildなので効いていない）も有効になり、コールドスタートも改善する。
2. **長期AWSキーの利用**: `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY` のシークレット保管はキー漏洩リスクと定期ローテーションの手間がある。GitHub OIDC + IAMロールに移行:
   ```yaml
   permissions:
     id-token: write
     contents: read
   steps:
     - uses: aws-actions/configure-aws-credentials@v6
       with:
         role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE_ARN }}
         aws-region: ap-northeast-1
   ```
   （IAM側でGitHub OIDCプロバイダーと信頼ポリシーの設定が必要）
3. **actionsのバージョンが古い**: `checkout@v3`→`v7`、`configure-aws-credentials@v2`→`v6`、`setup-dotnet@v3`→`v5`（詳細は [dependency-upgrades.md](dependency-upgrades.md) 項目5）
4. **CI成功を前提条件にする**: デプロイjobに `needs: build-and-test` を付ける（ci.ymlと統合する場合）か、branch protectionでCI必須にする
5. **同時デプロイ防止**: `concurrency: { group: deploy, cancel-in-progress: false }` を追加

## 4. ローカル開発のシークレット管理を user-secrets に統一（優先度: 中）

現状はApp.config（gitignore対象、Dummy.configがテンプレート）にJSON文字列を書く方式で、初回セットアップ手順が分かりにくく、JSONのクォートミスもしやすい。

.NET標準の user-secrets へ移行（docs/code-improvements.md 項目2の設定管理刷新とセット）:

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

## 5. ドライランモードの追加（優先度: 中）

現状、「ツイート文面を確認したい」だけでも実ツイートするしかない（Twitter API呼び出しをコメントアウトする等の一時改変が必要）。

`--dry-run` オプションを追加し、ツイートせずに文面をコンソール出力する:

```csharp
// Program.Main で引数解析
bool dryRun = args?.Contains("--dry-run") == true;
// dryRun時は ITwitterClient の実装を ConsoleTwitterClient（Console.WriteLineするだけ）に差し替え
```

DI導入済みなら実装差し替えは1行。文面調整のイテレーションが「編集→実行→コンソールで確認」で回るようになり、テスト用Twitterアカウントも不要になる。

## 6. コードスタイルの自動化（優先度: 中）

1. リポジトリ直下に `.editorconfig` を追加（`dotnet new editorconfig` で雛形生成）。インデント・using順・命名規則を機械可読にする
2. `Directory.Build.props` をリポジトリ直下に置き、全プロジェクト共通設定を一元化:
   ```xml
   <Project>
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
       <AnalysisLevel>latest-recommended</AnalysisLevel>
       <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
     </PropertyGroup>
   </Project>
   ```
3. CIに `dotnet format --verify-no-changes` を追加してフォーマット逸脱をPRで検出

## 7. Dependabot の設定ファイル追加（優先度: 低）

過去にdependabot PR（AutoMapper）が来ていたが、`.github/dependabot.yml` が無いためセキュリティアラート由来のみ。定期更新を有効化する:

```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /
    schedule:
      interval: monthly
  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: monthly
```

## 8. README の刷新（優先度: 低）

現状は6行で、記載の「.NET Core 3.1」も古い。以下を追記する:

- アーキテクチャ概要（EventBridge(?) → Lambda → sportsdata.io / X API の流れ。1枚図があると良い）
- ローカルセットアップ手順（SDKバージョン、user-secrets設定、実行コマンド、--dry-run）
- デプロイの仕組み（masterへのpushで自動デプロイされること。**知らずにpushすると本番反映される**現状では特に明記すべき）
- 必要なGitHub Secrets / Lambda環境変数の一覧

## 9. その他

- **ソリューションへのdocs追加**: `MlbBot.sln` にソリューションフォルダを作りドキュメントを見えるようにする（Visual Studio利用時）
- **PRテンプレート**: `.github/pull_request_template.md` に「動作確認方法（--dry-run出力の貼り付け等）」欄を設ける
- **branch protection**: masterへの直pushで即デプロイされる構成なので、PR必須 + CI必須にすると事故が減る
