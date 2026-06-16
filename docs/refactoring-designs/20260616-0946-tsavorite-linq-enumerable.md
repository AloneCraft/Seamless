# Tsavorite.Linq.Enumerable 移行設計

## 背景

`System.Linq.Enumerable` は `System.Collections.Generic.IEnumerable<T>` を中心にした LINQ API である。一方、`Tsavorite.Collections` には Tsavorite backed な `IHashSet<T>`/`IDictionary<TKey,TValue>`/memory-mapped `List<T>` があり、これらには Tsavorite の storage/session/scan を前提にした LINQ API が必要になる。

現状は `Tsavorite.Linq.HashSet` が `IHashSet<T>` 向け LINQ API に相当しているが、名前が対象型に寄りすぎており、`System.Linq.Enumerable` に対応する API としては `Tsavorite.Linq.Enumerable` が適切である。

## 現状の観察

- `src/Tsavorite.Collections/Linq/HashSet.cs` は `namespace Tsavorite.Linq; public static partial class HashSet` として広い LINQ API を提供している。
- `HashSet.cs` の多くの iterator は `global::System.Collections.Generic.List<T>` / `HashSet<T>` / `Dictionary<TKey,TValue>` に材料化してから返しており、大容量中間バッファ禁止方針と衝突する。
- `src/Tsavorite.Collections/Collections/IHashSet.cs` の既定 `IntersectWith` / `SymmetricExceptWith` も `System.Collections.Generic.List<T>` を中間に使っている。
- `Collection.Default.cs` の `Enumerator` は `IterateLookup` 結果を `InMemoryList<T>` に全件格納してから列挙しており、Tsavorite の並列性と大容量列挙の観点で見直し対象。
- `src/FLinq/SourceGenerator` は `System.Linq.Enumerable` 固定判定が多い。代表例は `LinqTargetDiscovery.cs`, `LinqMethodBodyRewriter.cs`, `LinqCounterOnlyMethodBodyRewriter.cs`, `LinqFragmentBuilder.cs`, `LinqGeneratorModels.cs`, 各 `Fragments`。
- `tests/Test.Tsavorite.Collections/Intercept/System/Linq` には Chain1/Chain2/FragmentCoverage/Optimization の豊富なテストがある。
- `tests/Test.Tsavorite.Collections/Intercept/Tsavorite/Linq` には既存 `Tsavorite.Linq.HashSet` 向けテストがある。
- `sandbox/Benchmark.Shared` は共通ソース化の下地として使える。
- `src/Tsavorite.Collections/Tsavorite.Collections.csproj` は `Microsoft.Garnet 2.0.1-beta.2`、`src/Seamless.Data/Seamless.Data.csproj` は `2.0.0-beta.3` を参照していた。方針確認により、どちらも NuGet 上の最新 prerelease `2.0.1-beta.3` へ更新する。
- Garnet/Tsavorite は read/upsert/delete/RMW を中心にした narrow-waist storage API を提供する、という公開説明がある。
- `Tsavorite.Collections.HashSet<TKey>` は `AutoSerializer<TKey>` を使っており、`RuntimeHelpers.IsReferenceOrContainsReferences<T>()` に応じて MemoryPack または blittable span を選ぶ。したがって receiver が既存 `HashSet<T>` の場合、managed/unmanaged serializer 差は LINQ 展開の主な制約ではない。

## 目的

- `Tsavorite.Linq.Enumerable` を正規 API にする。
- 既存 `Tsavorite.Linq.HashSet` は互換レイヤーに移す。
- `Tsavorite.Collections.HashSet<T>` / `IHashSet<T>` を generator の intercept 対象にする。
- System LINQ 用の既存テストをできるだけ共有し、同じ入力ソースを System/Tsavorite の両方に適用できる構造にする。
- 大容量中間バッファとして `System.Collections.Generic` を使わない。
- Tsavorite の storage/session/scan/RMW 前提を壊さず、並列性を殺す全件ロックや全件メモリ材料化を避ける。

