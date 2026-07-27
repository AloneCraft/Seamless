---
phase: code-basic-reverse
reverse_document: true
canonical_target: Document/Code/Basic/ModuleBreakdown.md
evidence_sources:
  - Seamless.slnx
  - Reverse/Code/Internal/
  - Reverse/Code/External/
derived_from:
  - Reverse/Code/Internal/
  - Reverse/Code/External/
confidence: medium
review_status: draft
---

# Module Breakdown

## モジュール分割

| モジュール | サブモジュール | 責務 |
|---|---|---|
| Source generation | `FLinq`, `Seamless.Database`, `Seamless.Database.Generator` | LINQ interceptor と schema record generation。 |
| Runtime collections | `Benchmark.Tsavorite.Collections/Stores`, `Collections`, `SessionFunctions`, `Linq`, `Devices` | Tsavorite-backed collection、serializer、LINQ extension、counter、device abstraction。 |
| Benchmark infrastructure | `Common`, `Benchmark workload projects` projects | System LINQ / FLinq / ZLinq / Tsavorite LINQ の比較 workload。 |
| Benchmark UI/tool | `Benchmark` | CPU affinity 選択、worker process 実行、CSV/HTML report。 |
| Tests | `Test`, `Test.System.*`, `Test.Tsavorite.*` | source generator、collection、LINQ、benchmark route の検証。 |

## モジュール根拠

- solution に source generator、runtime、benchmark、test project が明示されている（`Seamless.slnx:13`, `Seamless.slnx:26`, `Seamless.slnx:28`, `Seamless.slnx:29`, `Seamless.slnx:30`）。
- `FLinq` と `Seamless.Database` は Roslyn component である（`FLinq/FLinq.csproj:8`, `Seamless.Database/Seamless.Database.csproj:8`）。
- `Benchmark.Tsavorite.Collections` は MemoryPack と Microsoft.Garnet に依存する（`Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:14`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:15`）。
- MAUI runner は benchmark projects を参照する（`Benchmark/Benchmark.UI/UI.csproj:34`, `Benchmark/Benchmark.UI/UI.csproj:41`）。

## Review Gate: Code Basic

### Confirmed
- 実装単位は project と top-level folder で明確に分かれる。

### Inferred
- confidence: medium。source generator と runtime collection は相互補完関係にあるが、別配布単位として扱われる可能性がある。

### Unknown
- パッケージ公開時の最終 module/package 境界。
