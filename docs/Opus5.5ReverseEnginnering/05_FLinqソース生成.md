---
phase: opus5.5-reverse
reverse_document: true
base_commit: d3a5a3e8
target: Seamless.FLinq.Generator
evidence_sources:
  - Seamless.FLinq.Generator/SourceGenerator/Interceptor.cs
  - Seamless.FLinq.Generator/SourceGenerator/FLinqIncrementalGenerator.TargetDiscovery.cs
  - Seamless.FLinq.Generator/SourceGenerator/LinqConfigurationResolver.cs
  - Seamless.FLinq.Generator/SourceGenerator/LinqGeneratorModels.cs
  - Seamless.FLinq.Generator/SourceGenerator/Linq/Registry/*
  - Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/*
  - Test/SourceGenerator/*
  - FLinq.md
  - FLinq-Optimization-Candidate-Backlog.md
  - FLinq-Benchmark-Improvement-Backlog.md
confidence: medium
review_status: draft
---

# 05 FLinq ソース生成（Seamless.FLinq.Generator）

## 行うべき機能

LINQ 呼び出しを含むメソッドをコンパイル時に見つけ、インターセプターで置き換える。置き換え先は、デリゲート呼び出しと中間の列挙を減らした展開コードにする。展開できないものは隠さず、診断と実行時例外で知らせる。

| 機能 | 内容 | 状態 | 根拠 |
|---|---|---|---|
| 対象の発見 | 呼び出し式から、LINQ を含むメソッドを傍受対象にする。ジェネリックメソッドは未構築の定義に正規化して扱う | 実装済み | `FLinqIncrementalGenerator.TargetDiscovery.cs:54-58` |
| 有効化の指定 | `[LinqIntercept(bool)]` をアセンブリ・クラス・メソッドに付ける | 実装済み | `Interceptor.cs:43-61` |
| 最適化の選択 | `Rewrite`（代数的書き換え）・`PipelineFusion`・`PredicatePushdown`・`ProjectionMerge`。既定は全部 | 実装済み | `Interceptor.cs:25-33`、`:57` |
| 収束モード | `Legacy` と `GlobalFixedPoint`（大域固定点）。既定は `Legacy` | 実装済み | `Interceptor.cs:34-37`、`:58`、`FixedPointRunner.cs` |
| 最適化の記録 | `EmitOptimizationTrace` で書き換えの経過を出す | 実装済み | `Interceptor.cs:59`、`OptimizationTrace.cs` |
| 計測 | `LinqInstrumentation.Counter` で反復・デリゲート呼び出しなどを数える | 実装済み | `Interceptor.cs:39-42`、`Seamless.Linq/LinqCounter.cs` |
| 演算子の登録表 | 演算子ごとの展開部品（Fragment）を登録表で管理し、不整合は `FLINQ006` エラー | 実装済み | `Linq/Registry/OperatorRegistry*.cs`、`LinqGeneratorModels.cs:72-76` |
| 演算子の網羅 | `System.Linq.Enumerable` のほぼ全演算子（Aggregate〜Zip、.NET 10 の `LeftJoin`/`RightJoin`/`CountBy`/`AggregateBy`/`Index`/`Shuffle` を含む） | 実装済み | `Linq/Fragments/*.cs`（129 ファイル）、`Linq/Registry/OperatorCoverageManifest.cs` |
| ストレージのネイティブ走査 | Tsavorite/Persisted の `Iterate(ref strategy)` へ展開 | 実装済み | `Linq/Fragments/Tsavorite/*`、`Linq/Fragments/Persisted/*` |
| 非公開メンバーへのアクセス | 生成コードから `UnsafeAccessor` で読む | 実装済み | `FLinqIncrementalGenerator.UnsafeAccessor.cs` |
| 列挙子の破棄 | 終端が取得した列挙子を、System.Linq と同じく必ず1回破棄する | 実装済み（2026-09-25 修正、未コミット） | `Linq/Fragments/Bases/BufferedScalar.cs` の `RenderOwnedEnumeratorComputation`、`Test/System/Linq/Intercept/Optimization/ScalarTerminalDisposeSemantics.cs` |
| 性能の改善 | ZLinq 同等以上を目指す個別の最適化 | **継続中** | `FLinq-Optimization-Candidate-Backlog.md`、`FLinq-Benchmark-Improvement-Backlog.md` |

## 1. 不変条件

1. **静かに元の実装へ戻す経路を作らない。** 展開に失敗したら `LinqInterceptorFailure` を投げるコードを生成し（`FLinq.md` §12）、`FLINQ002`（警告）で知らせる（`LinqGeneratorModels.cs:64-68`）。
2. **展開後の意味は、元の `System.Linq` の意味と一致する。** 等値性は .NET の既定（スコープ付きクエリのバイト一致を持ち込まない）。順序・例外・遅延評価の時点も一致させる。
3. **ジェネリックのインターセプター署名を合法に作れないときは `FLINQ005`（エラー）で止める**（`LinqGeneratorModels.cs:19-23`）。
4. **匿名型を含む式は、最適化できる部分だけを傍受し、`FLINQ001`（警告）を出す**（`LinqGeneratorModels.cs:47-51`）。
5. **登録表と展開部品が食い違ったら `FLINQ006`（エラー）**（`LinqGeneratorModels.cs:72-76`）。
6. **既定の動作は `Legacy` 収束モード。大域固定点は明示指定のときだけ**（`Interceptor.cs:58`）。【記録】`Benchmark.Dynamic実装計画.md` §1「既存 FLinq の既定動作は変更しない」。
7. **生成器は `netstandard2.0` で動き、生成時にリフレクションを使わない**（コミット `4283ec7b`、`NetStandard20Compatibility.cs`）。
8. **生成コードは元の名前空間に `Intercept.` を前置した名前空間に置く**（`FLinq.md` §13）。利用側は `InterceptorsPreviewNamespaces` に `Intercept` を含める。
9. **Tsavorite/Persisted の型を文字列の型名で照合している。** これらの型名・メンバー（`Iterate`、`.Count`、`.Contains`、`AsSpan`）を変えるときは生成器を同時に直す（【記録】承認済み計画「壊してはいけない結合点」）。
10. **終端は、ソースから取得した列挙子を必ず1回破棄する。** 集計関数 `Compute` へは列挙用 struct を参照（`ref`）で渡し、列挙した実体そのものを `finally` で破棄する。値渡しにすると、内側の列挙子を遅延作成したコピーが破棄されず、`Seamless.Database` の読み取りセッションが残る（BUG-0007。`Linq/Fragments/Bases/BufferedScalar.cs`、`SequenceEqual.cs`）。
11. **ベンチの `Route.cs` の型消去は意図的**（【記録】ベンチ運用メモ）。生成器側の判定は `TryGetSpan` の実行時判定を正とし、静的判定だけで経路を決めない。

## 2. 禁止事項

- 展開できない呼び出しを、黙って `System.Linq` 呼び出しへ戻さない。
- 既定の最適化の意味を、ベンチの数字を良くするために変えない。性能改善は、元の意味との一致テスト（形状テスト・意味テスト）を揃えてから入れる。
- 性能の良し悪しを自作の簡易計測で判断しない。Worker/BenchmarkDotNet の実測で判断する（【記録】ベンチ運用メモ「TieredPGO の罠」「ウォームアップ非対称」）。
- `[MethodImpl(AggressiveOptimization)]` をファイル単位で一律に付けない。メソッド単位で実測して決める（【記録】同上）。
- 却下済みの最適化候補を、同じ仮説のまま名前を変えて再試行しない。`FLinq-Optimization-Candidate-Backlog.md` の「不採用仮説の保存規約」に従う。

## 3. 前提条件

- Roslyn の incremental generator とインターセプター（preview 機能）。
- 利用側は `Seamless.Linq`（生成コードの呼び先）を参照する。
- 生成コードの確認は `Test/SourceGenerator/GeneratorTestHelpers.cs` の Roslyn 試験基盤で行う。

## 4. 変更範囲 / スコープ

- `Seamless.FLinq.Generator/SourceGenerator/**`
- 呼び先の `Seamless.Linq/*.cs`（`GeneratedCodeHelpers.cs` ほか）
- 形状・意味のテスト：`Test/SourceGenerator/*`、`Test.System.Linq.Enumerable.*`、`Test.{Tsavorite,Persisted}.Linq.Enumerable.*`
- ベンチ：`Benchmark/Benchmark.Optimization.*`（`DISABLE_REWRITE`、`DISABLE_PIPELINE_FUSION`、`DISABLE_PREDICATE_PUSHDOWN`、`DISABLE_PROJECTION_MERGE` の比較）

## 5. 非対象 / Out of Scope

- スコープ付きクエリ（[04](04_LINQランタイムと大規模LINQ.md)）の退避処理。
- ベンチハーネスの計測方式（[08](08_ベンチマーク基盤.md)）。

## 6. 完了条件 / 成功条件

- 全演算子の展開について、意味テスト（`System.Linq` との一致）と形状テスト（期待する展開形）が通る。
- `FLINQ002` が、意図的に未対応としたパターン（デリゲート変数・メソッドグループなど：`FLinq.md` §6）以外で出ない。
- 性能：Benchmark.db（または検証基盤）で、比較対象と同条件の実測で劣位の行が、原因の説明つきで一覧化されている。

## 7. 検証項目 / 受け入れ条件

| 確かめること | 手段 |
|---|---|
| 演算子の網羅 | `Test/SourceGenerator/LinqFragmentCoverageTests.cs`、`OperatorCoverageManifest.cs` |
| 最適化ごとの展開形 | `PredicatePushdownShapeTests.cs`、`ProjectionMergeShapeTests.cs`、`SetOperationPushdownShapeTests.cs`、`UnaryPushdownShapeTests.cs`、`PerformanceShapeTests.cs` |
| 固定点の収束 | `OptimizationFixedPointTests.cs` |
| 失敗時の診断 | `FallbackDiagnostics.cs` |
| 終端が列挙子を必ず破棄する | `Test/System/Linq/Intercept/Optimization/ScalarTerminalDisposeSemantics.cs`（System.Linq と GetEnumerator/Dispose の回数を比べる） |
| 増分生成のキャッシュ | `IncrementalCacheTests.cs` |
| 意味の一致（ソース8種） | `Test.System.Linq.Enumerable.*`、`Test.{Tsavorite,Persisted}.Linq.Enumerable.*`、Benchmark.Dynamic の4経路比較（[09](09_動的クエリ検証.md)） |
| 性能 | `seamless-verify run --suite benchmark`、Benchmark UI の実測 |

## 8. 移行条件

- 新しい最適化は、`LinqOptimizations` の新しいフラグとして追加し、既定で有効にする前に「無効化ベンチ」（`Benchmark.Optimization.No*`）と比べる。【記録】新しい最適化フラグの登録箇所は8か所ある（ベンチ運用メモ「射影融合とCarrierAnalysis」）。
- 大域固定点を既定にする場合は、全演算子の意味テストと固定点テストが通ることを条件にする。

## 9. ロールバック条件

- 意味テストの不一致、または生成コードのコンパイルエラーが出たら、その最適化フラグの既定を外す（コードは残してよい）。
- 性能の `Regressed` が同条件で再現したら、その最適化の変更を戻し、却下理由を `FLinq-Optimization-Candidate-Backlog.md` に残す。

## 10. 既知の問題 / 許容事項

- 【確認】BUG-0007 は 2026-09-25 に修正・検証済み（**未コミット**）。原因は、終端の `Compute(TEnum source)` への値渡しで、内側の列挙子を作ったコピーが破棄されなかったこと。`Compute(ref …)` と try/finally に変更した。新規テスト7件が修正前に失敗・修正後に成功。性能の変化なし（修正後÷修正前の幾何平均 0.999、`Work/qa/artifacts/BUG-0007/perf/`）。
- 【記録】BUG-0010：`Test/SourceGenerator` の7件が旧名前空間 `Seamless.FLinq.*` の型を探して失敗する（596f5387 で `Seamless.FLinq.Generator` へ移った後の更新漏れ）。生成器の内部契約の回帰を検出できない。
- 【記録】BUG-0008：`SequenceEqual_second_comparer` が Tsavorite/Persisted の FLinq フレーバー（IEnumerable 版を除く）などで、`IEnumerableRootTests` が IEnumerable 版で、HEAD から失敗。
- 【確認】`Test/Test.csproj` は生成器を `bin` の固定パスから読むため、`--artifacts-path` の隔離ビルドでは古い生成器でテストされる（OBS-0002）。生成器を変えたら通常の出力先で、`-p:UseSharedCompilation=false` を付けてビルドする。
- 【記録】ベンチの既知失敗：`Sum` の overflow、`TryGetNonEnumeratedCount`（ベンチ運用メモ）。
- 利用者の未コミット変更（2026-09-25 時点）：`SelectMany.cs` で添字つき selector の許容、`Shared/Linq.Enumerable/Base.cs` で `List<T>.ToArray()` の傍受漏れ対策。本文書はこの変更を前提にしない。
- `FLinq.md` §3「非ジェネリックメソッドのみ」と §4「リフレクションで自動列挙」は現状と異なる（ジェネリックは正規化して対象、部品は登録表で管理）。

## 確認済み / 推測 / 不明

- **確認済み**：属性と最適化フラグ、診断4種、ジェネリック対応、登録表の存在、無効化ベンチ4種。
- **推測**：なし（BUG-0007 の原因は実験で確定した）。
- **不明**：インターセプターを公開 API（利用者が意識する契約）として扱うか、内部の最適化機構として扱うか（`FLinq.md` の Review Gate にも同じ未決事項がある）。