## 非目的

- `System.Linq.Enumerable` の全 API を一度で完全互換にすること。
- テストコード内の小規模期待値生成や正規化まで完全に `System.Collections.Generic` 禁止にすること。
- `List<T>` を Tsavorite backed に変更すること。`List<T>` は memory-mapped file のまま扱う。
- 既存公開 API を即時削除すること。

## 提案設計

1. API 名
   - `namespace Tsavorite.Linq; public static partial class Enumerable` を追加する。
   - 実装ファイルは `Enumerable.HashSet.cs`, `Enumerable.Dictionary.cs`, `Enumerable.List.cs`, `Enumerable.Set.cs`, `Enumerable.Internal.*.cs` に分ける。
   - `Tsavorite.Linq.HashSet` は互換用 partial class として残し、可能な範囲で `Enumerable` に委譲する。

2. Tsavorite LINQ の戻り値
   - 単純な `Where` / `Select` / `Take` などは遅延列挙できる専用 enumerable/enumerator を返す。
   - set/join/group/order 系で状態が必要なものは、`System.Collections.Generic` の大容量バッファではなく Tsavorite backed `HashSet`/`Dictionary` または memory-mapped `List<T>` を使う。
   - `ToHashSet`/`ToDictionary` は Tsavorite collection を返す overload を追加する。System 互換名が曖昧になる場合は `ToTsavoriteHashSet` / `ToTsavoriteDictionary` を併設する。

3. 走査基盤
   - 現在の `IEnumerable<T>` enumerator は全件 `InMemoryList<T>` に積むため、intercept と専用演算では `ForEach<TCollection,TStrategy>` / `IScanIteratorFunctions` ベースのストリーミング走査を優先する。
   - `IHashSet<T>` の静的型でも、`IEnumerable<T>` と同じく `GetEnumerator()` を root にした LINQ-level 展開は可能とする。
   - storage-level 展開では `IHashSet<T>` の `foreach` に戻さず、Tsavorite 専用 scan strategy を呼ぶコードを生成する。
   - storage-level 展開を要求する経路で対応できない場合は既存 enumerator 経由へ黙って戻さず、例外を生成する。完全展開できない LINQ はテストで検出できる状態にする。

4. Source Generator
   - `System.Linq.Enumerable` 固定判定を `LinqMethodFamily` のような内部 descriptor に置き換える。
   - family は最低限 `SystemEnumerable` と `TsavoriteEnumerable` を持つ。
- `TsavoriteEnumerable` は `Tsavorite.Linq.Enumerable` の reduced extension call、かつ receiver が `Tsavorite.Collections.IHashSet<T>` または具象 `Tsavorite.Collections.HashSet<T>` のものを対象にする。
   - 初期対応は `IHashSet<T>` の LINQ-level 展開と、具象 `Tsavorite.Collections.HashSet<T>` の storage-level 展開を分けて扱う。
   - 最初の実装対象は `HashSet<T>` receiver の scalar 系とする。対象は `Any` / `All` / `Count` / `LongCount` / `Contains` / `First` / `FirstOrDefault` / `Aggregate`。
   - `Where` / `Select` / `ToArray` など列挙結果を返すものは、LINQ-level 展開なら `GetEnumerator()` root で初期対応できる。storage-level 展開では scan callback と結果 enumerable の寿命管理を別途設計し、全件 `GetEnumerator()` fallback は使わない。
   - `GroupBy` / `Join` / `OrderBy` / `Distinct` / `Except` / `Intersect` / `Union` は専用 storage-backed 中間構造が整ってから段階対応する。

