#!/usr/bin/env bash
# Claude / Codex 共通。シェルを実行・展開せず、単独のドライランだけを許可する。
deny() {
  echo '実投稿を防ぐため拒否しました。単独の dotnet run --project TwitterMlbBot -- --dry-run を使用してください。' >&2
  exit 2
}

input=$(cat)
command=$(jq -er '.tool_input.command | strings' <<< "$input") || deny
cwd=$(jq -r '.cwd // empty' <<< "$input") || deny
cwd=${cwd:-$PWD}

# 引用・エスケープされた起動も見落とさない。これは検出専用で、許可判定には使わない。
plain=$(tr -d "'\"\\\\" <<< "$command")
case "$plain" in
  *dotnet*run*)
    [[ "$plain" == *TwitterMlbBot* || -f "$cwd/TwitterMlbBot.csproj" ]] || exit 0 ;;
  *TwitterMlbBot.dll*) ;;
  *) exit 0 ;;
esac

# 引用・展開・複合コマンドを解析する代わりに、単純な表記に限定する。
simple_command='^[a-zA-Z0-9_./=[:blank:]-]+$'
[[ "$command" =~ $simple_command ]] || deny
read -r -a words <<< "$command"
set -- "${words[@]}"
[ "${1-}" != env ] || shift

dry_run=false
while [[ "${1-}" == *=* ]]; do
  case "$1" in
    DRY_RUN=*) dry_run=false; [[ "$1" =~ ^DRY_RUN=[Tt][Rr][Uu][Ee]$ ]] && dry_run=true ;;
  esac
  shift
done
[[ "${1##*/}" == dotnet ]] || deny

case "${2-}" in
  run)
    shift 2
    # dotnet 自身のオプション値を、アプリの --dry-run と取り違えない。
    while [ "$#" -gt 0 ] && [ "$1" != -- ]; do
      # CLI 側で環境変数を上書きする起動には、明示的な --dry-run を要求する。
      case "$1" in -e* | --environment*) dry_run=false ;; esac
      shift
    done
    [ "$#" -eq 0 ] || shift ;;
  *TwitterMlbBot.dll) shift 2 ;;
  exec) [[ "${3-}" == *TwitterMlbBot.dll ]] || deny; shift 3 ;;
  *) deny ;;
esac
$dry_run && exit 0
for arg; do
  [ "$arg" != --dry-run ] || exit 0
done
deny
