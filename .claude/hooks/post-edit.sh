#!/usr/bin/env bash
# 共通プラグインから、リポジトリルートを作業ディレクトリとして呼ばれる。
# 編集先のディレクトリで検証する共通処理ではなく、共有モジュールもprodで検証する。
project_dir=$PWD
has_terraform_files=false
status=0
for file; do
  case "$file" in
    *.tf)
      has_terraform_files=true
      # infra配下のバージョン指定を使い、編集していないファイルには整形差分を作らない。
      (cd "${file%/*}" && terraform fmt "$file") >&2 || status=2
      ;;
  esac
done
$has_terraform_files || exit 0

# modules配下の変更も、実際にそれらを使うprodの構成で検証する。
# clone直後のhookがprovider取得・backend初期化を始めないよう、initは自動実行しない。
prod_dir="$project_dir/infra/environments/prod"
if [ -d "$prod_dir/.terraform" ]; then
  terraform -chdir="$prod_dir" validate -no-color >&2 || status=2
fi
exit "$status"