5. テスト/ベンチ共通化
   - 共通のテストシナリオは `Shared` 化し、source factory を差し替える。
   - System 版は `int[]` / `System.Collections.Generic.HashSet<T>` を返す factory。
   - Tsavorite 版は `HashStore.Create().Create<T>().CreateOwned()` で `Tsavorite.Collections.HashSet<T>` を返す factory。
   - using/alias で `Enumerable` の解決先を切り替える案を使う。ただし extension method の解決衝突が出る箇所は明示 static call または namespace 分離にする。
   - `sandbox/Benchmark.Shared` は正式な共有プロジェクトまたは `Directory.Build.props` からの Compile Include に移すか、少なくとも `src/Benchmark` と `tests/Test.Tsavorite.Collections.Benchmark` が同じソースを参照する構成にする。

6. 最新 Tsavorite API 対応
   - `Microsoft.Garnet 2.0.1-beta.3` に上げ、`Tsavorite.core.xml` と reflection で `TsavoriteKV`, `ClientSession`, `IScanIteratorFunctions`, `IterateLookup`, `NewSession`, `BasicContext` の使用箇所を確認する。
   - API 直接依存を `Collection` 内部の薄い scan/session helper に集約し、次の beta での差分を局所化する。

## 展開可能性の分類

展開は次の 2 段階に分けて扱う。

- LINQ-level 展開: `IHashSet<T>` の静的型でも `IEnumerable<T>` と同じく、`GetEnumerator()` を root にして LINQ iterator/delegate を生成コードのループへ畳み込む。これは可能であり、初期対応に含める。
- storage-level 展開: `Tsavorite.Collections.HashSet<T>` または専用 scan interface から直接走査し、`IEnumerable<T>` fallback や大容量 `System.Collections.Generic` 中間材料化を使わない。これを最終的な完全展開と呼ぶ。

### 初期対応候補

以下は状態をほぼ持たない、または定数サイズの状態だけで走査できるため、`IHashSet<T>` の LINQ-level 展開にも、Tsavorite scan strategy への storage-level 展開にも落とし込みやすい。

- `Where`
- `Select`
- `Any`
- `All`
- `Count`
- `LongCount`
- `Contains`
- `Aggregate`
- `First`
- `FirstOrDefault`
- `Single`
- `SingleOrDefault`
- `ElementAt`
- `ElementAtOrDefault`
- `Last`
- `LastOrDefault`
- `Sum`
- `Average`
- `Min`
- `Max`
- `MinBy`
- `MaxBy`
- `Take`
- `Skip`
- `TakeWhile`
- `SkipWhile`
- `Append`
- `Prepend`
- `Concat`
- `DefaultIfEmpty`
- `Index`
- `Zip`
- `SequenceEqual`
- `Cast`
- `OfType`

`ToArray` は最終結果が配列であるため例外的に許可する。`ToList` は System `List<T>` ではなく memory-mapped `Tsavorite.Collections.List<T>` を返す overload を追加する場合に初期対応候補に入れる。

### Tsavorite-backed 中間構造が必要

以下は現行 System LINQ fragment が `System.Collections.Generic.List/HashSet/Dictionary/Queue` を大容量中間構造として生成するため、そのままでは Tsavorite 版の完全展開とはみなさない。Tsavorite backed `HashSet`/`Dictionary` または memory-mapped `List<T>` へ置換してから対応する。

- `Distinct`
- `DistinctBy`
- `Except`
- `ExceptBy`
- `Intersect`
- `IntersectBy`
- `Union`
- `UnionBy`
- `Order`
- `OrderBy`
- `OrderDescending`
- `OrderByDescending`
- `ThenBy`
- `ThenByDescending`
- `GroupBy`
- `GroupJoin`
- `Join`
- `LeftJoin`
- `RightJoin`
- `Reverse`
- `Shuffle`
- `SkipLast`
- `TakeLast`
- `ToHashSet`
- `ToDictionary`
- `ToLookup`
- `CountBy`
- `AggregateBy`

### 対象外または特殊扱い

- `AsQueryable`: LINQ to Objects の展開ではなく `IQueryable` 化なので、初期対応対象外。
- `TryGetNonEnumeratedCount`: LINQ chain fragment ではなく count 取得 API として、`HashSet<T>.EntryCount` / `GetLongCount` / 専用 count helper に直接落とす。
- `Empty`, `Range`, `Repeat`, `InfiniteSequence`, `Sequence`: collection receiver を持たない root generator。Tsavorite collection 用ではなく root sequence として扱う。

