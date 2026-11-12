# テストの責務とDynamic回帰ケース

FLinqの振る舞い検証は `Benchmark.Dynamic.Tests` とDynamic GUIの「回帰ケース」タブから、共通の `RegressionSuite` で実行する。元の期待値、例外、評価回数、順序、解放、割当量のassertionをソース資産として保持し、独自型と補助ソースを含む一式をRoslynでコンパイルする。ZLinqとの比較を含む演算子カタログも同じ一覧にある。

## 実行する場所

| 対象 | 所有プロジェクト／入口 |
|---|---|
| FLinq・System.Linq・ZLinqの振る舞い、評価回数、例外、解放 | `Benchmark.Dynamic.Tests` の `RegressionSuiteTests`。GUIの「回帰ケース」と同じWorkerを使う |
| 生成器の診断、キャッシュ、言語構文、最適化契約、計数器の正負例 | `Benchmark.Dynamic.Tests` の共通契約Theoryと `DynamicFallbackCaptureTests` |
| 実MSBuildでのpartial・ジェネリック・独自型の展開 | `Benchmark.Dynamic.Tests/StaticBuildContracts.cs` の3ケース |
| DBモデル生成、スキーマ、RDBMS生成 | `Tests/Database.Compilation.Tests` |
| DB問い合わせ、ビュー、TPC-Hの問い合わせ | `Tests/Database.Query.Tests` |
| トランザクション、読み取り解放、永続化と復旧 | `Tests/Seamless.Database.Transactions.Tests` |
| SQL Server変換と実接続、DBツール連携 | `Tests/Database.Verification.Tests` |
| Tsavorite固有の容量、件数、保存と並列区間 | `Test.Tsavorite.Collections` |
| Persisted固有のコレクション | `Test.Persisted.Collections` |
| 共通ストア・実行制御 | `Execution.Tests` |
| Headlessの理想型解決 | `Benchmark/Benchmark.Infrastructure.Tests` のWindows target |
| ベンチマークIL監査・理想版比較・ParameterSweep | `Benchmark/Benchmark.Infrastructure.Tests` のnet10.0 target |
| Scalableの計画・実行・Persisted／Tsavoriteコレクションと所有権 | `Benchmark/Benchmark.Scalable.Tests`。共有ソースをBENCH_AS_TESTで含める |
| ベンチマーク共通ロジックと旧画面の契約 | `Benchmark/Benchmark.Infrastructure.Tests`。純粋ロジックはnet10.0、Windows画面の契約はnet10.0-windows |
| 共通fixture、AdventureWorks生成モデル、Roslyn試験ヘルパー | `Tests/Seamless.TestKit` |

## Dynamicの実行

GUIの「回帰ケース」で一覧を読み込み、ケースIDで検索して実行・再実行する。選択したケースの元ソースと、実行結果・エラー・評価回数の出力を確認できる。コンパイル失敗、実行失敗、フォールバック、アセンブリ解放失敗、Skip、中止を合格に含めない。

一般的な演算子比較はSystem.Linq・FLinq最適化なし・FLinq・ZLinqの共通カタログで行う。実装固有のfixtureや計数契約を持つケースは、そのケース内のassertionを実行する。画面には比較対象と引数を表示する。実行結果・展開コード・ソースと生成器／ランタイムの指紋を保存する。「今回の展開コードをプレビュー」は最大16,384文字を表示し、全文ファイルのパスを併記する。全文は結果JSONLと並ぶ `.generated.cs` に保存される。

自動検証の基本入口は次のとおり。

```powershell
dotnet test Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj -p:CoreOnly=true -c Debug
dotnet test Benchmark.Dynamic.Tests/Benchmark.Dynamic.Tests.csproj -c Release --filter 'Category!=ChainCorpusFull&Category!=Survey'
```

WorkerのCLIでも同じケースを指定できる。`<Worker.dll>` は実際にビルドした `Benchmark.Dynamic.Worker.dll` のパスに置き換える。

```powershell
dotnet <Worker.dll> --regressions --list
dotnet <Worker.dll> --regressions --case Test.System.Linq.Intercept.Chain1.Where.predicate_Static
dotnet <Worker.dll> --regressions
```

元の固定30ケースのIDと「理想コード」タブは維持する。回帰ケースは独自のfixtureやassertionを持つため、固定のint[]クエリへ無理に変換しない。

## 守る条件

- 展開時のFLINQ002と失敗マーカーを検査し、実行時も入れ子の計数範囲をまたいでフォールバックを観測する。例外やCounter.Resetで発生事実を消さない。
- 生成コードの形状は、診断用属性、手順コメント、非実行テキストを除いて検査する。診断メタデータ自体の契約は元の生成物を検査する。
- 期待値の受け渡しに使うdynamicは、呼出元の静的型を解決してコンパイルする。比較結果を維持し、DLRによる回帰アセンブリの保持を避ける。
- xunit.assertの型別キャッシュも回帰ソースと同じ解放可能コンテキストに置く。テスト本体の合格と、コレクション後処理の解放成功は別に確認する。
- 速度の短い順序固定測定を機能テストの合否にしない。値と割当量を検証し、時間はTimingObservationに保持する。速度比較は独立した測定経路で行う。
- 容量不足を試すfixtureは固定成長を明示する。見込み件数なしの並列区間で通常の大量書込みを試すfixtureは、ログ容量を開始前に確保する。

## 環境依存の確認

