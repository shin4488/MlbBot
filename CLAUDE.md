# CLAUDE.md

MLBの順位表を毎日X（Twitter）に投稿するボット。AWS Lambda上で毎日06:00 UTC（15:00 JST、EventBridgeルール `CronTweetMlbStandings`）に実行され、地区ごとに計6ツイートする。

## アーキテクチャ

```
EventBridge → Lambda (TwitterMlbBotExecution.Function)
  └→ TwitterMlbBot.Program.Main
       ├→ Mlb.MlbService      … sportsdata.io API から順位データ取得
       ├→ Program 内でマッピング・ハッシュタグ生成
       └→ Twitter.TwitterService … 文面組み立て + X API v2 (OAuth1.0a署名) でツイート
```

- `TwitterMlbBot/` … 本体ロジック（OutputType=Library。`dotnet run` では起動できない。実行はVSCodeのlaunch設定 or Lambda経由）
- `TwitterMlbBotExecution/src/` … Lambdaハンドラ（`Program.Main(null)` を呼ぶだけの薄いラッパー）
- `TwitterMlbBotExecution/test/` … Skip指定の手動疎通用テスト（`FunctionTest`）と、ドライラン等の純粋な単体テスト

## ビルド・テスト

```bash
dotnet build MlbBot.sln     # 全プロジェクトビルド
dotnet test MlbBot.sln      # 安全（実ツイートするFunctionTestはSkip指定済み）
dotnet format MlbBot.sln    # コード変更後に実行（CIが --verify-no-changes で検証する）
```

- ターゲットは net6.0（3プロジェクトすべて）
- コード内コメント・コミットメッセージのスタイルは日本語
- `.editorconfig` は最小構成。C#スタイルはRoslyn / dotnet format の既定値に任せる方針

## ドライラン（ツイートせずに文面確認）

`--dry-run` 引数、または環境変数 `DRY_RUN=true` で、ツイートせず文面をコンソール出力する。
ドライラン時はX API認証情報を読み込まず `ExecuteTweet` も例外で拒否する二重ガードのため、誤投稿は構造的に起きない（必要なのは `MLB_API_KEY` のみ）。VSCodeのlaunch構成「Lambda Function (dry-run / ツイートしない)」から実行できる。

## ⚠️ 重要な注意事項

1. **`FunctionTest` のSkipを外したまま一括実行しないこと**: 本番の `Program.Main` を直接実行するため、認証情報がある環境では実ツイートが投稿される。手動の疎通確認専用。
2. **masterへのpush（マージ）は本番デプロイ**: `.github/workflows/lambda_deploy.yml` の verify（ビルド+テスト）通過後、Releaseの `dotnet publish` 成果物がLambdaへデプロイされる（`.md`・`.github/`・`.vscode/`・`.gitignore` のみの変更は除く）。変更は必ずPR経由にし、PRのCI（ビルド+フォーマット検証+テスト）を通すこと。
3. **シークレットをコミットしない**: 実際のAPIキーは `TwitterMlbBot/App.config`（gitignore済み）または Lambda環境変数（`MLB_API_KEY`, `CONSUMER_KEY`, `CONSUMER_SECRET`, `ACCESS_KEY`, `ACCESS_SECRET`）で管理。`Dummy.config` はキーを含まないテンプレート。
4. **GitHub Actionsはcommit SHA固定**: `@v7` のようなタグではなく、フルcommit hash + バージョンコメント（例: `actions/checkout@3d3c42e... # v7.0.1`）で指定する。バージョン更新はDependabot（月次）が担う。

## 設定の仕組み（現状）

`ProcessUtility.ReadAppConfig` がApp.configからJSON文字列を読み、Lambda上では App.config が null になるため `GetEnvVarByKey` が環境変数へフォールバックする。この「nullなら環境変数」という暗黙分岐が前提になっているので、設定関連を触るときは両方の実行経路（ローカル/Lambda）を確認すること。

## ドメイン知識

- **X APIは従量課金**（投稿 $0.015/件・リンク入りは $0.20/件）。6ツイート/日・3〜10月稼働で年間約$22。ツイート件数を増やす変更はコスト増を意識すること
- sportsdata.io のレスポンスには All-Star 用の擬似チーム（League と Division が同名: "AL"/"AL"）が含まれるため、グループ化後に要素数1のグループを除外している（`Program.MapToTwitterParam`）
- チーム公式ハッシュタグは `Program.OfficialHashtagMap` で一元管理（毎シーズン変わる可能性あり）
- X APIの連続POSTは503になるため、ツイート間に1秒のインターバルを入れている（Lambdaタイムアウト15秒との兼ね合いで調整済み）
- OAuth1.0a署名は自前実装（`Authorization/OAuth1.cs`）。タイムスタンプが未来だとX APIに弾かれるため UNIXタイムスタンプ切り捨てを使用

## 改善計画ドキュメント

リファクタリングや機能追加の際は、まず以下を参照すること:

- [docs/code-improvements.md](docs/code-improvements.md) … 保守性・責務分離・DI導入の計画
- [docs/developer-experience.md](docs/developer-experience.md) … CI/CD・ツール整備の計画（.NET 10移行 / OIDC化 / user-secrets 等）
- [docs/tweet-content-ideas.md](docs/tweet-content-ideas.md) … ツイート文面の改善案
- [docs/dependency-upgrades.md](docs/dependency-upgrades.md) … .NET 10移行・パッケージ更新の計画（期限: Lambda dotnet6は2027-02-01に更新ブロック。ローカルに.NET 10 SDKのインストールが必要）
