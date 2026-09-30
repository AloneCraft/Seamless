---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
target: Seamless.Collections.Persisted, Seamless.Collections/Persisted
evidence_sources:
  - Seamless.Collections.Persisted/StoreExtensions.cs
  - Seamless.Collections.Persisted/Store.cs
  - Seamless.Collections.Persisted/Set.cs
  - Seamless.Collections.Persisted/Dictionary.cs
  - Seamless.Collections.Persisted/AppendOnlyList.cs
  - Seamless.Collections.Persisted/ScopedByteIndex.cs
  - Seamless.Collections.Persisted/ScopedStoredSequence.cs
  - Seamless.Collections/Persisted/PersistedList.cs
  - Seamless.Collections/Persisted/SerializedList.cs
  - Seamless.Collections/Persisted/PersistedHashIndex.cs
  - Test/Seamless/Persisted/StoreExtensions.cs
  - Work/qa/handoff.md
confidence: high
review_status: draft
---

# 03 Persisted コレクション（Seamless.Collections.Persisted）

## 行うべき機能

Persisted は「LINQ と DB の一時退避・中間結果を置く、内部の作業用ストレージ」である（【記録】決定9）。書き込みは1スレッド、件数は呼び出し側が管理する。

```csharp
var ids=store.CreatePersistedSet<long>("ids");
ids.Add(2);          // 追記。重複はここでは除かない
ids.CompactSave();   // 並べ替えて重複を除き、そのまま保存する(ストア全体の Save が1回入る)。ここから正確な件数を得る
```

| 機能 | 内容 | 状態 | 根拠 |
|---|---|---|---|
| ストアから作る | `CreatePersistedSet`・`CreatePersistedList`・`CreatePersistedDictionary` | 実装済み | `StoreExtensions.cs:11-23` |
| ディスク上限の確認 | ファイルが伸びるたびにストアのディスク予算で確認する | 実装済み | `StoreExtensions.cs:43-50`、`67-71` |
| 保存時点への復元 | 保存（Commit）した件数とデータ長へ戻す。保存されていない書きかけは捨てる | 実装済み | `StoreExtensions.cs:30-38`、`63-66`、`Seamless.Collections/Persisted/PersistedList.cs:13-25` |
| メモリ上限のない Contains | 要素のバイト列をキーにした mmap 上のハッシュ索引 | 実装済み | `Seamless.Collections/Persisted/PersistedHashIndex.cs:4-10` |
| ゼロコピー参照 | 固定長の要素を `AsSpan` で直接読む | 実装済み | 【記録】承認済み計画 §1「Persisted」 |
| 集合演算・列挙の逐次化 | 集合演算・列挙・Keys・Values を全件メモリに載せない | 実装済み | 【記録】STAGE-4b |
| Set/Dictionary のリフレクション除去 | レコード本体の形式を維持して `AutoSerializer` で作り分け | 実装済み | `Set.cs:9-17`、【記録】STAGE-4c |
| スコープ所有の一時資源 | `ScopedByteIndex`・`ScopedStoredSequence`（`ExecutionScope` が所有） | 実装済み | `ScopedByteIndex.cs`、`ScopedStoredSequence.cs` |
| AppendOnlyList のストア対応 | `CreatePersistedAppendOnlyList` | **未実装**（段階4残り） | `StoreExtensions.cs` に無い。旧 `Store.CreateAppendOnlyList`（`Store.cs:28-32`）だけ |
| 辞書の Compact の逐次化 | 現在は `System.Collections.Generic.Dictionary` に全件を集めてから書き直す | **未実装**（段階4残り） | `Dictionary.cs:185-193` |
| 固定長リスト生成のリフレクション除去 | `Activator.CreateInstance` と `MakeGenericType` | **未実装**（段階4残り） | `Store.cs:78-80` |
| 旧静的 API の削除 | `Store.CreateSet/CreateDictionary/CreateList/CreateTemp*` | **未実装**（段階3b/5） | `Store.cs:3-76`。【確認】2026-09-25 時点で利用箇所 46 |

