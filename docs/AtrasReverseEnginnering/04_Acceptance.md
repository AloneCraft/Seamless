# 検証・受け入れ・移行・ロールバック

この文書は今後実行する検証の計画であり、ここに挙げたコマンドや実験は今回実行していない。各 Phase は [Phase 計画](03_Phases.md) に指定した AC-ID を満たして終了する。

## 証跡と判定

各実行で、日時、対象絶対パス、HEAD、dirty 差分 hash、SDK/runtime/OS/CPU、構成/TFM、入力/Seed、コマンド、終了コード、テスト XML、標準出力/エラー、実行 binary/依存 DLL/生成物 hash、対象 DB/Store を保存する。性能には実行方法・校正と生測定・結果 digest、画面確認にはアプリ PID/実 exe、画面サイズ/DPI、操作、前後画像、表示値と保存結果の対応を追加する。

| 結果 | 扱い |
|---|---|
| PASS | 指定条件と対象で完了し、期待挙動・証跡を確認した |
| FAIL | 正しさ・資源・回帰・画面等の受け入れ違反を確認した |
| BLOCKED | 必須環境・接続先・入力がなく、当該検証を実行できない |
| NOT_RUN / SKIPPED | 未実施または条件付きで除外。PASS と集計しない |
| NEEDS_HUMAN | VS F5 等の必須操作や製品判断が残る。対象 gate は未完了 |
| Incompatible / Incomplete / Inconclusive | 比較条件不一致、未完了、測定不足/失敗種別変化などで判定できない状態。遅い/速いという結果に変換しない |

上表は計画管理用の区分であり、コード内の enum を新設・変更する指示ではない。Verification の既存状態/比較判定はそのまま保存する。テスト0件、skipのみ、途中停止、プロセス未終了、ログファイル名に PASS/FAIL が含まれるだけのものは合格証拠にならない。

## 受け入れマトリクス

