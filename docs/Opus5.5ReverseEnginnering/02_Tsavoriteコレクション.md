---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
target: Seamless.Collections.Tsavorite
evidence_sources:
  - Seamless.Collections.Tsavorite/Stores/StoreExtensions.cs
  - Seamless.Collections.Tsavorite/Stores/TsavoriteLayout.cs
  - Seamless.Collections.Tsavorite/Stores/TsavoriteTuning.cs
  - Seamless.Collections.Tsavorite/Stores/CollectionStore.cs
  - Seamless.Collections.Tsavorite/Stores/DictionaryStore.cs
  - Seamless.Collections.Tsavorite/Stores/Store.cs
  - Seamless.Collections.Tsavorite/Collections/Collection.cs
  - Seamless.Collections.Tsavorite/Collections/HashSet.cs
  - Seamless.Collections.Tsavorite/Collections/Dictionary.cs
  - Seamless.Collections.Tsavorite/Collections/IDictionary.cs
  - Seamless.Collections.Tsavorite/Collections/TryAddResult.cs
  - Test/Seamless/Stores/StoreExtensions.cs
  - Test/Seamless/Tsavorite/HashSet.cs
  - Work/qa/handoff.md
  - C:/Users/kazuh/.claude/plans/qa-workflow-md-validated-stream.md
confidence: high
review_status: draft
---

# 02 Tsavorite コレクション（Seamless.Collections.Tsavorite）

## 行うべき機能

作り方は「共通ストア → データ本体 → 操作用オブジェクト（スレッドごと）」の3段階。

```csharp
using var store=Seamless.Execution.Store.Open(path,new(){MemoryBytes=1L<<30,DiskBytes=64L<<30});
using var orders=store.CreateTsavoriteHashSet<long>("orders",expectedCount:10_000_000); // データ本体
using(var set=orders.Create()){set.Add(1);}                                          // 操作用オブジェクト
store.Save();
```

| 機能 | 内容 | 状態 | 根拠 |
|---|---|---|---|
| ストアから作る | `CreateTsavorite<K,V>`（辞書）・`CreateTsavoriteHashSet<T>`・`CreateTsavoriteSet<K,R>` | 実装済み | `Stores/StoreExtensions.cs:9-18` |
| 配置の自動決定 | デバイス、保存フォルダ、索引・ログの大きさをストアの種類・上限・予想件数から決める | 実装済み | `Stores/TsavoriteLayout.cs:44-60` |
| 詳細設定の公開 | ページ・セグメント・索引・自動拡張・ストレージ方式は公開 `TsavoriteTuning` で指定する。省略時の自動決定を維持 | 実装済み（2026-09-25 の追加承認） | `Stores/TsavoriteTuning.cs`、`Stores/StoreExtensions.cs` |
| 件数の管理 | データ本体が件数を持つ。`Create()` は増減する版、`CreateUncounted()` は増減しない版、`ApplyCountDelta` で差分反映 | 実装済み | `Stores/CollectionStore.cs:190-240`、`HashSetStore.cs:21-26`、`DictionaryStore.cs:31-64` |
| 索引の自動拡張 | ログの伸びから件数を推定し、混んでいれば索引を倍にする。拡張分は予算から先に予約 | 実装済み | `Stores/CollectionStore.cs:311-337` |
| 保存と復元 | ストアの `Save()` で確定。保存時にログを flush（EXP-0002） | 実装済み | `Test/Seamless/Stores/StoreExtensions.cs:121-175`、【記録】EXP-0002 |
| 標準の集合インターフェイス | `HashSet` が `System.Collections.Generic.ISet<T>` を実装 | 実装済み | `Collections/HashSet.cs:9` |
| 関係演算の逐次化 | 部分集合判定などで自分を全件メモリに載せない | 実装済み | `Collections/HashSet.cs:10-35` |
| `IReadOnlySet<T>` の実装 | 決定10 | **未実装**（文書コメントだけが実装済みと書いている） | `Collections/HashSet.cs:5` と `:9` の食い違い |
| 辞書の型引数を2つにする | 現在は `Dictionary<TKey,TValue,TKeySerializer,TValueSerializer>` の4つ | **未実装**（段階3d） | `Collections/Dictionary.cs:9-22` |
| 比較子の指定 | 既定はバイト一致、比較子を渡せば標準と同じ判定（決定7） | **未実装**（段階3d） | 比較子を受け取る生成 API がない（`Stores/StoreExtensions.cs`） |
| `TryAdd` を `bool` にする | 標準と同じ戻り値。容量超過は専用例外（決定10） | **未実装**（段階3d） | `Collections/IDictionary.cs:23`、`Collections/TryAddResult.cs:3-7` |
| 誤った並行使用の検出 | Release でも安価に検出して例外（段階3） | **未実装**（DEBUG で「作成スレッド以外からの使用」を警告するだけ） | `Collections/Collection.cs:135`、`:640-650` |
| 旧 API の削除 | 設定値 struct の `Store`、多数の `Create`、`IDevice` を受け取る公開 API、公開 `TsavoriteKV` フィールド | **未実装**（段階3b） | `Stores/Store.cs:6-50`、`Stores/CollectionStore.cs:94` |
| データ本体の改名 | `HashSetStore<T>`→`HashSet<T>.Store`、`DictionaryStore<K,V>`→`Dictionary<K,V>.Store`（決定12） | **未実装**（段階3b） | 現在の型名 `Stores/HashSetStore.cs`、`Stores/DictionaryStore.cs` |
| リフレクション生成の除去 | 辞書の生成が `MakeGenericType`/`Activator` に頼っている | **未実装** | `Stores/DictionaryStore.cs:71-83` |

