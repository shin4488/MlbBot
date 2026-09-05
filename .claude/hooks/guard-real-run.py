#!/usr/bin/env python3
"""ボットのローカル起動は、確認できる単一のドライランコマンドだけ許可する。"""
import json
import os
from pathlib import Path
import re
import shlex
import sys


def is_allowed(event):
    command = event['tool_input']['command']
    if not isinstance(command, str):
        return False
    cwd = Path(event.get('cwd') or os.getcwd())
    tokens = shlex.split(command, comments=True)
    runs_dotnet = re.search(r'\bdotnet\s+run\b', command) or any(
        Path(token).name == 'dotnet' and tokens[index + 1] == 'run'
        for index, token in enumerate(tokens[:-1])
    )
    mentions_bot = 'TwitterMlbBot' in command
    implicit_project = (cwd / 'TwitterMlbBot.csproj').is_file()
    if not ((runs_dotnet and (mentions_bot or implicit_project))
            or 'TwitterMlbBot.dll' in command):
        return True

    # シェル構文を実行・展開して判定しない。複合コマンドや展開が必要な場合は
    # ドライランの起動を独立したツール呼び出しに分けてもらう。
    if any(character in command for character in ';|&<>\n`$()'):
        return False
    if tokens and tokens[0] == 'env':
        tokens.pop(0)
    dry_run_env = False
    while tokens and re.match(r'^[A-Za-z_][A-Za-z0-9_]*=', tokens[0]):
        name, value = tokens.pop(0).split('=', 1)
        if name == 'DRY_RUN':
            dry_run_env = value.lower() == 'true'
    if not tokens or Path(tokens[0]).name != 'dotnet':
        return False
    if len(tokens) < 2:
        return False
    if tokens[1] == 'run':
        # dotnet 自身のオプション値やプロジェクト名をアプリの引数と誤認しない。
        app_args = tokens[tokens.index('--') + 1:] if '--' in tokens else []
    elif tokens[1].endswith('TwitterMlbBot.dll'):
        app_args = tokens[2:]
    elif len(tokens) > 2 and tokens[1] == 'exec' and tokens[2].endswith('TwitterMlbBot.dll'):
        app_args = tokens[3:]
    else:
        return False
    return dry_run_env or '--dry-run' in app_args


if __name__ == '__main__':
    try:
        allowed = is_allowed(json.load(sys.stdin))
    except (OSError, ValueError, KeyError, TypeError, AttributeError):
        allowed = False
    if not allowed:
        print('実投稿を防ぐため実行を拒否しました。ローカル起動は単独の '
              'dotnet run --project TwitterMlbBot -- --dry-run を使用してください。',
              file=sys.stderr)
        sys.exit(2)