### 静的型による制約

receiver の静的型が `IHashSet<T>` のままでも、現在の interface だけで LINQ-level 展開はできる。ただし interface が公開しているのは列挙子であり、現在の `HashSet<T>` enumerator は内部で `InMemoryList<T>` に積むため、storage-level 展開にはならない。

storage-level 展開まで行うには次のいずれかが必要。

- `Tsavorite.Collections.HashSet<T>` など具象型を generator が認識する。
- `IHashSet<T>` に非列挙 scan/for-each 用の専用 primitive を追加する。
- `where TSource : ITsavoriteEnumerableSource<T>` のような generic 制約で、展開可能な source contract を明示する。

### InMemoryList と展開の関係

`InMemoryList` を経由しても LINQ-level 展開はできる。つまり `IHashSet<T>.GetEnumerator()` を root にして、`Where` / `Select` / `Count` などの LINQ chain を生成コードのループへ畳み込むことはできる。

ただし `HashSet<T>.GetEnumerator()` は `Collection.Default.Enumerator` の constructor で `new InMemoryList<T>` を作り、`IterateLookup` と `CompletePending` を constructor 中に完了させ、`MoveNext()` はその list を読むだけになる。この時点で Tsavorite storage との接続が切れるため、storage-level 展開とはみなさない。

`InMemoryList` 経由が storage-level 展開ではない理由は次の通り。

- `Any` / `First` / `Take` のような早期終了可能な演算でも、列挙子生成時点で全件 scan と全件格納が走る。
- `Where` の predicate や `Take` の件数制限を Tsavorite の scan callback に押し込めない。
- 中間に `System.Collections.Generic.List<T>` が残り、大容量中間バッファを避けるという目的を満たせない。
- scan 中に処理を止める、集計だけを保持する、結果だけを Tsavorite-backed collection に流す、といった専用戦略を generator が選べない。

推奨実装は、互換性のための LINQ-level 展開と、高性能な storage-level 展開を明示的に分ける。

1. `IHashSet<T>` では `GetEnumerator()` root の LINQ-level 展開を許容する。ただしこれは互換・段階移行用であり、storage-level 完全展開とは呼ばない。
2. `HashSet<T>` には既存の `HashSet.Scan<TStrategy>(ref TStrategy)` を土台に、source generator から参照できる scan contract を追加する。
3. scan contract は `void Scan<TStrategy>(ref TStrategy strategy)` とし、`TStrategy` は `struct` + `static abstract` reader/invoker を持つ。delegate や interface dispatch を避け、predicate/projection/aggregate state を struct に畳み込む。
4. generator は receiver が scan contract を満たす場合だけ storage-level code path を生成する。満たさない `IHashSet<T>` は LINQ-level code path に落とすか、storage-level を要求する API では例外にする。
5. `Where` / `Select` / `Any` / `All` / `Count` / `First` / `Aggregate` などは scan strategy の合成で実装し、`Distinct` / `GroupBy` / `Join` / `OrderBy` などは Tsavorite-backed 中間構造ができてから対応する。

### scan contract の具体例

scan contract とは、`IEnumerator<T>` を返す代わりに、storage scan 中に 1 レコードずつ struct strategy を呼ぶための共通 API である。

最小形は次のようにする。

```csharp
public interface ITsavoriteScanStrategy<TSelf, T>
    where TSelf : struct, ITsavoriteScanStrategy<TSelf, T>
{
    // true: scan 継続、false: scan 停止
    static abstract bool Invoke(ref TSelf self, in T item);
}

public interface ITsavoriteScanSource<T>
{
    void Scan<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ITsavoriteScanStrategy<TStrategy, T>;
}
```

`HashSet<T>` は既存の `HashSet.Scan<TStrategy>(ref TStrategy)` と `IHashSetScanStrategy<TSelf,T>` をこの public/internal contract に寄せて実装する。

