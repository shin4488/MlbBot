# 投稿仕様と開発ガイド

本ドキュメントでは、MLBボットの投稿スケジュール、順位表のデータ処理仕様、エラーハンドリング方針、および各コンポーネントの責務についてまとめます。

## 処理フロー

実行時の全体の流れは以下のとおりです。

```mermaid
flowchart LR
    S["実行グループ・対象日（現地前日）の決定"] --> A["シーズン日程の確認・判定"] --> B["順位データ取得・整合性検証"]
    B --> C["文面生成・文字数判定"] --> D["X（旧Twitter）へ投稿"]
```

ボットは年間を通して定期実行されますが、シーズン期間やデータの状態に応じて投稿要否を判定します。起動スケジュールは [インフラ設定](../infra/environments/prod/main.tf)、投稿種別や文面生成ルールは [TweetComposer](../TwitterMlbBot/Composing/TweetComposer.cs) で定義されています。

## 投稿対象と対象日の算出

### グループ別の投稿スケジュール

| グループ | 代表タイムゾーン | 起動時刻（現地時間） | 投稿対象 |
| --- | --- | --- | --- |
| East | America/New_York | 07:00 | AL East・NL East |
| Central | America/Chicago | 07:00 | AL Central・NL Central |
| West | America/Los_Angeles | 07:00 | AL West・NL West（表示日が8月以降はAL/NLワイルドカードも追加） |

### 算出ルールとデータ取得仕様

| 項目 | 仕様 | 詳細・具体例 |
| --- | --- | --- |
| **起動時刻** | 各代表タイムゾーンの現地時間 07:00 | サマータイム（夏時間）は EventBridge Scheduler とタイムゾーン変換で自動吸収 |
| **表示日（対象日）** | 各代表タイムゾーンの前日日付 | ・順位取得年も前日の年を使用<br>・レギュラーシーズン最終日の翌朝実行時まで前日分（最終日分）の順位を投稿<br>・**WC適用例**: Westグループの8月1日朝実行（前日7月31日分）はWCなし、8月2日朝実行（前日8月1日分）からWC追加 |
| **取得データの特性** | 起動時点でAPIから取得した最新順位 | ・前日の全試合終了や反映完了の待機・照合・再確認は行わない<br>・試合がない日や全試合延期の日でも、正常な順位データが取得できれば投稿 |
| **球団検証** | 全30球団の存在・構成を検証 | グループ別の地区に絞り込む前に全30球団の存在を検証（ワイルドカード順位も全地区のデータから算出） |

## 投稿・スキップの判定条件

| 日程・順位の状態 | 動作 | ログ記録・通知 |
| --- | --- | --- |
| レギュラーシーズン最終日まで | 順位を取得し、データが存在すれば投稿 | 通常ログ |
| シーズン終了日の翌日以降 | 順位取得・投稿をスキップ | 正常終了（ログ記録のみ） |
| 日程API・通信の失敗（シーズン中の可能性あり） | 順位取得・投稿処理を継続 | エラーログ記録・メール通知 |
| 日程API・通信の失敗（シーズン外と判断可能） | 順位取得・投稿をスキップ | 警告ログ記録・正常終了（通知なし） |
| 順位データが空 | 投稿をスキップ | 正常終了（シーズン開始前の空データも同様） |

