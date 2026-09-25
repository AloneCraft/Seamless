# 機能一覧と現状

状態と調査範囲は [README](README.md) に従う。ここでの「実装あり」は静的確認であり、現在の checkout での正常動作を認定するものではない。

## システムが提供する価値

Seamless は、メモリに収まらないデータを扱うコレクションと LINQ、コンパイル時 LINQ 最適化、宣言的 Database、これらの正しさ・性能・容量を同じ条件で検証するツール群である。利用者がストレージの低レベル設定を毎回組み立てなくても、保存場所・寿命・資源予算を明示し、失敗理由を識別して利用できることを目指す。

## 機能カタログ

| ID | 行うべき機能 | 現在の状態・根拠 | 次に必要な作業 / Phase |
|---|---|---|---|
| F-01 | 共通 Store から一時・永続・メモリ用のデータを作成し、子資源と保存時点を管理する | `Store.CreateTemporary/Open/CreateMemory` とエンジン別拡張あり。永続保存は明示的 `Save`。S-02、S-04 | 異常終了・複数子資源・Save 境界の再確認。Phase 2A |
| F-02 | 全演算子でメモリ・一時領域の予算、キャンセル、Codec、所有権を共有する | ExecutionScope、ResourceBudget、決定的 Codec、所有確認付き残骸回収あり。S-03 | 残骸・例外・キャンセル・破棄順の回帰。プロセス全体のメモリ上限とは区別。Phase 2A |
| F-03 | ファイル backed Set / Dictionary / List / AppendOnlyList を使う | Persisted 実装あり。共通 Store 拡張は Set/List/Dictionary。AppendOnlyList は旧パス API。S-04 | AppendOnlyList の所有・保存契約の統合は追加実装候補。Dictionary.Compact の全件メモリ化は先に対処。Phase 2A、4C |
| F-04 | Tsavorite の集合・辞書をスレッドごとの操作オブジェクトから使う | 共通 Store 作成、Create / CreateUncounted、Count 差分、索引拡張あり。S-05 | 重複追加・上書き・削除・並列書込み・再集計の検証。旧 factory API 移行。Phase 2A、4C |
| F-05 | 通常の LINQ 演算を value enumerable として提供する | Seamless.Linq に Where / Select / SelectMany、集約、結合、集合、順序、実体化等あり。S-06 | 対応 overload・型・順序・例外をテストで固定する。演算子名の存在だけで全 overload 対応としない。Phase 2B |
| F-06 | 大規模 LINQ を予算内で逐次処理し、必要時にディスクへ退避する | ScopedQuery、外部ソート、GroupBy、Join、集合演算、ToStorage あり。S-03、S-06 | 偏った巨大グループ、空列、巨大単一要素、重複キー、キャンセル、メモリ限界を確認。Phase 2A/2B |
| F-07 | System.Linq 呼出しを意味互換性を保ってコンパイル時に展開する | FLinq incremental generator、Registry、Fragment、ProviderNative、最適化設定と trace あり。S-07 | Dynamic で除外されている FLinq 経路の再現と修復。意味を固定してから性能改善。Phase 2B、4B |
| F-08 | 未対応展開・Registry 不整合・順序なし列の誤用を診断する | FLINQ001/002/005/006 と SEAMLINQ001/002 あり。Registry には明示 Excluded あり。S-07、S-08 | 診断の位置・重要度・opt-out、意図した除外を維持する。除外解除は独立した受け入れ対象。Phase 2B |
| F-09 | 宣言的な Table / View / TableFunction とコンテナメタデータを生成する | Database ランタイムと DatabaseContainerGenerator、materialized view 等のテスト定義あり。SQL 由来 view は import 時の行を実体化する。S-09 | 元 Table 更新への自動追随・差分更新は今回確認した経路にない。手書きの実行 delegate と SQL から生成する未実装 routine も区別。Phase 2A、4A |
| F-10 | 読取セッション、更新トランザクション、WAL、checkpoint、再起動復旧を提供する | DB の共有予算・逐次読取・writer 直列化・commit 記録に基づく復旧あり。S-09 | セッション解放、終了停止、checkpoint 競合を現行で再現。電源断まで保証を拡張する場合は専用実験。Phase 2A |
| F-11 | SQL Server のカタログ・型・routine を解析し、コードと取込構造を生成する | Rdbms / SqlServer にメタデータ、関係代数モデル、C# 生成・型変換あり。S-10 | 解析できることと実行できることを別の対応表にする。全 SQL Server 互換は未保証。Phase 4A |
| F-12 | SQL Server データを逐次取り込み、検証して公開する | DB Tool が staging、再 open、件数比較、manifest、既存宛先 backup を実装。S-10 | 型・NULL・キー・代表クエリ・再起動も受け入れに含める。件数一致のみを意味互換としない。Phase 2A、4A |
| F-13 | SQL から生成した関数・ストアドプロシージャを実行する | CSharpDatabaseGenerator が TableFunction / ScalarFunction / StoredProcedure に `requires relational-algebra execution` の例外 delegate を生成する経路あり。S-10 | **未実装確認**。対象 routine subset、NULL/型/例外/更新契約を決め、最小の実行経路から追加。Phase 4A |
| F-14 | SQL 由来の制約・trigger を必要範囲で保持・執行する | 制約メタデータ取得と fingerprint への組込みはある。生成 SQL 制約・trigger の一般的な実行系は今回確認した経路にない。S-10 | **対応範囲未確定**。DB ランタイムの既存のキー機能と混同しない。必要な制約 subset を選択してから実装。Phase 4A |
| F-15 | 通常ベンチマークを UI / Headless から Worker 分離して測定・保存・比較する | Benchmark.UI、Scalable、Headless、Worker、SQLite 保存と比較 UI あり。S-11 | 同じ成果物・入力・構成を実行した証拠、再起動後の読込、終了・中断の受け入れ。Phase 3 |
| F-16 | コレクションの容量・設定の成功境界を探索する | Scalable と Tsavorite.ParameterSweep に計画・Runner・UI/Headless とテストあり。S-12 | 境界に達した操作の失敗種別と最大成功件数を分けて保存する。境界を正常に記録した探索 run の Passed と、操作自体の成功は区別する。Phase 3 |
| F-17 | Seed 固定の動的 LINQ を複数実装で比較し、失敗を再現・縮小する | Dynamic Core/Infrastructure/Worker/UI と CapabilityProfile あり。S-13 | 除外理由を保存し、実装不具合由来の除外は再現・回帰追加後に解除。比較基準の問題は実装・生成 Source・fixture を切り分け、FLinq 製品バグと混同しない。Phase 2B、3 |
| F-18 | 検証を計画→実行→保存→固定基準比較→replay/resume/shrink で管理する | Verification.Core/Infrastructure/Cli/UI と共有 Workspace、SQLite、成果物 hash あり。S-14 | 現在ソースで full と限定実行を区別。異条件 Incompatible、未完了 NotRun/Interrupted、baseline 明示切替を確認。Phase 1、3 |
| F-19 | 共有ソース・ソリューション・CI・パッケージを整合させる | shproj/projitems、Seamless.slnx、軽量 solution 生成 script、CI あり。S-01、S-15 | 全登録・GUID・生成 solution・NuGet の実消費テストを行う。配布文書の古い package 名等を修正する。Phase 1、5 |