```csharp
public partial class HashSet<T> : ITsavoriteScanSource<T>
{
    public void Scan<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ITsavoriteScanStrategy<TStrategy, T>
    {
        var state = new ScanState<TStrategy>(strategy);
        var adapter = new ScanAdapter<TStrategy>(state);

        this.Store.Log.ShiftReadOnlyAddress(this.Store.Log.TailAddress, wait: true);
        this.ReadSession.IterateLookupSnapshot(ref adapter, includeTombstones: false);
        this.ReadSession.BasicContext.CompletePending(wait: true);

        strategy = state.Strategy;
    }
}
```

例えばユーザーコードが次の場合、

```csharp
var result = source.Where(x => x > 10).Count();
```

LINQ-level 展開では次のように列挙子 root になる。この場合、`GetEnumerator()` の時点で `InMemoryList<T>` への全件読み込みが発生する。

```csharp
var count = 0;
using var e = source.GetEnumerator();
while (e.MoveNext())
{
    var x = e.Current;
    if (x > 10)
        count++;
}
var result = count;
```

storage-level 展開では次のような strategy に変換する。

```csharp
private struct WhereCountGreaterThan10
    : ITsavoriteScanStrategy<WhereCountGreaterThan10, int>
{
    public int Count;

    public static bool Invoke(ref WhereCountGreaterThan10 self, in int item)
    {
        if (item > 10)
            self.Count++;

        return true;
    }
}
```

呼び出し側は列挙子を作らず、Tsavorite scan に strategy を渡す。

```csharp
var strategy = new WhereCountGreaterThan10();
source.Scan(ref strategy);
var result = strategy.Count;
```

`First` のような早期終了も strategy の戻り値で表現する。

```csharp
private struct FirstGreaterThan10
    : ITsavoriteScanStrategy<FirstGreaterThan10, int>
{
    public bool Found;
    public int Value;

    public static bool Invoke(ref FirstGreaterThan10 self, in int item)
    {
        if (item <= 10)
            return true;

        self.Found = true;
        self.Value = item;
        return false;
    }
}
```

この形なら `Any` / `First` / `Take` が全件 `InMemoryList<T>` を作らずに途中停止できる。`Where` / `Select` / `Aggregate` は strategy の状態として畳み込める。`ToArray` / `ToList` のように結果を蓄積するものは、strategy 内の出力先を配列 builder、memory-mapped `List<T>`、または Tsavorite-backed collection にする。

### 外部 iterator と内部 iterator

この設計は、外部 iterator と内部 iterator の使い分けでもある。

- 外部 iterator: 呼び出し側が `IEnumerator<T>` を受け取り、`MoveNext()` / `Current` で pull する。LINQ to Objects の標準形。
- 内部 iterator: collection 側が scan を所有し、各 record に対して strategy/callback を呼ぶ。Tsavorite storage に近い場所で処理を push する。

外部 iterator でも generator は LINQ chain をループへ展開できる。ただし現在の `HashSet<T>.GetEnumerator()` は列挙子構築時に `InMemoryList<T>` へ全件材料化するため、storage 側で predicate、projection、aggregate、早期終了を融合できない。

内部 iterator の利点は、ネイティブコードになるという意味ではなく、storage-native な実行計画にできる点である。`struct` strategy と generic/static abstract 呼び出しにすれば delegate allocation や interface dispatch を避けやすく、JIT が inline しやすい形になる。さらに `Any` / `First` / `Take` は scan 中に停止でき、`Count` / `Aggregate` は状態だけを持てる。

ただし内部 iterator が常に速いとは限らない。小さい in-memory collection では外部 iterator のほうが単純で十分な場合がある。今回内部 iterator を優先する理由は、Tsavorite-backed collection で全件 `InMemoryList<T>` 材料化を避け、storage/session/scan の並列性と早期終了を保つためである。

### GetEnumerator の外部 iterator 化方針

