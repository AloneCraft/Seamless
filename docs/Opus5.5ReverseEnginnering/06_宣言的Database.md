---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
target: Seamless.Database, Seamless.Database.Generator
evidence_sources:
  - Seamless.Database/DatabaseContainer.cs
  - Seamless.Database/DatabaseContainerOptions.cs
  - Seamless.Database/DatabaseConcurrencyGate.cs
  - Seamless.Database/DatabaseTransaction.cs
  - Seamless.Database/DatabaseWriteAheadLog.cs
  - Seamless.Database/Table.cs
  - Seamless.Database/KeyedTable.cs
  - Seamless.Database/MaterializedView.cs
  - Seamless.Database/View.cs
  - Seamless.Database/TableFunction.cs
  - Seamless.Database.Generator/DatabaseContainerGenerator.cs
  - Seamless.Database.Generator/DatabaseSchemaGenerator.cs
  - Test/Seamless/Database/*
  - docs/execution-and-database.md
  - Work/qa/state.json
confidence: medium
review_status: draft
---

# 06 宣言的 Database（Seamless.Database）

## 行うべき機能

`DatabaseContainer` を継承した partial クラスに、テーブル・ビュー・テーブル関数を C# の宣言として書く。生成器がスキーマ情報と配線を作り、Tsavorite を実体として読み書き・トランザクション・WAL による復旧を行う。

| 機能 | 内容 | 状態 | 根拠 |
|---|---|---|---|
| テーブル（キーなし） | `Table<TRecord>`：集合として追加・削除・包含・集合演算 | 実装済み | `Table.cs:39-128` |
| テーブル（キーあり） | `Table<TKey,TRecord>`：キーで引く・追加・削除 | 実装済み | `KeyedTable.cs:42-104` |
| ビュー・テーブル関数 | 定義ラムダを列挙のたびに評価。中の LINQ は FLinq が展開 | 実装済み | `View.cs:3-16`、`TableFunction.cs:3-16` |
| マテリアライズドビュー | キーつきで実体を持つビュー。WAL から復旧 | 実装済み | `MaterializedView.cs`、`DatabaseContainer.cs:487-554`（作成）、`:555-`（WAL からの復旧） |
| 実行モード | メモリ（ディスク I/O なし）とストレージ（退避あり） | 実装済み | `DatabaseContainerOptions.cs:4-9` |
| 共有予算 | テーブルのメモリ予約・変更集合・WAL 符号化・一時集合演算を1つの予算で管理 | 実装済み | `DatabaseContainer.cs:26-41`、`DatabaseContainerOptions.cs:23-30` |
| 書き込みトランザクション | DB 全体で writer を直列化。入れ子可。変更は commit まで保留 | 実装済み | `DatabaseContainer.cs:56-101` |
| 読み取りセッション | 読み取り中は writer を待たせる | 実装済み | `DatabaseContainer.cs:103-` |
| WAL と復旧 | begin→operation→commit を書いて flush してから反映。再起動時は commit 済みだけ再生 | 実装済み | `DatabaseWriteAheadLog.cs:28-87` |
| 変更集合の退避 | 閾値を超えた変更集合を一時 Tsavorite ストアへ退避 | 実装済み | `DatabaseContainerOptions.cs:29-30`、`docs/execution-and-database.md` |
| スキーマ生成器 | `DatabaseGenerator` 印と名前空間規約から Keys/Records を集め、MemoryPack 対応のレコードを生成 | 実装済み | `Seamless.Database.Generator/DatabaseSchemaGenerator.cs` |
| コンテナ生成器 | コンテナのメタデータと取り込み配線を生成 | 実装済み | `Seamless.Database.Generator/DatabaseContainerGenerator.cs` |
| 共通ストアへの移行 | テーブルの作成を `Seamless.Execution.Store` 経由にし、Index/Page/Segment/Memory を上限に置き換える | **未実装**（段階5） | 旧 API を直接使用：`DatabaseContainer.cs:364-554` |
| 終了時の安全な破棄 | FLinq 経由の集計後も `Dispose` できる | 実装済み（2026-09-25 BUG-0007 修正、未コミット） | `DatabaseContainer.cs:704-707` |

## 1. 不変条件

1. **commit の順序**：WAL へ begin・各操作・commit を書き、ディスクへ flush してから、テーブルへ反映する（`DatabaseWriteAheadLog.cs:51-53`、`DatabaseContainer.cs:86-91`）。
2. **再起動時は commit 記録のある操作だけを再生する。** 未 commit の末尾と途中で切れた行は公開状態へ反映しない（`DatabaseWriteAheadLog.cs:59-69`）。
3. **反映の途中で失敗したら DB を「故障」状態にし、以後の操作を `DatabaseFaultedException` で拒否する。** 閉じて開き直せば、commit 済みの WAL から復旧できる（`DatabaseContainer.cs:95`、`:208`、`DatabaseConcurrencyGate.cs:98`）。
4. **writer と更新トランザクションは直列化する。** 読み取りセッション中は writer が待つ（`docs/execution-and-database.md`「Database」）。
5. **読み取りセッションを持つ実行コンテキストが writer へ昇格しようとしたら、デッドロックさせずに `InvalidOperationException`**（`DatabaseContainer.cs:60-62`）。
6. **内側のトランザクションを Rollback すると、外側のトランザクション全体を中止する**（`DatabaseContainer.cs:73-75`、`docs/execution-and-database.md`）。
7. **件数の正本は `long`（`LongCount`/`GetLongCount()`）。`int Count` は範囲外で `OverflowException`**（`Table.cs:39-40`）。
8. **テーブル・キー列挙・トランザクション内の読み取りはストリーミング。** キーなしテーブルの `Clear`・`IntersectWith`・`SymmetricExceptWith` も全件スナップショットを作らない（`docs/execution-and-database.md`）。
9. **WAL の archive は、WAL 内の全対象テーブルが checkpoint 済みのときだけ。** 同じ SHA 名で内容が違う archive があれば active WAL を保持したまま失敗する（`DatabaseWriteAheadLog.cs:96-142`、`docs/execution-and-database.md`）。
10. **トランザクションか読み取りセッションが残っている間は `Dispose` できない**（`DatabaseContainer.cs:707`）。
11. **起動時に、所有を確認できた一時資源の残骸だけを回収する**（`DatabaseContainer.cs:36`）。

## 2. 禁止事項

- WAL の flush 前にテーブルへ反映しない。
- 故障状態を黙って解除しない（開き直しで復旧させる）。
- テーブルごとに最大テーブルの固定メモリを重複配分しない（取り込みでは関係ごとの件数から `TableStorage` を作る。`DatabaseContainerOptions.cs:20-22`）。
- 旧形式のストレージを自動変換しない（`docs/execution-and-database.md`「旧データからの移行」）。

## 3. 前提条件

- レコードは struct（キーありテーブルは `where TKey:struct where TRecord:struct`：`DatabaseContainer.cs:419-423`）。
- 読み書きの排他は `AsyncLocal` の実行コンテキスト単位（`DatabaseContainer.cs:18-20`）。
- ストレージモードでは、テーブルごとのフォルダに log・obj・checkpoint を置く（`DatabaseContainer.cs:381-395`）。

## 4. 変更範囲 / スコープ

- `Seamless.Database/**`、`Seamless.Database.Generator/**`、`Test/Seamless/Database/**`、`Test/SourceGenerator/Database*Tests.cs`
- 共通ストアへ移行する場合：`DatabaseContainer.cs:364-554`（テーブル・マテリアライズドビューの作成、checkpoint、WAL の順序）と `DatabaseContainerOptions.cs:16-19`。

## 5. 非対象 / Out of Scope

- SQL Server からの取り込み（[07](07_SQLServer取り込み.md)）。
- 複数プロセスからの同時オープン。
- SQL の実行（DB 内のクエリは C# の LINQ で書く）。

## 6. 完了条件 / 成功条件

- テーブルの作成が共通ストア経由になり、`DatabaseContainerOptions` の Index/Page/Segment/Memory が上限（と `internal` の詳細設定）に置き換わっている。WAL→反映→checkpoint の順序は変わらない。
- FLinq で展開した集計（`Sum` など）のあとも `Dispose` が成功する（BUG-0007。2026-09-25 達成）。
- DB テスト群（TPC-H、AdventureWorks、保存・復旧）が最後まで終了する（BUG-0005。2026-09-25 達成：136件成功後 36 秒で終了）。

## 7. 検証項目 / 受け入れ条件

| 確かめること | 手段 |
|---|---|
| 保存・再起動・復旧 | `Test/Seamless/Database/DatabaseStoragePersistenceTests.cs` |
| マテリアライズドビュー | `Test/Seamless/Database/MaterializedViewTests.cs`、`Test/SourceGenerator/DatabaseMaterializedViewGeneratorTests.cs` |
| 生成器 | `Test/SourceGenerator/DatabaseContainerGeneratorTests.cs`、`DatabaseSetImportGeneratorTests.cs` |
| 実データ規模のクエリ | `Test/Seamless/Database/TPC_H.cs`、`AdventureWorks2017` |
| FLinq 集計後の Dispose | `Test/Seamless/Database/DatabaseScalarQueryDisposeTests.cs`（同じコンテキストの Dispose と、実行コンテキストを引き継がない Dispose の両方） |
| 故障状態と復旧 | commit 反映中の例外を注入し、`DatabaseFaultedException` と、開き直し後の復旧を確かめる（【不明】専用テストの有無は未確認） |

## 8. 移行条件

- 共通ストアへの移行は、Tsavorite の段階3b（改名・旧 API 削除）と同時に行う（【記録】承認済み計画 段階5）。
- 保存済みの DB は変換しない。新形式で作り直すか、SQL Server から取り込み直す。
- 移行前後で、TPC-H・AdventureWorks の代表クエリの結果と行数が一致することを条件にする。

## 9. ロールバック条件

- WAL の再生結果、行数、代表クエリのいずれかが移行前と一致しなければ、`DatabaseContainer.cs` の移行を戻す。
- 基準は `df0aee37`（Database 機能拡充）以降、段階5の直前のコミット。

## 10. 既知の問題 / 許容事項

- 読み取りセッションは `AsyncLocal` とロックで管理するため、列挙子を破棄し忘れると、同じコンテキストでは `Dispose` が例外、別コンテキスト（xUnit のフィクスチャ破棄など）では書き込みロック待ちで止まる。FLinq 側の破棄漏れ（BUG-0007）は 2026-09-25 に修正済みだが、利用者が列挙子を手で扱う場合は同じ注意が要る。
- `Seamless.Database.Tool/DatabaseImporter.cs:102` がリフレクションで `LongCount` を読む。型の安全性は実行時にしか分からない。

## 確認済み / 推測 / 不明

- **確認済み**：WAL の順序と再生条件、故障状態、昇格の禁止、入れ子 Rollback、旧 API の直接使用、Dispose 時の検査。
- **推測**：なし。
- **不明**：故障状態からの復旧テストの有無。複数プロセス同時オープンを禁止する仕組み（ロックファイル等）の有無。
