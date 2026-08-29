# フレームワーク・ライブラリのバージョンアップ計画

現状の依存関係の棚卸しと、バージョンアップの具体的な作業手順。
バージョン情報は 2026-07-07 時点で NuGet / AWS公式ドキュメント / GitHub Releases を実際に照会して確認したもの。

## サマリ（現状 → 推奨）

| 対象 | 現状 | 推奨 | 種別 | リスク |
|---|---|---|---|---|
| .NET (TFM) | net6.0 | **net10.0** | ランタイム | 中 |
| Lambda ランタイム | dotnet6 | **dotnet10** | インフラ | 中 |
| Amazon.Lambda.Core | 2.1.0 | 3.1.1 | メジャー | 小 |
| Amazon.Lambda.Serialization.SystemTextJson | 2.3.1 | 3.0.0 | メジャー | 小 |
| System.Configuration.ConfigurationManager | 4.7.0 | 削除（暫定なら10.0.9） | — | 小 |
| Microsoft.NET.Test.Sdk | 15.5.0 | 18.7.0 | メジャー | 小 |
| xunit | 2.4.2 | 2.9.3 | マイナー | 小 |
| xunit.runner.visualstudio | 2.4.5 | 3.1.5 | メジャー | 小 |
| Amazon.Lambda.TestUtilities | 2.0.0 | 4.1.0 | メジャー | 小 |
| dotnet-lambda-test-tool-6.0 | 6.0用 | Amazon.Lambda.TestTool 1.0.0 | ツール | 小 |

## 全体の進め方（PR分割）

| # | PR | 含める作業 | 備考 |
|---|---|---|---|
| PR | .NET 10化一式 | 項目1〜4すべて（csproj 3つ + workflow + launch.json + tools-defaults） | **分割しない**こと。TFMとLambdaパッケージは片方だけだとビルドが通らない |
| 手動作業 | Lambdaランタイム変更 | 項目1の手順4 | **PRマージの直前**に実施（順序の理由は項目1参照） |
| 任意 | global.json追加 / TestTool刷新 | 項目5・6 | いつでも可 |

期限: dotnet6ランタイムは**2027-02-01に関数更新ブロック**（デプロイパイプラインが失敗するようになる）。余裕をもって2026年内に.NET 10化を完了させる。

---

## 1. .NET 6 → .NET 10（最重要・期限あり）

### なぜ .NET 8 ではなく 10 か

AWS Lambda公式ドキュメントのランタイム非推奨スケジュール（2026-07-07確認）:

| ランタイム | 非推奨開始 | 関数更新ブロック |
|---|---|---|
| dotnet6 | **2024-12-20（既に非推奨）** | 2027-02-01 |
| dotnet8 | **2026-11-10（あと4か月）** | 2027-02-01以降 |
| dotnet10 | 提供中（.NET 10はLTS、サポートは2028-11まで） | — |

.NET 8 に上げても4か月後に再び非推奨が始まるため、net10.0 / dotnet10 へ直接移行する。

### 手順1: ローカルに .NET 10 SDK をインストール

```bash
brew install --cask dotnet-sdk
dotnet --list-sdks    # 10.0.x が表示されること（既存の6.0.408と共存する）
```

### 手順2: TFMとパッケージの書き換え（3ファイル）

[TwitterMlbBot/TwitterMlbBot.csproj](../TwitterMlbBot/TwitterMlbBot.csproj):

```diff
-    <TargetFramework>net6.0</TargetFramework>
+    <TargetFramework>net10.0</TargetFramework>
   ...
-    <PackageReference Include="System.Configuration.ConfigurationManager" Version="4.7.0" />
+    <PackageReference Include="System.Configuration.ConfigurationManager" Version="10.0.9" />
```

[TwitterMlbBotExecution/src/TwitterMlbBotExecution/TwitterMlbBotExecution.csproj](../TwitterMlbBotExecution/src/TwitterMlbBotExecution/TwitterMlbBotExecution.csproj):

```diff
-    <TargetFramework>net6.0</TargetFramework>
+    <TargetFramework>net10.0</TargetFramework>
   ...
-    <PackageReference Include="Amazon.Lambda.Core" Version="2.1.0" />
-    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.3.1" />
+    <PackageReference Include="Amazon.Lambda.Core" Version="3.1.1" />
+    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="3.0.0" />
```

[TwitterMlbBotExecution/test/TwitterMlbBotExecution.Tests/TwitterMlbBotExecution.Tests.csproj](../TwitterMlbBotExecution/test/TwitterMlbBotExecution.Tests/TwitterMlbBotExecution.Tests.csproj):

