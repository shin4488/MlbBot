# MLB bot

MLBの順位表を毎日X（Twitter）に自動投稿するボット。投稿先: [@MLBbot2](https://twitter.com/MLBbot2)

## アーキテクチャ

```mermaid
flowchart LR
    EB["EventBridge<br>CronTweetMlbStandings<br>毎日 06:00 UTC（15:00 JST）"] --> L["AWS Lambda<br>TwitterMlbBot (dotnet10)"]
    L --> MLB["sportsdata.io<br>MLB順位データ取得"]
    L --> X["X API v2<br>地区ごとに6ツイート投稿"]
```

- [TwitterMlbBot/](TwitterMlbBot/) … 本体ロジック（順位取得・文面組み立て・OAuth1.0a署名・ツイート）
- [TwitterMlbBotExecution/src/](TwitterMlbBotExecution/src/) … Lambdaハンドラ（`Program.Main` を呼ぶ薄いラッパー）
- [TwitterMlbBotExecution/test/](TwitterMlbBotExecution/test/) … テスト（下記の注意参照）

## ローカルセットアップ

必要なもの: .NET 10 SDK（使用バージョンは [global.json](global.json) で10.0系に固定している）

```bash
dotnet build MlbBot.sln
```

設定値は以下のいずれかで渡す（[TwitterMlbBot/Dummy.config](TwitterMlbBot/Dummy.config) がApp.configのテンプレート。App.configはgitignore済み）:

| App.configのキー | 環境変数 | 内容 |
|---|---|---|
| mlb.apiKey | `MLB_API_KEY` | sportsdata.io のAPIキー |
| twitter.consumerKey | `CONSUMER_KEY` | X API Consumer Key |
| twitter.consumerSecret | `CONSUMER_SECRET` | X API Consumer Secret |
| twitter.accessKey | `ACCESS_KEY` | X API Access Token |
| twitter.accessSecret | `ACCESS_SECRET` | X API Access Token Secret |

App.configが無い場合は環境変数へフォールバックする（Lambda上はこの経路）。

## 実行方法

通常送信（実ツイート）はLambdaの定期実行経由のみとする。ローカルではドライランで文面を確認する。

### ドライラン（ツイートせず文面だけ確認する）

`--dry-run` 引数、または環境変数 `DRY_RUN=true` で、ツイートせずに文面をコンソール出力する。
ドライラン時はX APIの認証情報を読み込まないため、`MLB_API_KEY` だけで動く。

```bash
MLB_API_KEY=xxx dotnet run --project TwitterMlbBot -- --dry-run
```

- VSCode: launch構成「TwitterMlbBot (dry-run / ツイートしない)」を実行
- 出力例: `----- dry-run: 以下はツイートされません（xx文字） -----` に続けて各地区の文面

## ⚠️ 注意事項

1. **masterへのpush（マージ）は本番デプロイ**。[.github/workflows/lambda_deploy.yml](.github/workflows/lambda_deploy.yml) により、ビルド・テスト検証後にAWS Lambdaへ自動デプロイされる（`.md`・`.github/`・`.vscode/`・`.gitignore` のみの変更は除く）。
2. **`FunctionTest` は手動疎通確認専用**。本番の `Program.Main` をそのまま実行するためSkip指定してある。Skipを外して実行すると実際にツイートが投稿される。
3. **シークレットをコミットしない**。APIキーはApp.config（gitignore済み）またはLambda環境変数で管理する。

## デプロイに必要な設定

GitHub Secrets（Actionsデプロイ用）:

- `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` … デプロイ用IAMユーザーの認証情報
- `AWS_REGION` … `ap-northeast-1`
- `AWS_LAMBDA_FUNCTION_NAME` … デプロイ先Lambda関数名

Lambda環境変数（実行時）: `MLB_API_KEY`, `CONSUMER_KEY`, `CONSUMER_SECRET`, `ACCESS_KEY`, `ACCESS_SECRET`

## 改善計画ドキュメント

- [docs/code-improvements.md](docs/code-improvements.md) … 保守性・責務分離・DI導入の計画
- [docs/developer-experience.md](docs/developer-experience.md) … CI/CD・ツール整備の計画
- [docs/tweet-content-ideas.md](docs/tweet-content-ideas.md) … ツイート文面の改善案
- [docs/infrastructure.md](docs/infrastructure.md) … インフラのTerraform化方針