| ID | 何を確認するか | 既存入口・追加すべき証拠 |
|---|---|---|
| AC-BASELINE | 正本 checkout、dirty、SDK、対象成果物、テストと QA 記録が一致する。開始前からある失敗と新規失敗を分けられる | `git status/diff`、ソースと実 binary hash、QA state/history/handoff、現在の TRX |
| AC-RESOURCE | 同じ管理予算へ予約/解放を計上し、上限直前/超過・単一巨大値・大量 key・巨大 group で予測可能に完了または理由付き失敗する | Execution.Tests、ScopedQuery、Persisted shared tests。Compact の入力増加に対する予約量・peak private bytes・temp 使用を記録 |
| AC-LIFETIME | 空列、全消費、First/Any、途中 break、predicate/consumer 例外、取消し、Dispose 後アクセスで、列挙子・read session・budget・file lock が契約どおりになる | Execution.Tests、DB transaction tests、FLinq 実行経路。特に Sum(Table) 後に writer/Dispose が進み testhost が終了する証拠 |
| AC-CODEC | 形式/型/版の不一致、hash 衝突、匿名型/構造体、明示 codec、registry freeze、一般 class 不対応を検証する | Execution.Tests。+0/-0、NaN 表現、文字列、null の入力を scope の byte semantics と .NET semantics に分ける |
| AC-COLLECTION | 空/重複/上書き/削除/再追加、Count 不明→走査、strict/delta accounting、同時 accessor、保存/再 open が一致する | Test.Persisted.Collections、Test.Tsavorite.Collections、StoreExtensions、StoreCheckpoint、共有 collection tests。int overflow も確認 |
| AC-DB | read/write の排他、昇格拒否、nested rollback、未commit Dispose、キー重複、spill、commit 異常後の拒否、transaction 完了後の資源解放 | `DatabaseTransactionTests`、`DatabaseStoragePersistenceTests`。結果だけでなく待機解除と process 終了を観測 |
| AC-RECOVERY | Save/token Load、未保存、WAL commit 前後、checkpoint 前後、未 open table、壊れた末尾、archive 内容不一致、反復 reopen が正しい | Store tests、DB storage/transaction tests、隔離 worker を終了させる fault-injection。OS/電源断は別実験 |
| AC-LINQ | 結果、順序、安定 sort、重複、null、比較子、例外型/発生時点、評価回数、遅延性、早期終了を保つ | `Test/System/Linq`、各 `Test.*.Linq.Enumerable.*`、通常 .NET 基準と scope 基準を分離 |
| AC-GENERATOR | 実際に到達する生成コードがコンパイル/実行できる。Registry/能力/coverage/診断、入力単位 cache、再生成の決定性を保つ | LinqFragmentCoverageTests、IncrementalCacheTests、RenderContractTests、GenerationServicesTests、FallbackDiagnostics、UnorderedEnumerableAnalyzerTests |
| AC-DYNAMIC | Seed/IR/source/route/診断/result/trace が対応し、全対象 route を正しく評価する。修復対象の除外解除が実経路の成功と一致する | DynamicRouteShapeTests、DynamicEndToEndTests、DynamicWorkerRestartTests。通常 replay は保存入力を現在のビルドで再実行し、修復後の成功を許す。shrink の採用候補と縮小後の再現は同じ failure signature を保持 |
| AC-IMPORT | generate→build→staging import→Dispose→reopen→manifest→公開を検証する。schema/件数/対象値、取消し/失敗、backup 復旧が正しい | DatabaseToolImportIntegrationTests、DatabaseToolIntegrationTests、SqlServerImportSourceTests、SqlServerImportSizerTests。分離レベルを記録 |
| AC-SQL | 選定 subset の解析/生成/実行が対応表と一致し、SQL Server 基準と値/NULL/順序契約/副作用/失敗が一致する | SqlServerRoutineAnalyzerTests、RdbmsSourceGeneratorTests、RdbmsViewSourceGeneratorTests に実行 oracle を追加。全 SQL の完全互換試験とはしない |
| AC-WORKER | clean restore/build、Worker だけの変更、Debug/Release、VS F5 で意図した Worker がコピーされ起動する | project graph と実 exe path、Worker exe/dll/deps.json/runtimeconfig.json/pdb（出力される構成）の SHA-256 |
| AC-COMPARE | 条件一致だけ比較し、digest 相違は CorrectnessFailure、無効/欠損測定は成功にしない。既定の中央値回帰判定を正しく適用する | VerificationComparison と Verification.Tests。各性能ケースに5回以上の有効な生測定、既定5%境界。容量は成功件数と失敗種別 |
| AC-VERIFY | init は明示/既存拒否、baseline は全成功 run へ明示設定、same-source resume、changed-source replay、改変/ルート外成果物拒否、Interrupted 復旧 | Verification.Tests と隔離 workspace の CLI。既存本番履歴で破壊系 fixture を試さない |
| AC-UI | 計画の理由、実行状態、結果、詳細、履歴、比較が CLI/DB と一致する。開始/停止/取消/再開/再実行/再起動の導線と画面が成立する | 下表の Debug/Release 実画面 matrix、Benchmark.UI.UITests、Benchmark.Dynamic.UITests、必要な手動証跡 |
| AC-API | 共通 Store の寿命と Save を維持し、旧/新 API の選定された互換性と未対応の拒否を確認する | StoreExtensions tests、旧/新 consumer fixture、AppendOnlyList の共通 Store 下の新しい契約テスト |
| AC-PROJECT | `.csproj/.shproj` 全登録、共有 GUID と import 名、軽量 solution の再生成結果が一致する | XML 突合、Update-FocusedSolutions.ps1、対象 solution build。生成 solution を手修正しない |
| AC-PACKAGE | analyzer/runtime/tool の TFM、依存、生成物、バージョンが正しく、repository 外の小 consumer で restore/build/実行できる | 選定 project の Release pack、nupkg 検査、FLinq と Database generator の生成・実行。NuGet 公開は別操作 |
| AC-ROLLBACK | コード/バイナリと DB/schema/Codec/manifest を一致した組で戻し、再 open と代表操作を再現できる | 隔離コピーでの restore 演習、戻す前後の件数/値/代表クエリ/履歴比較、新規書込みの保全記録 |

## 正しさと性能を分ける

正しさ失敗を高速化で相殺しない。性能判定の正本は `Verification.Core/VerificationComparison.cs` と保存された `ComparisonConditions` である。過去 backlog の独立3 Run・3%改善・比率1.02や旧 Run 249 のゲートは、それぞれの歴史的な採否条件であり、この計画の新しい共通ゲートにはしない。

候補 task ごとに対象ケース、改善する metric、改善を認定する基準を計測前に記録する。既定5%は回帰の分類境界であり、5%改善をすべての最適化へ要求する値ではない。結果を見て都合よく閾値を変更しない。ノイズや条件不一致で採否を決められなければ採用保留にする。