`GetEnumerator()` が行っている現在の eager 全件材料化は、外部 iterator 方式へ置き換える。

現状の `Collection.Default.Enumerator` は constructor で `InMemoryList<T>` を作り、`IterateLookup` を最後まで走らせてから `MoveNext()` で list を読む。このため public API は `IEnumerator<T>` だが、実態は列挙開始前の全件 materialize である。

新方針では、`MoveNext()` が必要になった時だけ Tsavorite scan を進める chunked external iterator にする。実装では `ClientSession.ScanCursor(ref cursor, count, ...)` を使う。`IterateLookup(ref iterator, ref cursor, resetCursor: false)` と `CursorRecordResult.EndBatch` / `RetryLastRecord` も候補だったが、可変長 disk record を小さい count で再開すると欠落するケースがあったため、列挙子本体では `ScanCursor` を採用する。既存の `tests/Test.Tsavorite.Collections/Tsavorite/IterateLookupChunkTests.cs` は低レベル cursor 動作の参考として残す。

外部 iterator 化後の概形は次の通り。

```csharp
public struct Enumerator<TIterator, T> : IEnumerator<T>
    where TIterator : ChunkScanIteratorFunctions<T>
{
    private long cursor;
    private int index = -1;
    private int count;
    private bool done;
    private TIterator iterator;

    public T Current => this.iterator.Chunk[this.index];

    public bool MoveNext()
    {
        if (++this.index < this.count)
            return true;

        if (this.done)
            return false;

        this.iterator.ResetChunk();

        var mayHaveMore = this.session.ScanCursor(
            ref this.cursor,
            this.iterator.Chunk.Length,
            this.iterator,
            this.untilAddress,
            validateCursor: true,
            maxAddress: this.untilAddress,
            resetCursor: false,
            includeTombstones: false);

        this.session.BasicContext.CompletePending(wait: true);

        this.count = this.iterator.Count;
        this.done = !mayHaveMore;

        if (this.count == 0)
            return false;

        this.index = 0;
        return true;
    }
}
```

この方針では `InMemoryList<T>` は `GetEnumerator()` の中間バッファとして使わない。必要なのは固定長または小容量の chunk buffer だけであり、全件分の `System.Collections.Generic.List<T>` は作らない。実装上の下限は `4096` records とし、`Const.Chunk_Length` がそれより大きい場合はそちらを使う。これは可変長 disk record の cursor validation 境界で欠落しないための固定上限 block であり、全件材料化ではない。

外部 iterator 化によって、通常の `foreach` / `IEnumerable<T>` / 既存 LINQ は大容量 materialize を避けられる。ただし generator が `Any` / `First` / `Take` などを完全に storage-level fuse したい場合は、内部 iterator / scan strategy 経路を使う余地を残す。

未決事項は列挙中更新の意味論である。旧実装は enumerator constructor 時点で全件 materialize するため、その後の更新は列挙結果に混ざりにくい。lazy 外部 iterator では `MoveNext()` 時点で scan が進むため、更新が観測される可能性が変わる。互換性を優先するなら、列挙開始時に TailAddress を固定する snapshot 寄りの設計が必要になる。

### InMemoryList 全件材料化後の for loop との比較

`InMemoryList` に全件を積んだ後で次のように回す方式は、外部 iterator ではなく、全件 materialize + index loop である。

```csharp
var items = new InMemoryList<T>(Const.Chunk_Length);
// IterateLookup で全件 items に Add する
for (var i = 0L; i < items.Count; i++)
{
    var item = items[i];
    // LINQ fragment を展開した処理
}
```

この方式は、材料化後の処理だけ見れば `IEnumerator<T>` より速くしやすい。`MoveNext()` / `Current` の状態機械を通らず、`Count` と indexer で単純な loop にできるためである。また、列挙開始時点の snapshot に近い結果を作りやすく、複数回走査する処理や `OrderBy` のようにどのみち全件 buffer が必要な処理には使える。

ただし、Tsavorite LINQ の標準実行経路としては採用しない。

