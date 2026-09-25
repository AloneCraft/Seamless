---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
investigated_at: 2026-09-25
review_status: draft
---

# Seamless 機能要件の復元と実装計画（Opus 5.5）

実装・テスト・作業記録から、このシステムが「行うべき機能」を復元し、機能ごとに次の10項目を書いた文書群です。

| 項目 | この文書群での意味 |
|---|---|
| 不変条件 | 変更後も必ず成り立ち続けること |
| 禁止事項 | やってはいけない変更・避けるべき実装 |
| 前提条件 | 設計や作業が成り立つための前提 |
| 変更範囲 / スコープ | その機能を直すときに触ってよい範囲 |
| 非対象 / Out of Scope | その機能の責任外、今回は触らないもの |
| 完了条件 / 成功条件 | 機能（または Phase）が「できた」と言える条件 |
| 検証項目 / 受け入れ条件 | テスト・画面・計測で何を確かめるか |
| 移行条件 | 旧構造から新構造へ段階的に切り替える条件 |
| ロールバック条件 | 問題が出たとき、どの状態まで戻すか |
| 既知の問題 / 許容事項 | 今回直さなくてよい既知の不具合・差異 |

## 読む順番

1. [00_全体像と共通ルール.md](00_全体像と共通ルール.md) — システムの目的、機能の地図、全機能に共通する10項目
2. 機能別の文書（01〜10）— 機能ごとの10項目と実装状況
3. [20_未実装と既知の問題.md](20_未実装と既知の問題.md) — 未実装の機能・既知の不具合・文書のずれの一覧
4. [30_実装計画.md](30_実装計画.md) — 優先順位つきの実装計画（Phase ごとに10項目）

| 番号 | 機能群 | 主なプロジェクト |
|---|---|---|
| [01](01_共通ストアと実行スコープ.md) | 共通ストアと実行スコープ | `Seamless.Execution` |
| [02](02_Tsavoriteコレクション.md) | Tsavorite コレクション | `Seamless.Collections.Tsavorite` |
| [03](03_Persistedコレクション.md) | Persisted コレクション | `Seamless.Collections.Persisted`、`Seamless.Collections` |
| [04](04_LINQランタイムと大規模LINQ.md) | LINQ ランタイムと大規模 LINQ | `Seamless.Linq`、`Seamless.Linq.Analyzers` |
| [05](05_FLinqソース生成.md) | FLinq ソース生成 | `Seamless.FLinq.Generator` |
| [06](06_宣言的Database.md) | 宣言的 Database | `Seamless.Database`、`Seamless.Database.Generator` |
| [07](07_SQLServer取り込み.md) | SQL Server 取り込み | `Seamless.Database.Rdbms`、`.SqlServer`、`.Tool` |
| [08](08_ベンチマーク基盤.md) | ベンチマーク基盤 | `Benchmark/*`、`Shared/UI.*`、`Benchmark/Worker` |
| [09](09_動的クエリ検証.md) | 動的クエリ検証 | `Benchmark/Benchmark.Dynamic.*` |
| [10](10_検証基盤.md) | 検証基盤 | `Verification.*` |

## 表記のきまり

- **【確認】** コード、テストコード、作業記録（`Work/qa/`）で確かめた事実。根拠に `パス:行番号` を付ける。
- **【推測】** コードから推測したこと。確度を「高・中・低」で付ける。
- **【不明】** コードだけでは決められないこと。
- **【記録】** `Work/qa/state.json`・`Work/qa/handoff.md`・承認済み計画 `C:\Users\kazuh\.claude\plans\qa-workflow-md-validated-stream.md` に書かれていて、今回の調査で再実行していないこと。日付つきで扱う。
- 状態の表記：**実装済み** / **一部** / **未実装** / **スタブ**（呼び出せるが中身が例外を投げる）

## 調査の範囲と限界

- 基準コミットは `d3a5a3e8`。作業ツリーには利用者の未コミット変更が2件ある（`Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/SelectMany.cs`、`Shared/Linq.Enumerable/Base.cs`）。本文書はこれらに依存しない。
- 今回はビルドとテストを実行していない。Visual Studio が常駐している可能性があり、並列ビルドを避ける運用（`Work/QA_WORKFLOW.md` §7 と記録）に従った。テストの成否は【記録】として扱う。
- ルート直下の `FLinq.md`・`Collection.md`・`Database.md`・`UI.md` と `Document/` は旧パスや旧スキーマ版を含む（例：`Database.md` はUI DBスキーマ版24、実装は26）。そのため実装を正とし、これらは参考にとどめた。ずれの一覧は [20](20_未実装と既知の問題.md) の「文書のずれ」にある。
- ゲーム向けの Reverse Spec Suite（Game 階層の固定22ファイル）は、このリポジトリがゲームではないため適用していない。事実・推測・不明の区別と、行番号つきの根拠を書く方針だけを取り入れた。
