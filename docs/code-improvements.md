# コード改善提案（保守性・高凝集・疎結合）

現在の構造は [README](../README.md) のアーキテクチャ図（内部構造）を参照。残っている改善候補を優先度順に記す。

## 1. リトライの導入（Terraform化後に対応）

### 現状の問題

- X API・MLB APIの一時的エラー（429/503等）に対するリトライがなく、単発の失敗がそのままツイート欠落になる
- ツイートの部分失敗はログ出力のみで検知手段がない（全件失敗の場合のみLambda実行がエラー終了する）

### 提案

1. 一時的エラーにリトライを入れる。手書きでもよいが `Polly` を使うと簡潔（`WaitAndRetryAsync(3回, 指数バックオフ)`）。
2. リトライ分の実行時間を確保するため、Lambdaタイムアウト（現状15秒）を60秒程度へ引き上げる。

**対応時期**: Lambdaタイムアウト変更というインフラ設定変更を伴うため、**Terraform化（[docs/infrastructure.md](infrastructure.md)）が完了してから**実施する。

## 2. ロギングの整備

### 現状の問題

- `Console.WriteLine` 直書きでログレベルの概念がなく、ログの重要度をフィルタできない

### 提案

`Microsoft.Extensions.Logging` を導入し、`ILogger<T>` を各クラスのコンストラクタで受け取る（Lambda環境ではConsoleロガーで十分。CloudWatchに流れる）。

```csharp
logger.LogInformation("MLB standings fetched: {TeamCount} teams for {Year}", teams.Count, year);
logger.LogError("Tweet failed: {StatusCode} {Body}", response.StatusCode, body);
```

## 3. テストの拡充（残り）

- `OAuth1.CreateSignature`（署名生成本体）の検証。タイムスタンプ・nonceが内部生成でテストから固定できないため、注入可能にするリファクタとセットで行う
- `xunit.v3` への移行検討（パッケージ名・名前空間が変わる大型移行）
