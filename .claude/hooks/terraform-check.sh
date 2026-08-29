#!/usr/bin/env bash
# .tfファイルのEdit/Write後に terraform fmt（自動整形）と validate を実行するPostToolUseフック。
# 標準入力でツール実行情報のJSONを受け取る。validate失敗時はexit 2でClaudeにエラー内容をフィードバックする。
set -u

file_path=$(jq -r '.tool_input.file_path // empty')

# .tfファイル以外は対象外
case "$file_path" in
  *.tf) ;;
  *) exit 0 ;;
esac

# 整形は自動適用（差分はエラーにしない）
terraform fmt "$file_path" >/dev/null 2>&1

# validateはinit済みの場合のみ実行する（clone直後の未init環境でフックが誤作動しないようにする）
prod_dir="$CLAUDE_PROJECT_DIR/infra/environments/prod"
if [ -d "$prod_dir/.terraform" ]; then
  if ! output=$(terraform -chdir="$prod_dir" validate -no-color 2>&1); then
    echo "$output" >&2
    exit 2
  fi
fi
