#!/usr/bin/env bash
# Claude / Codex の編集情報を、共有フックで使う files 配列に揃える。
input=$(cat)
project_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P) || exit 2
event_cwd=$(jq -r '.cwd // empty' <<< "$input") || exit 2
event_cwd=${event_cwd:-$PWD}

paths=$(jq -c '[
  if .tool_name == "apply_patch" then
    (.tool_input.command | split("\n")) as $lines
    | range(0; $lines | length) as $i
    | select(($lines[$i + 1] // "" | startswith("*** Move to: ")) | not)
    | $lines[$i]
    | capture("^\\*\\*\\* (?:Add File|Update File|Move to): (?<path>.+)$").path
  else
    .tool_input.file_path // .tool_response.filePath // empty
  end
] | unique' <<< "$input") || exit 2

files=()
while IFS= read -r -d '' path; do
  case "$path" in /*) ;; *) path="$event_cwd/$path" ;; esac
  [ -f "$path" ] || continue
  # ../ とシンボリックリンクを解決してから、リポジトリ外を除外する。
  # 末尾の改行を含むファイル名も、コマンド置換で欠けないようにする。
  path=$(realpath "$path" && printf '.') || exit 2
  path=${path%$'\n.'}
  case "$path" in "$project_dir"/*) files+=("$path") ;; esac
done < <(jq -j '.[] | ., "\u0000"' <<< "$paths")
