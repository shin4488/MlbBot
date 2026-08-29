# CLAUDE.md

MLBの順位表を毎日X（Twitter）に投稿するボット。AWS Lambda上で毎日06:00 UTC（15:00 JST、EventBridgeルール `CronTweetMlbStandings`）に実行され、地区ごとに計6ツイートする。

## アーキテクチャ

「取得 → 文面組み立て → 送信」を分離し、取得元・送信先はinterfaceで差し替える構成（詳細図はREADME参照）。

```mermaid
flowchart LR
    EB["EventBridge<br>毎日06:00 UTC"] --> F["Function<br>（Lambdaハンドラ）"] --> P["Program.Main<br>引数解析・組み立て"] --> R["BotRunner<br>オーケストレーション"]
    R --> MAC["IStandingsProvider<br>← MlbApiClient<br>(sportsdata.io)"]
    R --> TC["TweetComposer + HashtagProvider<br>純粋ロジック（Composing/）"]
    R --> ITS["ITweetSender<br>← TwitterApiSender (X API)<br>← DryRunTweetSender (dry-run)"]
```

- `TwitterMlbBot/` … 本体ロジック（OutputType=Exe。ローカル実行は `dotnet run --project TwitterMlbBot -- --dry-run`）
- `TwitterMlbBotExecution/src/` … Lambdaハンドラ（`Program.Main(null)` を呼ぶだけの薄いラッパー）
- `TwitterMlbBotExecution/test/` … Skip指定の手動疎通用テスト（`FunctionTest`）と、純粋ロジック・オーケストレーションの単体テスト（フェイク使用・ネットワーク不要）

## ビルド・テスト

```bash
dotnet build MlbBot.sln     # 全プロジェクトビルド
dotnet test MlbBot.sln      # 安全（実ツイートするFunctionTestはSkip指定済み）
dotnet format MlbBot.sln    # コード変更後に実行（CIが --verify-no-changes で検証する）
```

- ターゲットは net10.0（3プロジェクトすべて）。SDKは `global.json` で10.0系に固定
- コード内コメント・コミットメッセージのスタイルは日本語
- `.editorconfig` は最小構成。C#スタイルはRoslyn / dotnet format の既定値に任せる方針
- **テストは仕様ベースで書く**: 文面フォーマットの詳細・内部実装・具体的な例外型など変わりやすいものに依存させず、入出力の不変条件（データが文面に反映される、ツイートされない等）を検証する。リファクタや文面変更のたびにテストを直さなくて済む状態を保つ

## ドライラン（ツイートせずに文面確認）

`--dry-run` 引数、または環境変数 `DRY_RUN=true` で、ツイートせず文面をコンソール出力する。
ドライラン時は送信先が `DryRunTweetSender`（コンソール出力のみ）に差し替わり、X API認証情報の読み込みも送信コードへの到達も起きないため、誤投稿は構造的に不可能（必要なのは `MLB_API_KEY` のみ）。`dotnet run --project TwitterMlbBot -- --dry-run`、またはVSCodeのlaunch構成「TwitterMlbBot (dry-run / ツイートしない)」で実行できる。

## ⚠️ 重要な注意事項

1. **`FunctionTest` のSkipを外したまま一括実行しないこと**: 本番の `Program.Main` を直接実行するため、認証情報がある環境では実ツイートが投稿される。手動の疎通確認専用。
2. **masterへのpush（マージ）は本番デプロイ**: `.github/workflows/lambda_deploy.yml` の verify（ビルド+テスト）通過後、Releaseの `dotnet publish` 成果物がLambdaへデプロイされる（`.md`・`.github/`・`.vscode/`・`.gitignore` のみの変更は除く）。変更は必ずPR経由にし、PRのCI（ビルド+フォーマット検証+テスト）を通すこと。
3. **シークレットをコミットしない**: APIキーは環境変数（`MLB_API_KEY`, `CONSUMER_KEY`, `CONSUMER_SECRET`, `ACCESS_KEY`, `ACCESS_SECRET`）でのみ扱い、リポジトリ内のファイルには書かない。
4. **GitHub Actionsはcommit SHA固定**: `@v7` のようなタグではなく、フルcommit hash + バージョンコメント（例: `actions/checkout@3d3c42e... # v7.0.1`）で指定する。バージョン更新はDependabot（月次）が担う。

## 設定の仕組み

設定は**環境変数のみ**（ローカル・Lambda共通）。必須の環境変数が未設定の場合は `Program` が起動時に変数名入りのエラーで即失敗する。ドライラン時はX API系の変数を読み込まないため `MLB_API_KEY` だけで動く。

## ドメイン知識

- **X APIは従量課金**（投稿 $0.015/件・リンク入りは $0.20/件）。6ツイート/日・3〜10月稼働で年間約$22。ツイート件数を増やす変更はコスト増を意識すること
- sportsdata.io のレスポンスには All-Star 用の擬似チーム（League と Division が同名: "AL"/"AL"）が含まれるため、グループ化後に要素数1のグループを除外している（`Program.MapToTwitterParam`）
- チーム公式ハッシュタグは `Program.OfficialHashtagMap` で一元管理（毎シーズン変わる可能性あり）
- X APIの連続POSTは503になるため、ツイート間に1秒のインターバルを入れている（Lambdaタイムアウト15秒との兼ね合いで調整済み）
- OAuth1.0a署名は自前実装（`Authorization/OAuth1.cs`）。タイムスタンプが未来だとX APIに弾かれるため UNIXタイムスタンプ切り捨てを使用

## 改善計画ドキュメント

リファクタリングや機能追加の際は、まず以下を参照すること:

- [docs/code-improvements.md](docs/code-improvements.md) … 保守性・責務分離・DI導入の計画
- [docs/developer-experience.md](docs/developer-experience.md) … CI/CD・ツール整備の計画（OIDC化 / 共通設定一元化 等）
- [docs/tweet-content-ideas.md](docs/tweet-content-ideas.md) … ツイート文面の改善案
- [docs/infrastructure.md](docs/infrastructure.md) … インフラのTerraform化方針（OIDC化・タイムアウト調整・アラームはTerraform化後に対応）
