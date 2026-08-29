# コード改善提案（保守性・高凝集・疎結合）

現状のコードを読んだうえでの改善提案。優先度順に並べている。
各項目は独立して着手できるが、「1. 責務分離とDI導入」を先にやると他の項目が楽になる。

## 現状の構造と課題サマリ

```
TwitterMlbBot/
├── Program.cs          … エントリポイント + マッピング + ハッシュタグ生成（責務過多）
├── ProcessUtility.cs   … HTTP汎用処理 + 設定読み取り（無関係な2責務が同居）
├── Mlb/
│   ├── MlbService.cs   … MLB API呼び出し（設定読み取りにも依存）
│   ├── Param.cs        … リクエストDTO（クラス名が汎用的すぎる）
│   └── Result.cs       … レスポンスDTO
├── Twitter/
│   ├── TwitterService.cs … ツイート文面組み立て + API送信（2責務が同居）
│   └── Param.cs        … DTO（Mlb.Paramと同名で紛らわしい）
└── Authorization/
    └── OAuth1.cs       … OAuth1署名（凝集度は高い。ほぼこのままでよい）
```

主な課題:

- `Program.Main` が「引数解析・API呼び出し・データ変換・ハッシュタグ生成・ツイート」のオーケストレーションと実装詳細を両方持っている
- サービスクラスが自分で設定を読む（`new MlbService()` の中で `ProcessUtility.ReadAppConfig` を呼ぶ）ため、テスト時に差し替え不可能
- 文面組み立てロジック（純粋関数にできる部分）がAPI送信と癒着していて単体テストが書けない
- 一時的エラー（429/503等）へのリトライがない

---

## 1. 責務分離とDIの導入（最優先）

### 目的
- 「データ取得」「文面組み立て」「送信」を独立したクラスに分け、interfaceで疎結合にする
- 純粋ロジック（文面・ハッシュタグ・マッピング）を単体テスト可能にする

### 手順

1. NuGetパッケージ `Microsoft.Extensions.DependencyInjection` を `TwitterMlbBot.csproj` に追加する。

2. 以下のinterfaceを切る。

```csharp
// Mlb/IMlbApiClient.cs
public interface IMlbApiClient
{
    Task<List<TeamStanding>> GetStandingsAsync(int year);
}

// Twitter/ITwitterClient.cs
public interface ITwitterClient
{
    Task PostTweetAsync(string text);
}
```

（ドライランは現在 `TwitterService` 内の `dryRun` フラグ分岐で実装している。`ITwitterClient` 導入時は、コンソール出力するだけの `DryRunTwitterClient` 実装への差し替えに置き換え、フラグ分岐を廃止する）

3. 文面組み立てを純粋クラスに抽出する（`TwitterService.CreateTweet` の前半部分と `Program.MapToTwitterParam` を統合）。

```csharp
// Composing/TweetComposer.cs — 入出力がデータだけの純粋クラス。単体テストの主対象
public class TweetComposer
{
    // 順位データ → 地区ごとのツイート文リスト
    public IReadOnlyList<string> Compose(IReadOnlyList<TeamStanding> standings, DateOnly date);
}

// Composing/HashtagProvider.cs — Program.cs の OfficialHashtagMap と GetTeamHashtags を移動
public class HashtagProvider
{
    public string GetHashtags(string teamName);
}
```

4. オーケストレーションだけを持つクラスを作る。Lambda（`Function.cs`）が `Program.Main(null)` を直接呼んでいる静的結合もここで解消する。

```csharp
// BotRunner.cs
public class BotRunner
{
    private readonly IMlbApiClient mlbClient;
    private readonly TweetComposer composer;
    private readonly ITwitterClient twitterClient;

    public BotRunner(IMlbApiClient mlbClient, TweetComposer composer, ITwitterClient twitterClient) { ... }

    public async Task RunAsync(int year)
    {
        var standings = await mlbClient.GetStandingsAsync(year);
        var tweets = composer.Compose(standings, /* 日付 */);
        foreach (var tweet in tweets) await twitterClient.PostTweetAsync(tweet);
    }
}
```

5. `Program.Main` はDIコンテナ組み立てと引数解析だけにする。

