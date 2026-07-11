# Benchmark Project Split

## 背景

現状の benchmark 関連は、主に次の構成になっている。

- `Shared/Common/Benchmarks/`
  - `System.Linq.Enumerable` の intercept benchmark/test が中心。
  - `TsavoriteEnumerableCatalog.cs` は `Tsavorite.Linq.Enumerable` と `System.Linq.Enumerable` の直接呼び出し比較として追加されている。
- `Test/Tests.csproj`
  - shared benchmark を xUnit test として実行する test project。
- `Benchmark/Benchmark.UI/UI/UI.csproj`
  - shared benchmark を BenchmarkDotNet で実測する runner project。
- `tests/Test.Benchmark.Tsavorite.Collections.Benchmark/Test.Benchmark.Tsavorite.Collections.csproj`
  - Benchmark.Tsavorite.Collections 向け benchmark test project。
- `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/*.cs`
  - Benchmark.Tsavorite.Collections 向け benchmark test の実体。

ユーザー要望は、benchmark を次の軸で分離すること。

- `Benchmark.Tsavorite.Collections`
- `System.Linq.Enumerable`
- `Tsavorite.Linq.Enumerable`
- `Intercept.System.Linq.Enumerable`
- `Intercept.Tsavorite.Linq.Enumerable`

また、test project は `tests/` 配下、実測 benchmark project は `sandbox/` 配下に置くことで合意済み。

## 現状の観察

- `Shared/Common/Common.projitems` は `Benchmarks/**/*.cs` を一括 import しているため、現状は benchmark 種別ごとの選択ができない。
- `Test/Tests.csproj` と `Benchmark/Benchmark.UI/UI/UI.csproj` は同じ shared project を import している。
- `Benchmark/Benchmark.UI/Program.cs` は assembly reflection で `[Benchmark]` method を持つ型を列挙するため、個別 benchmark type の namespace には依存していない。
- `tests/Test.Benchmark.Tsavorite.Collections.Benchmark/Test.Benchmark.Tsavorite.Collections.csproj` は project 本体に source file を持たず、SDK default compile により project 配下だけを対象にしている。実体の多くは `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/*.cs` にある。
- `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/RecordSetcs` だけは前回変更で `namespace Test.Benchmarks;` になっており、他の Benchmark.Tsavorite.Collections benchmark と namespace がずれている。

## 目的

- benchmark test project と実測 benchmark project を用途ごとに分ける。
- shared benchmark source を用途ごとの物理フォルダに分ける。
- 既存 benchmark/test を適切な場所に移動し、旧 project 名・旧 namespace への依存をなくす。
- build と対象 test が通る状態まで整える。

## 非目標

- 各 benchmark method の性能改善は行わない。
- `System.Linq.Enumerable` と `Tsavorite.Linq.Enumerable` の未実装 benchmark を大量に新規作成することは、この変更では行わない。
- solution file への登録は、既存 solution が確認できる場合のみ行う。明確な solution 管理がなければ project file 作成と build/test 確認を優先する。
- 既存の unrelated dirty worktree を戻さない。

## 提案設計

### 共有ソース

`Shared/Common/Benchmarks/` を次の4フォルダに分ける。

```text
Shared/Common/Benchmarks/
  System.Linq.Enumerable/
  Tsavorite.Linq.Enumerable/
  Intercept.System.Linq.Enumerable/
  Intercept.Tsavorite.Linq.Enumerable/
```

移動方針。

- 現在の System intercept benchmark 群は `Intercept.System.Linq.Enumerable/` へ移動する。
- `TsavoriteEnumerableCatalog.cs` は `Tsavorite.Linq.Enumerable/` へ移動する。
- `Intercept.Tsavorite.Linq.Enumerable/` は、現時点で対応する shared benchmark が不足しているため project と folder を作り、必要なら空 marker class を置く。
- `System.Linq.Enumerable/` は、intercept なしの直接 System benchmark を今後置くため project と folder を作り、必要なら空 marker class を置く。

### shared project

一括 import の `Common.projitems` は段階的に廃止し、用途別の projitems を追加する。

```text
Shared/Common/System.Linq.Enumerable.projitems
Shared/Common/Tsavorite.Linq.Enumerable.projitems
Shared/Common/Intercept.System.Linq.Enumerable.projitems
Shared/Common/Intercept.Tsavorite.Linq.Enumerable.projitems
```

既存 `Common.projitems` は互換用に残すが、新 project では用途別 projitems を import する。

### test project

次を作成または移動する。

```text
tests/System.Linq.Enumerable/System.Linq.Enumerable.csproj
tests/Tsavorite.Linq.Enumerable/Tsavorite.Linq.Enumerable.csproj
tests/Intercept.System.Linq.Enumerable/Intercept.System.Linq.Enumerable.csproj
tests/Intercept.Tsavorite.Linq.Enumerable/Intercept.Tsavorite.Linq.Enumerable.csproj
tests/Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj
```