- 日程APIのHTTPエラー・データ不正、通信障害・HTTPタイムアウトのみを回復対象とします。実装上の不具合や通常のキャンセルはそのまま伝播し、異常終了します。
- 日程情報は [MLB公式Stats API](https://statsapi.mlb.com/)（認証不要）から取得します。
- 対象年のシーズンIDで絞り込みを行い、対象データが存在しない・複数件ある・終了年の不整合がある場合は、日程取得失敗としてフォールバック判定を行います。
- 判定ロジックの詳細は [SeasonCalendar](../TwitterMlbBot/Mlb/SeasonCalendar.cs) および [BotRunner](../TwitterMlbBot/BotRunner.cs) を参照してください。

## 順位表の作成と文面生成仕様

| 項目 | 仕様・処理内容 | 関連実装 |
| --- | --- | --- |
| **不完全データの除外と球団検証** | ・sportsdata.io から取得したデータから、All-Star用の擬似チーム（リーグ名と地区名が同一）を除外<br>・球団の重複、欠落、所属リーグ・地区の不整合がないかを検証（30球団構成の担保） | [MlbApiClient.cs](../TwitterMlbBot/Mlb/MlbApiClient.cs)<br>（`ValidateTeamCoverage`） |
| **勝率・ゲーム差の算出** | ・API提供の丸め済み値は使用せず、勝敗数から算出<br>・**ゲーム差の基準**:<br>　- 地区順位: 首位チーム基準<br>　- ワイルドカード: プレーオフ圏内最終枠（当確ライン）基準 | [TeamStanding.cs](../TwitterMlbBot/Mlb/TeamStanding.cs)<br>（`GamesBehind`） |
| **公式ハッシュタグの付与** | ・チーム名と公式ハッシュタグの対応表を管理<br>・シーズンごとのタグ変更に対応 | [HashtagProvider.cs](../TwitterMlbBot/Composing/HashtagProvider.cs) |
| **文字数カウント仕様** | ・X API の仕様に準拠したカウント方式を採用<br>・結合絵文字等のカウント差異を考慮し、計算上の上限超過時でも警告ログを出力して送信を試行（実際の超過時はX API側でエラー） | [TweetContent.cs](../TwitterMlbBot/Composing/TweetContent.cs)<br>（`CharacterCount`） |

## 送信制御とエラーハンドリング

**Xへの投稿リクエスト後、レスポンス受信に失敗した場合でも実際には投稿が完了している可能性があるため、二重投稿防止の観点から自動再送は行いません。**

| 状況 | 動作仕様 |
| --- | --- |
| 複数ツイートの連続投稿 | 連続リクエストによるレート制限・一時エラー（503）を回避するため、投稿間に待機時間を設ける（最終ツイート後は待機なし） |
| 一部のツイート送信失敗（HTTPエラー・通信障害・HTTPタイムアウト） | 当該ツイートのエラーを記録し、残りのツイート送信を継続 |
| 送信処理の不具合・通常のキャンセル | 成功済みの投稿があっても例外をそのまま伝播し、残りの投稿を中止 |
| 全ツイート送信失敗 | `AllTweetsFailedException` をスローして異常終了し、CloudWatchアラーム経由でSNS通知 |

- 送信間隔の制御は [TwitterApiSender](../TwitterMlbBot/Twitter/TwitterApiSender.cs)、全体の実行制御は [BotRunner](../TwitterMlbBot/BotRunner.cs) を参照してください。
- Lambdaの自動再試行もインフラ設定側で無効化（0回）しています。将来的に再実行機能を導入する場合は、アプリケーション側での重複排除の仕組みが必須となります。
- OAuth 1.0a 認証用のタイムスタンプは、時計の微小な進みによる認証エラーを防ぐため秒未満を切り捨てて生成します（[OAuth1](../TwitterMlbBot/Authorization/OAuth1.cs) 参照）。

## コンポーネント構成と責務

| ディレクトリ / クラス | 主な責務 |
| --- | --- |
| [Program.cs](../TwitterMlbBot/Program.cs) | アプリケーションのエントリポイント。DIコンテナの構成、外部クライアントの初期化とリソース破棄を管理 |
| [BotRunner.cs](../TwitterMlbBot/BotRunner.cs) | 実行フローの制御。日程判定、順位取得、文面生成、投稿実行、およびエラーハンドリングの統括 |
| [Mlb/](../TwitterMlbBot/Mlb/) | 外部API（Stats API、sportsdata.io）との通信、データパース、球団構成検証 |
| [TeamStanding.cs](../TwitterMlbBot/Mlb/TeamStanding.cs) | 球団成績データモデル。勝率およびゲーム差の計算処理 |
| [Composing/](../TwitterMlbBot/Composing/) | 投稿文面の組み立て、ハッシュタグ付与、文字数カウント |
| [Twitter/](../TwitterMlbBot/Twitter/) | X API への投稿クライアント（実投稿用およびドライラン用） |

データ取得および送信処理はインターフェースで抽象化されており、外部通信を伴わない単体テストが可能です。処理間の詳細な依存関係は [README.md](../README.md#プログラム構成) を参照してください。

## 正常系・例外系の実行シーケンス

外部APIへの通信には共通の [ApiHttpClientFactory](../TwitterMlbBot/ApiHttpClientFactory.cs) を使い、転送禁止・タイムアウト・応答サイズ上限を適用します。

```mermaid
sequenceDiagram
    autonumber
    actor Entry as 起動元（Lambda / CLI）
    participant P as Program / RunOptions
    participant B as BotRunner
    participant C as MlbStatsApiClient
    participant S as MlbApiClient
    participant T as TweetComposer
    participant X as 投稿先実装
    participant API as 外部API

    Note over Entry,P: Lambdaはイベントを読み取り、Functionからgroupを渡す<br/>CLIは起動引数を渡す。イベントJSONの型不正はハンドラ実行前に失敗
    Entry->>P: Main（引数）
    P->>P: 引数・投稿グループ・対象年を検証<br/>グループごとの現地前日を表示日にする
    break 引数不正・通常実行のグループ未指定
        P-->>Entry: ArgumentExceptionで異常終了
    end
    P->>P: 必須環境変数を確認し、依存先を組み立てる
    Note over P,X: 通常実行はMLB・Xの認証情報を要求<br/>ドライランはMLBのみ要求し、DryRunTweetSenderを選ぶ
    break 必須環境変数が未設定・空文字
        P-->>Entry: InvalidOperationExceptionで異常終了
    end

    loop 実行グループごと（通常は1つ、グループ未指定のドライランは3つ）
        P->>B: RunAsync（年・表示日・グループ）
        B->>C: GetSeasonCalendarAsync（年）
        C->>API: Stats APIへ日程を要求
        API-->>C: HTTP応答、または通信例外
        C->>C: 成功応答ならJSON解析・対象シーズン・終了日を検証
        Note over C: HTTP不成功・JSON不正・応答null・対象シーズン0件や複数件・終了日欠落や年不一致はMlbApiException<br/>JSON解析では年・解析位置を残し、元の例外は保持しない。通信例外はそのまま伝播
        break 日程取得の不具合・通常のキャンセル
            C-->>B: 想定したAPI・通信の失敗以外の例外
            B-->>P: 元の例外をそのまま伝播
            P-->>Entry: 季節によらず異常終了
        end
        alt 日程取得・検証が成功
            C-->>B: SeasonCalendar
            B->>B: 表示日がシーズン終了日を過ぎていればスキップ判定<br/>終了日以前なら続行判定
        else 日程API・通信の失敗（HTTPタイムアウトを含む）
            C-->>B: 例外
            Note over B: MlbApiException・通信障害・HTTPタイムアウトだけを捕捉
            alt 表示日が11〜2月
                B->>B: 元の例外を警告ログに残し、スキップ判定
            else 表示日が3〜10月
                B->>B: 元の例外をエラーログに残し、続行判定
            end
        end

        alt スキップ判定
            B-->>P: 投稿せずグループ処理を正常終了
        else 続行判定
            B->>S: GetStandingsAsync（年）
            S->>API: sportsdata.ioへ順位を要求（キーはヘッダー）
            API-->>S: HTTP応答、または通信例外
            S->>S: 成功応答ならParseStandings（年・応答）
            Note over S: JSON解析 → null要素の拒否 → All-Star擬似チームの除外<br/>勝敗の欠落確認 → TeamStanding生成 → 球団名重複・地区構成の検証<br/>空配列は正常。球団名の実在・正しい所属との照合は行わない
            break 順位の取得・解析・検証が失敗
                Note over S: HTTP不成功はMlbApiException<br/>JSON不正・欠落・構成不正はInvalidOperationException<br/>TeamStandingの名前・所属の空値、負の勝敗はArgumentException<br/>JSON解析では年・解析位置を残し、元の例外は保持しない
                S-->>B: 例外（通信例外を含め、取得側で包み直さない）
                B-->>P: 例外を伝播
                P-->>Entry: 投稿せず異常終了（後続グループも実行しない）
            end
            S-->>B: 読み取り専用のチーム成績一覧
            B->>T: ComposeTweets（全成績・表示日・グループ）
            T->>T: 地区別に順位・ゲーム差を算出し、対象地区の文面を生成<br/>ハッシュタグを付与
            opt Westかつ表示日が8月以降
                T->>T: 全地区から地区首位を除いてWC順位を算出<br/>AL・NLのWC文面を地区文面の後に追加
            end
            T-->>B: 読み取り専用の投稿文面一覧
            alt 文面が0件（空順位）
                B->>B: 投稿しない旨を通常ログに記録
                B-->>P: グループ処理を正常終了
            else 文面が1件以上
                loop 文面を順に処理
                    opt Xの数え方（英字は1、日本語・絵文字は2）で文字数上限を超える可能性
                        B->>B: 警告ログを記録（送信は試みる）
                    end
                    B->>X: TrySendAsyncからSendAsyncを呼ぶ
                    alt ドライラン
                        X->>X: 文面と文字数を出力（Xへ通信しない）
                    else 通常投稿
                        X->>X: 2件目以降の送信前に間隔を空ける<br/>OAuth署名・JSON本文を生成
                        X->>API: X APIへ投稿
                        API-->>X: HTTP応答、または通信例外
                    end
                    break 送信処理の不具合・通常のキャンセル（署名生成・出力など）
                        X-->>B: 想定した通信障害以外の例外
                        B-->>P: 元の例外をそのまま伝播
                        P-->>Entry: 異常終了（残りの投稿・グループは実行しない）
                    end
                    alt 通信障害・HTTPタイムアウト
                        X-->>B: 例外
                        B->>B: 元の例外をエラーログに残し、1件失敗とする
                    else XがHTTP不成功応答を返す
                        X->>X: 応答コードを警告ログに記録（本文は残さない）
                        X-->>B: false（1件失敗）
                    else XがHTTP成功応答、またはドライラン出力成功
                        X-->>B: true
                        B->>B: 成功件数を加算
                    end
                    Note over B,API: 失敗した文面は再送せず、残りの文面へ進む<br/>応答を受け取れなくても投稿済みの可能性がある
                end
                B->>B: 成功件数と総件数を通常ログに記録
                break 成功が0件
                    B-->>P: AllTweetsFailedException（個別原因は直前のログ）
                    P-->>Entry: 異常終了（後続グループも実行しない）
                end
                B-->>P: 1件以上成功ならグループ処理を正常終了
            end
        end
    end
    P-->>Entry: 正常終了
```

図中の捕捉箇所以外で発生した例外（タイムゾーンの取得・文面生成など）は、`Program`・`Function` でも捕捉せず異常終了します。日程取得と1件の送信では回復対象の例外だけを捕捉します。HTTPタイムアウトは内部例外が `TimeoutException` の `OperationCanceledException` で判別し、通常のキャンセルは伝播させます。Lambdaでは異常終了がエラーメトリクス、エラーログがログ監視の対象となり、通知は [運用設定](../infra/README.md) に従います。