## 混同してはいけない境界

- `Store` のメモリ/ディスク上限と `ExecutionScope` の演算予算は用途と設定 API が異なる。どちらも CLR プロセス全体の常時上限を保証するものとはしない。
- `System.Linq` の最適化では .NET の既定等値性を維持する。一方、scope 付きクエリの既定キー等値性は決定的 Codec のバイト一致である。両者を同じ正解データで比較しない。
- 順序なし collection と順序付き列を区別する。位置依存の演算を明示 opt-in なしで許す変更を性能改善として行わない。
- `OperatorCoverageKind.Excluded` は Registry の分類であり、同名 API のすべての経路が未実装という意味ではない。`Order`、`OrderDescending`、`TryGetNonEnumeratedCount` の扱いは経路単位で確認する。
- Benchmark の既存 UI/履歴との互換性と、Verification の新しい比較契約は別物である。旧固定 run ID や旧比率目標を新しい受け入れ基準に流用しない。
- SQL のカタログ取得、型生成、データ import、routine 実行、制約執行はそれぞれ独立した能力である。
- SQL import は通常 Snapshot を使うが、memory-optimized table が存在すると ReadCommitted を選ぶ。全関係の同一時点 snapshot を無条件に保証しない。SqlServerImportSource.cs と対応テストを基準とする。

## 証拠索引

行番号は 2026-09-25 の調査時点の目安である。変更後は ID を維持し、参照を更新する。

