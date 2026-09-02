---
name: pin-github-actions
description: GitHub Actionsの `uses:` 参照をタグ（@v4 等）ではなくフルcommit SHA + バージョンコメントに固定する（pinactを使う）。ワークフロー（.github/workflows）や composite action に新しいアクションを追加・更新するとき、タグ指定の `uses:` を見つけたとき、「SHA固定して」「pinして」「アクションを追加して」と言われたときに必ず使う。
---

# GitHub Actions を commit SHA に固定する

## なぜタグではなくSHAか

`uses: actions/checkout@v4` のようなタグ参照は、タグの付け替えで中身が変わりうる（タグ差し替えによる
サプライチェーン攻撃が実際に起きている）。フルcommit SHAは不変なので、同じ定義なら常に同じコードが動く。
末尾のバージョンコメントは人が読むためだけでなく、Dependabot がSHAとコメントの両方を更新するための目印になる。

目標の形:

```yaml
uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
```

## 使うツール: pinact

SHAの解決・書き換え・検証は [pinact](https://github.com/suzuki-shunsuke/pinact) に任せる
（annotated tag の解決、`v4` のようなメジャー指定から最新フルバージョンへの展開、コメント付与まで行う）。
自前でAPIを叩いてSHAを書かない。未導入なら `brew install pinact`（GitHub APIを使うため `gh auth login` 済みか
`GITHUB_TOKEN` が必要）。

```bash
pinact run                                     # .github/workflows 配下を固定（既に固定済みなら何もしない）
pinact run .github/actions/*/action.yml        # composite action は既定の探索対象外なので明示する
pinact run --check                             # 変更せず、未固定・不整合があれば非0で終了（検証用）
pinact run --update                            # 最新バージョンへ更新したいとき（依頼があった場合のみ）
```

## 手順

1. アクションを追加するときは、まず `uses: owner/repo@vX`（メジャーでよい）で書く。
2. `pinact run` と composite action 分を実行し、書き換わった行を `git diff` で確認する。
3. `pinact run --check`（composite action のパスも渡す）で出力が無いことを確認する。
4. 同じアクションが複数箇所にあれば同じSHAに揃っているかを見る（バージョンが割れていると挙動差の原因になる）。

## 判断に迷いやすい点

- ユーザーが `@v4` のようなメジャー指定で依頼してきても、固定はそのメジャー内の最新フルバージョンになる。
  どのバージョンに固定したかを報告する。
- 新しいアクションは公式（`actions/*`、`aws-actions/*` などベンダー公式）を優先し、サードパーティなら
  採用理由を説明してから使う。
- Dependabot の `github-actions` エコシステムが有効なら、固定後のバージョン更新は自動で追随する。
  composite action（`.github/actions/*/action.yml`）は `dependabot.yml` の `directories` に個別指定しないと
  対象外なので、追加したら設定も確認する。