実SQL Server連携には `SEAMLESS_SQLSERVER_TEST_CONNECTION` が必要。未設定の場合は明示的なSkipとなる。2026-11-04の実接続試験では既存SQL Serverに専用DBを作り、データ・ログ・COPY_ONLYバックアップをFへ配置した。接続先と配置の記録は `F:/CodexTemp/test-retirement-20261104/live-acceptance/sql-environment.json`。CLI連携の追加DBと子ビルドも `SEAMLESS_TEST_DATA_ROOT` 配下を使う。

GUIの実画面試験は、`BENCHMARK_DYNAMIC_APP_PATH` に実行するアプリの絶対パスを指定する。`BENCHMARK_DYNAMIC_GUI_MODE=IsolatedDesktop` を設定すると、既存の `Benchmark.Dynamic.UITests` から `tools/testing/Invoke-IsolatedGuiCheck.ps1` を呼ぶ。専用Win32デスクトップ上でUI AutomationのInvoke・Value・SelectionItem・ScrollItemパターンを使い、起動、タブ、検索、選択、実行、再実行、展開コードの可視表示を検査する。ウィンドウ描画はPrintWindowでPNGへ保存する。モード未指定の場合はAppiumの接続先を `BENCHMARK_DYNAMIC_APPIUM_SERVER` で指定できる。内部API、画面描画、実OS入力の確認は別々に記録する。

アプリの履歴DBを配置するフォルダーは `SEAMLESS_BENCHMARK_DATA_ROOT` で指定できる。隔離GUI試験では結果ディレクトリ配下の `profile` を指定し、アプリの実行パス、生成時刻、DLLのSHA-256、デスクトップ名と工程別の画面画像を保存する。

ローカルの比較検証は共通8CPU上限を守る。2026-11-04の利用者の追加指示により、この作業では成果物・実データ・ログ・キャッシュをすべてFへ保存する。移行用スクリプトは `tools/testing/Invoke-TestMigrationCheck.ps1` と `Invoke-TestRetirementVerification.ps1`。成果物とログのルートは `F:/CodexTemp/test-retirement-20261104`、短いテストデータルートは `F:/CodexTemp/stm`。同じ成果物へのアクセスは排他する。

監査対象のベンチマークDLLはInfrastructure.Testsのビルドが出力する `AuditedBenchmarkPaths.txt` から解決する。ScalableのTsavorite試験はログ容量の設定を維持し、実ストアと同じ予約会計でoverflow pool・管理配列・alignmentを含む有限予算を確保する。

移行元の598ファイルの対応・ハッシュと、移行前に検出した2250ケースは `docs/refactoring-designs/20261104-test-migration.json` に保存する。`tools/testing/audit_retired_test_coverage.py` は、移行後のTRXと完全なケースIDを持つJSONLを照合する。移行先が存在することと、実行して合格したことを区別する。

Test廃止時の検証一覧は `docs/refactoring-designs/20261104-test-verification.json`、元の2250ケースの照合は `20261104-test-coverage.json`。Test廃止時のCoreOnly検証はDebug・Release各541件合格、共有ソース1206ケースは全件フォールバック0・解放成功、2中間演算子＋終端の間引きなし検証は4365件合格。実SQL接続6件を含むDatabase.Verification.TestsはDebug・Release各39件合格、Skipは0件。

残存5プロジェクトの統合後は、全体133プロジェクト、うちテスト19プロジェクト。移行元163ケースは両構成で全件合格・欠落0。Infrastructure net10.0は各359件、WindowsはDebug 410件／Release 405件、Scalableは各157件が全件合格した。Windowsの差5件は既存のDEBUG専用試験による。

Dynamicの固定6ケースは型別の広域試験へ統合した。固定境界値と旧コーパスの直接実行も、結果・例外型の比較に加えて実行時fallback 0を確認する。検出器の新しい正負試験は、修正前に正常終了・例外終了の2件が失敗し、修正後に4件合格することを確認した。Windowsの変更対象51件も両構成で合格した。CoreOnlyは全件実行と実行中の追加回帰の差分実行を行い、TRX testIdで両構成562件の合格集合を照合した。振る舞い1206件・生成器契約376件も両構成で全件合格している。

統合後のreceipt・ケース対応・ソースハッシュは `docs/refactoring-designs/20261104-test-consolidation-verification.json` に記録する。構成別のログとTRXはFの作業ルート直下の `con-*` フォルダーにある。

実画面試験は `live-gui-debug4` と `live-gui-release2` で、両構成とも7工程を合格した。各試験の `gui` フォルダーに工程別画像、実行パス・DLLハッシュ・デスクトップを記録した `gui-result.json`、履歴DBと結果ファイルを保存している。検索語の読込後保持、4実装比較ケースの初回実行・再実行、フォールバック0、展開コードの可視表示を確認し、各構成2回分の保存結果と生成コード全文のハッシュも照合した。元の2250ケースの照合は欠落・未検証ともに0件。


Dynamic GUIで新しく検証する場合は「ライブ検証」を使う。「検証対象と詳細設定」で入力元と要素型を絞り込み、ペアから順に実行するなら「全段階を開始」、直接探索するなら「ランダム探索を開始」を押す。探索件数を指定した場合は、対応する各入力元・要素型で指定件数を検証する。ランキングでは速度比とメモリ割り当てを切り替え、行を選ぶと生成コード、判定と既存のQuery再実行・縮小操作を確認できる。
