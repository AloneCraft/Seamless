# Tsavorite.Linq.Enumerable 移行設計

## 背景

`System.Linq.Enumerable` は `IEnumerable<T>` を中心にした LINQ API である。一方、`Tsavorite.Collections` には Tsavorite backed な `IHashSet<T>`、`IDictionary<TKey,TValue>`、memory-mapped `List<T>` があり、storage、session、scan を前提にした LINQ API が必要になる。

現状の `Tsavorite.Linq.HashSet` は `IHashSet<T>` 向け API に寄っているため、System LINQ と対応する API 名としては `Tsavorite.Linq.Enumerable` へ移行する。

## 目的

- `Tsavorite.Linq.Enumerable` を正規 API 名にする。
- 既存の `Tsavorite.Linq.HashSet` は互換レイヤーへ移す。
- `Tsavorite.Collections.HashSet<T>` と `IHashSet<T>` を generator の intercept 対象にする。
- System LINQ 用テストを共有し、System と Tsavorite の両方へ適用できる構造にする。
- 大容量中間バッファとして `System.Collections.Generic` を使わない。
- Tsavorite の storage、session、scan、RMW 前提を壊さず、全件ロックや全件メモリ材料化を避ける。

## 非目的

- `System.Linq.Enumerable` の全 API を一度で完全互換にすること。
- テストコード内の小規模な一時材料化まで完全に禁止すること。
- `List<T>` を Tsavorite backed に変更すること。`List<T>` は memory-mapped file のまま扱う。
- 既存公開 API を即時削除すること。

## 提案設計

1. `namespace Tsavorite.Linq; public static partial class Enumerable` を追加する。
2. 実装ファイルを `Enumerable.HashSet.cs`、`Enumerable.Dictionary.cs`、`Enumerable.List.cs`、`Enumerable.Set.cs`、`Enumerable.Internal.*.cs` に分ける。
3. `Tsavorite.Linq.HashSet` は互換用 partial class として残し、可能な範囲で `Enumerable` に委譲する。
4. 単純な `Where`、`Select`、`Take` などは遅延列挙できる専用 enumerable/enumerator を返す。
5. set、join、group、order 系で状態が必要な処理は、Tsavorite backed collection または memory-mapped `List<T>` を使う。
6. `ToHashSet` と `ToDictionary` は Tsavorite collection を返す overload を追加する。曖昧になる場合は `ToTsavoriteHashSet` と `ToTsavoriteDictionary` を併設する。

## 走査基盤

現在の `IEnumerable<T>` enumerator は全件を `InMemoryList<T>` に積むため、intercept と専用演算では `ForEach<TCollection,TStrategy>` や `IScanIteratorFunctions` ベースのストリーミング走査を優先する。

`IHashSet<T>` の静的型でも、`GetEnumerator()` を root にした LINQ-level 展開は可能とする。ただし storage-level 展開では `foreach` に戻さず、Tsavorite 専用 scan strategy を呼ぶ生成コードを使う。

storage-level 展開を要求する経路で対応できない fragment は、既存 enumerator へ黙って fallback せず例外にする。

## Source Generator

- `System.Linq.Enumerable` 固定判定を `LinqMethodFamily` のような descriptor に置き換える。
- family は最低限 `SystemEnumerable` と `TsavoriteEnumerable` を持つ。
- `TsavoriteEnumerable` は `Tsavorite.Linq.Enumerable` の reduced extension call を対象にする。
- receiver が `Tsavorite.Collections.IHashSet<T>` または具象 `Tsavorite.Collections.HashSet<T>` の場合を扱う。
- 初期対応は `Any`、`All`、`Count`、`LongCount`、`Contains`、`First`、`FirstOrDefault`、`Aggregate` から始める。
- `GroupBy`、`Join`、`OrderBy`、`Distinct`、`Except`、`Intersect`、`Union` は storage-backed 中間構造が整ってから段階対応する。

## 展開可能性

LINQ-level 展開は、`IHashSet<T>` の静的型でも `IEnumerable<T>` と同じく `GetEnumerator()` を root にして LINQ chain を生成コードの loop へ畳み込む方式である。互換性は高いが、現在の enumerator が全件材料化する場合は storage-level 展開ではない。

storage-level 展開は、`Tsavorite.Collections.HashSet<T>` または専用 scan interface から直接走査し、`IEnumerable<T>` fallback や `System.Collections.Generic` 中間材料化を使わない方式である。これを最終的な完全展開と呼ぶ。

## 初期対応候補

状態をほぼ持たない、または定数サイズの状態だけで走査できる API を優先する。

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

## Tsavorite-backed 中間構造が必要な API

以下は System LINQ fragment が `System.Collections.Generic.List`、`HashSet`、`Dictionary`、`Queue` を大容量中間構造として使いやすいため、そのままでは Tsavorite 版の完全展開とはみなさない。

