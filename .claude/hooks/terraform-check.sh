#!/usr/bin/env bash
# Claude / Codex 共通。.tf の整形と、初期化済み環境の検証だけを行う。
source "$(dirname "$0")/edited-files.sh"
terraform_files=()
for file in "${files[@]}"; do
  case "$file" in *.tf) terraform_files+=("$file") ;; esac
done
[ "${#terraform_files[@]}" -gt 0 ] || exit 0

status=0
for file in "${terraform_files[@]}"; do
  terraform fmt "$file" >&2 || status=2
done

# clone 直後に init を要求しない。複数ファイルの編集でも validate は1回だけ。
prod_dir="$project_dir/infra/environments/prod"
if [ -d "$prod_dir/.terraform" ]; then
  terraform -chdir="$prod_dir" validate -no-color >&2 || status=2
fi
exit "$status"