移動方針。

- `Test/Tests.csproj` は `tests/Intercept.System.Linq.Enumerable/` に移動・改名する。
- `tests/Test.Benchmark.Tsavorite.Collections.Benchmark/Test.Benchmark.Tsavorite.Collections.csproj` は `tests/Benchmark.Tsavorite.Collections/` に移動・改名する。
- `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/*.cs` は `tests/Benchmark.Tsavorite.Collections/` 配下へ移動する。
- Benchmark.Tsavorite.Collections benchmark namespace は `Benchmark.Tsavorite.Collections` に統一する。

### 実測 benchmark project

次を作成する。

```text
Benchmark/UI.System.Linq.Enumerable/System.Linq.Enumerable.csproj
sandbox/Tsavorite.Linq.Enumerable/Tsavorite.Linq.Enumerable.csproj
Benchmark/UI.Intercept.System.Linq.Enumerable/Intercept.System.Linq.Enumerable.csproj
Benchmark/UI.Intercept.Tsavorite.Linq.Enumerable/Intercept.Tsavorite.Linq.Enumerable.csproj
```

既存 `Benchmark/Benchmark.UI/Program.cs` は共通 runner としてコピーまたは共有する。まずは各 project に `Program.cs` を置くより、既存 `Program.cs` を linked compile する方針を優先する。

### namespace

各共有 benchmark source の namespace は project 名に合わせる。

- `System.Linq.Enumerable`
- `Tsavorite.Linq.Enumerable`
- `Intercept.System.Linq.Enumerable`
- `Intercept.Tsavorite.Linq.Enumerable`
- `Benchmark.Tsavorite.Collections`

`System` セグメントと class 名 `Range` / `AsEnumerable` の衝突を避けるため、BCL 参照は必要に応じて `global::System...` を使う。

## 影響範囲

- `Shared/Common/Benchmarks/**`
- `Shared/Common/*.projitems`
- `Test/**`
- `Benchmark/Benchmark.UI/**`
- `tests/Test.Benchmark.Tsavorite.Collections.Benchmark/**`
- `tests/Test.Benchmark.Tsavorite.Collections/Benchmark/**`
- 新規 `tests/*`
- 新規 `Benchmark/UI.*`

## 移行手順

1. 共有ソースの folder を作成し、既存 benchmark source を移動する。
2. namespace を新設計に合わせて置換する。
3. 用途別 `.projitems` を作成する。
4. test project を `tests/*` に作成・移動する。
5. 実測 benchmark project を `Benchmark/UI.*` に作成する。
6. 旧 project/folder の残存参照を `rg` で確認して修正する。
7. build/test を通す。

## テスト計画

最低限、次を確認する。

- `dotnet build tests/Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj --no-restore -v:minimal`
- `dotnet build tests/Intercept.System.Linq.Enumerable/Intercept.System.Linq.Enumerable.csproj --no-restore -v:minimal`
- `dotnet test tests/Tsavorite.Linq.Enumerable/Tsavorite.Linq.Enumerable.csproj --no-restore -v:minimal`
- `dotnet test tests/Intercept.System.Linq.Enumerable/Intercept.System.Linq.Enumerable.csproj --no-restore -v:minimal`
- `dotnet build Benchmark/UI.System.Linq.Enumerable/System.Linq.Enumerable.csproj --no-restore -v:minimal`
- `dotnet build sandbox/Tsavorite.Linq.Enumerable/Tsavorite.Linq.Enumerable.csproj --no-restore -v:minimal`
- `dotnet build Benchmark/UI.Intercept.System.Linq.Enumerable/Intercept.System.Linq.Enumerable.csproj --no-restore -v:minimal`
- `dotnet build Benchmark/UI.Intercept.Tsavorite.Linq.Enumerable/Intercept.Tsavorite.Linq.Enumerable.csproj --no-restore -v:minimal`

全 test が長時間化する場合は、まず build と代表的な catalog/result match test を通し、timeout した全件 test は理由を記録する。

## リスクと対策

- リスク: physical move により analyzer generated file path が変わり、interceptor hint や generated diagnostics が変化する。
  - 対策: build/test で generated compile error を確認し、必要に応じて namespace や path 前提を修正する。
- リスク: `System` namespace セグメントにより BCL 型解決が壊れる。
  - 対策: `global::System` を使う。
- リスク: 既存 dirty worktree の変更と衝突する。
  - 対策: 変更対象を benchmark 関連に限定し、既存変更を戻さない。
- リスク: 空 project が test discovery warning を出す。
  - 対策: marker class だけを置き、test がない project は build 確認対象にする。

## 未解決事項

- なし。`Collection`/`Collections` は `Benchmark.Tsavorite.Collections` にすることで合意済み。
- test project は `tests/`、実測 benchmark project は `sandbox/` に置くことで合意済み。

## 合意状態

- 合意済み。ユーザー指示により実装へ進む。
