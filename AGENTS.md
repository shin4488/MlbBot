# 開発ガイド（Claude Code / Codex / Gemini共通）

MLBの順位表をXに投稿するAWS Lambdaボット。本体の入口は [Program.cs](TwitterMlbBot/Program.cs)、実行判断は [BotRunner.cs](TwitterMlbBot/BotRunner.cs)、Lambdaの入口・テストは `TwitterMlbBotExecution/`。構成図は [README](README.md#概要とアーキテクチャ)。

## 必ず守ること

- **ローカル確認はドライランのみ。** 通常モードは実投稿する。`FunctionTest` は本番の `Program.Main` を呼ぶ手動疎通専用で、**Skipを外したままテストを一括実行しない**。
- **masterへ直接pushしない。** 管理者にもbranch protectionが適用され、PRとCIの `build-and-test` 通過が必須。masterへのマージは本番デプロイにつながる。対象は [ワークフロー](.github/workflows/lambda_deploy.yml) の `paths-ignore` を確認する。
- **APIキー・環境固有値をgit管理ファイルに書かない。** 設定は環境変数、ローカルの環境固有値はGit管理外のファイルに置く。設定例は実値への変更なしでは必ず失敗する `.example` のみ。コミット前にgitleaks・git-secretsで混入を確認する。
- **`terraform apply` / `terraform destroy` はレビュー後に人間が実行する。** `make` 経由も同じ。エージェントは `plan`・`validate`・`fmt` まで。Makefileは `.env` の `TF_AWS_PROFILE` だけを読み、全体をsource・eval・includeしない。
- **`infra/` のファイルは、ユーザーが内容を確認して明示的にコミットを指示した場合だけコミットする。**
- Actionsの `uses:` を変更するときは `pin-github-actions` skillに従い、フルcommit SHAとバージョンコメントで固定する。

## 変更内容に応じて読む資料

| 変更する内容 | 読む箇所 |
| --- | --- |
| 投稿内容・文面 | [ツイート改善案](docs/tweet-content-ideas.md)の関連節 |
| 責務・投稿条件・失敗時の動作 | [開発上の判断と投稿仕様](docs/development.md)の関連節 |
| インフラ・権限・デプロイ・運用 | [infra/README](infra/README.md)の関連節。検証処理は `.github/actions/verify-dotnet/` |
| 環境構築・認証・エージェント導入 | [README](README.md#ローカルでの実行ドライラン)、`.claude/settings.json`・`.codex/hooks.json` |

## ビルド・検証

コード・依存・ビルド設定の変更時は次を実行する。文書・指示だけの変更は内容・リンクを確認する。

```bash
dotnet build MlbBot.sln
dotnet test MlbBot.sln
dotnet format MlbBot.sln
```

- SDK・依存は `global.json` と各プロジェクト定義を正とする。`Directory.Build.props` は警告もエラーにするため、依存更新時も根本原因を直す。コード変更後にformatを適用し、CIの `--verify-no-changes` を通す。
- 変更の仕上げとコミット・push・PR作成前に `verify-changes` skillで検証・機密情報・関連資料を確認する。認証・権限・外部入力の変更には `security-review` skillを使う。hookはアプリやIAM自体の防御の代わりにしない。
- ドライランは `MLB_API_KEY` のみ必要。Xの認証・送信は使わない。実投稿防止hookのため、次を単独・引用なしで実行する。

```bash
dotnet run --project TwitterMlbBot -- --dry-run
```

## 実装・テスト・文書の規約

- 規模に見合う単純な構成を保つ。層・interfaceは読みやすさや変更範囲の限定に役立つ場合だけ増やす。取得元・送信先はinterfaceで差し替え、通信しない `TweetComposer` は直接使う。
- 複雑な条件は判断の意味を表す変数にする。単純なnull/bool判定には説明用変数を重ねない。コメントは処理の言い換えでなく判断理由を書く。
- **コメント・コミットメッセージは日本語。** エラーは取得できなかった情報や業務への影響を伝え、応答コード・環境変数名は調査用の補足にする。`this.` は同名の引数との区別だけ。書式はRoslyn / dotnet formatに任せ、`.editorconfig` は最小限にする。
- 外部APIには `ApiHttpClientFactory` を使い、自動転送禁止と時間・応答サイズ上限を維持する。外部応答本文・認証情報・解析時の断片をログや例外（内部例外を含む）へ入れず、操作名・HTTPコード等だけを残す。
- 保持・公開する成績や順位は不変にする。入力辞書の内容を固定し、返却リストは読み取り専用にする。遅延評価にも注意し、一時リストや不変recordには不要なコピーを足さない。
- API応答はクライアント内のprivate recordから検証してドメイン型へ変換し、欠落した勝敗を0で補わない。成績の妥当性は `TeamStanding` 作成時に保証し、API解析側に同じ規則を重複させない。
- テストの追加・修正は `spec-based-testing` skillに従う。入力と結果を検証し、文面の細かな配置・内部実装・不要な例外型に依存させない。時刻・API応答は固定入力、外部通信はフェイク（HTTPハンドラ自体の検証のみループバック）を使う。
- 文書の処理・構成図はLR方向のMermaidでまとめ、ルールや例外は文章・表で補う。READMEの情報を落とさず、変わる値は実装・設定へリンクする。設計意図はコードコメントや関連資料に置く。自然で短い日本語を使い、引用でない文章を引用形式にしない。

## CI・エージェント固有の条件

- CIとデプロイ前の検証は共通actionを使い、その実行で検証・作成した成果物だけをデプロイする。ビルドには本番認証情報・OIDC権限を渡さず、deployではcheckoutしない。master以外の手動実行は拒否する。シェル引数は環境変数で受けて引用し、式を直接埋め込まない。
- Dependabotレビューは `review-dependabot-prs` skillに従い、OK/NGともPRに結果を残す。OKならマージ、NGなら保留する。
- 共通skillはプラグイン側で管理する。`.claude/hooks/post-edit.sh` による `.tf` の整形・初期化済みprodの検証を維持し、適用・破棄は人間が行う。
- 実投稿防止の `guard-real-run.sh` は `.claude/settings.json` と `.codex/hooks.json` の `PreToolUse` に残す。`.codex/hooks` → `.claude/hooks` の相対リンクを維持する。Claudeの権限設定はCodexに引き継がれない。

## 作業の進め方

- 関連箇所・資料・skillsに絞って読み、根拠が足りなければ調査範囲を広げる。
- 判断に必要な不明点は既存資料で確認し、解消できなければ依存する作業の前に質問する。合意済み事項は再確認しない。
- 文書の言語を保ち、読み手に自然な表現にする。
- 該当する必須検証を行い、問題を修正する。差分・依存・設定・実行条件が同じなら結果を再利用し、結果と未確認事項を簡潔に報告する。
- 継続する規約と参照先だけを残し、進捗・設定値・他の資料やskillsの手順は複製しない。