## 1. 不変条件

1. **Garnet に依存しない**（`Seamless.Collections.Persisted.csproj` の参照は Collections と Execution だけ）。
2. **メモリストアでは作れない。** ファイルを使うため `NotSupportedException`（`StoreExtensions.cs:26-28`）。【記録】Q-003 で扱いが未決。
3. **保存されていない状態は確定していない。** 保存済み状態が無い名前でストアから作ると、前回の書きかけのファイルを消して新しく始める（`StoreExtensions.cs:30-38`）。
4. **保存後の追加は、開き直すと捨てられる。索引も保存時点へ戻る**（`Test/Seamless/Persisted/StoreExtensions.cs:30`）。
5. **ディスク上限を超えてファイルを伸ばすと `ResourceLimitException`。それまでの要素は読める**（同 `:52`）。
6. **Set の `Add` は追記だけで重複を除かない。`Count`/`GetLongCount()` は `CompactSave()` 前は重複を含む追記件数である**（`Set.cs:20-24`）。正確な件数が必要なときは `CompactSave()` の後で求める（決定9）。
7. **`Contains` は保存済みのバイト列の一致で判定する**（`Set.cs:79` 以降、`SerializedSet` の索引）。
8. **書き込みは1スレッド。内部ロックは持たない**（決定9）。
9. **Tsavorite と同じストアに作り、1回の `Save()` でまとめて確定できる**（`Test/Seamless/Persisted/StoreExtensions.cs:71`）。
10. **スコープ所有の資源（`ScopedByteIndex`・`ScopedStoredSequence`）は `ExecutionScope` が所有し、スコープ破棄後は使えない**（`docs/execution-and-database.md`「ExecutionScope」）。
11. **`Save()`（`Commit()` とスナップショットの書き出し）が既にディスクへ確実に書き出すので、`IStoreComponent.Flush()` は何もしない。** 共通ストアが全子資源の `Flush()` をまとめて呼ぶ（`docs/Opus5.5ReverseEnginnering/01`）恩恵は、Persisted にはまだ無い（`Commit()` を「書き込み」と「強制書き出し」に分けていない）。
12. **可変長リストの索引・データファイルは、保存後に追記した未確定の部分が壊れていても開ける。** 強制終了では、保存(`Commit`)後に追記した部分の書き戻しの順序が歯抜けになりうるので、構築時は最初の未使用エントリまでを数え(`LoadCount`)、壊れた記録から先は未確定として数えない(`LoadDataLength`)。保存済みの状態へ戻す `RollbackTo` が、数えた件数が保存済みの件数より少なければ拒否する(保存済みの部分の破損は検出される)。【記録】2026-09-30 の強制終了ハーネス(Persisted の辞書を含む)で、開く時点の `InvalidDataException("...gap between live entries")` を確認して直した(単体テスト `PersistedList.RollbackTo`)。保存(コミット)状態を持たない `AppendOnlyList` は、末尾が壊れていれば例外のまま。

## 2. 禁止事項

- Persisted に件数の正確さの責任を持たせない（重複除去を `Add` に入れない）。決定9 に反する。
- 全件をメモリへ集める経路を新しく足さない（承認済み計画 §2-8）。
- Persisted に Garnet への依存を入れない。
- 削除を公開しない（ハッシュ索引は tombstone を持たない。`PersistedHashIndex.cs:9`）。削除を足すなら索引の設計から見直す。
- Scoped 系を共通ストアへ移さない（【記録】承認済み計画 段階4「範囲外とする」）。

## 3. 前提条件

- 参照を含む型は MemoryPack で保存する。利用者は `[MemoryPackable]` を付け、MemoryPack を参照に加える（`README.md`）。
- ファイルはストアのフォルダの中（名前ごとのフォルダ）に `items.dat` などとして置く（`StoreExtensions.cs:13-22`）。
- 固定長の要素（参照を含まない型）はそのままのバイト列で保存する（`Set.cs:9-10` のコメント）。

## 4. 変更範囲 / スコープ

