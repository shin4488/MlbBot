#!/usr/bin/env python3
"""Codex の編集情報を Claude Code と共有するフックへ渡す。"""
import json
import os
from pathlib import Path
import subprocess
import sys


def edited_files(event, root):
    tool_input = event.get('tool_input') or {}
    tool_response = event.get('tool_response') or {}
    file_path = tool_input.get('file_path')
    if not file_path and isinstance(tool_response, dict):
        file_path = tool_response.get('filePath')
    candidates = [file_path] if file_path else []

    # Codex の apply_patch は file_path ではなくパッチ本文を渡す。
    # 削除したファイルは整形せず、移動は移動先だけを対象にする。
    if event.get('tool_name') == 'apply_patch':
        lines = tool_input.get('command', '').splitlines()
        for index, line in enumerate(lines):
            if line.startswith('*** Update File: ') and index + 1 < len(lines):
                if lines[index + 1].startswith('*** Move to: '):
                    continue
            for prefix in ('*** Add File: ', '*** Update File: ', '*** Move to: '):
                if line.startswith(prefix):
                    candidates.append(line[len(prefix):])
                    break

    cwd = Path(event.get('cwd') or os.getcwd())
    files = []
    for candidate in candidates:
        path = Path(candidate)
        path = (cwd / path).resolve() if not path.is_absolute() else path.resolve()
        # リポジトリ外への ../ やシンボリックリンクを整形対象にしない。
        if root not in path.parents or not path.is_file() or path in files:
            continue
        files.append(path)
    return files


def main():
    hooks_dir = Path(__file__).resolve().parent
    root = hooks_dir.parent.parent
    event = json.load(sys.stdin)
    script = hooks_dir / sys.argv[1]
    env = dict(os.environ, CLAUDE_PROJECT_DIR=str(root))

    if event.get('hook_event_name') == 'Stop':
        events = [event]
    else:
        events = [dict(event, tool_input={'file_path': str(path)})
                  for path in edited_files(event, root)]

    contexts = []
    status = 0
    for payload in events:
        result = subprocess.run(
            [str(script)], cwd=root, env=env, input=json.dumps(payload),
            text=True, capture_output=True,
        )
        if result.stderr:
            print(result.stderr, end='', file=sys.stderr)
        if result.returncode:
            status = 2  # 編集後の失敗をエージェントに返し、残りのファイルも確認する。
        if result.stdout.strip():
            output = json.loads(result.stdout)
            context = output.get('hookSpecificOutput', {}).get('additionalContext')
            if context:
                contexts.append(context)

    # 複数ファイル分の JSON を連結すると不正な出力になるため、1件にまとめる。
    if contexts:
        print(json.dumps({'hookSpecificOutput': {
            'hookEventName': 'PostToolUse',
            'additionalContext': '\n\n'.join(contexts),
        }}, ensure_ascii=False))
    return status


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, TypeError, AttributeError) as error:
        print(f'Codex hook: {error}', file=sys.stderr)
        sys.exit(2)