- `Any` / `First` / `Take` でも処理前に全件 `InMemoryList` へ入る。
- `Where` で落ちる要素まで全件 deserialization と list 追加が走る。
- `System.Collections.Generic.List<T>` ベースの大容量中間バッファを使うため、今回の制約に反する。
- storage scan と LINQ fragment が融合せず、並列 scan の結果を lock 付き list add に集約しやすい。

採用する基本形は、全件 buffer ではなく chunk buffer + index loop である。

```csharp
while (true)
{
    iterator.ResetChunk();

    var mayHaveMore = session.ScanCursor(
        ref cursor,
        iterator.Chunk.Length,
        iterator,
        untilAddress,
        validateCursor: true,
        maxAddress: untilAddress,
        resetCursor: false,
        includeTombstones: false);

    session.BasicContext.CompletePending(wait: true);

    for (var i = 0; i < iterator.Count; i++)
    {
        var item = iterator.Chunk[i];
        // LINQ fragment を展開した処理
    }

    if (!mayHaveMore)
        break;
}
```

この形なら、内側は `for` の単純 loop にできる一方で、全件 `InMemoryList` は作らない。`Any` / `First` / `Take` は chunk 内で条件を満たした時点で終了でき、次の chunk を読まない。`ToArray` / `OrderBy` / `GroupBy` など全件蓄積が本質的に必要な演算だけ、専用の出力 buffer または storage-backed 中間構造へ明示的に積む。

### 物理ブロックと論理チャンク

ここでいう chunk は、常に memory-mapped file の物理ブロックそのものを指すわけではない。次の 2 種類を分ける。

- 物理ブロック: memory-mapped file、OS page、Tsavorite log page/segment など、storage が実際に読み書きする単位。
- 論理チャンク: LINQ/enumerator が一度の `MoveNext()` 補充で扱う record のまとまり。内側を `for` で回すための小さな buffer。

`List<T>` は memory-mapped file 前提なので、論理チャンクを物理ブロックまたは mapped view の境界に寄せるのが自然である。固定長要素なら `blockBytes / sizeof(T)` に近い件数で chunk を切れる。可変長要素や managed 参照を含む場合は、件数ではなく byte budget と index table を持つ。

`HashSet<T>` / `Dictionary<TKey,TValue>` は Tsavorite storage 前提なので、論理チャンクは memory-mapped file block ではなく `ScanCursor` の count/cursor 単位である。scan 結果を固定上限の record buffer に受けてから `for` で処理する。

したがって、`List<T>` では物理ブロックと論理チャンクを一致させやすい。一方、`HashSet<T>` / `Dictionary<TKey,TValue>` では物理配置に強く結びつけず、`Math.Max(Const.Chunk_Length, 4096)` を現在の固定 block とし、将来 target byte size で調整できる論理チャンクにする。

## 影響範囲

- `src/Tsavorite.Collections/Linq/*`
- `src/Tsavorite.Collections/Collections/IHashSet.cs`
- `src/Tsavorite.Collections/Collections/Collection*.cs`
- `src/FLinq/SourceGenerator/*`
- `src/FLinq/SourceGenerator/Linq/Fragments/*`
- `tests/Test.Tsavorite.Collections/Intercept/System/Linq/*`
- `tests/Test.Tsavorite.Collections/Intercept/Tsavorite/Linq/*`
- `tests/Test.Tsavorite.Collections.Benchmark/*`
- `sandbox/Benchmark.Shared/*`
- `src/Benchmark/*`

## 移行手順

1. `Tsavorite.Linq.Enumerable` を追加し、既存 `HashSet` API を移植する。
2. `HashSet` を互換レイヤーにして、既存テストを壊さない。
3. `System.Collections.Generic` 大容量材料化を使う iterator を分類し、単純遅延・Tsavorite-backed state・memory-mapped state に分ける。
4. generator の LINQ family 判定を導入する。
5. `IHashSet<T>` / `HashSet<T>` 向けの最小 fragment を通す。
6. 既存 Tsavorite HashSet intercept テストを `Enumerable` 名前へ移行する。
7. System LINQ テストを共有 source factory 化し、Tsavorite 版を追加する。
8. ベンチ共有化を `Benchmark.Shared` ベースで整理する。
9. 依存更新を行う場合は Garnet 更新後に build/test を通す。

