"""実際の登録コマンドを、一時リポジトリと外部コマンドのフェイクで検証する。"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

REPOSITORY = Path(__file__).resolve().parents[3]


class HookTestCase(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve() / 'repo with spaces'
        self.root.mkdir()
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        shutil.copytree(REPOSITORY / '.claude/hooks', self.root / '.claude/hooks',
                        ignore=shutil.ignore_patterns('tests', '__pycache__'))
        shutil.copy(REPOSITORY / '.claude/settings.json', self.root / '.claude/settings.json')
        (self.root / '.codex').mkdir()
        (self.root / '.codex/hooks').symlink_to('../.claude/hooks')
        shutil.copy(REPOSITORY / '.codex/hooks.json', self.root / '.codex/hooks.json')
        (self.root / 'sub').mkdir()
        self.log = self.root / 'commands.jsonl'
        binary = self.root / 'fake-bin'
        binary.mkdir()
        fake = binary / 'fake'
        fake.write_text('#!' + os.sys.executable + '\n' + '''import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
with open(os.environ['HOOK_TEST_LOG'], 'a') as f:
    f.write(json.dumps([name, args]) + '\\n')
if name == 'docker' and os.environ.get('HOOK_DOCKER_DOWN'):
    print('Docker unavailable', file=sys.stderr)
    sys.exit(1)
if name == 'docker' and args[:2] == ['compose', 'ps']:
    if os.environ.get('HOOK_PS_UNCREATED'):
        print('no such service: app', file=sys.stderr)
        sys.exit(1)
    print(os.environ.get('HOOK_CONTAINER', ''))
    sys.exit(0)
match = os.environ.get('HOOK_FAIL_MATCH')
if match and match in ' '.join([name] + args):
    print('fixture diagnostic', file=sys.stderr)
    sys.exit(1)
''')
        fake.chmod(0o755)
        for tool in ['docker', 'make', 'terraform', 'bundle', 'eslint', 'prettier', 'tsc']:
            (binary / tool).symlink_to('fake')
        local_bin = self.root / 'application/frontend/node_modules/.bin'
        local_bin.mkdir(parents=True)
        for tool in ['eslint', 'prettier', 'tsc']:
            (local_bin / tool).symlink_to(binary / tool)
        self.env = dict(os.environ, PATH=str(binary) + os.pathsep + os.environ['PATH'],
                        HOOK_TEST_LOG=str(self.log))
        self.env.pop('CLAUDE_PROJECT_DIR', None)

    def file(self, name, content='fixture'):
        p = self.root / name
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(content)
        return p

    def invoke(self, event='PostToolUse', agent='codex', cwd=None, **payload):
        cwd = cwd or self.root
        config = self.root / ('.codex/hooks.json' if agent == 'codex' else '.claude/settings.json')
        command = json.loads(config.read_text())['hooks'][event][0]['hooks'][0]['command']
        data = dict(hook_event_name=event, cwd=str(cwd), **payload)
        env = dict(self.env)
        if agent == 'claude':
            env['CLAUDE_PROJECT_DIR'] = str(self.root)
        result = subprocess.run(command, shell=True, cwd=cwd, env=env, input=json.dumps(data),
                                text=True, capture_output=True, timeout=10)
        return result

    def commands(self):
        return [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []
