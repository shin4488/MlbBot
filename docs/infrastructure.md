# インフラのTerraform化方針

現在、AWSリソース（Lambda関数設定・IAM・EventBridge・CloudWatch Logs）はコンソール/CLIでの手動管理になっている。これをTerraformでコード管理し、インフラ変更もPRでレビュー・履歴管理できるようにする。

## 対象リソース

| リソース | 現状 |
|---|---|
| Lambda関数 `TwitterMlbBot` の設定 | ランタイム dotnet10 / メモリ512MB / タイムアウト15秒 / 環境変数（APIキー類）/ 実行ロール |
| EventBridgeルール `CronTweetMlbStandings` | `cron(0 6 * * ? *)`（毎日15:00 JST）+ Lambdaへの関連付け |
| CloudWatch Logs ロググループ | `/aws/lambda/TwitterMlbBot`（保持期間が未設定＝無期限） |
| IAM | Lambda実行ロール、デプロイ用IAMユーザー（→OIDC化でロールに置き換え予定） |

## 方針

- **state管理**: S3バックエンド（バケットのみ手動作成 or bootstrap用の最小構成で作る）
- **既存リソースの取り込み**: 再作成せず `terraform import`（または import ブロック）で現行リソースをそのまま管理下に置く
- **関数コードのデプロイとの棲み分け**: コードのデプロイは現行のGitHub Actions（`aws lambda update-function-code`）を維持し、Terraformは**設定のみ**を管理する
  - `aws_lambda_function` に `lifecycle { ignore_changes = [filename, source_code_hash, ...] }` を指定し、コード関連属性の差分を無視する
- **シークレットを漏らさない**: Lambda環境変数のAPIキー値は`.tf`ファイル・stateに直書きしない
  - 環境変数ブロックを `ignore_changes` で管理外にする（現状維持で最小コスト）か、SSM Parameter Store（SecureString）参照へ移行する
  - tfstateにはリソース属性が平文で入るため、stateバケットは非公開 + 暗号化を必須とする
- **適用方法**: まずローカルからの `terraform plan/apply` で運用開始。安定したらCI化（planをPRに貼る等）を検討

## Terraform化の完了後に対応する項目

以下はインフラ変更を伴うため、Terraform管理になってから実施する:

1. **デプロイのOIDC化**（[developer-experience.md](developer-experience.md) 項目1）… GitHub OIDCプロバイダーとデプロイ用IAMロールをTerraformで作成し、長期アクセスキーを廃止する
2. **Lambdaタイムアウト調整とリトライ導入**（[code-improvements.md](code-improvements.md) 項目4）… タイムアウト15秒→60秒程度への引き上げとセットで行う
3. **ツイート全滅を検知するCloudWatchアラーム** … 全件失敗時はLambdaがエラー終了する仕様のため、`Errors` メトリクスにアラームを張れば検知できる（通知先はSNS→メール等）
4. **CloudWatch Logsの保持期間設定** … 現状は無期限保持のため、30〜90日程度に設定してコストを抑える