```diff
-    <TargetFramework>net6.0</TargetFramework>
+    <TargetFramework>net10.0</TargetFramework>
   ...
-    <PackageReference Include="Amazon.Lambda.Core" Version="2.1.0" />
-    <PackageReference Include="Amazon.Lambda.TestUtilities" Version="2.0.0" />
-    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="15.5.0" />
-    <PackageReference Include="xunit" Version="2.4.2" />
-    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
+    <PackageReference Include="Amazon.Lambda.Core" Version="3.1.1" />
+    <PackageReference Include="Amazon.Lambda.TestUtilities" Version="4.1.0" />
+    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
+    <PackageReference Include="xunit" Version="2.9.3" />
+    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
```

書き換え後、ローカルで確認:

```bash
dotnet build MlbBot.sln    # 警告・エラーなしで通ること
dotnet test MlbBot.sln     # 単体テストが全件パスすること（実ツイートするFunctionTestはSkip指定のため安全）
```

### 手順3: 設定ファイル類の書き換え（3ファイル）

[aws-lambda-tools-defaults.json](../TwitterMlbBotExecution/src/TwitterMlbBotExecution/aws-lambda-tools-defaults.json):

```diff
-  "function-runtime": "dotnet6",
+  "function-runtime": "dotnet10",
```

ワークフローの `dotnet-version`（[lambda_deploy.yml](../.github/workflows/lambda_deploy.yml) のverify/deployの2ジョブ + [ci.yml](../.github/workflows/ci.yml) の計3か所）:

```diff
-          dotnet-version: '6.0.x'
+          dotnet-version: '10.0.x'
```

`.vscode/launch.json` 内の `net6.0` を含むパスをすべて `net10.0` に置換（`program` 1か所 + `cwd` 2か所。Lambda Test Tool自体の後継は項目5参照）:

```diff
-            "program": "${workspaceFolder}/TwitterMlbBot/bin/Debug/net6.0/TwitterMlbBot.dll",
+            "program": "${workspaceFolder}/TwitterMlbBot/bin/Debug/net10.0/TwitterMlbBot.dll",
   ...
-            "cwd": "${workspaceFolder}/TwitterMlbBotExecution/src/TwitterMlbBotExecution/bin/Debug/net6.0",
+            "cwd": "${workspaceFolder}/TwitterMlbBotExecution/src/TwitterMlbBotExecution/bin/Debug/net10.0",
```

### 手順4: Lambdaランタイムの切り替え（手動・PRマージの直前に実施）

デプロイパイプラインは関数コードしか更新しないため、ランタイム設定はCLIで1回変更する:

```bash
aws lambda update-function-configuration --function-name <関数名> --runtime dotnet10
# 反映確認（"dotnet10" と "Successful" が返ること）
aws lambda get-function-configuration --function-name <関数名> --query '[Runtime,LastUpdateStatus]'
```

**順序が重要**: 「ランタイム変更 → PRマージ（デプロイ）」の順で行う。

- net6.0でビルドされた現行コードは、.NET 10ランタイム上でも後方互換で動作する → 先にランタイムを切り替えても壊れない
- 逆に、先にnet10.0のコードをデプロイすると、dotnet6ランタイム上では起動できず、**次の定期実行が失敗する**

ロールバックが必要になった場合:

```bash
aws lambda update-function-configuration --function-name <関数名> --runtime dotnet6
git revert <マージコミット> && git push   # revertのpushで旧コードが自動デプロイされる
```

### 手順5: デプロイ後の動作確認

マージ前にローカルのドライラン（`DRY_RUN=true`。README参照）で文面出力まで確認しておく。デプロイ後は:

1. GitHub Actionsのデプロイjobが成功していること
2. 翌日の定期実行後、CloudWatch Logsで対象ロググループを確認:
   ```bash
   aws logs tail /aws/lambda/<関数名> --since 1d
   ```
   エラーが無いこと、`Init Duration`（コールドスタート）が極端に悪化していないことを見る
3. 実際のXアカウントで、6地区分のツイートが投稿されていることを確認

※ `aws lambda invoke` での即時確認は**実ツイートが飛ぶ**ため、翌日の定期実行を待つ確認方法を推奨。すぐ確認したい場合は、Lambda環境変数に `DRY_RUN=true` を一時設定してからinvokeし、CloudWatch Logsで文面出力を確認する（**確認後は必ず環境変数を削除する**）。

---

## 2. Amazon.Lambda.Core / Amazon.Lambda.Serialization.SystemTextJson（項目1に含めて実施）

具体的なdiffは項目1・手順2の通り。補足事項のみ記す。

- 3.x系は **net8.0 / net10.0 / netstandard2.0** 対応で、net6.0ターゲットを削除している。**TFM更新と同一PRで行うこと**（パッケージだけ先に上げるとnet6.0のままでは復元エラー、TFMだけ先に上げると2.1.0の古い資産で動くが警告が出る）
- `Amazon.Lambda.Core` は本体側とテスト側の両csprojにあるので、**必ず同じ3.1.1に揃える**。揃っているかは以下で確認:
  ```bash
  dotnet list MlbBot.sln package | grep Amazon.Lambda
  ```
