"""Codex の公開 hook 入力から、共有 hook に渡されるファイルと結果を検証する。"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[1]


class CodexAdapterTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve() / 'workspace with spaces'
        self.hooks = self.root / '.claude/hooks'
        self.hooks.mkdir(parents=True)
        shutil.copy(SOURCE / 'codex-hook.py', self.hooks)
        (self.root / '.codex').mkdir()
        (self.root / '.codex/hooks').symlink_to('../.claude/hooks')
        self.log = self.root / 'calls.jsonl'
        fake = self.hooks / 'fake-hook'
        fake.write_text('#!' + os.sys.executable + '\n' + '''import json, os, sys
p = json.load(sys.stdin)
with open(os.environ['HOOK_TEST_LOG'], 'a') as f:
    f.write(json.dumps({'payload': p, 'cwd': os.getcwd(), 'root': os.environ['CLAUDE_PROJECT_DIR']}) + '\\n')
path = p.get('tool_input', {}).get('file_path', '')
if 'failure' in path:
    print('formatter failure', file=sys.stderr)
    sys.exit(2)
if path:
    print(json.dumps({'hookSpecificOutput': {'hookEventName': 'PostToolUse', 'additionalContext': path}}))
''')
        fake.chmod(0o755)
        (self.root / 'sub').mkdir()
        self.env = dict(os.environ, HOOK_TEST_LOG=str(self.log))
        self.env.pop('CLAUDE_PROJECT_DIR', None)

    def file(self, name):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('fixture')
        return path

    def invoke(self, event, cwd=None):
        result = subprocess.run(
            ['python3', str(self.root / '.codex/hooks/codex-hook.py'), 'fake-hook'],
            input=json.dumps(event), text=True, capture_output=True,
            env=self.env, cwd=cwd or self.root, timeout=10,
        )
        calls = [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []
        return result, calls

    def test_claude_edit_and_response_paths_remain_supported(self):
        path = self.file('sub/a "quoted" file.ts')
        for event in ({'tool_input': {'file_path': str(path)}}, {'tool_response': {'filePath': str(path)}}):
            result, calls = self.invoke(event)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(calls[-1]['payload']['tool_input']['file_path'], str(path))

    def test_patch_formats_add_update_and_move_destination_only(self):
        paths = [self.file(name) for name in ['add.ts', 'update.ts', 'moved.ts']]
        self.file('old.ts')  # 移動元が別用途で残っていても対象にしない。
        self.file('deleted.ts')
        patch = '\n'.join(['*** Begin Patch', '*** Add File: add.ts', '+content',
            '*** Update File: update.ts', '@@', '-old', '+new',
            '*** Update File: old.ts', '*** Move to: moved.ts', '@@', '-old', '+new',
            '*** Delete File: deleted.ts', '*** End Patch'])
        result, calls = self.invoke({'tool_name': 'apply_patch', 'tool_input': {'command': patch}})
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual([c['payload']['tool_input']['file_path'] for c in calls], list(map(str, paths)))
        context = json.loads(result.stdout)['hookSpecificOutput']['additionalContext']
        for path in paths:
            self.assertIn(str(path), context)

    def test_subdirectory_session_resolves_relative_paths_and_sets_project_root(self):
        path = self.file('sub/file.ts')
        event = {'cwd': str(self.root / 'sub'), 'tool_name': 'apply_patch',
                 'tool_input': {'command': '*** Begin Patch\n*** Update File: file.ts\n*** End Patch'}}
        result, calls = self.invoke(event, cwd=self.root / 'sub')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(calls[0]['payload']['tool_input']['file_path'], str(path))
        self.assertEqual(calls[0]['root'], str(self.root))
        self.assertEqual(calls[0]['cwd'], str(self.root))

    def test_missing_external_and_external_symlink_paths_are_not_modified(self):
        outside = self.root.parent / 'outside.ts'
        outside.write_text('outside')
        (self.root / 'link.ts').symlink_to(outside)
        for path in ['missing.ts', '../outside.ts', 'link.ts', str(outside)]:
            result, calls = self.invoke({'tool_input': {'file_path': path}})
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(calls, [])
        self.assertEqual(outside.read_text(), 'outside')

    def test_patch_content_cannot_inject_an_extra_file(self):
        path = self.file('real.ts')
        self.file('decoy.ts')
        patch = '*** Begin Patch\n*** Update File: real.ts\n@@\n+*** Update File: decoy.ts\n*** End Patch'
        result, calls = self.invoke({'tool_name': 'apply_patch', 'tool_input': {'command': patch}})
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual([c['payload']['tool_input']['file_path'] for c in calls], [str(path)])

    def test_failure_is_reported_and_other_files_are_still_checked(self):
        self.file('failure.ts')
        second = self.file('second.ts')
        event = {'tool_name': 'apply_patch', 'tool_input': {'command':
                 '*** Begin Patch\n*** Update File: failure.ts\n*** Update File: second.ts\n*** End Patch'}}
        result, calls = self.invoke(event)
        self.assertEqual(result.returncode, 2)
        self.assertIn('formatter failure', result.stderr)
        self.assertIn(str(second), json.loads(result.stdout)['hookSpecificOutput']['additionalContext'])
        self.assertEqual(len(calls), 2)

    def test_stop_preserves_loop_prevention_flag(self):
        event = {'hook_event_name': 'Stop', 'stop_hook_active': True}
        result, calls = self.invoke(event)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(calls[0]['payload'], event)

    def test_invalid_json_returns_feedback(self):
        result = subprocess.run(['python3', str(self.hooks / 'codex-hook.py'), 'fake-hook'],
                                input='{', text=True, capture_output=True, timeout=10)
        self.assertEqual(result.returncode, 2)
        self.assertFalse(self.log.exists())


if __name__ == '__main__':
    unittest.main()
