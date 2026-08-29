# MLB bot

MLBの順位表を毎日X（Twitter）に自動投稿するボット。投稿先: [@MLBbot2](https://twitter.com/MLBbot2)

## アーキテクチャ

### 実行環境

```mermaid
flowchart LR
    EB["EventBridge<br>CronTweetMlbStandings<br>毎日 06:00 UTC（15:00 JST）"] --> L["AWS Lambda<br>TwitterMlbBot (dotnet10)"]
    L --> MLB["sportsdata.io<br>MLB順位データ取得"]
    L --> X["X API v2<br>地区ごとに6ツイート投稿"]
```

### 内部構造

「取得 → 文面組み立て → 送信」を分離し、取得元と送信先はinterfaceで差し替え可能にしている（テスト用フェイク・ドライランは送信先の差し替えで実現）。

```mermaid
flowchart TB
    F["TwitterMlbBotExecution.Function<br>Lambdaハンドラ（薄いラッパー）"] --> P
    P["Program.Main<br>引数解析（RunOptions）と依存関係の組み立てのみ"] --> R["BotRunner.RunAsync<br>取得 → 組み立て → 送信 の流れだけを持つ"]

    R --> ISP([IStandingsProvider])
    R --> TC
    R --> ITS([ITweetSender])

    subgraph mlb["取得・ドメインモデル（Mlb/）"]
        ISP -.実装.-> MAC["MlbApiClient<br>sportsdata.io / キーはヘッダー送信"]
        MAC --> TS["TeamStanding（不変record）<br>All-Star判定などのルールを保持"]
        DS["DivisionStanding<br>地区順位表。順位順（RankedTeam）を型で保証"] --> TS
    end

    subgraph composing["文面組み立て（Composing/）<br>ネットワーク・設定に依存しない純粋ロジック"]
        TC["TweetComposer<br>文面の見た目だけに責任を持つ"] --> HP["HashtagProvider<br>公式タグマップ"]
        TC --> TCN["TweetContent（値オブジェクト）<br>280字上限の知識を持つ"]
    end
    R --> DS
    DS --> TC

    subgraph twitter["送信（Twitter/）"]
        ITS -.実装.-> TAS["TwitterApiSender<br>X API v2 + OAuth1.0a署名"]
        ITS -.実装.-> DRS["DryRunTweetSender<br>コンソール出力（ドライラン時）"]
    end
```

## ローカルセットアップ

必要なもの: .NET 10 SDK（使用バージョンは [global.json](global.json) で10.0系に固定している）

```bash
dotnet build MlbBot.sln
```

設定値はローカル・Lambda共通で**環境変数**で渡す:

| 環境変数 | 内容 |
|---|---|
| `MLB_API_KEY` | sportsdata.io のAPIキー |
| `CONSUMER_KEY` | X API Consumer Key |
| `CONSUMER_SECRET` | X API Consumer Secret |
| `ACCESS_KEY` | X API Access Token |
| `ACCESS_SECRET` | X API Access Token Secret |
| `DRY_RUN`（任意） | `true` でドライラン |

必須の環境変数が未設定の場合は、変数名入りのエラーで起動時に失敗する（ドライランで必須なのは `MLB_API_KEY` のみ）。

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
3. **シークレットをコミットしない**。APIキーは環境変数でのみ扱い、リポジトリ内のファイルには書かない。

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