- `Distinct`
- `DistinctBy`
- `Except`
- `ExceptBy`
- `Intersect`
- `IntersectBy`
- `Union`
- `UnionBy`
- `OrderBy`
- `ThenBy`
- `GroupBy`
- `GroupJoin`
- `Join`
- `Reverse`
- `SkipLast`
- `TakeLast`
- `ToHashSet`
- `ToDictionary`
- `ToLookup`
- `CountBy`
- `AggregateBy`

## Scan Contract

scan contract は、`IEnumerator<T>` を返す代わりに storage scan 中に 1 record ずつ struct strategy を呼ぶための共通 API である。

```csharp
public interface ITsavoriteScanStrategy<TSelf, T>
    where TSelf : struct, ITsavoriteScanStrategy<TSelf, T>
{
    static abstract bool Invoke(ref TSelf self, in T item);
}

public interface ITsavoriteScanSource<T>
{
    void Scan<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ITsavoriteScanStrategy<TStrategy, T>;
}
```

`Any`、`First`、`Take` は strategy の戻り値で早期終了を表現する。`Count` と `Aggregate` は strategy の状態だけを更新する。

## GetEnumerator 方針

`GetEnumerator()` が行っている eager な全件材料化は、`MoveNext()` 時に `ScanCursor` を進める chunked external iterator へ置き換える。列挙開始時の `TailAddress` を固定し、snapshot に近い意味論を維持する。

論理チャンクは `Math.Max(Const.Chunk_Length, 4096)` records を現在の固定上限とし、将来的には target byte size で調整できるようにする。

## 影響範囲

- `src/Tsavorite.Collections/Linq/*`
- `src/Tsavorite.Collections/Collections/IHashSet.cs`
- `src/Tsavorite.Collections/Collections/Collection*.cs`
- `src/FLinq/SourceGenerator/*`
- `src/FLinq/SourceGenerator/Linq/Fragments/*`
- `tests/Test.Tsavorite.Collections/Intercept/System/Linq/*`
- `tests/Test.Tsavorite.Collections/Intercept/Tsavorite/Linq/*`
- `tests/Test.Tsavorite.Collections.Benchmark/*`
- `Shared/Common/*`
- `src/Benchmark/*`

## 移行手順

1. `Tsavorite.Linq.Enumerable` を追加し、既存 `HashSet` API を移植する。
2. `HashSet` を互換レイヤーにして、既存テストを壊さない。
3. `System.Collections.Generic` 大容量材料化を使う iterator を分類する。
4. generator の LINQ family 判定を導入する。
5. `IHashSet<T>` と `HashSet<T>` 向けの最初の fragment を通す。
6. 既存 Tsavorite HashSet intercept テストを `Enumerable` 名へ移行する。
7. System LINQ テストを共通 source factory 化し、Tsavorite 版を追加する。
8. benchmark 共有化を `Common` ベースで整理する。
9. Garnet 更新後に restore、build、test を通す。

## テスト計画

- `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj`
- `dotnet test tests/Test.Tsavorite.Collections.Benchmark/Test.Tsavorite.Collections.csproj`
- Chain1 と Chain2 の System/Tsavorite 共有テスト。
- Generated source assertion で `Tsavorite.Linq.Enumerable` が intercept 対象になることを確認する。
- `Tsavorite.Linq.Enumerable` 実装内の大容量 `System.Collections.Generic` 使用を `rg` で検出する。

## リスクと対策

- extension method 解決が `System.Linq` と `Tsavorite.Linq` で衝突する。
  - テスト名前空間と using alias を分け、必要な箇所は明示 static call にする。
- `IEnumerable<T>` 戻り値を維持すると全件バッファ enumerator に戻りやすい。
  - generator 展開と専用 enumerable を優先し、未対応 fragment は例外にする。
- `GroupBy`、`Join`、`OrderBy` は状態管理が大きい。
  - 初期範囲から外し、storage-backed 中間構造を先に設計する。
- Garnet prerelease 更新で Tsavorite API が変わる可能性がある。
  - session/scan API を内部 helper に閉じる。

## 合意状況

- 正規 API 名は `Tsavorite.Linq.Enumerable` とする。
- 既存 `Tsavorite.Linq.HashSet` は移行対象とする。
- System LINQ 用 intercept/test を参考に Tsavorite 版を作る。
- storage-level 展開で未対応 fragment を silent fallback しない。
- `Microsoft.Garnet` は最新 prerelease へ更新する。
- `GetEnumerator()` は全件 `InMemoryList<T>` 材料化ではなく、外部 iterator 方式と固定 block scan にする。

## 参照

- NuGet `Microsoft.Garnet`: https://www.nuget.org/packages/Microsoft.Garnet
- Garnet repository: https://github.com/microsoft/garnet