```csharp
public static async Task Main(string[] args)
{
    var services = new ServiceCollection()
        .AddSingleton<IMlbApiClient, MlbApiClient>()
        .AddSingleton<ITwitterClient, TwitterClient>()
        .AddSingleton<TweetComposer>()
        .AddSingleton<HashtagProvider>()
        .AddSingleton<BotRunner>()
        .BuildServiceProvider();

    int year = ParseYear(args);
    await services.GetRequiredService<BotRunner>().RunAsync(year);
}
```

6. `Function.cs` は `Program.Main(null)` ではなく `BotRunner` を組み立てて呼ぶ（DI組み立て部を共通メソッドに切り出して両エントリポイントから使う）。

### 完了条件
- `Program.cs` からマッピング・ハッシュタグロジックが消えている
- `TweetComposer` がHTTPにも設定にも依存せず、コンストラクタ引数とメソッド引数だけで動く

---

## 2. 設定管理の一本化

### 現状の問題
- `ProcessUtility.ReadAppConfig`（App.config読み取り）と `GetEnvVarByKey`（環境変数フォールバック）の組み合わせが「Lambda上ではconfigがnullになる」という暗黙の挙動に依存している
- App.configの値がJSON文字列で、その中をさらにパースしている（`Dummy.config` 参照）。二重構造で追いにくい
- サービスクラスのコンストラクタ内で設定を読むため、設定源を差し替えられない

### 提案
`Microsoft.Extensions.Configuration` に置き換え、Optionsパターンで注入する。

1. パッケージ追加: `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Configuration.EnvironmentVariables`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Options.ConfigurationExtensions`
2. 設定クラスを定義する。

```csharp
public class MlbOptions
{
    public string ApiKey { get; set; } = "";
}