- `Seamless.Collections.Persisted/**`、`Seamless.Collections/Persisted/**`、`Seamless.Collections/Serialization/**`
- 利用側：`Seamless.Linq`（`ScopedQuery.ToStorage()`、`DiskLookup.cs`、`ExternalSort.cs`）、`Seamless.Database`（`DatabaseTransaction` の `ScopedByteIndex`）、FLinq の `Linq/Fragments/Persisted/*`、テスト・ベンチ。
- ファイル形式を変える場合は、`PersistedHashIndex` のヘッダ（`PersistedHashIndex.cs:12`）と `PersistedCommitState`（`PersistedList.cs:14`）を同時に見直す。

## 5. 非対象 / Out of Scope

- 並列書き込み。
- メモリストアでの動作（Q-003 の回答までは例外のまま）。
- OS 停止・電源断での耐久性（プロセス強制終了だけ確認済み：【記録】EXP-0005）。

## 6. 完了条件 / 成功条件

- 上の「未実装」4件がすべて実装され、旧静的 API の利用がリポジトリに残っていない。
- すべての操作が、ストアのメモリ上限に関係なく、要素数に比例したメモリを使わない（Compact を含む）。
- 保存→破棄→開き直し→復元で、Set・List・Dictionary・AppendOnlyList の内容が保存時点と一致する。

## 7. 検証項目 / 受け入れ条件

| 確かめること | 手段 |
|---|---|
| 一時ストアで作り、破棄でフォルダごと消える | `Test/Seamless/Persisted/StoreExtensions.cs:16` |
| 保存時点への復元（索引を含む） | 同 `:30`、`:101`（固定長）、`:121`（辞書） |
| ディスク上限の例外と既存要素の読み取り | 同 `:52` |
| メモリストアでは作れない | 同 `:66` |
| Tsavorite と同じストアで一括確定 | 同 `:71` |
| `Remove()` 後に索引が古いまま残らない | 【記録】BUG-0003（修正前 FAIL → 修正後 PASS） |
| Compact がメモリ上限内で動く | **未整備**。辞書の Compact を上限の小さいスコープで大量件数に対して動かすテストを追加する |
| LINQ 経由の走査 | `Test.Persisted.Linq.Enumerable.*`（4種） |

## 8. 移行条件

- 旧静的 API（`Store.CreateSet(path)`、`Store.CreateTemp*`）の用途は、一時用途を一時ストアへ、パス指定を永続ストアへ置き換える。
- 置き換えは Tsavorite の移行と同じ区切り（DB → LINQ → テスト・ベンチ）で行う。
- レコード本体の形式は維持する。2026-09-26の利用者決定により `.hindex` は新形式のみ受け付け、旧形式を拒否する。保存済みデータの自動変換はしない（決定11）。

## 9. ロールバック条件

- 保存・復元テストまたは LINQ の Persisted フレーバーで回帰が出たら、その区切りの変更だけを戻す。
- 基準は `73950c6e`（ストア対応を入れたコミット）。

## 10. 既知の問題 / 許容事項

- 【記録】BUG-0008（DEFERRED、HEAD から）：Persisted の FLinq フレーバーで `SequenceEqual(second,comparer)` が、順序なし列に対する例外を投げない。仕様と実装が不一致。
- 【記録】BUG-0004（DEFERRED）：`SerializedSet<T>` と `SerializedDictionary` に不要な `where T:struct` 制約（`Set.cs:56-62`）。
- `SerializedList.Rewrite` が一度 `ToArray()` で全件を集める（`Seamless.Collections/Persisted/SerializedList.cs:211`）。【推測・中】辞書の Compact と同じく、大量件数でメモリ上限を超えうる。

## 確認済み / 推測 / 不明

- **確認済み**：新 API 3種、メモリストアでの例外、書きかけの破棄、追記型の件数、未実装4件の位置、旧 API の利用数。
- **推測**：`Rewrite` の全件実体化による上限超過の可能性（中）。
- **不明**：Q-003 の結論（例外のまま／一時フォルダを使う／ファイルなしメモリマップ）。
