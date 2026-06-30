# Tsavorite Linq Enumerable Intercept Order

## 背景

`Intercept.Tsavorite.Linq.Enumerable` という並びは、テスト一覧では `Intercept` が先頭側に出るため、同じ Tsavorite LINQ 系の benchmark と並べて見づらい。今回の主目的は、名前空間とフォルダ構造を `Tsavorite.Linq.Enumerable.Intercept` に寄せ、テスト一覧で `Tsavorite.Linq.Enumerable` の近くに並ぶようにすること。

## 現状の観察

- `tests/Intercept.Tsavorite.Linq.Enumerable/Intercept.Tsavorite.Linq.Enumerable.csproj` は test project で、`RootNamespace` と shared import が `Intercept.Tsavorite.Linq.Enumerable` を指している。
- `Benchmark/UI.Intercept.Tsavorite.Linq.Enumerable/Intercept.Tsavorite.Linq.Enumerable.csproj` は実測 benchmark runner で、同じ shared import を使っている。
- shared source は `Shared/Common/Benchmarks/Intercept.Tsavorite.Linq.Enumerable/` 配下にあり、多数の `.cs` が `namespace UI.Intercept.Tsavorite.Linq.Enumerable;` を持つ。
- `Shared/Common/Intercept.Tsavorite.Linq.Enumerable.projitems` は上記 shared source を import している。
- `tests/Tsavorite.Collections/Tsavorite.Collections.csproj` は既に `Tsavorite.Collections` と `FLinq` analyzer を参照し、benchmark test project として動ける構成を持つ。
- `Seamless.slnx` には test 側の旧 `Intercept.Tsavorite.Linq.Enumerable` project が登録されている。実装時点で sandbox 側の旧 project は未登録だった。

## 既存設計から引き継ぐ仕様

- `docs/refactoring-designs/20260617-0158-benchmark-project-split.md` では、test project は `tests/`、実測 benchmark project は `sandbox/` に置く方針になっている。
- shared source は `Shared/Common/Benchmarks/<project-like-name>/` に分け、用途別 `.projitems` から import する方針になっている。
- project file 名とそれを格納する folder 名は同じ名前にそろえる。

## 目的

- `Intercept.Tsavorite.Linq.Enumerable` を `Tsavorite.Linq.Enumerable.Intercept` に改名する。
- shared source の物理フォルダを namespace と同じ順序にする。
- test 一覧で見やすくするため、test 側の `Tsavorite.Linq.Enumerable.Intercept` project 内容を `Tsavorite.Collections` project に統合する。
- 実測 benchmark runner は `sandbox/Tsavorite.Linq.Enumerable.Intercept/Tsavorite.Linq.Enumerable.Intercept.csproj` として残す。

## 非目的

- benchmark method の中身や測定ロジックは変更しない。
- System LINQ 側の命名や構造は変更しない。
- `Tsavorite.Linq.Enumerable` 本体や `Tsavorite.Collections` 既存 benchmark の挙動は変更しない。
- `bin/` と `obj/` の生成物整理はこの変更の主目的に含めない。

## 提案設計

- shared source folder を次のように移す。
  - 旧: `Shared/Common/Benchmarks/Intercept.Tsavorite.Linq.Enumerable/`
  - 新: `Shared/Common/Benchmarks/Tsavorite.Linq.Enumerable.Intercept/`
- shared source の namespace を `Tsavorite.Linq.Enumerable.Intercept` に置換する。
- `.projitems` を次のように改名し、中身も新 folder と namespace に合わせる。
  - 旧: `Shared/Common/Intercept.Tsavorite.Linq.Enumerable.projitems`
  - 新: `Shared/Common/Tsavorite.Linq.Enumerable.Intercept.projitems`
- sandbox の実測 project を次のように改名する。
  - 旧: `Benchmark/UI.Intercept.Tsavorite.Linq.Enumerable/Intercept.Tsavorite.Linq.Enumerable.csproj`
  - 新: `sandbox/Tsavorite.Linq.Enumerable.Intercept/Tsavorite.Linq.Enumerable.Intercept.csproj`
- sandbox project の `RootNamespace` と import path を新名に合わせる。
- test 側の旧 project は standalone project として残さず、`tests/Tsavorite.Collections/Tsavorite.Collections.csproj` に以下を統合する。
  - `DefineConstants` の `BENCH_AS_TEST;TSAVORITE_LINQ_COUNTER`
  - `CompilerGeneratedFilesOutputPath`
  - `Using Include="BenchmarkDotNet.Attributes"` など benchmark 用 using
  - `Using Include="Common"`
  - 新 `.projitems` の import
- `Seamless.slnx` は旧 test project を削除し、新 sandbox project path を追加する。
- 旧 `tests/Intercept.Tsavorite.Linq.Enumerable/` は source project としては不要になるため、tracked project file を削除する。生成物が残っていても git 管理外なら変更対象外にする。

## 影響範囲

- `tests/Tsavorite.Collections/Tsavorite.Collections.csproj`
- `sandbox/Tsavorite.Linq.Enumerable.Intercept/`
- `Shared/Common/Tsavorite.Linq.Enumerable.Intercept.projitems`
- `Shared/Common/Benchmarks/Tsavorite.Linq.Enumerable.Intercept/**/*.cs`
- `Seamless.slnx`
- 旧名への参照を持つドキュメントや project 参照

## 移行手順

1. shared source folder と `.projitems` を新名へ移動する。
2. shared source namespace と `.projitems` の `Import_RootNamespace` / `Compile Include` を新名へ更新する。
3. sandbox project folder と `.csproj` file 名を新名へ移動し、`RootNamespace` と import path を更新する。
4. test 側の import 内容を `Tsavorite.Collections.csproj` に統合し、旧 standalone test project file を削除する。
5. `Seamless.slnx` の project path を更新する。
6. `rg` で旧名の残存を確認し、意図的な過去設計メモ以外は解消する。
7. build/test で確認する。

## テスト計画

- `dotnet build tests/Tsavorite.Collections/Tsavorite.Collections.csproj --no-restore -v:minimal`
- `dotnet build sandbox/Tsavorite.Linq.Enumerable.Intercept/Tsavorite.Linq.Enumerable.Intercept.csproj --no-restore -v:minimal`
- 必要なら `dotnet test tests/Tsavorite.Collections/Tsavorite.Collections.csproj --no-restore -v:minimal`

## リスクと対策

- リスク: `Tsavorite.Collections` に shared benchmark を統合すると、既存 test project 内の namespace と using が増えて名前解決衝突が起きる。
  - 対策: build error を見て、必要なら shared 側の `global::System` 指定や using の整理だけを行う。
- リスク: interceptor の generated path が変わり、生成コードの namespace とずれる。
  - 対策: `RootNamespace`、source namespace、`.projitems` の `Import_RootNamespace` を同じ値にそろえる。
- リスク: 旧 test project が solution に残ると、テスト一覧の見え方が改善されない。
  - 対策: `Seamless.slnx` から旧 test project を外し、`Tsavorite.Collections` に統合する。

## 未解決事項

- なし。`Tsavorite.Linq.Enumerable.Intercept` の standalone test project は作らず、`Tsavorite.Collections` へ統合する前提で進める。

## 合意状態

- ユーザー指示により、主目的はテスト一覧での見やすさ。
- 実装前にこの設計を確認し、問題なければこのまま実装する。
