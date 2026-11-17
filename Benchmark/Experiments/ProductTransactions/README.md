# 製品MVCCの代表測定

実行プロジェクトは `Benchmark/Experiments/Experiments.csproj`。`-p:ExperimentMode=ProductTransactions` でこの用途を選び、従来の引数を `--` の後へ渡す。データと報告書はFへ配置する。

中間ValueTupleとGroupJoinの比較は`TestSeamless/Invoke-FlinqCarrierComparison.ps1`を使う。旧版と改善版のF上の保存ソースを、同じ`Program.cs`、`Program.OrderedComparison.cs`、`OrderedComparisonQueries.cs`でReleaseビルドし、両方のmanifestを渡す。DBのMemory／Storageとworker上限1／4、配列・List・Tsavorite・Persistedを4,096／16,384行で比較する。Spill用のセルは16,384行で親予算を18MiBにする。

```powershell
& .\TestSeamless\Invoke-FlinqCarrierComparison.ps1 -BeforeSource $before -BeforeManifest $beforeManifest -AfterSource $after -AfterManifest $afterManifest -LogRoot $logRoot
```

`-Smoke`はDBと配列の各一条件。`-Cells`には再実行するセル番号を指定できる。全実行では旧版・改善版・通常LINQの実行順を巡回し、各queryの10反復と保持量を分けて記録する。値や寿命が一致しない旧版の形、Spill条件の資源上限到達は、個別のreport.jsonのUnavailableへ記録する。summary.csvには検証できた標本だけを載せる。

`-IsolatedQuery CarrierJoinOnly -Cells 0,2,3`は4,096行のDB条件で匿名複合キーJoinだけを実行する。`-IsolatedQuery JoinMultipleOnly`はJoin後2段整列だけを実行する。いずれも同じfixtureで旧版・改善版をビルドし、`-Cells`でDB条件を指定する。

`--carrier-comparison <Fのデータルート> <Fのreport.json> <source SHA-256> 16384 Memory 4 FLinq 5 GroupJoinSpill` は、親予算18 MiB、ページ16 KiBでGroupJoinの件数取得と一致列の所有化を測る。FLinqは各形状で実際の一時領域予約を要求し、所有した一致列の内容と同じ内側groupの共有を取引確定後に照合する。通常LINQの対照は同じ条件で `Linq` を指定する。

`--client-results <Fのデータルート> <Fのreport.json> <source SHA-256> 16384 RawBuffered` は、同じ型付きNativeフレームをメモリ上の入力から読む。4,096／16,384行で `Stream`、`Memory`、`Persisted`、`Raw`、`RawBuffered`、`RawValidated` を指定できる。受信と保存の完了まで、再列挙の初行と完了まで、managed割当、作業予算、一時ファイル容量を別々に記録する。バッファ付きの2経路は64 KiBを予算に計上する。 `RawValidated` は保存前にNativeの型付き検査を行い、切り詰めた行の拒否も確認する。保持経路は受信元を閉じてから行順と値を2回照合し、全ファイルと予約が解放されることを確かめる。

Joinの処理区間を観察するときは`TestSeamless/Invoke-FlinqJoinSampling.ps1 -Source $after -Manifest $afterManifest -LogRoot $traceRoot`を使う。別プロセスの.NETスタック標本を採る。待機を含むスレッド時間の推定であり、通常の時間比較とは別に扱う。`-AnalyzeOnly`は保存済みの変換済みトレースから処理区間を集計する。

Join・整列の追加経路の比較は`TestSeamless/Invoke-MvccOrderedComparison.ps1`を使う。既定はRelease、4,096/16,384行、Memory/Storage、query worker上限1/4、FLinq/通常LINQ、各形状2回warmup後10反復。Join結果は入力と同じ件数に抑える。恒等キーのJoin整列、途中投影を含む複数整列Join、GroupBy集計の恒等キー整列、groupを返す複数整列を比較する。通常LINQは同じ版付きRowsをIEnumerableとして読む。取引はOptimisticで、クエリ同士は同時実行しない。worker上限は一クエリ内部の設定である。

```powershell
& .\TestSeamless\Invoke-MvccOrderedComparison.ps1 `
    -TaskRoot 'F:\Seamless\ordered-comparison' `
    -LogRoot 'F:\CodexTemp\Seamless\ordered-comparison'
```

完了後は`TestSeamless/Test-MvccOrderedComparison.ps1 -LogRoot 'F:\CodexTemp\Seamless\ordered-comparison'`で全16条件・640標本、結果/入力/生成コードのhash、強制ビルド、CPU/予算の返却を監査し、JSONとCSVの中央値表を出力する。

