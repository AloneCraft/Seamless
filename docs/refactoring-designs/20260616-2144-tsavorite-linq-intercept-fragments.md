# Tsavorite LINQ Intercept Fragment 実装計画

## 背景

`Tsavorite.Linq.Enumerable` は `IHashSet<T>` 向けの LINQ API として整備中である。既存の FLinq source generator は主に `System.Linq.Enumerable` を対象にしており、`Tsavorite.Linq.Enumerable` の呼び出しは Fragment pipeline の入口で十分に認識されていない。

今回の目的は、`Tsavorite.Linq.Enumerable` を `System.Linq.Enumerable` と同じ実装思想で intercept 対象にしつつ、`Tsavorite.Collections.HashSet<T>` 具象型では Tsavorite storage scan を使う専用経路を追加することである。

## 現状の観察

- `src/FLinq/SourceGenerator/LinqTargetDiscovery.cs`, `LinqFragmentBuilder.cs`, `LinqMethodBodyRewriter.cs`, `Where.cs` などに `System.Linq.Enumerable` 固定判定がある。
- 既存 Fragment は `src/FLinq/SourceGenerator/Linq/Fragments` 直下にあり、reflection で `Fragment` 継承型を収集している。クラス名が `Where_predicate` のように method/variant 名として使われる。
- 既存 Fragment は `System.Linq.Enumerable` 専用というより、`IEnumerable<T>` セマンティクスの共通基盤である。配置は現状維持する。
- `Tsavorite.Collections.HashSet<T>.Scan<TStrategy>(ref TStrategy)` と `IHashSetScanStrategy<TSelf,T>` は現在 internal である。
- `Tsavorite.Linq.Enumerable.Where` / `Select` などは `IEnumerable<T>` を返す。そのため `source.Where(...).Select(...).Count()` は、最初だけ `Tsavorite.Linq.Enumerable`、後続は `System.Linq.Enumerable` に解決されることがある。
- 現在の `Tsavorite.Linq.Enumerable` の sequence-returning method は、多くが `List<T>` に即時 materialize して返している。これは `System.Linq.Enumerable` の遅延実行と異なる。
- 既存 System 側テストは `tests/Test.Tsavorite.Collections/Intercept/System/Linq` に `Chain1`, `Chain2`, `FragmentCoverage`, `EnumerableComparisonCatalog` があり、Tsavorite 側より構造化されている。

## 目的

- `Tsavorite.Linq.Enumerable` 呼び出しを FLinq の Fragment pipeline に乗せる。
- `Tsavorite.Collections.HashSet<T>` 具象型 root では `Scan` ベースの専用 terminal fusion を使う。
- `IHashSet<T>` など抽象型 root では既存 `Root` ベースの struct enumerator 経路を正式経路として使う。
- Tsavorite root から始まるチェーンでは、途中の `System.Linq.Enumerable` stage も Tsavorite pipeline の一部として扱う。
- 未対応メソッドは静かに元実装へ戻さず、未実装として検出可能にする。
- System 側と同じ形のテストを、共有ソースまたは `<Compile Include=... Link=...>` で可能な範囲で共通化する。

## 非目標

- 既存 Fragment を `Fragments/System` や `Fragments/Common` へ移動しない。
- 全 LINQ メソッドの storage-level 最適化を一度に完了しない。
- `Tsavorite.Linq.Enumerable` の全 sequence-returning method を一度に完全な遅延実行へ直さない。
- `Tsavorite.Linq.Enumerable.Where` などの戻り値を独自 query 型に変更しない。
- 未対応メソッドを fallback forwarding で隠さない。

## 提案設計

### Provider 判定

`System.Linq.Enumerable` 固定の判定を、provider/family 判定へ置き換える。

- `System.Linq.Enumerable`
- `Tsavorite.Linq.Enumerable`

の両方を LINQ method family として認識する。ただし pipeline の実行方針は method provider だけではなく root provider で決める。

```text
root が Tsavorite.Collections.HashSet<T>
  -> Tsavorite Scan path

root が Tsavorite.Collections.IHashSet<T>
  -> Tsavorite generic Root path

root が通常 IEnumerable<T>
  -> 既存 path
```

