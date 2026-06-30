# Common の BENCH_AS_TEST 条件分岐削減

## 背景

`Shared/Common/Benchmarks` の各ベンチマーククラスには、xUnit 実行時だけ有効な `#if BENCH_AS_TEST` が散在している。
主な用途は、クラス単位の `Xunit.TestCaseOrderer`、結果比較用の `Results_match`、最適化なし比較用の `FLinqNoOptimized` である。

一方、`Base.cs` 内の `#if BENCH_AS_TEST` は、テスト時の件数縮小、`Consumer` の capture、`AssertResults` の実比較など、BenchmarkDotNet 実行条件に関わる分岐を含む。
この分岐は今回の削減対象から外す。

## 現状の観察

- `Shared/Common/Common.projitems` は `Benchmarks/**/*.cs` を `Benchmark/UI` と `Test` の両方へ取り込む。
- `Test/Tests.csproj` は `BENCH_AS_TEST` を定義し、xUnit 参照と `Using Include="Xunit"` を持つ。
- `Benchmark/UI/UI/UI.csproj` は `BENCH_AS_TEST` を定義せず、BenchmarkDotNet 実行を目的にする。
- `BenchFact.cs` は `BENCH_AS_TEST` 時に xUnit の `FactAttribute` / trait / orderer を定義し、非 `BENCH_AS_TEST` 時は `BenchFactAttribute` だけを no-op として定義している。
- 各ベンチマーククラスには同じ `#if BENCH_AS_TEST [Xunit.TestCaseOrderer(typeof(TestPriorityOrderer))] #endif` が重複している。
- `Xunit.TestCaseOrdererAttribute` は assembly に適用できるため、クラスごとの指定は assembly 属性へ集約できる。
- `Results_match` / `FLinqNoOptimized` はクラスごとに `Linq` / `Linq1..4` などの具体メソッドを参照するため、完全に `Base` へ移動するのは難しい。
- `FLinqNoOptimized` は `[LinqIntercept(false, LinqInstrumentation.Counter)]` を個別メソッドへ付けることで、最適化なしかつ counter 付きの比較対象を作っている。

## 目的

- 各ベンチマーククラスに散らばる `#if BENCH_AS_TEST` を減らす。
- `Results_match` と `FLinqNoOptimized` のクラス内記述は維持し、条件コンパイルだけを外す。
- xUnit 側の結果比較とカウンター比較の意味は変えない。
- BenchmarkDotNet 側の測定条件に、結果比較・capture 処理を混入させない。

## 非目標

- `Base.cs` のテスト時 / BDN 時の実行条件分岐を消さない。
- ベンチマーク本体の LINQ / FLinq / ZLinq 実装を変えない。
- `FLinqNoOptimized` を `Base` の汎用 `Action` 実行へ置き換えない。
- 結果比較の正規化仕様を変更しない。
- BenchmarkDotNet の測定対象メソッドを追加しない。

## 提案設計

1. `BenchFact.cs` の `BENCH_AS_TEST` 側に assembly 属性を追加する。

   ```csharp
   [assembly: Xunit.TestCaseOrderer(typeof(Seamless.Benchmarks.TestPriorityOrderer))]
   ```

   これにより各ベンチマーククラス先頭の `Xunit.TestCaseOrderer` 条件ブロックを削除する。

2. `BenchFact.cs` の非 `BENCH_AS_TEST` 側に no-op 属性を追加する。

   ```csharp
   public sealed class FactAttribute:Attribute;
   public sealed class TraitAttribute(string name,string value):Attribute;
   public sealed class TestPriorityAttribute(int priority):Attribute;
   ```

   これにより各ベンチマーククラスの `Results_match` に付く `[Fact,Trait(...),TestPriority(...)]` を BDN 側でもコンパイルできるようにする。

3. 各ベンチマーククラスの下部にある `#if BENCH_AS_TEST` / `#endif` を削除し、`Results_match` と `FLinqNoOptimized` を常時コンパイルする。

   BDN 側にもメソッドは存在するが、`[Benchmark]` は付かないため BenchmarkDotNet の測定対象にはならない。

4. `Base.cs` の非 `BENCH_AS_TEST` 側に `AssertResults` の throw 実装だけを追加する。

   ```csharp
   protected void AssertResults(Action linq,Action flinq,Action noOptimized,Action zlinq)
       => throw new NotSupportedException("Result comparison is available only when BENCH_AS_TEST is defined.");

   protected void AssertResults(Action linq,Action flinq,Action noOptimized)
       => throw new NotSupportedException("Result comparison is available only when BENCH_AS_TEST is defined.");
   ```

   BDN 側では `Results_match` が呼ばれない前提で、比較・capture・normalize ロジックは混入させない。

5. `Base.cs` の `Consumer` capture、`Result` 初期化、`count` 切り替え、`Normalize` 群は現状どおり `BENCH_AS_TEST` 側に残す。

## 影響範囲

- `Shared/Common/Benchmarks/BenchFact.cs`
- `Shared/Common/Benchmarks/Base.cs`
- `Shared/Common/Benchmarks/*.cs`
- `Test/Tests.csproj` での xUnit 実行
- `Benchmark/UI/UI/UI.csproj` での BenchmarkDotNet ビルド

## 移行手順

1. `BenchFact.cs` に assembly orderer と非テスト時 no-op 属性を追加する。
2. `Base.cs` の非 `BENCH_AS_TEST` 側に throw 版 `AssertResults` オーバーロードを追加する。
3. 各ベンチマーククラス先頭の `TestCaseOrderer` 条件ブロックを削除する。
4. 各ベンチマーククラス下部の `Results_match` / `FLinqNoOptimized` を囲む `#if BENCH_AS_TEST` を削除する。
5. `Base.cs` と `BenchFact.cs` に残る `#if BENCH_AS_TEST` が、共有基盤の実行モード差分だけになっていることを確認する。

## テスト計画

- `dotnet test Test/Tests.csproj --no-restore`
- `dotnet build Benchmark/UI/UI/UI.csproj --no-restore`
- 必要に応じて代表ケースを filter して確認する。
  - 単発スカラー: `Aggregate_func`
  - 4 段チェーン: `Where_predicate`
  - ZLinq なしケース: `AsEnumerable`

## リスクと対策

- リスク: BDN 側で `Results_match` が何らかの方法で呼ばれると `NotSupportedException` になる。
  - 対策: `[Benchmark]` を付けず、テスト用メソッドとして明示する。これは意図的な fail-fast とする。
- リスク: assembly orderer が想定外のテストにも適用される。
  - 対策: 適用対象は `Common` を取り込む `Tests` アセンブリ全体。現状の用途では全ベンチ共有テストへの一律適用が意図に合う。
- リスク: BDN 側に `FLinqNoOptimized` が存在することで source generator の出力が増える。
  - 対策: ユーザー合意済み。測定対象ではないため実行時測定には影響させない。
- リスク: 機械的削除で `#if` / `#endif` の対応を壊す。
  - 対策: 変更後に `rg "#if BENCH_AS_TEST"` とビルド・テストで確認する。

## 未決事項

- なし。

## 合意状態

- テスト用メンバーが BDN 側アセンブリにもコンパイルされることを許容する。
- `TestCaseOrderer` は assembly 属性へ集約する。
- `Base.cs` 内の実行モード差分の `#if BENCH_AS_TEST` は残す。
- BDN 側の `AssertResults` は呼ばれない前提の throw 実装にする。
