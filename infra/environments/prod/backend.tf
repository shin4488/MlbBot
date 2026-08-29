# tfstateはS3バックエンドで管理する（stateにはLambda環境変数の値が平文で入るため、
# バケットは非公開・暗号化・バージョニング必須。リポジトリにはコミットしない）。
# バケット名などの接続情報は環境固有のためgitignore対象のbackend.hclで渡す。
#
# 初回セットアップ: backend.hcl.example を backend.hcl にコピーして実値を記入し、
#   terraform init -backend-config=backend.hcl
terraform {
  backend "s3" {}
}