実装前後の時間を比較する場合は、異なるFの個別TaskRoot/LogRootでそれぞれ上の16条件を保存した後、`Invoke-MvccOrderedRevisionComparison.ps1 -BaselineLogRoot ... -CandidateLogRoot ... -LogRoot ...`を使う。保存済みのDLLを再ビルドせず、同じ行数/保存方式/workerの中で旧FLinq・新FLinq・旧LINQ・新LINQを巡回する。全32プロセス・1,280標本・128メモリ観測を別のFのLogRootへ残し、`Test-MvccOrderedRevisionComparison.ps1 -LogRoot ...`で監査する。旧版は保存snapshotと照合し、新版は現在の入力とも照合する。全runtime DLLと生成物のhashも確認する。異なる測定日時の中央値だけを比べるより同居負荷の変動を抑える方法だが、OSの実時間スケジューリングの揺れを除去するものではない。

各条件/flavorを別プロセスで逐次実行し、flavorの実行順と反復内のquery順を巡回する。TieredCompilation=0、ログに収まるwarm状態。初期投入・取引開始/enlist/Commit・結果照合を時間とmanaged割当から除く。各出力の全要素とgroup内順序を通常LINQの期待値へ照合し、取引終了後のgroup寿命、CPU枠と親予算の返却も確認する。予備検証は`-Configuration Debug -Repeats 3 -Smoke`で4,096行/Memory/worker1の2方式だけを実行する。

予算ピークは実メモリ量とは異なる。測定専用コードが、処理の動いていないcaller所有の親ResourceBudgetの非公開peakカウンターだけを現在値へ戻す。予約の現在値・上限・製品APIは変更しない。通常LINQのLookup/整列/結果は共有予算へ登録されないため、予約量が小さいことを実使用メモリが小さい根拠にしない。managed割当は全workerを含むプロセス全体の増分。GC後の結果保持量とOS private bytes/working setは時間標本と別の観測で、結果をKeepAliveする。OS値はDB/runtime/cacheも含み、各queryの厳密なピークではない。累積プロセスピークも個別queryへ帰属させない。DB親予算64MiB・ログ上限4MiB・索引64KiBで、行データを64MiB確保する設定ではない。

同条件の製品比較には`Invoke-MvccComparison.ps1`を使う。通常nativeは`Set<int,Row>`、取引側は同じint/Rowを持つ`TransactionRows`とし、Auto符号化、主索引2MiB、ページ64KiB、ログ上限32MiBを揃える。DB側の`IndexSize`はbucket数なので32,768を渡す。`SharedStoreOptions`で既存StoreのFixed growthを指定し、ログ上限の自動拡大を止める。既定のDB設定は変更しない。保持履歴24MiB、metadata等も含む親予算128MiBで、行データを128MiB確保する指定ではない。

runnerはオフラインの復元を使う。Garnet 2.2.5と推移依存を含むパッケージは`LogRoot/packages`の隔離キャッシュへ事前に用意する。新しいTaskRootで既存の自分のLogRootを再利用すれば、揃っているキャッシュを利用できる。ビルド結果や各実行のmanifestは固有のartifactディレクトリに保存し、比較用の固定ビルドは`comparison-build.json`で照合する。

```powershell
& .\TestSeamless\Invoke-MvccComparison.ps1 `
    -TaskRoot 'F:\Seamless\comparison' `
    -LogRoot 'F:\CodexTemp\Seamless\comparison' `
    -Rows 10000 -StorageMode Memory -OperationsPerWorker 64 -Stage all
# 同じTaskRoot/LogRootで、残る保存方式と行数には-UseExistingBuildを指定する。
```

1万/10万行×Memory/Storageを各5反復する。1万行では古い読取専用snapshotを保持した8版条件も測る。Where付きCount、Sum、64群のGroupBy、64キーを外側に持つJoinを、通常native/取引それぞれのFLinqとBCL LINQで比較する。1版条件には取引開始・commit・disposeを含むEndToEndもある。古いsnapshotは開始済みのKernelだけを測る。生成コードでは比較メソッド自身の展開とnativeの`.Iterate`を検査する。

独立取引の同時数1/2/4/8、読取100/95/50%、一様/1%集中をExclusive/Optimisticで交互に実行し、自動再試行せず成功した更新だけを独立モデルと照合する。クエリ内worker上限1/2/4/8とは別の軸である。worker指定は取引クエリの上限で、実際のworker数やnative側の並列方式と同じとは限らない。取引本体の重なりはBegin後からCommit前までの読取・変更準備区間で数える。RNG・標本・モデル更新などのハーネス費用も混在取引の時間に含まれる。

4件の実行結果を`Rows`、`Storage`、`Result`の配列として`matrix-results.json`へ保存し、同じLogRootの`comparison-build.json`とともに監査する。

```powershell
& .\TestSeamless\Test-MvccComparison.ps1 `
    -MatrixFile 'F:\CodexTemp\Seamless\comparison\matrix-results.json' `
    -OutputFile 'F:\CodexTemp\Seamless\comparison\comparison-audit.json'
