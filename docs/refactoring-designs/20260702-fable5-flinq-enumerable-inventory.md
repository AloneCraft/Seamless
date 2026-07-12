# FLinq × System.Linq.Enumerable 全メソッド棚卸し (2026-07-02)

fable5 一括改修の一環として、System.Linq.Enumerable(.NET 10)全メソッドと
FLinq Fragment/Pipeline の対応状況、fallback 0 の検証機構、設計上の除外を記録する。

## 対応判定の正規ルール

生成器側の正規判定は `LinqFragmentBuilder.Enumerableメソッドがフラグメント対応済みか`:

- `Any` / `TryGetNonEnumeratedCount` は専用経路。
- ルート生成メソッド: `Range` / `Repeat` / `Empty` / `InfiniteSequence` / `Sequence` / `AsEnumerable`。
- それ以外は `Fragments` 配下の Fragment 派生クラス名の先頭部(`_` 区切り)と一致するもの。
  一覧はリフレクションで自動構築されるため、Fragment クラスを追加すればそのまま対応済みになる。

## fallback の定義と検証機構

FLinq は「静かなフォールバック経路を作らない」方針で実装されている。

1. **コンパイル時**: intercept 対象だが展開できなかった呼び出しは、生成インターセプターが
   `GeneratedCodeHelpers.LinqInterceptorFailure(...)` を throw するコードになり、`Tsavorite002` 警告が出る。
2. **生成コード検査**: `Test.SourceGenerator.FallbackDiagnostics.GeneratedInterceptors_ContainNoFallbackFailure` が
   `Test\obj\<Configuration>\net10.0\generated` 配下の生成インターセプターを走査し、`LinqInterceptorFailure` の残存を fail にする。
3. **実行時診断**: `LinqCounter.EnumerableFallback` / `EnumerableFallbackDetails` を追加した。
   `LinqInterceptorFailure` の構築時に加算されるため、実行後にカウンタが 0 なら fallback 未発生。
   ベンチマーク実行では `UiRunStore.SaveFallbackDiagnostics` → `BenchmarkFallbackDiagnostics` View で SQLite に保存・集計できる。

## メソッド別対応表

| Enumerable メソッド | 対応 | 実装場所 |
| --- | --- | --- |
| Aggregate | ○ | Fragments/Aggregate.cs |
| AggregateBy | ○ | Fragments/AggregateBy.cs |
| All | ○ | Fragments/All.cs |
| Any | ○ | 専用経路 + Fragments/Any.cs |
| Append | ○ | Fragments/Append.cs |
| AsEnumerable | ○ | ルート経路(IsKnownEnumerableRootMethod) |
| Average | ○ | Fragments/Average.cs |
| Cast | ○ | Fragments/Cast.cs |
| Chunk | ○ | Fragments/Chunk.cs |
| Concat | ○ | Fragments/Concat.cs |
| Contains | ○ | Fragments/Contains.cs |
| Count | ○ | Fragments/Count.cs |
| CountBy | ○ | Fragments/CountBy.cs |
| DefaultIfEmpty | ○ | Fragments/DefaultIfEmpty.cs |
| Distinct | ○ | Fragments/Distinct.cs |
| DistinctBy | ○ | Fragments/DistinctBy.cs |
| ElementAt | ○ | Fragments/ElementAt.cs |
| ElementAtOrDefault | ○ | Fragments/ElementAtOrDefault.cs |
| Empty | ○ | ルート経路 |
| Except | ○ | Fragments/Except.cs |
| ExceptBy | ○ | Fragments/ExceptBy.cs |
| First / FirstOrDefault | ○ | Fragments/First.cs / FirstOrDefault.cs |
| GroupBy | ○ | Fragments/GroupBy.cs |
| GroupJoin | ○ | Fragments/GroupJoin.cs |
| Index | ○ | Fragments/Index.cs |
| InfiniteSequence | ○ | ルート経路 |
| Intersect / IntersectBy | ○ | Fragments/Intersect.cs / IntersectBy.cs |
| Join | ○ | Fragments/Join.cs |
| Last / LastOrDefault | ○ | Fragments/Last.cs / LastOrDefault.cs |
| LeftJoin | ○ | Fragments/LeftJoin.cs |
| LongCount | ○ | Fragments/LongCount.cs |
| Max / MaxBy / Min / MinBy | ○ | Fragments/Max.cs 他 + Bases/MinMax* |
| OfType | ○ | Fragments/OfType.cs |
| Order / OrderDescending | ○ | Pipeline 経由(Pipeline.cs の Order/OrderDescending ステージ) |
| OrderBy / OrderByDescending | ○ | Fragments/OrderBy.cs |
| Prepend | ○ | Fragments/Prepend.cs |
| Range / Repeat | ○ | ルート経路 + RangeExceptRoot/RangePredicateOptimizer |
| Reverse | ○ | Fragments/Reverse.cs |
| RightJoin | ○ | Fragments/RightJoin.cs |
| Select | ○ | Fragments/Select.cs |
| SelectMany | ○ | Fragments/SelectMany.cs |
| Sequence | ○ | ルート経路 |
| SequenceEqual | ○ | Fragments/SequenceEqual.cs |
| Shuffle | ○ | Fragments/Shuffle.cs |
| Single / SingleOrDefault | ○ | Fragments/Single.cs / SingleOrDefault.cs |
| Skip / SkipLast / SkipWhile | ○ | Fragments/Skip*.cs |
| Sum | ○ | Fragments/Sum.cs |
| Take / TakeLast / TakeWhile | ○ | Fragments/Take*.cs |
| ThenBy / ThenByDescending | ○ | Fragments/ThenBy.cs |
| ToArray / ToDictionary / ToHashSet / ToList / ToLookup | ○ | Fragments/To*.cs |
| TryGetNonEnumeratedCount | △ | 専用経路。既知の失敗テストあり(後述) |
| Union / UnionBy | ○ | Fragments/Union.cs / UnionBy.cs |
| Where | ○ | Fragments/Where.cs |
| Zip | ○ | Fragments/Zip.cs |

`AsQueryable` は `System.Linq.Queryable` のメソッドであり Enumerable 対象外。

## 設計上の除外(呼び出し箇所レベル)

以下は「メソッド未対応」ではなく、インターセプト機構の安全制約による呼び出し箇所レベルの除外。
該当する呼び出しは展開されず元実装のまま動く(意味論は保たれる)。理由は
`LinqTargetDiscovery.TryCreateLocalMethodInterceptionTarget` のガードコメントに記載。

- 仮想/抽象/override インスタンスメソッド呼び出し(動的ディスパッチを壊さないため)
- ジェネリック型に属するメソッド(生成コードへ型パラメータが漏れるため)
- ref/out/in 引数、ref struct、匿名型を署名に含むメソッド
- ソース本体が参照できないメタデータのみのメソッド
- 呼び出し箇所が `MethodDeclarationSyntax` 配下にないもの
  (プロパティ矢印本体・フィールド初期化子内の呼び出し。
  Seamless.Database の View/TableFunction が LINQ 本体を通常メソッドに置き、
  ラムダからそれを呼ぶ形式にしているのはこの制約のため)

## 既知の課題

- `TryGetNonEnumeratedCount` は既知の失敗テストがある(過去メモ: Pipeline 展開との相互作用)。
- `Test.Benchmark.BenchmarkUiRunRepository.QueueSaveResult_ReplacesMeasurementForSameJob` の不安定は、
  保存キューを直列タスクチェーン + `FlushAsync()` に置き換えて確定同期化した(2026-07-02)。
