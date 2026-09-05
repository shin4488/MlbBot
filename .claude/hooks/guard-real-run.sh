#!/usr/bin/env bash
# Claude Code / Codex 共通。入力不正や安全と確認できない起動は exit 2 で拒否する。
exec python3 "$(dirname "$0")/guard-real-run.py"