## UI と画面比較

対象の第一候補は現行 Windows 構成であり、Q-05 で範囲を確認する。以下の全セルは今回未実施。

| アプリ / 入口 | Debug | Release | 必須の利用シナリオ |
|---|---|---|---|
| Benchmark.UI | NOT_RUN | NOT_RUN | ケース/CPU 選択、開始、停止/一時停止/再開、結果、履歴再読込、比較、アプリ終了 |
| Benchmark.Scalable.UI | NOT_RUN | NOT_RUN | 件数/collection 選択、成功/資源失敗の表示、保存、再起動、最新 Worker |
| Benchmark.Tsavorite.ParameterSweep.UI | NOT_RUN | NOT_RUN | 設定範囲入力、無効条件、探索中断、最大成功件数/失敗種別、履歴 |
| Benchmark.Dynamic.UI | NOT_RUN | NOT_RUN | source/route/Seed、生成/実行、取消、query 詳細、trace、履歴再実行、失敗縮小、trash/復元 |
| Verification.UI | NOT_RUN | NOT_RUN | 未初期化表示、明示 init、選定理由、実行、履歴、条件不一致、baseline 比較、replay/resume/shrink の提供導線 |
| UI に依存する Worker/CLI | NOT_RUN | NOT_RUN | 実際の payload・起動先一致、異常終了の状態保存、UI との結果対応 |

画面比較は固定データ・同じ window size/DPI で開始前、実行中、完了、取消、失敗、再起動後を採取する。文字切れ、欠落ボタン、操作不能、古い結果、状態表示と実行実体のずれを確認する。elapsed time などの可変領域は意味を損なわない範囲で別評価し、画像完全一致だけを合否にしない。DB の件数/ID/状態/値と画面の表示を対応づける。

UITest の環境変数や Appium/WinAppDriver が不足して skip された場合は、そのセルを PASS にしない。VS F5 の最新判定は、Worker ソースだけを一時的に変えた後の実際の F5 と binary hash で確認する。手順用変更は既存 dirty を保全し、検証後にその差分だけ戻す。

## 検証コマンドの出発点

以下は既存 project/test 名に対応する候補である。Phase 着手時に TFM と選択テスト数を再確認し、既存プロセスとの出力先競合を解消して直列実行する。`--no-build` は現在ソースと成果物の一致確認後だけ使う。restore が未実施なら `--no-restore` を付けない。実験データは task ごとに隔離する。

```powershell
dotnet --info
git status --short
git rev-parse HEAD

dotnet test .\Execution.Tests\Execution.Tests.csproj -c Release --logger "trx;LogFileName=execution.trx"
dotnet test .\Test.Persisted.Collections\Test.Persisted.Collections.csproj -c Release --logger "trx;LogFileName=persisted.trx"
dotnet test .\Test.Tsavorite.Collections\Test.Tsavorite.Collections.csproj -c Release --logger "trx;LogFileName=tsavorite.trx"

dotnet test .\Test\Test.csproj -c Release -f net10.0 --filter "FullyQualifiedName~DatabaseTransactionTests|FullyQualifiedName~DatabaseStoragePersistenceTests|FullyQualifiedName~SqlServerImportSizerTests|FullyQualifiedName~DatabaseToolImportIntegrationTests" --logger "trx;LogFileName=database.trx"
dotnet test .\Test\Test.csproj -c Release -f net10.0 --filter "FullyQualifiedName~LinqFragmentCoverageTests|FullyQualifiedName~IncrementalCacheTests|FullyQualifiedName~RenderContractTests|FullyQualifiedName~UnorderedEnumerableAnalyzerTests" --logger "trx;LogFileName=generator.trx"

dotnet test .\Verification.Tests\Verification.Tests.csproj -c Release --logger "trx;LogFileName=verification.trx"
dotnet test .\Benchmark.Dynamic.Tests\Benchmark.Dynamic.Tests.csproj -c Release -f net10.0-windows --filter "FullyQualifiedName~DynamicRouteShapeTests|FullyQualifiedName~DynamicEndToEndTests|FullyQualifiedName~DynamicWorkerRestartTests" --logger "trx;LogFileName=dynamic.trx"
dotnet test .\Test.Tsavorite.ParameterSweep\Test.Tsavorite.ParameterSweep.csproj -c Release --logger "trx;LogFileName=sweep.trx"
```

