# Claude Code / Codex の hooks

実体はこのディレクトリに置き、Codex は `.codex/hooks` の相対シンボリックリンク経由で共有する。`.agents/hooks` は自動登録先ではない。

## Codex での有効化

1. このリポジトリを作業ディレクトリとして Codex を開き、プロジェクトを信頼する。親ディレクトリや別の clone の信頼設定だけでは、このリポジトリの設定は有効にならない。
2. `.codex/config.toml` と `.codex/hooks.json` が読み込まれたら、CLI の `/hooks` でこのリポジトリ由来の定義を確認して信頼する。未承認・変更後の定義は実行されない。登録内容を変更した場合は再確認する。
3. 対象ファイルを編集し、hook の実行結果を確認する。Claude の `.claude/settings.json` の permissions は Codex に移植されない。

登録形式と信頼手順は [Codex 公式 hooks ドキュメント](https://learn.chatgpt.com/docs/hooks) を参照。Codex CLI 0.153.0 の `hooks/list` でプロジェクト定義の検出と構文を検証している。

## 入力の互換処理

Codex の `Edit|Write` matcher は `apply_patch` にも一致するが、入力はファイル名ではなく `tool_input.command` のパッチ本文になる。`codex-hook.py` が追加・更新・移動先を抽出し、既存のシェルフックへ1ファイルずつ渡す。複数ファイルの診断は1件の JSON にまとめる。

相対パスはイベントの `cwd` を基準に解決し、共有のフック本体は Git ルートで起動する。削除済み・存在しないファイル、リポジトリ外のパス、外部を指すシンボリックリンクは対象外。Claude の `file_path` / `filePath` 入力も扱う。PostToolUse は実行済みの編集を取り消すものではない。

- `guard-real-run.sh` は通常モードのローカル起動を終了コード 2 で拒否する。許可する起動は単独の `dotnet run --project TwitterMlbBot -- --dry-run`、または先頭で `DRY_RUN=true` を設定するコマンド。DLL の明示的なドライランにも対応する。
- `--dry-run=false`、コメントや別コマンド中の `--dry-run` は許可の根拠にしない。`cd ... && dotnet run ...` などは作業ディレクトリを指定し、起動部分を独立したツール呼び出しにする。
- シェルの完全な解析器ではない。別スクリプト・別名コマンド・特殊なツール経路まで実行を強制的に制限するものではなく、アプリのドライラン仕様・権限管理と併用する。
- `terraform-check.sh` は `.tf` の整形を行い、prod の `.terraform` があれば validate する。fmt / validate の失敗は終了コード 2 で返す。init / plan / apply / destroy は実行しない。
- ホストに Python 3・jq・Git・Terraform が必要。テストから実投稿や AWS 操作は行わない。

## 検証

リポジトリのルートで実行する。Python 標準ライブラリだけを使い、外部コマンドは一時ディレクトリ内のフェイクに置き換える。

```sh
python3 -B -m unittest discover -s .claude/hooks/tests -v
```

登録済みのコマンドを直接起動し、Claude / Codex の入力、複数ファイル・移動・削除、空白や引用符を含むパス、サブディレクトリ起動、対象外ファイル、外部コマンドの失敗を確認する。実際の Codex セッションでの自動発火には上記の信頼設定が別途必要。