## テスト計画

- `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj`
- `dotnet test tests/Test.Tsavorite.Collections.Benchmark/Test.Tsavorite.Collections.Benchmark.csproj`
- Chain1 のうち初期対応 fragment の System/Tsavorite 共有テスト。
- Chain2 の `Where_Select`, `Select_Where`, `Where_Count`, `Select_Sum` から開始。
- Generated source assertion で `Tsavorite.Linq.Enumerable` が intercept 対象になることを確認。
- 大容量中間バッファ禁止の静的検査として、`Tsavorite.Linq.Enumerable` 実装内の `global::System.Collections.Generic.List/HashSet/Dictionary` 使用を `rg` で検出する。

## リスクと対策

- extension method 解決が `System.Linq` と `Tsavorite.Linq` で衝突する。
  - テスト名前空間と using alias を分け、必要な箇所は明示 static call にする。
- `IEnumerable<T>` 戻り値を維持すると全件バッファ enumerator に戻りやすい。
  - generator 展開と専用 enumerable を優先し、未対応 fragment は例外にする。
- `GroupBy`/`Join`/`OrderBy` は状態管理が大きく、初期対応で破綻しやすい。
  - 初期範囲から外し、storage-backed 中間構造を先に設計する。
- Garnet prerelease 更新で Tsavorite API が動く可能性がある。
  - session/scan API を内部 helper に閉じる。

## 未決事項

- `ToHashSet` / `ToDictionary` の戻り値を Tsavorite collection に寄せるか、System 互換を残して `ToTsavorite*` を追加するか。

## 合意状況

- 合意済み: 正規 API 名は `Tsavorite.Linq.Enumerable`。
- 合意済み: 既存 `Tsavorite.Linq.HashSet` は妥当名ではないため移行対象。
- 合意済み: System LINQ 用 intercept/テストを参考に Tsavorite 版を作る。
- 合意済み: storage-level 展開を要求する経路では、未対応 fragment や完全展開できない LINQ は fallback せず例外にする。
- 合意済み: `Microsoft.Garnet` は最新 prerelease `2.0.1-beta.3` へ更新する。
- 合意済み: `GetEnumerator()` は全件 `InMemoryList<T>` 材料化ではなく、外部 iterator 方式の固定 block scan にする。
- 実施済み: `src/Tsavorite.Collections/Tsavorite.Collections.csproj` と `src/Seamless.Data/Seamless.Data.csproj` の `Microsoft.Garnet` 参照を `2.0.1-beta.3` に更新した。
- 検証済み: `dotnet restore` と `dotnet build --no-restore` は両プロジェクトで成功した。`Tsavorite.Collections` には既存の nullable/CS9124 警告が残るが、Garnet 更新によるエラーは出ていない。
- 実施済み: `Collection.Default.Enumerator` を constructor 全件材料化から `MoveNext()` 時の `ScanCursor` 補充へ変更した。`HashSet<T>` / `Dictionary<TKey,TValue>` / `Set<TKey,TRecord>` は列挙開始時の `TailAddress` を渡す。
- 実施済み: scan buffer は `Math.Max(Const.Chunk_Length, 4096)` records とし、可変長 disk record の cursor validation 境界で欠落しないよう `validateCursor: true` を使う。
- 検証済み: `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj --no-restore --filter "FullyQualifiedName~Enumerator|FullyQualifiedName~IterateLookupChunkTests"` は 41 件成功した。

## 参照

- NuGet `Microsoft.Garnet`: https://www.nuget.org/packages/Microsoft.Garnet
- Garnet repository: https://github.com/microsoft/garnet