Tsavorite root から始まるチェーンでは、後続 stage が `System.Linq.Enumerable` に解決されても Tsavorite pipeline として扱う。

### 既存 Fragment の配置

既存 Fragment は現状の `src/FLinq/SourceGenerator/Linq/Fragments` 直下に残す。Tsavorite 固有の実装だけを以下に追加する。

```text
src/FLinq/SourceGenerator/Linq/Fragments/Tsavorite/
  HashSetScanRoot.cs
  HashSetScanPipeline.cs
  HashSetScanTerminal.cs
  ...

src/FLinq/SourceGenerator/Linq/Fragments/Tsavorite/Pending/
  TsavoriteScanPendingFragments.cs
```

`Pending` 側は `Fragment` を継承しない空クラス群にする。reflection に拾われて未実装 Fragment として誤認されることを避ける。

### Scan API

利用者向け public API は増やさない。`HashSet<T>` の internal scan primitive を、生成コード側の `UnsafeAccessor` で呼ぶ。

生成コードには `unsafe` と function pointer が必要になるため、利用側プロジェクトは `AllowUnsafeBlocks=true` を前提にする。テストプロジェクトにも必要なら明示する。

概念形は以下。

```csharp
internal unsafe void Scan<TState>(
    ref TState state,
    delegate* managed<ref TState, TKey, bool> body);
```

生成コード側は以下のような `UnsafeAccessor` を出す。

```csharp
[global::System.Runtime.CompilerServices.UnsafeAccessor(
    global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method,
    Name = "Scan")]
private static extern unsafe void __Scan<TState>(
    global::Tsavorite.Collections.HashSet<int> source,
    ref TState state,
    delegate* managed<ref TState, int, bool> body);
```

### Scan fusion の範囲

Scan path は、terminal で閉じる query を初期対象にする。

- `Any`
- `All`
- `Count`
- `LongCount`
- `Contains`
- `First`
- `FirstOrDefault`
- `Single`
- `SingleOrDefault`
- `Aggregate`

`Where` / `Select` は単独 Fragment として scan に置き換えるのではなく、terminal fusion の stage として scan body に組み込む。既存 `Where_predicate` / `Select_selector` クラスを直接流用せず、`Pipeline` の stage 情報または Tsavorite 専用 stage 表現へ変換して scan body を生成する。

sequence-returning query は初期段階では scan path にしない。

```csharp
source.Where(x => x > 0).Select(x => x * 2)
```

のように `IEnumerable<T>` として返るケースは、既存 Root-based struct enumerator 経路で扱う。

### Generic Root path

`IHashSet<T>` など抽象型 root は、起点 source を既存 `Root` として扱う。

```text
Root(source.GetEnumerator())
  -> Where_predicate
  -> Select_selector
  -> Count
```

これは fallback ではなく、Tsavorite provider の正式な generic path とする。

### 通常実装の遅延実行化

`Tsavorite.Linq.Enumerable` の通常実装も、今回の Interceptor テストに直接関係する範囲だけ遅延実行へ寄せる。

初期対象は以下。

- `Where`
- `Select`
- `Skip`
- `Take`
- `SkipWhile`
- `TakeWhile`
- `Append`
- `Prepend`
- `Concat`
- `Distinct`
- `Intersect`
- `Union`
- `Except`
- `Zip`
- 必要なら `SelectMany`

set 系は内部 set 構築を列挙開始時に行い、返り値は `yield return` ベースの遅延 `IEnumerable<T>` にする。

`GroupBy`, `Join`, `OrderBy`, `ToLookup` など、完全な System 互換の遅延タイミング調整が大きいものは別フェーズにする。

## 影響範囲

- `src/FLinq/SourceGenerator/LinqTargetDiscovery.cs`
- `src/FLinq/SourceGenerator/LinqFragmentBuilder.cs`
- `src/FLinq/SourceGenerator/LinqMethodBodyRewriter.cs`
- `src/FLinq/SourceGenerator/Linq/Fragments/Pipeline.cs`
- `src/FLinq/SourceGenerator/Linq/Fragments/Where.cs`
- `src/FLinq/SourceGenerator/Linq/Fragments/Tsavorite/*`
- `src/Tsavorite.Collections/Collections/HashSet.Scan.cs`
- `src/Tsavorite.Collections/Linq/Enumerable.cs`
- `tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj`
- `tests/Test.Tsavorite.Collections/Intercept/System/Linq/*`
- `tests/Test.Tsavorite.Collections/Intercept/Tsavorite/Linq/*`

