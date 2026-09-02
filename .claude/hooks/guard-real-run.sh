#!/usr/bin/env bash
# Bashツール実行前（PreToolUse）に、ボットを「実ツイートする通常モード」でローカル実行しようとする
# コマンドを止めるフック。通常送信はLambdaの定期実行経由のみとし、ローカルはドライラン限定にする運用を
# Claude側の操作ミスから機械的に守る（環境変数に認証情報がある環境では、1回の実行で実際に投稿されてしまう）。
# 標準入力でツール実行情報のJSONを受け取り、exit 2 で実行を拒否し理由をClaudeへ返す。
set -u

command=$(jq -r '.tool_input.command // empty')

# ボット本体の実行コマンド以外は対象外（ビルド・テスト・他プロジェクトの dotnet run は通す）
case "$command" in
  *"dotnet run"*TwitterMlbBot*|*TwitterMlbBot.dll*) ;;
  *) exit 0 ;;
esac

# ドライラン指定（引数 --dry-run、または環境変数 DRY_RUN=true）があれば通す
case "$command" in
  *--dry-run*|*DRY_RUN=true*|*DRY_RUN=TRUE*) exit 0 ;;
esac

cat >&2 <<'MSG'
ボットの通常送信（実ツイート）をローカルで実行するコマンドのため拒否しました。
ローカルでの実行はドライラン限定です: dotnet run --project TwitterMlbBot -- --dry-run
MSG
exit 2