| ID | 現行ソース・文書 | 確認用途 |
|---|---|---|
| S-01 | [AGENTS.md](../AGENTS.md)、[AI_SYNTAX_RULES.md](../AI_SYNTAX_RULES.md)、[global.json](../global.json)、[Seamless.slnx](../Seamless.slnx) | DB 変更、共有 project、言語、環境、solution 境界 |
| S-02 | [README.md](../README.md)、[ModuleBreakdown](../Document/Code/Basic/ModuleBreakdown.md)、[Store.cs](../Seamless.Execution/Store.cs) | 機能概要・共通 Store |
| S-03 | [Execution の現在仕様](../docs/execution-and-database.md)、[ExecutionScope.cs](../Seamless.Execution/ExecutionScope.cs)、[ResourceBudget.cs](../Seamless.Execution/ResourceBudget.cs) | 予算・寿命・Codec・現在の DB 契約 |
| S-04 | [Persisted StoreExtensions](../Seamless.Collections.Persisted/StoreExtensions.cs) 11–21、[Dictionary](../Seamless.Collections.Persisted/Dictionary.cs) 185–194、[AppendOnlyList](../Seamless.Collections.Persisted/AppendOnlyList.cs) 3–17、[旧 Store](../Seamless.Collections.Persisted/Store.cs) | 共通 Store 接続と未統合 API、Compact の全件蓄積 |
| S-05 | [Tsavorite StoreExtensions](../Seamless.Collections.Tsavorite/Stores/StoreExtensions.cs)、[Tsavorite Store](../Seamless.Collections.Tsavorite/Stores/Store.cs)、[Tsavorite tests](../Test.Tsavorite.Collections/) | Count・操作オブジェクト・旧 factory |
| S-06 | [ScopedQuery](../Seamless.Linq/ScopedQuery.cs)、[Seamless.Linq](../Seamless.Linq/)、[Execution.Tests](../Execution.Tests/) | 通常 LINQ と scope 付き大規模 LINQ |
| S-07 | [Registry](../Seamless.FLinq.Generator/SourceGenerator/Linq/Registry/OperatorRegistry.Registrations.cs) 78–88、[診断](../Seamless.FLinq.Generator/SourceGenerator/LinqGeneratorModels.cs) 45–78、[Coverage tests](../Test/SourceGenerator/LinqFragmentCoverageTests.cs)、[現行 FLinq 設計](../docs/FLinq.md) | Registry、例外・診断境界、残る責務分離 |
| S-08 | [UnorderedEnumerableAnalyzer](../Seamless.Linq.Analyzers/UnorderedEnumerableAnalyzer.cs) 15–41、[Analyzer tests](../Test/System/Linq/Analyzer/UnorderedEnumerableAnalyzerTests.cs) | 順序なし列の禁止事項 |
| S-09 | [Database](../Seamless.Database/)、[DatabaseContainerGenerator](../Seamless.Database.Generator/DatabaseContainerGenerator.cs)、[DB tests](../Test/Seamless/Database/) | 宣言 API、transaction、WAL、復旧 |
| S-10 | [CSharpDatabaseGenerator](../Seamless.Database.Rdbms/CSharpDatabaseGenerator.cs) 190–219、302–328、[SqlServer](../Seamless.Database.SqlServer/)、[DB Tool](../Seamless.Database.Tool/) | SQL routine stub、制約情報、取り込み・公開 |
| S-11 | [Benchmark.UI project](../Benchmark/Benchmark.UI/Benchmark.UI.csproj)、[Worker](../Benchmark/Worker/)、[UI 仕様](../Document/UI/BenchmarkMeasurementSpecification.md)、[UITest 手順](../Benchmark.UI.UITests/README.md) | UI と Worker、測定・画面受け入れ |
| S-12 | [ParameterSweep](../Benchmark/Benchmark.Tsavorite.ParameterSweep/)、[ParameterSweep tests](../Test.Tsavorite.ParameterSweep/)、[Scalable Headless](../Benchmark/Benchmark.Scalable.Headless/) | 容量探索 |
| S-13 | [Dynamic Core](../Benchmark/Benchmark.Dynamic.Core/)、[Dynamic UI project](../Benchmark/Benchmark.Dynamic.UI/Benchmark.Dynamic.UI.csproj) 31–56、[Dynamic tests](../Benchmark.Dynamic.Tests/) | 能力除外・動的検証・Worker 配布構造 |
| S-14 | [Verification 仕様](../docs/verification.md)、[VerificationComparison](../Verification.Core/VerificationComparison.cs)、[Verification.Infrastructure](../Verification.Infrastructure/)、[Verification.Tests](../Verification.Tests/) | 新しい比較基準と再現契約 |
| S-15 | [verification CI](../.github/workflows/verification.yml)、[dotnet CI](../.github/workflows/dotnet-ci.yml)、[軽量 solution 生成](../Update-FocusedSolutions.ps1)、[FLinq project](../Seamless.FLinq.Generator/Seamless.FLinq.Generator.csproj) | 検証経路、実際の TFM/PackageId |
| S-16 | [QA state](../Work/qa/state.json)、[QA handoff](../Work/qa/handoff.md)、[QA workflow](../Work/QA_WORKFLOW.md) | 歴史的な失敗・人間確認・未完了項目。現在の成功の証拠にはしない |
| S-17 | [候補 backlog](../FLinq-Optimization-Candidate-Backlog.md)、[改善 backlog](../FLinq-Benchmark-Improvement-Backlog.md)、[RenderCoordinator](../Seamless.FLinq.Generator/SourceGenerator/GeneratedSourceRenderCoordinator.cs) 4–18、[MethodBodyRewriter](../Seamless.FLinq.Generator/SourceGenerator/FLinqIncrementalGenerator.MethodBodyRewriter.cs) | 不採用仮説と現在の残存責務を区別 |

旧統合仕様 `FLinq.md` 等には旧プロジェクト名や旧 TFM の記述が残る。現行プロジェクト・コード・テストを優先し、この計画へ旧名を正本として転記しない。
