# MLB bot

MLBの順位表をXへ自動投稿するボットです。投稿先: [@MLBbot2](https://twitter.com/MLBbot2)

## プログラムの構成

本体は `TwitterMlbBot/` にあります。Lambdaからの呼び出しだけ、別プロジェクトの `TwitterMlbBotExecution/` で受け付けます。

### 起動から実行まで

```mermaid
flowchart LR
    F["Function<br>EventBridgeからAWS Lambdaで起動<br>本体を呼び出す"] --> P["Program.Main<br>取得・送信などに使うクラスを用意する"]
    P --> O["RunOptions<br>起動引数を読み取る"]
    P --> R["BotRunner.RunAsync<br>日程確認 → 順位取得 → 文面作成 → 送信<br>投稿するか、エラー時に続けるかを判断する"]
    click F "TwitterMlbBotExecution/src/TwitterMlbBotExecution/Function.cs"
    click P "TwitterMlbBot/Program.cs"
    click O "TwitterMlbBot/RunOptions.cs"
    click R "TwitterMlbBot/BotRunner.cs"
```

EventBridgeのルール名・実行時刻・Lambdaで使う.NETのバージョンは、[Terraformの設定](infra/environments/prod/main.tf)で確認できます。

### 日程と順位の取得

取得元と送信先にはインターフェースを使い、テストでは実際のAPIに接続しない処理に差し替えます。

```mermaid
flowchart LR
    ISC["ISeasonCalendarProvider<br>日程を取得するインターフェース"] -->|実装| MSC["MlbStatsApiClient<br>MLB公式 statsapi.mlb.com<br>認証不要"]
    MSC -->|取得結果| SC["SeasonCalendar<br>日程を保持する変更不可のrecord<br>シーズン終了後かどうかを判定する"]
    ISP["IStandingsProvider<br>順位を取得するインターフェース"] -->|実装| MAC["MlbApiClient<br>sportsdata.io<br>APIキーはHTTPヘッダーで送信<br>All-Star用の擬似チームを除外"]
    MAC -->|取得結果| TS["TeamStanding<br>成績を保持する変更不可のrecord<br>勝率・ゲーム差・順位付けの計算"]
    TS --> DS["DivisionStanding / WildCardStanding<br>地区・ワイルドカードの順位表<br>各チームをRankedTeamとして順位順に保持"]
    click ISC "TwitterMlbBot/Mlb/ISeasonCalendarProvider.cs"
    click MSC "TwitterMlbBot/Mlb/MlbStatsApiClient.cs"
    click SC "TwitterMlbBot/Mlb/SeasonCalendar.cs"
    click ISP "TwitterMlbBot/Mlb/IStandingsProvider.cs"
    click MAC "TwitterMlbBot/Mlb/MlbApiClient.cs"
    click TS "TwitterMlbBot/Mlb/TeamStanding.cs"
    click DS "TwitterMlbBot/Mlb/"
```

### 文面の作成と投稿

文面の作成では通信や環境変数を使いません。送信先を差し替えることで、同じ文面をXに投稿することも、ドライランで確認することもできます。

```mermaid
flowchart LR
    C["TweetComposer<br>投稿の種類・件数・時期を決める<br>地区・ワイルドカードの文面を作る"] -->|タグを取得| H["HashtagProvider<br>チーム名と公式タグの対応表"]
    C -->|文面を作成| T["TweetContent<br>文面を表す値オブジェクト<br>文字数と上限超過を判定する"]
    T --> I["ITweetSender<br>送信先のインターフェース"]
    I -->|Xへ投稿| X["TwitterApiSender<br>X API v2に送信する"]
    X -->|リクエストに署名| A["OAuth1<br>OAuth 1.0aによる認証"]
    I -->|ドライラン| D["DryRunTweetSender<br>コンソールに文面を表示する"]
    click C "TwitterMlbBot/Composing/TweetComposer.cs"
    click H "TwitterMlbBot/Composing/HashtagProvider.cs"
    click T "TwitterMlbBot/Composing/TweetContent.cs"
    click I "TwitterMlbBot/Twitter/ITweetSender.cs"
    click X "TwitterMlbBot/Twitter/TwitterApiSender.cs"
    click A "TwitterMlbBot/Authorization/OAuth1.cs"
    click D "TwitterMlbBot/Twitter/DryRunTweetSender.cs"
```

## 手元のPCで実行する

[global.jsonで指定している.NET SDK](global.json)をインストールし、環境変数 `MLB_API_KEY` にAPIキーを設定します。

```bash
dotnet build MlbBot.sln
dotnet test MlbBot.sln
MLB_API_KEY=xxx dotnet run --project TwitterMlbBot -- --dry-run
```

`xxx` は手元のAPIキーに置き換えてください。Xへの投稿はLambdaの定期実行に限り、手元のPCでは投稿せずに文面だけを確認するドライランを使います。

| 実行方法 | 出力先 | 必要な認証情報 |
|---|---|---|
| `--dry-run` / `DRY_RUN=true` | コンソール | MLBのみ |
| Lambdaの定期実行 | X | MLBとX |

VSCodeでは、実行構成 **TwitterMlbBot (dry-run / ツイートしない)** を選択してください。
コードを変更した後は `dotnet format MlbBot.sln` で整形します。

ドライランでは、次の見出しに続いて各順位表の文面が表示されます。

```text
----- dry-run: 以下はツイートされません（xx文字） -----
```

### 環境変数（ローカル・Lambda共通）

| 変数 | 用途 | 必要な場面 |
|---|---|---|
| `MLB_API_KEY` | sportsdata.io APIキー | すべて |
| `CONSUMER_KEY` | X Consumer Key | Xへ投稿するとき |
| `CONSUMER_SECRET` | X Consumer Secret | Xへ投稿するとき |
| `ACCESS_KEY` | X Access Token | Xへ投稿するとき |
| `ACCESS_SECRET` | X Access Token Secret | Xへ投稿するとき |
| `DRY_RUN` | `true` でドライラン | 任意 |

必要な環境変数が未設定の場合は、起動時に変数名を含むエラーを表示して終了します。ドライランではXの認証情報を読み込みません。

## デプロイ

```mermaid
flowchart LR
    PR["PR"] --> CI["ビルド・整形・テスト"]
    CI --> M["masterへマージ"]
    M --> L["Lambdaへ自動デプロイ"]
```

| GitHub Secret | 用途 |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | OIDC認証で使用するIAMロールのARN |
| `AWS_REGION` | デプロイ先リージョン |
| `AWS_LAMBDA_FUNCTION_NAME` | デプロイ先Lambda名 |

GitHub ActionsはOIDCで一時的な認証情報を取得するため、長期アクセスキーは使いません。Lambdaにも、上記の表で「Xへ投稿するとき」に必要な変数と `MLB_API_KEY` を設定します。

変更してもデプロイが実行されないファイルやディレクトリは、[ワークフローの `paths-ignore`](.github/workflows/lambda_deploy.yml)で確認できます。

### 運用上の注意

- masterへマージすると本番に反映されます。ブランチ保護により直接pushできないため、PRを作成し、CIチェック `build-and-test` を通す必要があります。
- `FunctionTest` は、本番の `Program.Main` を直接呼んで接続を確認するためのテストです。Skipを解除すると実際に投稿されるため、他のテストとまとめて実行しないでください。
- APIキーは環境変数で管理し、ファイルに書いてコミットしないでください。

## 関連ドキュメント

| 文書 | 内容 |
|---|---|
| [ツイート改善案](docs/tweet-content-ideas.md) | 文面の改善や、掲載する情報の追加案 |
| [インフラ管理](infra/README.md) | Terraformの構成・運用手順・今後の対応 |