## 1. 不変条件

1. **等値判定の既定はシリアライズ後のバイト列の一致**（`Collections/HashSet.cs:6`）。比較子を導入しても、既定の意味は変えない（決定7）。
2. **列挙の順序は保証しない。** 順序付き LINQ 演算子を使う側は、`AsEnumerable()` などで明示的に受け入れる（`Seamless.Linq.Analyzers` の `SEAMLINQ001`）。
3. **件数の正本は `long`（`GetLongCount()`）**（`Stores/CollectionStore.cs:236`）。件数を管理する版では、追加・削除のたびに増減し、挿入か上書きか分からない操作や一括の集合演算のあとは「不明」にして、次回に全走査で数え直す（`CollectionStore.cs:191-193`）。
4. **`CreateUncounted()` の操作用オブジェクトはデータ本体の件数を変えない。** 反映は `ApplyCountDelta` の1回だけ（`CollectionStore.cs:226-233`）。旧 API で作ったデータ本体に `ApplyCountDelta` を呼ぶと `NotSupportedException`（`:230-231`）。
5. **操作用オブジェクトが2つ以上生きている間は索引を拡張しない**（`CollectionStore.cs:313-318`）。Tsavorite 2.0.1-beta.5 の `GrowIndexAsync` を並行操作中に呼ぶと停止するため（【記録】EXP-0004）。
6. **索引拡張のメモリは予算から先に予約し、予約できなければ拡張しない。** 正しさは変わらず、性能だけが落ちる（`CollectionStore.cs:307-329`）。
7. **メモリ種は上限を超える追加を `ResourceLimitException` にし、それまでの要素を失わない**（`Test/Seamless/Stores/StoreExtensions.cs:75`）。退避先は `NullDevice`、判定は実アドレス＋余裕で行う（【記録】EXP-0001）。
8. **一時ストア・永続ストアでは、メモリ上のログを超えた分をディスクへ退避し、すべて読める**（`Test/Seamless/Stores/StoreExtensions.cs:91`）。ディスク上限を超える追加は `ResourceLimitException`（`:109`）。
9. **保存は明示的な `Save()` の時点だけが確定。** 保存後の追加は、開き直すと消える（`Test/Seamless/Stores/StoreExtensions.cs:121`）。
10. **索引の大きさはストアの保存状態に記録し、開き直すと拡張後の大きさで復元される**（`Test/Seamless/Stores/StoreExtensions.cs:132`、`:175`）。
11. **同じ名前のデータ本体は1つのストアに2つ作れない**（`Test/Seamless/Stores/StoreExtensions.cs:197`、`Seamless.Execution/Store.cs:111-115`）。
12. **FLinq 生成器が前提にしているメンバーを保つ。** `Iterate(ref strategy)`、`.Count`、`.Contains`、型名 `HashSet`/`Dictionary`。生成器は型名を文字列で照合している（【記録】承認済み計画「壊してはいけない結合点」：`ProviderFacts.cs`、`UnorderedSourceSafety.cs`、`OptimizeVisitor.cs`）。