## 移行手順

1. LINQ provider 判定を導入し、`System.Linq.Enumerable` 固定判定を置き換える。
2. Tsavorite root 判定を追加する。
3. `IHashSet<T>` root は既存 `Root` に流す。
4. `HashSet<T>` root は Tsavorite Scan path の候補として認識する。
5. `UnsafeAccessor` で呼ぶ internal unsafe scan primitive を `HashSet.Scan.cs` に追加する。
6. `Where` / `Select` stage と scalar terminal を scan body に融合する Tsavorite 専用 Fragment を追加する。
7. 未実装 Scan 対象を `Fragments/Tsavorite/Pending` に空クラスで記録する。
8. `Tsavorite.Linq.Enumerable` の対象 sequence-returning method を遅延実行へ直す。
9. System 側テストと同じ形の Tsavorite テストを追加し、共有可能な部分はリンクソース化する。
10. targeted test を通した後、関連テスト範囲を広げる。

## テスト計画

- `Tsavorite.Linq.Enumerable` の通常実装について、対象 method の遅延実行を確認する。
- `HashSet<T>` 具象型 root で `Scan` path が生成されることを generated source assertion で確認する。
- `IHashSet<T>` root で generic Root path が生成されることを確認する。
- Tsavorite root から始まる `Tsavorite.Linq.Enumerable` + `System.Linq.Enumerable` 混在チェーンを確認する。
- `Any`, `All`, `Count`, `LongCount`, `Contains`, `First`, `FirstOrDefault`, `Single`, `SingleOrDefault`, `Aggregate` の terminal query を System 側の書き方に合わせて比較する。
- `Where` / `Select` の stage fusion を `Where_Count`, `Select_Count`, `Where_Select_Count`, `Select_Where_Any` のようなチェーンで確認する。
- `Intersect`, `Union`, `Except`, `Distinct` は通常実装の遅延実行と結果一致を確認し、Scan 最適化は Pending に残す。
- 実行候補:
  - `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj --filter "Category=Intercept"`
  - 必要に応じて System LINQ intercept テストも個別実行する。

## リスクと対策

- `UnsafeAccessor` + function pointer は利用側に `AllowUnsafeBlocks=true` を要求する。テストプロジェクトに明示し、設計上の前提として扱う。
- `UnsafeAccessor` で generic internal method を呼ぶ形はコンパイル形状が厳しい。最小サンプルの generator compile test を先に追加する。
- Tsavorite root から始まるチェーンで System/Tsavorite method が混在するため、provider 判定だけでなく root 判定を維持する。
- `HashSet<T>` の列挙順は安定前提にしない。比較テストは必要に応じて正規化する。
- Pending 空クラスを `Fragment` 継承にすると reflection で拾われるため、必ず非 Fragment 型にする。
- 通常実装の遅延実行化により例外発生タイミングが変わる。これは `System.Linq.Enumerable` 互換へ寄せる意図した変更として扱う。

## 未解決事項

- なし。実装中に `UnsafeAccessor` の generic/function pointer 形状で制約が出た場合は、この設計文書を更新して代替案を記録する。

## 合意済み方針

- 既存 Fragment は移動しない。
- Tsavorite 固有 Fragment は `Fragments/Tsavorite` に置く。
- 未実装 Scan 対象は `Fragments/Tsavorite/Pending` に `Fragment` 非継承の空クラスで残す。
- `HashSet<T>` 具象型は Scan path、抽象型は Root-based generic path。
- Scan path は terminal で閉じる query を初期対象にする。
- `Tsavorite.Linq.Enumerable` と `System.Linq.Enumerable` が混在するチェーンも、Tsavorite root 由来なら Tsavorite pipeline として扱う。
- 通常実装の遅延実行化は今回の Interceptor テストに直接関係する範囲に絞る。
