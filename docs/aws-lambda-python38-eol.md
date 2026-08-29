# AWS Lambda Python 3.8 EOL通知への対応（Sample関数）

2026-08-29 に AWS Health から届いた「AWS Lambda Python 3.8 のサポート終了」通知
（アカウント 725977920676 / ap-northeast-1）への対応記録。

## 調査結果（2026-08-29 実施）

- python3.8 ランタイムの関数は **`Sample` の1つだけ**。us-east-1 / us-west-2 / ap-northeast-3 に該当関数はなし。公開バージョンもなし（$LATESTのみ）。
- `Sample` の最終更新は **2020-08-01**。実体は現行の `TwitterMlbBot` 関数（dotnet6）に置き換えられる前の**本ボットのプロトタイプ**:
  - ハンドラ `executer.py`（11行・標準ライブラリのみ）が、同梱の `netcoreapp3.1/TwitterMlbBot.exe`（CoreTweet時代の旧実装）を subprocess で起動するだけ。
  - トリガー・リソースポリシー・イベントソースマッピングは一切なし。**どこからも呼ばれていない**。
  - 対応するソースコードはGitHub上のどのリポジトリにも存在しない（コンソールから直接アップロードされたもの）。
- ⚠️ デプロイパッケージ内に**当時の App.config 相当ファイルが平文のまま同梱**されている（sportsdata.io APIキー、Twitter consumer/access キー）。値はここには記載しない。

## 対応方針

### 推奨: 関数を削除する

未使用で、旧キーを平文で抱え込んでいるため、削除が最もクリーン。通知の根本原因も消える。

```bash
aws lambda delete-function --function-name Sample --region ap-northeast-1
```

削除する場合、同梱キーが現行の稼働キーと同一であればキーのローテーションも検討すること。

### 残す場合: ランタイムを python3.14 に更新する

2026-08-29 時点でGAの最新 Python ランタイムは **python3.14**（非推奨開始 2029-06-30）。
python3.15 はパブリックプレビューのため本番利用不可。
なお Python に LTS という区分はなく（全バージョン5年サポート）、Lambda が安定版のみ提供する方針。

```bash
aws lambda update-function-configuration --function-name Sample --runtime python3.14 --region ap-northeast-1
```

`executer.py` は標準ライブラリのみで 3.8 → 3.14 の非互換なし。コード・パッケージの変更は不要
（そもそも呼び出し元がないため、実行互換性は実質問題にならない）。

## 関連

- 現行の `TwitterMlbBot` 関数（dotnet6）も既に非推奨で **2027-03-03 に関数更新がブロック**される。
  移行計画は [dependency-upgrades.md](dependency-upgrades.md) を参照。