## 2. 禁止事項

- 型名 `HashSet`・`Dictionary` を変えない（FLinq の照合が壊れる。決定10）。
- 他の操作用オブジェクトが生きている間に `GrowIndexAsync` を呼ばない（不変条件5）。
- 集合演算・関係演算で自分自身を全件メモリに載せない（承認済み計画 §2-8）。
- ページ・セグメント・`MutableFraction` を利用者向け API に出さない（決定3・6）。
- 1つの操作用オブジェクトを複数スレッドから**同時に**使わない。並行する処理ごとに `Create()` で作る。
- 誤用の検出を「作成スレッドと違えば例外」で実装しない。`Seamless.Database` はテーブルごとに1つの操作用オブジェクトを持ち（`Seamless.Database/DatabaseContainer.cs:408-411`）、書き込みを直列化したうえで別スレッドから順に使う（`AsyncLocal` の実行コンテキスト単位：同 `:18-20`）。スレッド固定で例外にするとこの使い方が壊れる。【推測・中】検出は「同時に2つの操作が入った」ことを数える方式にする。

## 3. 前提条件

- Garnet `Microsoft.Garnet`（Tsavorite 2.0.1-beta.5）に依存する。この版の制約（並行時の索引拡張不可）を受け入れる。
- シリアライザは型から自動で選ぶ。参照を含む型は MemoryPack、それ以外は生バイト（`Stores/DictionaryStore.cs:26-27`）。
- テストは挙動が分かるよう、わざと最小の大きさで作る。自動決定にしても、テストから `TsavoriteTuning` で最小サイズを指定できる状態を保つ（【記録】承認済み計画「前提（ユーザー指示）」）。

## 4. 変更範囲 / スコープ

- `Seamless.Collections.Tsavorite/**`
- 型名・メンバーを変える場合の照合先：`Seamless.FLinq.Generator/SourceGenerator/FLinqIncrementalGenerator.ProviderFacts.cs`、`UnorderedSourceSafety.cs`、`OptimizeVisitor.cs`、`Linq/Fragments/Tsavorite/*`。
- 利用側：`Seamless.Database/DatabaseContainer.cs`、`Seamless.Linq/Internal/ProviderEnumerators.cs`、`ProviderValueEnumerable.cs`、`Contains.cs`、テスト・ベンチ。

## 5. 非対象 / Out of Scope

- Tsavorite 本体の不具合修正（並行時の索引拡張）。
- 旧形式のチェックポイントとの互換（決定11）。
- 1つの `TRecord` がキーを二重に保存している点の最適化（`Stores/SetStore.cs:5` の `KEYED-RECORD-CODEC-TODO`）。別課題。

## 6. 完了条件 / 成功条件

- 上の「未実装」がすべて実装され、旧 API への参照がリポジトリに残っていない。
- `HashSet` が `ISet<T>` と `IReadOnlySet<T>`、`Dictionary` が `IDictionary<K,V>` と `IReadOnlyDictionary<K,V>` を完全に実装し、戻り値と例外が標準と一致する（決定10）。
- 標準コレクションとの差分テスト（同じ操作列を両方に流して結果を比べる）が、比較子あり・なしの両方で通る。