生成形状の試験では generator 本体と consumer の再構築後に DLL hash と生成ファイルを確認する。上の選択実行は全回帰の代用ではない。変更影響を広げた場合は該当 LINQ/collection/DB 全 suite も実行する。

```powershell
# 既存 Verification DB は init し直さない。事前に backup と実行対象を確認する。
dotnet run --project .\Verification.Cli\Verification.Cli.csproj -c Release -- plan --root G:\GitHub\Seamless --suite changed --profile quick --output Work\Verification\plan.json

# Phase 5 の対象とデータ保全を確認してから実行する。
dotnet run --project .\Verification.Cli\Verification.Cli.csproj -c Release -- run --root G:\GitHub\Seamless --suite all --profile full
```

`run` は履歴と成果物へ書き込む操作である。今回の文書作成では実行しない。共有 SQL Server 接続や OS 強制終了が必要な試験は専用 fixture と明示した対象で実施する。

## 移行条件と切替手順

| 移行対象 | 旧→新の段階 | 切替可能条件 |
|---|---|---|
| API | 現行利用箇所の列挙→共通 Store adapter→一利用側→一 library 群→互換層整理 | 各段で所有/Count/Save/reopen の契約が合格。旧 API 削除には Q-02 の決定と consumer 検証 |
| generator 内部 | 現行 Fragment→描画契約/adapter→責務ごとの移設→adapter 解消 | 生成コンパイル、意味、診断/trace/cache、現在の performance に未説明回帰なし |
| 検証 UI/ゲート | 旧 UI/履歴の保持→Verification 正本の比較→対象導線の移行 | 生測定/条件/result が同じ規則で表示・保存される。旧 UI 廃止は Q-04 決定後 |
| SQL import/保存先 | 旧宛先保持→別 staging 生成/取込→再 open 検証→旧宛先 backup→新宛先公開 | schema fingerprint、対象件数/値、代表クエリ、分離レベル、再起動検証が成立 |
| ローカル SQLite/schema | writer/アプリ停止→整合 backup→対象 DB へ明示変更→整合検査→再起動 | backup と変更手順が記録済み。既存データ/履歴/成果物参照が一致。起動時 migration は使わない |

SQLite の WAL モードを含め、アクティブ DB ファイルだけを無条件にコピーして整合 backup とみなさない。接続停止後の整合したコピー、または適切な backup 手段で復旧可能性を確認する。DB 本体だけでなく参照される成果物も保全する。旧データの自動変換は現在の仕様に含まれず、必要なら別の明示移行作業として設計する。

## ロールバック条件と戻し先

| 問題 | 即時対応 | 戻す状態・再受け入れ |
|---|---|---|
| 結果/Count/SQL 意味不一致、commit 境界違反、データ損失 | 対象 writer を停止し、失敗入力・新データ・ログを保全 | Phase 直前の受け入れ済み code/binary + 対応 schema/Codec/DB backup。AC-RECOVERY/AC-ROLLBACK |
| 資源上限超過、解放漏れ、終了停止 | 当該 task の所有プロセスを特定し、タイムアウトと trace を保存 | 直前の資源/列挙実装と隔離データ。AC-RESOURCE/AC-LIFETIME |
| stale Worker / DLL 不一致 | 当該数値を採用しない。起動実体と hash を保存 | 同じ構成の UI/Worker/依存一式と build graph。AC-WORKER |
| 比較条件/履歴/画面の不整合 | baseline 更新を止め、候補 run を保全 | 直前の比較実装と整合した履歴 backup。AC-COMPARE/AC-UI |
| 純粋な性能最適化が未達/回帰 | 候補の前後生成物・生測定・原因候補を保存 | 自分の候補差分のみ撤回し、基準形状と対象意味テストを再確認 |
| NuGet consumer 失敗 | release 候補の採用・公開を止める | 直前の受け入れ済み package 一式。AC-PACKAGE |

戻し先は「最新 backup」等の曖昧な指定ではなく、task ID、commit/dirty patch、binary hash、保存形式/Codec 版、DB/成果物 backup の絶対パスで識別する。`git reset --hard` や `git clean` を一般手順にしない。

切替後にユーザーの新しい書込みがあれば、旧 backup への復元だけではその書込みが失われる。新状態を退避し、可逆変換・データ移送・損失のない復旧手順を判断する。自動的な巻戻しで捨てない。最後に代表データ・件数・再 open・代表クエリ・UI履歴を照合し、復旧後の受け入れ結果を記録する。
