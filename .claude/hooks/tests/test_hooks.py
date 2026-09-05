import unittest
from hook_test_support import HookTestCase


class GuardTests(HookTestCase):
    def test_real_runs_and_dry_run_lookalikes_are_blocked(self):
        commands = [
            'dotnet run --project TwitterMlbBot',
            "dotnet 'run' --project TwitterMlbBot",
            '"dotnet" run --project TwitterMlbBot',
            'dotnet run --project TwitterMlbBot -- --dry-run=false',
            'dotnet run --project TwitterMlbBot -- --dry-run-backup',
            'DRY_RUN=trueish dotnet run --project TwitterMlbBot',
            'echo --dry-run; dotnet run --project TwitterMlbBot',
            'dotnet run --project TwitterMlbBot # --dry-run',
            'dotnet run --project TwitterMlbBot --configuration --dry-run',
            'dotnet run --project TwitterMlbBot -- --dry-run && dotnet run --project TwitterMlbBot',
            'dotnet TwitterMlbBot.dll',
            'bash -c "dotnet run --project TwitterMlbBot"',
        ]
        for agent in ['claude', 'codex']:
            for command in commands:
                with self.subTest(agent=agent, command=command):
                    result = self.invoke('PreToolUse', agent=agent, tool_input={'command': command})
                    self.assertEqual(result.returncode, 2, result.stderr)
        self.assertEqual(self.commands(), [])  # 投稿や dotnet を実行していない。

    def test_explicit_dry_runs_and_unrelated_commands_are_allowed(self):
        commands = [
            'dotnet run --project TwitterMlbBot -- --dry-run',
            'DRY_RUN=true dotnet run --project TwitterMlbBot',
            'env DRY_RUN=TrUe dotnet run --project TwitterMlbBot',
            'dotnet TwitterMlbBot.dll --dry-run',
            'dotnet exec TwitterMlbBot.dll --dry-run',
            'dotnet test MlbBot.sln', 'dotnet build MlbBot.sln',
            'dotnet run --project AnotherProject', 'git status',
        ]
        for agent in ['claude', 'codex']:
            for command in commands:
                with self.subTest(agent=agent, command=command):
                    result = self.invoke('PreToolUse', agent=agent, tool_input={'command': command})
                    self.assertEqual(result.returncode, 0, result.stderr)

    def test_implicit_project_run_is_blocked(self):
        self.file('TwitterMlbBot/TwitterMlbBot.csproj')
        result = self.invoke('PreToolUse', cwd=self.root / 'TwitterMlbBot', tool_input={'command': 'dotnet run'})
        self.assertEqual(result.returncode, 2)

    def test_invalid_input_is_blocked(self):
        for payload in [{}, {'tool_input': {}}, {'tool_input': {'command': None}}]:
            result = self.invoke('PreToolUse', **payload)
            self.assertEqual(result.returncode, 2)


class TerraformTests(HookTestCase):
    def test_patch_runs_fmt_and_skips_validate_before_init(self):
        path = self.file('infra/a.tf')
        result = self.invoke(tool_name='apply_patch', tool_input={'command': '*** Begin Patch\n*** Update File: infra/a.tf\n*** End Patch'})
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(['terraform', ['fmt', str(path)]], self.commands())
        self.assertFalse(any('validate' in args for _, args in self.commands()))

    def test_initialized_project_runs_validate_from_subdirectory(self):
        path = self.file('infra/a.tf')
        (self.root / 'infra/environments/prod/.terraform').mkdir(parents=True)
        result = self.invoke(cwd=self.root / 'sub', tool_input={'file_path': str(path)})
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(any('validate' in args for _, args in self.commands()))

    def test_fmt_and_validate_failures_return_feedback(self):
        path = self.file('infra/a.tf')
        (self.root / 'infra/environments/prod/.terraform').mkdir(parents=True)
        for match in ['fmt', 'validate']:
            self.env['HOOK_FAIL_MATCH'] = match
            result = self.invoke(tool_input={'file_path': str(path)})
            self.assertEqual(result.returncode, 2)
            self.assertIn('fixture diagnostic', result.stderr)

    def test_non_terraform_file_is_ignored(self):
        path = self.file('README.md')
        result = self.invoke(tool_input={'file_path': str(path)})
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.commands(), [])


if __name__ == '__main__':
    unittest.main()
