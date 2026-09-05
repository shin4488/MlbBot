.DEFAULT_GOAL := help

# .env全体を実行するとAPIキーまで取り込むため、必要な設定だけを文字列として読む。
TF_AWS_PROFILE ?= $(shell sed -n 's/^TF_AWS_PROFILE=//p' .env 2>/dev/null)
export TF_AWS_PROFILE

.PHONY: help check-tf-profile tf-init tf-plan tf-apply tf-fmt tf-validate tf-test

help:
	@printf '%s\n' \
	  'Terraform操作（リポジトリのルートで実行）' \
	  '  make tf-init      初回の準備・バックエンド接続' \
	  '  make tf-plan      本番環境との差分確認' \
	  '  make tf-apply     差分を確認して適用（人間が実行）' \
	  '  make tf-fmt       infra全体の書式を整える' \
	  '  make tf-validate  設定の整合性を検証' \
	  '  make tf-test      モジュールの準備・テスト（AWS接続なし）' \
	  'プロファイルは.envで設定（.env.exampleを参照）'

# 指定漏れで既定の認証情報に切り替わると、意図しない権限で実行してしまう。
check-tf-profile:
	@test -n "$$TF_AWS_PROFILE" || { printf '%s\n' '.envでTF_AWS_PROFILEを設定してください。' >&2; exit 1; }

# infra内で起動し、tfenvにもinfra/.terraform-versionを参照させる。
tf-init: check-tf-profile
	cd infra/environments/prod && AWS_PROFILE="$$TF_AWS_PROFILE" terraform init -backend-config=backend.hcl

tf-plan: check-tf-profile
	cd infra/environments/prod && AWS_PROFILE="$$TF_AWS_PROFILE" terraform plan

# 適用直前に内容を確認できるよう、Terraform標準の確認入力を残す。
tf-apply: check-tf-profile
	cd infra/environments/prod && AWS_PROFILE="$$TF_AWS_PROFILE" terraform apply

tf-fmt:
	cd infra && terraform fmt -recursive

tf-validate:
	cd infra/environments/prod && terraform validate

# 本番のバックエンドには接続せず、testsがあるモジュールだけを単独で検証する。
tf-test:
	@set -e; for tests_dir in infra/modules/*/tests; do \
	  [ -d "$$tests_dir" ] || continue; \
	  (cd "$${tests_dir%/tests}" && \
	    terraform init -backend=false -input=false && \
	    terraform test); \
	done