```

監査はterminal receipt、ソース/生成コード/DLLのhash、全条件・反復・巡回順序、独立した期待値、成功/競合件数、資源・CPU返却を確認し、中央値を出力する。測定後に別作業で実行入力が変わった場合、通常は拒否する。`-FrozenInputs`を明示すれば測定したsnapshotを監査し、現在入力との差をhash付きで列挙する。この結果を現在の作業ツリーの性能とは呼ばない。新しい実行入力の性能を確認するには新規snapshotをビルドする。

この比較はログに収まるデータのウォーム測定である。5回のquery標本を安定したtail分布とは呼ばない。プロセスのmanaged割当、Storeの予約量、累積ピークworking setは別の指標とし、終了後のファイル長も測定中のピーク・物理ディスク割当と区別する。native内部の既存私有lookupに同じ共有予算が適用されない箇所もあり、全資源の上限が同一とは主張しない。

保存方式は`-StorageMode Memory|Storage`で選択する（既定Memory）。`-OperationsPerWorker`は8～4096、既定128。結果v2には保存方式と取引数を記録する。Storageは新規の私有領域に業務WALを作り、終了後の再オープンでも行数と総和を照合し、その所要時間を別レコードへ記録する。小規模確認は`-Rows 256 -StorageMode Storage -OperationsPerWorker 8`を使う。通常終了からの再オープンであり、プロセス強制終了の試験とは区別する。

`TestSeamless/Invoke-MvccCrash.ps1 -TaskRoot ... -LogRoot ...`は別の64行試験で、commitが返った直後に`Environment.Exit(73)`でfinally/Dispose/checkpointを通さず終了する。次の独立プロセスがWALから全キー・値を復元することを照合する。writerの73は期待した終了コードとして記録し、readerの正常終了と値の照合を全体成功の条件にする。確定途中の各障害点、電源断、Gatewayを網羅した試験ではない。

製品の生成DB、共通TransactionCoordinator、版付きTsavorite、FLinqをそのまま使う測定入口。採用試験用の`TransactionModes`とは別の結果形式を使う。Memory、Exclusive/Optimistic、query worker上限1/4、100%の全走査、点操作の同時取引1/4、読取95%/50%、一様/1%集中を扱う。各点操作セルはworker当たり128取引、3反復で、競合の業務再試行を行わない。既定1万行、代表条件10万行までに制限する。

通常LINQ/native FLinqの全走査、並列Count、分割GroupBy/Joinの値を照合する。初期データは4,096行ずつ投入し、投入後の共通checkpointから測定する。10万行分の依存を一つの取引に抱えるためにmetadata予算を増やさない。成功処理量、成功時p50/p95/p99、競合、割当、共通資源ピークとCPU利用数をJSONへ残す。成功した更新総数と保存値の総和も各セルで照合し、照合の時間・割当は点操作の測定から除く。各点操作セル後の共通checkpointで回収し、短いセルの結果を長期保持時の成績とは呼ばない。JIT・ページキャッシュ・同居処理の影響を含む測定であり、Gateway、Persistent、障害復旧、長期運転の全体受入を代替しない。

```powershell
& .\TestSeamless\Invoke-MvccProbe.ps1 `
    -TaskRoot 'F:\Seamless\MvccImplementation\my-run' `
    -LogRoot 'F:\CodexTemp\Seamless\MvccImplementation\my-run' `
    -Rows 100000
```

runnerはFの隔離snapshotをReleaseビルドし、生成コードのnative走査・Count分割・GroupBy/Join接続を検査してから、共通8論理CPU以内で実行する。Fの個別領域に実データと結果、source/生成コードのhash、PID・生成時刻・affinityとログを残す。巨大な全件複製やOSキャッシュの消去は行わない。Debug/Releaseの正確性回帰は`Invoke-MvccValidation.ps1`で別途実行する。

