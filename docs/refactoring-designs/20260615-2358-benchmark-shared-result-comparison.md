# Common の結果比較横展開

## 背景

`Shared/Common/Benchmarks` は `Benchmark/UI` の BenchmarkDotNet 実行と、`Test` の xUnit 実行で共有されている。従来は多くのベンチが `Consumer` へ結果を流すだけで、LINQ / FLinq / ZLinq の結果一致は確認していなかった。

`Aggregate_func` では、ベンチ本体で計算値を `Base.Result` に保存しつつ `consumer.Consume(result)` することで、BenchmarkDotNet の測定を維持しながら、`BENCH_AS_TEST` 時に `AssertResults` で結果比較できることを確認した。

## 現状の観察

- `Aggregate_func` は `dotnet test Test/Tests.csproj --filter "FullyQualifiedName~Aggregate_func" --no-restore` で成功した。
- `Aggregate_func` は `dotnet run --project Benchmark/Benchmark.UI/UI/UI.csproj -c Release --no-restore -- --filter *Aggregate_func* --join` で LINQ / FLinq / ZLinq の 3 ベンチが実行できた。
- `Base` には `Result` と `AssertResults(Action linq, Action flinq, Action noOptimized, Action zlinq)` がある。
- `BenchFact.cs` には `[TestPriority]` と `TestPriorityOrderer` があり、`Results_match` を低優先度で後ろに回せる。
- ベンチには以下の形が混在する。
  - 単発の `Linq` / `FLinq` / `ZLinq`
  - `Linq1..4` / `FLinq1..4` / `ZLinq1..4`
  - `Any` や `TryGetNonEnumeratedCount` のようなループ内消費
  - `GroupBy` / `ToLookup` / `ToDictionary` など、単純な `Assert.Equal(object, object)` では比較が弱い型
  - `AsEnumerable` / `AsQueryable` のように ZLinq 対応がない、または比較対象が非対称なもの

## 目的

- BenchmarkDotNet 側では既存の測定対象を保つ。
- xUnit 側では各ベンチの LINQ / FLinq / ZLinq の結果一致を確認できるようにする。
- FLinq optimized と no optimized の両方を比較対象に含める。
- `Results_match` は測定用 `BenchFact` より後に実行されるようにする。

## 非目標

- LINQ チェーン自体の最適化ロジック変更はしない。
- BenchmarkDotNet の測定条件、iteration count、category 構成は変えない。
- 既存のベンチ式を別アルゴリズムへ置き換えない。
- `tests/Test.Benchmark.Tsavorite.Collections` の既存比較カタログとは統合しない。

## 提案設計

1. `Base` に結果保存・比較の共通 API を置く。
   - `Result` はベンチ本体が最後の観測値を保存する。
   - ループ系や複合結果は、必要に応じて `Result` に配列・タプル・正規化済み文字列を保存する。
   - `AssertResults` は `Action` を実行して `Result` を取得し、期待値と比較する。

2. ベンチ本体は既存の `consumer.Consume(...)` / `source.Consume(this.consumer)` を基本的に維持する。
   - `Consumer` を `Seamless.Benchmarks.Consumer` としてラップし、内部で `BenchmarkDotNet.Engines.Consumer` へ委譲する。
   - 通常 Benchmark 実行時は記録せず、`AssertResults` 実行時だけ `Consume` された値を捕捉する。
   - これにより、各ベンチ本文の大量な `Result=` 書き換えを避ける。

3. `Results_match` を各ベンチクラスに追加する。
   - 単発系:
     ```csharp
     [Fact,Trait("Category","Unit"),TestPriority(100)]
     public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);
     ```
   - `Linq1..4` 系は suffix ごとに比較する。
   - ZLinq がないものは `AssertResults` のオーバーロード追加、または個別 assertion にする。

4. no optimized メソッドを `BENCH_AS_TEST` 内に追加する。
   - 既存の `FLinq()=>this.Linq()` と同じ呼び出し形に合わせる。
   - `[LinqIntercept(false, LinqInstrumentation.Counter)] private void FLinqNoOptimized()=>this.Linq();`
   - `Linq1..4` 系では `FLinqNoOptimized1..4` を追加する。

5. 比較の正規化が必要な型を `Base` に集約する。
   - 配列、リスト、辞書、Lookup、Grouping は内容比較できる形へ変換する。
   - 単純な `object.Equals` で意味が弱い型は正規化文字列または配列へ寄せる。

## 影響範囲

- `Shared/Common/Benchmarks/Base.cs`
- `Shared/Common/Benchmarks/BenchFact.cs`
- `Shared/Common/Benchmarks/*.cs`
- `Test/Tests.csproj` の xUnit 実行
- `Benchmark/Benchmark.UI/UI/UI.csproj` の BenchmarkDotNet 実行

## 移行手順

1. `Aggregate_func` の確定形を基準にする。
2. 単発スカラー系へ横展開する。
3. 単発シーケンス系へ横展開する。
4. `Linq1..4` 系へ横展開する。
5. 特殊系を個別対応する。
6. `dotnet test Test/Tests.csproj --no-build` を通す。
7. `dotnet build Benchmark/Benchmark.UI/UI/UI.csproj --no-restore` を通す。
8. 代表ベンチとして `Aggregate_func` を BenchmarkDotNet で実行する。

## テスト計画

- `Aggregate_func` の xUnit と BenchmarkDotNet 実行。
- `Tests` 全体。
- `Benchmark` プロジェクトのビルド。
- 横展開後、代表として以下を個別 filter で xUnit 実行する。
  - スカラー: `Aggregate_func`
  - シーケンス: `Where_predicate`
  - 辞書: `ToDictionary_keySelector`
  - Grouping: `GroupBy_keySelector`
  - 特殊: `TryGetNonEnumeratedCount`

## リスクと対策

- リスク: シーケンス結果を materialize するとベンチ測定が変わる。
  - 対策: Benchmark 側では既存の `Consume` を維持し、比較用 `Result` の materialize は `BENCH_AS_TEST` に限定する。
- リスク: `GroupBy` / `Lookup` の参照比較で誤検出する。
  - 対策: `Base` に正規化比較を置く。
- リスク: SG が比較補助コード内の LINQ を intercept して生成エラーを出す。
  - 対策: orderer など補助コードでは LINQ を避ける。必要なら `[LinqIntercept(false)]` を付ける。
- リスク: 全ファイル一括変更でベンチ式を壊す。
  - 対策: 形ごとに段階移行し、ビルド・テストを小刻みに確認する。

## 未決事項

- なし。

## 合意状況

- `Aggregate_func` の Test/Benchmark 実行は成功済み。
- ZLinq 非対応や特殊系も含めて `Common/Benchmarks` 配下の全ベンチクラスへ適用することで合意済み。
- 全体横展開後、`Tests` 全体、`Benchmark` ビルド、`Aggregate_func` BenchmarkDotNet 実行が成功済み。
