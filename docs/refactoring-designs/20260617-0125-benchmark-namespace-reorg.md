# Benchmark Namespace Reorganization

## 背景

`Shared/Common/Benchmarks` と `Test` は現在 `Seamless.Benchmarks` namespace 直下にあり、内容は `System.Linq.Enumerable` の intercept benchmark/test として扱われている。一方で Tsavorite 向けには `TsavoriteEnumerableCatalog.cs` が追加されているが、同じ `Seamless.Benchmarks` namespace に置かれており、System 側との役割境界が見えにくい。

ユーザー要望は次の 3 点。

- 現在 `Seamless.Benchmarks` 直下にある `System.Linq.Enumerable` intercept benchmark/test を `Test.Benchmarks.Intercept.System.Linq.Enumerable` に移動する。
- `Test.Tsavorite.Benchmarks` を `Test.Benchmarks` に移動する。
- `Test.Benchmarks.Intercept.System.Linq.Enumerable` を参考に、`Test.Benchmarks.Intercept.Tsavorite.Linq.Enumerable` のテストを作る。

## 現状の観察

- `Shared/Common/Benchmarks/*.cs` の多くが `namespace Seamless.Benchmarks;` を持つ。
- `Shared/Common/Benchmarks/BenchFact.cs` は assembly 属性で `Seamless.Benchmarks.TestPriorityOrderer` を参照している。
- `Test/Tests.csproj` は `<RootNamespace>Seamless.Benchmarks</RootNamespace>` を持つ。
- `Shared/Common/Common.projitems` は `<Import_RootNamespace>Seamless.Benchmarks</Import_RootNamespace>` を持つ。
- `Benchmark/Benchmark.UI/Program.cs` は benchmark 型を assembly reflection で拾っており、個別 namespace への直接参照は見つかっていない。
- `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/RecordSetcs` に `namespace Test.Tsavorite.Benchmarks;` がある。
- `Shared/Common/Benchmarks/TsavoriteEnumerableCatalog.cs` は Tsavorite と System の結果比較を行う catalog 形式の benchmark/test として存在している。

## 目的

- System.Linq.Enumerable intercept benchmark/test の namespace を用途が分かる名前にする。
- Tsavorite.Linq.Enumerable intercept benchmark/test を System 側と並ぶ namespace に置く。
- 既存 benchmark runner と xUnit test project が namespace 変更後も動く状態にする。
- 既存の未確定変更を巻き戻さず、関連箇所だけを最小限に変更する。

## 非目標

- System 側 per-method benchmark の中身の改修や性能改善は行わない。
- Tsavorite 側で System 側の per-method ファイル群を全コピーすることは、この変更では行わない。
- Tsavorite fragment の新規実装範囲は広げない。
- benchmark 実行 UI や single-file report の仕様は変えない。

## 提案設計

System 側は namespace のみ移動する。

- `Shared/Common/Benchmarks/*.cs`
  - 原則 `Seamless.Benchmarks` から `Test.Benchmarks.Intercept.System.Linq.Enumerable` へ変更する。
  - `TsavoriteEnumerableCatalog.cs` は例外として `Test.Benchmarks.Intercept.Tsavorite.Linq.Enumerable` に変更する。
- `Test/*.cs`
  - `BenchmarkTiming.cs`、`BenchmarkParameters.cs` などの test helper も System intercept benchmark/test の helper として `Test.Benchmarks.Intercept.System.Linq.Enumerable` に変更する。
- `Shared/Common/Benchmarks/BenchFact.cs`
  - `TestPriorityOrderer` の assembly 参照も新 namespace に変更する。
- `Test/Tests.csproj`
  - `<RootNamespace>` を `Test.Benchmarks.Intercept.System.Linq.Enumerable` に変更する。
- `Shared/Common/Common.projitems`
  - `<Import_RootNamespace>` を `Test.Benchmarks.Intercept.System.Linq.Enumerable` に変更する。

Tsavorite 側は既存の `TsavoriteEnumerableCatalog.cs` を基点にする。

- namespace を `Test.Benchmarks.Intercept.Tsavorite.Linq.Enumerable` に変更する。
- `Base`、`Categories`、`BenchmarkTestIterationsAttribute` など System 側 helper を参照できるよう、必要に応じて `using Test.Benchmarks.Intercept.System.Linq.Enumerable;` を追加する。
- System 実装と Tsavorite 実装の結果比較は、現状の `RunSystemForTest` / `RunTsavoriteForTest` 形式を維持する。

`Test.Tsavorite.Benchmarks` は `Test.Benchmarks` へ移動する。

- `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/RecordSetcs` の namespace を `Test.Benchmarks` に変更する。
- 参照があれば同時に更新する。

## 影響範囲

- `Shared/Common/Benchmarks`
- `Test`
- `Shared/Common/Common.projitems`
- `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/RecordSetcs`
- namespace 参照を持つ関連ファイル

benchmark runner は assembly reflection ベースのため、namespace 変更そのものでは型 discovery の方式を変えない。

## 移行手順

1. `Seamless.Benchmarks` 参照を System 側 namespace に機械的に置換する。
2. `TsavoriteEnumerableCatalog.cs` だけ Tsavorite 側 namespace に戻し、System 側 helper の using を追加する。
3. `Test.Tsavorite.Benchmarks` を `Test.Benchmarks` に変更する。
4. `rg` で旧 namespace 残存を確認する。
5. benchmark test project を build/test して、namespace 変更による compile error を修正する。

## テスト計画

- `dotnet test Test/Tests.csproj --no-restore -v:minimal`
- 必要に応じて Tsavorite 関連だけに絞った test filter で追加確認する。
- build/test が既存の未復元 package などで失敗した場合は、失敗理由を記録する。

## リスクと対策

- リスク: `TsavoriteEnumerableCatalog.cs` が System 側 helper を同一 namespace 前提で参照している。
  - 対策: Tsavorite 側ファイルに System 側 helper namespace の using を追加する。
- リスク: assembly-level attribute の型参照が旧 namespace に残る。
  - 対策: `rg "Seamless\.Benchmarks"` で残存を確認する。
- リスク: Shared project の RootNamespace metadata と実 namespace がずれる。
  - 対策: `.projitems` と test `.csproj` の metadata も合わせて更新する。
- リスク: dirty worktree に既存変更が多い。
  - 対策: 既存変更を戻さず、対象ファイルの namespace/参照変更だけを行う。

## 未解決事項

- Tsavorite 側を将来 System 側と同じ per-method ファイル構成に展開するかは、この変更では決めない。

## 合意状態

- 合意済み。`TsavoriteEnumerableCatalog.cs` は `Test.Benchmarks.Intercept.Tsavorite.Linq.Enumerable` に移して育てる方針で進める。