- コード変更は不要（使用APIは `ILambdaContext` と `[assembly: LambdaSerializer(...)]` のみで、3.xにそのまま存続。ハンドラ文字列も変更なし）
- 万一3.xで問題が出た場合の退避先: net8.0 + `Amazon.Lambda.Core 2.8.1` + `Serialization.SystemTextJson 2.4.5`（2.x系最終）。ただしnet10.0ではこの組み合わせは使えないため、あくまで緊急避難

---

## 3. System.Configuration.ConfigurationManager 4.7.0 → 削除（推奨）

### 恒久対応（推奨）: パッケージごと削除する

使用箇所は `ProcessUtility.ReadAppConfig` 内の `ConfigurationManager.AppSettings[identifier]` の1行だけ。docs/code-improvements.md 項目2の手順（`Microsoft.Extensions.Configuration` + user-secrets への移行）を実施すると、この行が消え、以下が全て不要になる:

1. `TwitterMlbBot.csproj` から `<PackageReference Include="System.Configuration.ConfigurationManager" ... />` を削除
2. ローカルの `TwitterMlbBot/App.config`（存在する場合）と `Dummy.config` を削除
3. `.gitignore` から App.config の除外行を削除

### 暫定対応: バージョンだけ上げる

設定移行を後回しにする場合は項目1・手順2のdiffの通り 10.0.9 へ。`AppSettings` 読み取りAPIに破壊的変更はなく**コード変更不要**。ビルドが通ればそれ以上の確認は不要。

（メジャー番号が4→10に飛ぶのは.NET本体のバージョンに連動しているだけなので身構えなくてよい）

---

## 4. テスト関連パッケージ（項目1に含めて実施）

具体的なバージョンdiffは項目1・手順2の通り。コード修正は不要。

### 更新後の確認

```bash
dotnet test MlbBot.sln
```

- 単体テスト（`TwitterServiceDryRunTest` の3件）がパスし、`FunctionTest` がスキップ表示されれば、ランナーの世代交代（runner 3.1.5 + Test.Sdk 18.7.0）は成功
- Microsoft.NET.Test.Sdk 15.5.0（2017年リリース）のまま.NET 10に上げた場合、テストが「0件発見」または起動エラーになる。この症状が出たらまずTest.Sdkのバージョンを疑う

### xunit v3 への移行は見送り

`xunit.v3` はパッケージ名・名前空間から変わる大きな移行で、現状のテスト規模ではコストに見合わない。単体テストを拡充するタイミング（docs/code-improvements.md 項目3）で再検討する。

---

## 5. Lambdaローカル実行ツールの刷新（任意）

`.vscode/launch.json` のLambda Test Tool系の構成（通常実行・ドライランの2つ）が参照する `dotnet-lambda-test-tool-6.0` は .NET 6 専用で終息路線。**.NET 10移行後はこの2構成が動かなくなる**ため、移行と前後してどちらかを選ぶ:

**案A（推奨）: ツール自体を不要にする**
このボットはLambda固有のイベントペイロードを使っていない（`object input` を無視して `Program.Main(null)` を呼ぶだけ）ため、Lambdaエミュレーションの意味がほぼない。`TwitterMlbBot` を `OutputType=Exe` に変更すれば、Test Toolなしで `dotnet run --project TwitterMlbBot -- --dry-run` により起動でき、launch構成も `TwitterMlbBot.dll` 直接起動に一本化できる。ただし現在のLibrary設定はLambda Test Toolでのローカル実行との両立のためなので、Exe化はTest Toolを廃止する判断とセットで行うこと。

**案B: 後継ツールへ移行する**

```bash
dotnet tool uninstall -g Amazon.Lambda.TestTool-6.0   # 旧ツール削除
dotnet tool install -g Amazon.Lambda.TestTool          # 統合版(1.0.0)を導入
dotnet lambda-test-tool --help                         # 起動コマンド・ポートを確認
```

確認した起動方法に合わせて launch.json の `program` / `args` を書き換える（旧ツールとは起動方法が異なるため、`--help` の出力と aws/aws-lambda-dotnet リポジトリのREADMEを正とする）。

---

## 6. SDKバージョンの固定（任意・いつでも）

環境間（ローカル/CI）のSDKずれを防ぐため、リポジトリ直下に `global.json` を新規作成:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

- `version` は `dotnet --list-sdks` で実際に入っている10.0系の番号に合わせる
- `rollForward: latestFeature` により「10.0.x系なら新しいものを許容、メジャーは固定」となり、開発者ごとのパッチ差でビルド不能になる事態を防げる
- 置いた後は `dotnet --version` がリポジトリ内で10.0.xを返すことを確認（6.0.408しか無い環境では明確なエラーメッセージが出るようになり、原因調査が楽になる）