`TestSeamless/Invoke-MvccEndurance.ps1 -TaskRoot ... -LogRoot ... -Cycles 32`は、Memory/Storageそれぞれ256行で反復する独立した検証入口。各周期で古い読取りを保持したまま4つの更新取引を準備し、別行の更新をworker当たり8回確定する。更新後にも古い全行・FLinq Count/Sum/GroupBy/Joinを照合し、新しい取引は独立した256要素の期待値モデルと照合する。業務処理を自動再実行しない。各周期で共通の履歴回収と保存を通し、Storageは8周期ごとと最後に再オープンする。

周期数は1～512、既定32。`-Configuration Debug`も指定できる。Dに専用データ、Fにソース/生成コードhash・実行receipt・各周期の資源/CPU・成功更新数・所要時間を記録する。現在値と旧値、反復回収、再オープンの整合性を確認する有限回の試験であり、無期限の連続運転やGatewayの長期運転、性能受入の完了とは扱わない。

行数は256のまま、共有メモリ予算は32MiBとする。Tsavoriteの既存の固定配列と、再オープン時に新旧allocatorが重なる費用を計上するためであり、32MiBの行データを確保する指定ではない。履歴保持とmetadataの個別上限は維持する。予約量と実メモリの違い、nativeの固定費は実装設計書の資源会計を参照する。

`-Workload Churn`を指定すると、同じキーについて更新・削除・再挿入を3周期で繰り返す。削除状態も独立して確定・回収・保存する。存在フラグを持つ256要素の独立モデルと、全行・点検索・Count/Sum・各group・欠けた外側キーを持つJoinを比較し、更新中の古い読取りも変更前の結果を保持することを確認する。

ChurnのStorageは8周期ごとの共通checkpoint後と最後に、その時点までの結果台帳を明示的に忘却する。忘却前のCommitted、忘却後と再起動後のUnknown、業務行の保持、台帳予約の減少を検証する。既定`-Workload Update`は結果を保持する。JSON v2にはWorkload、更新/削除/再挿入数、存在行数、回収の呼出し所要時間、忘却位置と前後の予約値、最終再起動後の状態を記録する。予約値はプロセスの実メモリ量と区別する。回収の内訳は共通Coordinatorから直接取得したReclaimDrainMilliseconds（受付から物理処理開始まで）、ReclaimPhysicalMilliseconds（物理回収・保存・再取得・共通後始末）、ReclaimInternalMilliseconds（両者の合計）を記録する。ReclaimMillisecondsは呼出し側で測る全体時間として残す。ReclaimSequenceは運転世代内の番号で、再オープンで新しいdomainになれば再び初期値から始まる。待機時間はTask開始待ちと開始前の準備を含み、CPU時間とは区別する。

```powershell
& .\TestSeamless\Invoke-MvccEndurance.ps1 `
    -TaskRoot 'F:\Seamless\MvccImplementation\churn' `
    -LogRoot 'F:\CodexTemp\Seamless\MvccImplementation\churn' `
    -Workload Churn -Cycles 512 -Configuration Release
```

## クライアントの結果保持

`--client-results <data-root> <report.json> <source-sha256> <4096|16384> <mode>` は、同じNative行フレームを逐次消費、型付きメモリ、Persisted、フレーム直接保存、Tsavoriteで比較する。modeは `Stream`、`Memory`、`Persisted`、`Raw`、`RawBuffered`、`RawValidated`、`Adaptive`、`TsavoriteMemory`、`TsavoriteTemporary`。Adaptiveは検査済みのフレームを128 KiBでファイルへ移す。Tsavoriteは受信序数をキーとする辞書に保存し、再生時は序数順に点取得する。この比較は事前生成したMemoryStream上で実行する。

`--client-result-socket <data-root> <report.json> <source-sha256>` は、ループバックの実ソケットで、メモリ保持と途中の一時ファイル移行を確認する。正常時は接続終了後に2回読み直し、途中で列挙を終えてからの再読込、重複した列挙の拒否、結果破棄時の予約返却を照合する。切断・取消・不正な行・予算不足では、完了を返さず予約とファイルを解放する。

`--client-result-socket-comparison <data-root> <report.json> <source-sha256> <4096|16384> <mode>` は、同じフレーム列を実ソケットで送って受信時間と接続終了後の再生時間を別に計測する。modeは `Stream`、`TypedMemory`、`Persisted`、`RawMemory`、`Adaptive`。Adaptiveでは検査済みのフレームを128 KiBの保持閾値でファイルへ移す。保持経路は再生を2回照合し、破棄時の予算返却を確認する。

クライアント結果保持の各コマンドのデータとレポートは F 上の絶対パスを指定する。`Invoke-MvccValidation.ps1` の `-TaskRoot` と `-LogRoot` も F を指定し、ビルド後のDLLを `Invoke-LocalProcess.ps1` で実行する。