public class TwitterOptions
{
    public string ConsumerKey { get; set; } = "";
    public string ConsumerSecret { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string AccessSecret { get; set; } = "";
}
```

3. 構成の優先順位を「環境変数 > user-secrets」にする。Lambdaでは環境変数（現行の `MLB_API_KEY` 等をそのまま流用可能。ただし `Mlb__ApiKey` 形式に揃えると自動バインドできる）、ローカルでは `dotnet user-secrets` を使う。

```csharp
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()       // ローカル開発用
    .AddEnvironmentVariables()       // Lambda用（優先）
    .Build();
services.Configure<MlbOptions>(config.GetSection("Mlb"));
services.Configure<TwitterOptions>(config.GetSection("Twitter"));
```

4. `MlbApiClient` / `TwitterClient` はコンストラクタで `IOptions<MlbOptions>` を受け取る。
5. `ProcessUtility.ReadAppConfig` / `GetEnvVarByKey`、`System.Configuration.ConfigurationManager` パッケージ参照、`Dummy.config` を削除する。

### 完了条件
- 設定読み取りコードが構成ビルダー1か所に集約されている
- サービスクラスに `ProcessUtility` への参照が残っていない

---

## 3. 文面組み立てと送信の分離 + 単体テスト追加

（項目1の `TweetComposer` 抽出とセット）

### 現状の問題
- `TwitterService.CreateTweet` がStringBuilderでの文面組み立てとHTTP送信を両方行っており、文面ロジックを純粋関数として直接テストできない（既存の `TwitterServiceDryRunTest` はドライラン出力を経由した間接的な検証）
- 文面組み立て（`CreateTweet` 内のStringBuilder部分）と署名生成（`OAuth1.CreateSignature`）は直接テストできていない
- テストが `InternalsVisibleTo` でinternalクラスに触る前提になっており、テスト対象の公開APIとして設計されていない
- `FunctionTest.cs` は本番の `Program.Main` をそのまま呼ぶためSkip指定の手動疎通専用になっており、自動テストとして機能していない

### 提案
1. `TwitterMlbBot.Tests`（xUnit）プロジェクトを新設するか、既存の `TwitterMlbBotExecution.Tests` を拡張する。
2. 以下の純粋ロジックにテストを書く。
   - `HashtagProvider`: 公式タグありチーム（`"Red Sox"` → `"#DirtyWater #RedSox"`）、公式タグなしチーム（`"Cubs"` → `"#Cubs"`）、スペース除去
   - `TweetComposer`: 順位の連番付与、All-Star擬似チーム（1チームだけのグループ）の除外、地区ごとの分割数、280字以内であること
   - `OAuth1.CombineQueryParams`: 空辞書、複数パラメータの連結順
3. `FunctionTest.cs` は `BotRunner` にモックを注入する形に書き換え、Skipなしで常時実行できるようにする。
4. テスト容易性のため、現在時刻は `TweetComposer` の引数または `TimeProvider` の注入で渡す（クラス内部で `DateTime.Now` を呼ばない）。
5. あわせて xunit v3 への移行（パッケージ名・名前空間が変わる大型移行）もこのタイミングで検討する。

### 完了条件
- 文面組み立て・ハッシュタグ・OAuth署名の純粋ロジックがネットワーク接続なしのテストで担保されている
- Skip指定のテストが残っていない

---

## 4. リトライの導入（Terraform化後に対応）

### 現状の問題
- X API・MLB APIの一時的エラー（429/503等）に対するリトライがなく、単発の失敗がそのままツイート欠落になる
- ツイートの部分失敗はログ出力のみで検知手段がない（全件失敗の場合のみLambda実行がエラー終了する）

### 提案
1. 一時的エラーにリトライを入れる。手書きでもよいが `Polly` を使うと簡潔（`WaitAndRetryAsync(3回, 指数バックオフ)`）。
2. リトライ分の実行時間を確保するため、Lambdaタイムアウト（現状15秒）を60秒程度へ引き上げる。

**対応時期**: Lambdaタイムアウト変更というインフラ設定変更を伴うため、**Terraform化（[docs/infrastructure.md](infrastructure.md)）が完了してから**実施する。

---

## 5. ロギングの整備

### 現状の問題
- `Console.WriteLine` 直書きでログレベルの概念がなく、ログの重要度をフィルタできない

### 提案
1. `Microsoft.Extensions.Logging` を導入し、`ILogger<T>` をDIで注入する（項目1のDI基盤に乗せる）。Lambda環境ではConsoleロガーで十分（CloudWatchに流れる）。

```csharp
logger.LogInformation("MLB standings fetched: {TeamCount} teams for {Year}", teams.Count, year);
logger.LogInformation("Tweet posted for {League} {Division}", key.League, key.Division);
logger.LogError("Tweet failed: {StatusCode} {Body}", response.StatusCode, body);
```

---

## 6. DTOの命名と堅牢化

### 現状の問題
- `Mlb.Param` と `Twitter.Param` が同名で、`Program.cs` では名前空間修飾（`Mlb.Param` / `Twitter.Param`）で区別している。`Result` / `DetailResult` / `ParamByKey` も役割が名前から読めない
- `System.Text.Json` はデフォルトで大文字小文字を区別する。現状はAPIがPascalCaseを返すため動いているが、暗黙の前提

### 提案
1. リネーム（ファイル名も合わせる）:

| 現在 | 変更後 |
|---|---|
| `Mlb.Param` | `StandingsQuery`（または `int year` 引数に格上げして廃止） |
| `Mlb.Result` / `DetailResult` | `StandingsResponse` / `TeamStanding` |
| `Twitter.Param` | `TweetRequest`（`TweetComposer` 導入後は不要になる可能性大） |
| `ParamByKey` | `DivisionStandings` |
| `GroupKey` | `DivisionKey` |
| `DetailParam` | `RankedTeam` |

2. `TeamStanding` の各プロパティに `[JsonPropertyName("Wins")]` を付ける、またはデシリアライズ時に `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` を指定して前提を明示する。
3. `TwitterMlbBot.csproj` に `<Nullable>enable</Nullable>` を追加する（Lambda側プロジェクトは既にenable、本体だけdisableで不整合）。DTOは `required` か初期値で警告を潰す。

---

## 7. ProcessUtility の解体

`ProcessUtility` は「HTTP汎用ラッパー」と「設定読み取り」という無関係な2責務を持つ雑多クラスになっている。

- 設定読み取り → 項目2で `ConfigurationBuilder` に置き換えて削除
- `CalloutAsync` → 汎用化のメリットが薄い（呼び出し元は実質GET 1か所）。`MlbApiClient` 内に直接 `client.GetAsync(uri)` を書いてよい。HTTPヘッダ組み立てなどが増えたら、その時点で `HttpRequestMessage` 拡張として再抽出する
- クラスごと削除が最終形

---

## 推奨着手順

1. 項目3（テスト基盤 + 純粋ロジック抽出）… 以降のリファクタの安全網になる
2. 項目1（DI・責務分離）
3. 項目2（設定管理）+ 項目7（ProcessUtility解体）
4. 項目5（ロギング）+ 項目6（DTO命名）
5. 項目4（リトライ）… Terraform化（[docs/infrastructure.md](infrastructure.md)）完了後
