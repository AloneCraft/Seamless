# Collection 仕様書統合

# Source: Document\Code\Internal\TsavoriteCollectionsInternalDesign.md

---
phase: code-internal-reverse
reverse_document: true
canonical_target: Collection.md
evidence_sources:
  - Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj
  - Benchmark.Tsavorite.Collections/Stores/HashStore.cs
  - Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs
  - Benchmark.Tsavorite.Collections/Collections/Dictionary.cs
  - Benchmark.Tsavorite.Collections/Collections/HashSet.cs
  - Benchmark.Tsavorite.Collections/Collections/Set.cs
  - Benchmark.Tsavorite.Collections/Linq/Enumerable.cs
  - Benchmark.Tsavorite.Collections/LinqCounter.cs
confidence: medium
review_status: draft
---

# Benchmark.Tsavorite.Collections 内部設計

## 対象
`Benchmark.Tsavorite.Collections` は `net10.0` / C# 14 のライブラリで、Garnet/Tsavorite 系ストア、MemoryPack serializer、LINQ 拡張、計測用 counter を含む（`Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:4`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:5`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:14`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:15`）。

## 主要クラス

| クラス/領域 | 責務 | 実装証拠 |
|---|---|---|
| `HashStore` | index/page/segment/memory/checkpoint を受け取る store factory。Dictionary、HashSet、Set store を生成する。 | `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:38`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:146`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:185`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:240`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:280` |
| `Collection<TKey,TValue,...>` | Tsavorite KV を背後に持つ collection 基底。Add、TryAdd、Remove、TryGetValue、Clear、Count を提供する。 | `Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs:131`, `Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs:160`, `Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs:241`, `Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs:256`, `Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs:563` |
| `Dictionary<TKey,TValue,...>` | `IDictionary` 契約を collection 上に載せる。KeyValuePair 操作、remove with value、enumerator view を提供する。 | `Benchmark.Tsavorite.Collections/Collections/Dictionary.cs:8`, `Benchmark.Tsavorite.Collections/Collections/Dictionary.cs:23`, `Benchmark.Tsavorite.Collections/Collections/Dictionary.Tsavorite.cs:51`, `Benchmark.Tsavorite.Collections/Collections/Dictionary.Tsavorite.cs:65`, `Benchmark.Tsavorite.Collections/Collections/Dictionary.Tsavorite.cs:120` |
| `HashSet<TKey>` | set API と Tsavorite scan 最適化の対象。集合演算は partial 実装へ分割される。 | `Benchmark.Tsavorite.Collections/Collections/HashSet.cs:8`, `Benchmark.Tsavorite.Collections/Collections/HashSet.cs:12`, `Benchmark.Tsavorite.Collections/Collections/HashSet.cs:28`, `Benchmark.Tsavorite.Collections/Collections/HashSet.Tsavorite.cs:79`, `Benchmark.Tsavorite.Collections/Collections/HashSet.Scan.cs:8` |
| `Set<TKey,TRecord>` | `IRecord<TKey>` による key/value record set。Set 演算を `TsavoriteSetOperations` に委譲する。 | `Benchmark.Tsavorite.Collections/Collections/Set.cs:30`, `Benchmark.Tsavorite.Collections/Collections/Set.cs:36`, `Benchmark.Tsavorite.Collections/Collections/Set.cs:70`, `Benchmark.Tsavorite.Collections/Collections/Set.cs:203` |
| `Linq.Enumerable` | `IHashSet<T>` 向け LINQ-like extension を提供する。 | `Benchmark.Tsavorite.Collections/Linq/Enumerable.cs:8`, `Benchmark.Tsavorite.Collections/Linq/Enumerable.cs:18`, `Benchmark.Tsavorite.Collections/Linq/Enumerable.cs:2217`, `Benchmark.Tsavorite.Collections/Linq/Enumerable.cs:2238`, `Benchmark.Tsavorite.Collections/Linq/Enumerable.cs:2258` |
| `LinqCounter` / `LinqCounterScope` | 物理反復、論理演算、delegate、hash build/probe などを thread-safe に計測する。 | `Benchmark.Tsavorite.Collections/LinqCounter.cs:9`, `Benchmark.Tsavorite.Collections/LinqCounter.cs:31`, `Benchmark.Tsavorite.Collections/LinqCounter.cs:45`, `Benchmark.Tsavorite.Collections/LinqCounter.cs:142`, `Benchmark.Tsavorite.Collections/LinqCounter.cs:155` |

## データ構造

- `HashStore` は checkpoint metadata を `.Tsavorite` 配下の JSON として書き、復旧時の推奨 index size を解決する（`Benchmark.Tsavorite.Collections/Stores/HashStore.cs:82`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:90`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:94`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:104`）。
- serializer 戦略は blittable と MemoryPack に分かれる（`Benchmark.Tsavorite.Collections/Stores/SerializerStrategy.cs:3`, `Benchmark.Tsavorite.Collections/Stores/SerializerStrategy.cs:7`, `Benchmark.Tsavorite.Collections/Stores/SerializerStrategy.cs:11`）。
- `MemoryOnlyDevice` は read/write を例外化する memory-only storage device として定義される（`Benchmark.Tsavorite.Collections/Devices/MemoryOnlyDevice.cs:5`, `Benchmark.Tsavorite.Collections/Devices/MemoryOnlyDevice.cs:9`, `Benchmark.Tsavorite.Collections/Devices/MemoryOnlyDevice.cs:24`）。

## 処理アルゴリズム

1. 利用者は `HashStore.Create(...)` で store factory を作る。
2. factory overload で dictionary/hashset/set store を作る。
3. store から collection instance を作り、operation は session function または collection 基底へ流れる。
4. LINQ 拡張は `IHashSet<T>` を source とし、iterator または生成コード最適化の入力として動作する。

## Review Gate: Code Internal

### Confirmed
- Tsavorite/Garnet と MemoryPack に依存する collection library である。
- store factory、collection 基底、具象 collection、LINQ 拡張、counter が分離されている。
- checkpoint index metadata を読み書きする復旧支援がある。

### Inferred
- confidence: medium。メモリ内利用と device-backed 利用の両方を想定している。
- confidence: medium。`IHashSet<T>` 向け LINQ は `FLinq` の最適化対象としても設計されている。

### Unknown
- Tsavorite/Garnet の運用上の永続性保証、並行更新保証、障害復旧保証の公開範囲。
- serializer strategy の利用者向け推奨選択基準。

### Questions
- 通常設計化時には、ストアの永続化契約を API 契約として明文化するかを確認する。
