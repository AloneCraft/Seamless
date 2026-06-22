---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/FLinqSourceGeneratorInternalDesign.md
evidence_sources:
  - FLinq/FLinq.csproj
  - FLinq/SourceGenerator/interceptor.cs
  - FLinq/SourceGenerator/LinqTargetDiscovery.cs
  - FLinq/SourceGenerator/LinqGeneratorModels.cs
  - FLinq/SourceGenerator/OptimizeVisitor.cs
  - FLinq/SourceGenerator/AlgebraicRewriter.cs
  - FLinq/SourceGenerator/Linq/Fragments/
confidence: medium
review_status: draft
---

# FLinq Source Generator 内部設計

## 対象
`FLinq` は Roslyn source generator / interceptor として、LINQ 呼び出しを検出し、最適化済みのローカルメソッドまたは計測用メソッドへ置き換えるコードを生成する。プロジェクト自体は `net10.0`、`LangVersion latest`、`IsRoslynComponent true` として構成されている（`FLinq/FLinq.csproj:3`, `FLinq/FLinq.csproj:4`, `FLinq/FLinq.csproj:8`）。

## 主要クラス

| クラス/領域 | 責務 | 実装証拠 |
|---|---|---|
| `FLinqIncrementalGenerator` | incremental generator の入口。属性ソースと interceptor ソースを生成する。 | `FLinq/SourceGenerator/interceptor.cs:13`, `FLinq/SourceGenerator/interceptor.cs:55`, `FLinq/SourceGenerator/interceptor.cs:64`, `FLinq/SourceGenerator/interceptor.cs:69`, `FLinq/SourceGenerator/interceptor.cs:102` |
| `LinqTargetDiscovery` | LINQ 呼び出しを含むメソッドを interception target として生成する。 | `FLinq/SourceGenerator/LinqTargetDiscovery.cs:22`, `FLinq/SourceGenerator/LinqTargetDiscovery.cs:52` |
| `LinqGeneratorModels` | target、parameter、per-file group、比較器、診断定義などの内部モデルを保持する。 | `FLinq/SourceGenerator/LinqGeneratorModels.cs:65`, `FLinq/SourceGenerator/LinqGeneratorModels.cs:79`, `FLinq/SourceGenerator/LinqGeneratorModels.cs:140`, `FLinq/SourceGenerator/LinqGeneratorModels.cs:175` |
| `OptimizeVisitor` | query syntax 変換、extension/static 化、anonymous struct record 変換、式最適化を段階的に実行する。 | `FLinq/SourceGenerator/OptimizeVisitor.cs:5`, `FLinq/SourceGenerator/OptimizeVisitor.cs:294`, `FLinq/SourceGenerator/OptimizeVisitor.cs:301`, `FLinq/SourceGenerator/OptimizeVisitor.cs:310` |
| `AlgebraicRewriter` | `Count` 比較などの代数的な書き換えを行い、anchor を含む式を避ける。 | `FLinq/SourceGenerator/AlgebraicRewriter.cs:23`, `FLinq/SourceGenerator/AlgebraicRewriter.cs:33`, `FLinq/SourceGenerator/AlgebraicRewriter.cs:104`, `FLinq/SourceGenerator/AlgebraicRewriter.cs:176` |
| `AnchorAnalysis` | `.Anchor` または `AnchorExtensions.Anchor` の検出を行い、最適化境界を判断する。 | `FLinq/SourceGenerator/AnchorAnalysis.cs:6`, `FLinq/SourceGenerator/AnchorAnalysis.cs:15`, `FLinq/SourceGenerator/AnchorAnalysis.cs:33` |
| `Fragments` | LINQ 演算子ごとのコード生成単位。`Root`、`Where`、`Select`、集合演算、Tsavorite 専用 scan terminal などを持つ。 | `FLinq/SourceGenerator/Linq/Fragments/Root.cs:7`, `FLinq/SourceGenerator/Linq/Fragments/Where.cs:2`, `FLinq/SourceGenerator/Linq/Fragments/Select.cs:2`, `FLinq/SourceGenerator/Linq/Fragments/Tsavorite/TsavoriteHashSetScanTerminal.cs:5` |

## 処理アルゴリズム

1. generator は syntax provider で候補ノードを収集する（`FLinq/SourceGenerator/interceptor.cs:57`）。
2. `LinqTargetDiscovery` がメソッド呼び出し、属性指定、対象メソッド形状を解析し、interception target を作る（`FLinq/SourceGenerator/LinqTargetDiscovery.cs:22`）。
3. target は `Distinct` で重複排除され、source path 単位に group 化される（`FLinq/SourceGenerator/interceptor.cs:74`, `FLinq/SourceGenerator/interceptor.cs:96`）。
4. `OptimizeVisitor` と `AlgebraicRewriter` が式を正規化し、fragment pipeline で出力コードを構築する（`FLinq/SourceGenerator/OptimizeVisitor.cs:294`, `FLinq/SourceGenerator/AlgebraicRewriter.cs:33`）。
5. per-file group ごとに generated source が `AddSource` される（`FLinq/SourceGenerator/interceptor.cs:102`）。

## 状態と例外

- `LinqInterceptAttribute` は generator が生成する属性で、`OptimizeLinq`、`Intercept`、`Instrumentation` を保持する（`FLinq/SourceGenerator/interceptor.cs:29`, `FLinq/SourceGenerator/interceptor.cs:38`, `FLinq/SourceGenerator/interceptor.cs:48`）。
- fragment 生成不能な場合は実行時失敗用ヘルパーへ落とす設計がある（`FLinq/SourceGenerator/LinqGeneratorModels.cs:65`, `Tsavorite.Collections/GeneratedCodeHelpers.cs:3`）。
- Tsavorite `HashSet` scan は `Count`、`Any`、`First` などに限定して直接実行 terminal を構築する（`FLinq/SourceGenerator/Linq/Fragments/Tsavorite/TsavoriteHashSetScanTerminal.cs:29`, `FLinq/SourceGenerator/Linq/Fragments/Tsavorite/TsavoriteHashSetScanTerminal.cs:32`）。

## Review Gate: Code Internal

### Confirmed
- Roslyn incremental generator として構成されている。
- LINQ target discovery、最適化 visitor、fragment rendering、per-file source output の段階がある。
- anchor は最適化境界として扱われる。

### Inferred
- confidence: medium。主な設計意図は、LINQ の反復・delegate 呼び出し・中間列挙を減らすことだと推測できる。
- confidence: medium。`Counter` instrumentation は性能比較と最適化効果検証のために実装されていると見られる。

### Unknown
- 生成コード互換性の正式な公開保証範囲。
- fragment 未対応時に実行時例外へ落とす方針が利用者向け契約か内部保険か。

### Questions
- 通常設計書へ反映する場合、interceptor を公開 API として扱うか、内部最適化機構として扱うかを確認する。
