---
phase: code-external-reverse
reverse_document: true
canonical_target: Document/Code/External/CodeModuleInterface.md
evidence_sources:
  - FLinq/SourceGenerator/interceptor.cs
  - Tsavorite.Collections/Collections/
  - Tsavorite.Collections/Stores/
  - Seamless.Database/SourceGenerator/GenerateDatabase.cs
  - Benchmark/
derived_from:
  - Reverse/Code/Internal/
confidence: medium
review_status: draft
---

# Code Module Interface

## 公開境界

| モジュール | 外部から見える契約 | 実装証拠 |
|---|---|---|
| `FLinq` source generator | `LinqInterceptAttribute`、`LinqInstrumentation`、interceptor generated source。利用者コードの LINQ 呼び出しを compile-time に置換する。 | `FLinq/SourceGenerator/interceptor.cs:29`, `FLinq/SourceGenerator/interceptor.cs:38`, `FLinq/SourceGenerator/interceptor.cs:55`, `FLinq/SourceGenerator/interceptor.cs:64` |
| `Tsavorite.Collections` store factory | `HashStore.Create(...)` と overload による Dictionary/HashSet/Set store 生成。 | `Tsavorite.Collections/Stores/HashStore.cs:38`, `Tsavorite.Collections/Stores/HashStore.cs:146`, `Tsavorite.Collections/Stores/HashStore.cs:240`, `Tsavorite.Collections/Stores/HashStore.cs:280` |
| `Persisted.Collections` static store factory | `Store.CreateList`、`CreateSet`、`CreateDictionary`、`CreateAppendOnlyList` による path 指定の永続 collection 生成。Temp 系は一時 path を生成し、Dispose 時に一時 directory を削除する。 | `Seamless.Collections.Persisted/Persisted/Collections/Store.cs`, `Seamless.Collections.Persisted/Persisted/Collections/Dictionary.cs` |
| `Tsavorite.Collections` collection API | `IDictionary<TKey,TValue>`、`IHashSet<T>`、`ISet<TRecord>`、`IMemoryCollection<T>` 相当の操作。 | `Tsavorite.Collections/Collections/IDictionary.cs:9`, `Tsavorite.Collections/Collections/IHashSet.cs:8`, `Tsavorite.Collections/Collections/ISet.cs:3`, `Tsavorite.Collections/Collections/IMemoryCollection.cs:6` |
| `Tsavorite.Collections` LINQ API | `IHashSet<T>` に対する LINQ-like extension。 | `Tsavorite.Collections/Linq/Enumerable.cs:8`, `Tsavorite.Collections/Linq/Enumerable.cs:18`, `Tsavorite.Collections/Linq/Enumerable.cs:2238`, `Tsavorite.Collections/Linq/Enumerable.cs:2258` |
| `Seamless.Database` source generator | `DatabaseGenerator` marker と schema partial struct を入力に、MemoryPack 対応 generated source を出す。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:37`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:52`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:114` |
| `Benchmark` | desktop UI と worker mode。Benchmark method discovery、CPU 選択、CSV/HTML report 出力。 | `Benchmark/Shared/UI/BenchmarkWorkerClient.cs:17`, `Benchmark/MainPage.xaml.cs:841`, `Benchmark/MainPage.xaml.cs:1153` |

## API 利用順序

### FLinq
1. 利用者または assembly/class/method に `LinqIntercept` を付与する、または既定の interception 対象になる。
2. generator が LINQ 呼び出しを検出し、interceptor source を生成する。
3. 生成された interceptor が compile-time/runtime の呼び出し先になる。

### Tsavorite.Collections
1. `HashStore.Create` で store factory を構成する。
2. Dictionary/HashSet/Set store を生成する。
3. store から collection instance を生成して操作する。
4. 必要に応じて LINQ extension または `LinqCounterScope` を使用する。

### Persisted.Collections
1. 通常利用では `Store.CreateList`、`Store.CreateSet`、`Store.CreateDictionary`、`Store.CreateAppendOnlyList` に path と初期容量を渡す。
2. 一時利用では `Store.CreateTempList`、`CreateTempSet`、`CreateTempDictionary`、`CreateTempAppendOnlyList` を使い、Dispose 時に一時 directory を削除する。
3. Tsavorite の `Store.Create(...)` は index/page/segment/memory/checkpoint 設定を保持する instance factory だが、Persisted の `Store` は store-wide 状態を持たない static factory とする。
4. `StorageTuning` は Persisted 固有の file I/O tuning であり、Tsavorite の `MutableFraction` 相当の容量制御ではない。

### Benchmark
1. UI は benchmark project/method を読み込む。
2. selected CPU ごとに worker process を起動する。
3. worker task/result message で BenchmarkDotNet 実行結果を戻す。
4. report formatter が CSV/HTML を生成する。

## Review Gate: Code External

### Confirmed
- 公開境界は generator 属性、collection/store API、LINQ extension、benchmark worker protocol に分かれる。
- `Tsavorite.Collections` は `IDisposable` を含む collection 契約を公開している。

### Inferred
- confidence: medium。`Benchmark/UI` はライブラリ利用者向け API ではなく、開発・計測用ツール境界である。

### Unknown
- NuGet package として公開される API surface の正式リスト。
- source generator 属性の後方互換性保証。