## 7. 検証項目 / 受け入れ条件

| 確かめること | 手段 |
|---|---|
| 3種類のストアでの追加・上書き・読み取り・保存・復元 | `Test/Seamless/Stores/StoreExtensions.cs:14`（`CreateTsavorite`）、`:55`、`:204` |
| 上限超過（メモリ・ディスク）が例外になり、既存要素が残る | 同 `:75`、`:109` |
| 索引の自動拡張と、拡張後の保存・復元 | 同 `:159`、`:175` |
| 関係演算が標準 `HashSet` と一致 | `Test/Seamless/Tsavorite/HashSet.cs:25-68` |
| 並列で件数を管理する版を使っても件数が正確 | 【記録】STAGE-3c の仕様テスト |
| `CreateUncounted` の差分反映後に件数が正確 | 【記録】STAGE-3c |
| 大量件数で常駐メモリが上限内 | `Test.Scalable.Tsavorite.Collections`（1,000万件規模）【記録】承認済み計画「検証」 |
| FLinq の生成コードが引き続きネイティブ走査に展開される | `Test/SourceGenerator/ProviderContainsShapeTests.cs`、`Test.Tsavorite.Linq.Enumerable.*` |

## 8. 移行条件

- 旧 API（`Tsavorite.Store.Create(...)` → `.Create<T>(logDevice,objDevice)`）を使う箇所は、【確認】2026-09-25 時点で **49 ファイル**（`Test` 27、`Shared` 11、ライブラリ自身 3、`Tests` 2、`Benchmark` 2、`Seamless.Database` 1、その他 3）。新 API の利用はテスト 8 ファイルだけ。
- 【記録】2026-09-19 時点の棚卸し：旧 `Store.Create(` 249 か所、デバイス直渡し 90、番号指定の保存・復元 92、型名 57。
- 移行順は DB → LINQ → テスト・ベンチ。改名（段階3b）と型引数・比較子・`TryAdd`（段階3d）は同じ作業で行う。
- 最小サイズを使うテストは 公開 `TsavoriteTuning`へ移す。

## 9. ロールバック条件

- 改名・旧 API 削除の途中でビルドが通らない、または回帰が出たら、そのプロジェクト群の移行だけを戻す。新 API（`73950c6e`）は残す。
- FLinq の形状テスト（ネイティブ走査への展開）が崩れたら、型名・メンバーの変更を戻す。

## 10. 既知の問題 / 許容事項

- 【記録】Q-004（未回答）：辞書の代入（`dict[k]=v`）のあとは、次に件数を取るときに全件を数え直す（O(n)）。O(1) にするには Tsavorite に「挿入か上書きか」を返す関数を足す必要がある。
- 【記録】Q-004：並列書き込み中は索引を拡張しない。大量並列投入では `expectedCount` を渡す。
- 【記録】メモリ種の集合演算の作業リストは `%TEMP%` を使う。
- 【記録】BUG-0001（DEFERRED）：辞書のインデクサ setter が `fixed` の外で Pending を呼んでいる疑い。BUG-0002（DEFERRED）：セッションの二重 Dispose。どちらも段階3b でセッションまわりを作り直すときに解消する予定。
- 【記録】BUG-0006（DEFERRED）：`StoreCheckpoint` の async テストが共有違反で不安定。4つの仮説を否定済み、原因未特定。
- 【記録】作成時に Tsavorite 内部で約 8.5MB を確保する（OBS-0001）。空に近いストアでも減らせない。

## 確認済み / 推測 / 不明

- **確認済み**：新 API の3種類、件数管理、索引拡張の条件、旧 API の残存数、`IReadOnlySet<T>` 未実装、`TryAdd` が列挙型、リフレクション生成、スレッド検査が DEBUG 限定。
- **推測**：なし。
- **不明**：比較子を指定した場合のハッシュ計算と保存形式（比較子でハッシュし、候補を復元して比べる、という決定7の方針はあるが、ディスク上の表現は未設計）。
