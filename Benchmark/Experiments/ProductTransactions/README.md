# 製品MVCCの代表測定

保存方式は`-StorageMode Memory|Storage`で選択する（既定Memory）。`-OperationsPerWorker`は8～4096、既定128。結果v2には保存方式と取引数を記録する。Storageは新規の私有領域に業務WALを作り、終了後の再オープンでも行数と総和を照合し、その所要時間を別レコードへ記録する。小規模確認は`-Rows 256 -StorageMode Storage -OperationsPerWorker 8`を使う。通常終了からの再オープンであり、プロセス強制終了の試験とは区別する。

`TestSeamless/Invoke-MvccCrash.ps1 -TaskRoot ... -LogRoot ...`は別の64行試験で、commitが返った直後に`Environment.Exit(73)`でfinally/Dispose/checkpointを通さず終了する。次の独立プロセスがWALから全キー・値を復元することを照合する。writerの73は期待した終了コードとして記録し、readerの正常終了と値の照合を全体成功の条件にする。確定途中の各障害点、電源断、Gatewayを網羅した試験ではない。

製品の生成DB、共通TransactionCoordinator、版付きTsavorite、FLinqをそのまま使う測定入口。採用試験用の`TransactionModes`とは別の結果形式を使う。Memory、Exclusive/Optimistic、query worker上限1/4、100%の全走査、点操作の同時取引1/4、読取95%/50%、一様/1%集中を扱う。各点操作セルはworker当たり128取引、3反復で、競合の業務再試行を行わない。既定1万行、代表条件10万行までに制限する。

通常LINQ/native FLinqの全走査、並列Count、分割GroupBy/Joinの値を照合する。初期データは4,096行ずつ投入し、投入後の共通checkpointから測定する。10万行分の依存を一つの取引に抱えるためにmetadata予算を増やさない。成功処理量、成功時p50/p95/p99、競合、割当、共通資源ピークとCPU利用数をJSONへ残す。成功した更新総数と保存値の総和も各セルで照合し、照合の時間・割当は点操作の測定から除く。各点操作セル後の共通checkpointで回収し、短いセルの結果を長期保持時の成績とは呼ばない。JIT・ページキャッシュ・同居処理の影響を含む測定であり、Gateway、Persistent、障害復旧、長期運転の全体受入を代替しない。

```powershell
& .\TestSeamless\Invoke-MvccProbe.ps1 `
    -TaskRoot 'D:\CodexTemp\Seamless\MvccImplementation\my-run' `
    -LogRoot 'F:\CodexTemp\Seamless\MvccImplementation\my-run' `
    -Rows 100000
```

runnerはDの隔離snapshotをReleaseビルドし、生成コードのnative走査・Count分割・GroupBy/Join接続を検査してから、共通8論理CPU以内で実行する。Dに実データ、Fに結果、source/生成コードのhash、PID・生成時刻・affinityとログを残す。巨大な全件複製やOSキャッシュの消去は行わない。Debug/Releaseの正確性回帰は`Invoke-MvccValidation.ps1`で別途実行する。

`TestSeamless/Invoke-MvccEndurance.ps1 -TaskRoot ... -LogRoot ... -Cycles 32`は、Memory/Storageそれぞれ256行で反復する独立した検証入口。各周期で古い読取りを保持したまま4つの更新取引を準備し、別行の更新をworker当たり8回確定する。更新後にも古い全行・FLinq Count/Sum/GroupBy/Joinを照合し、新しい取引は独立した256要素の期待値モデルと照合する。業務処理を自動再実行しない。各周期で共通の履歴回収と保存を通し、Storageは8周期ごとと最後に再オープンする。

周期数は1～512、既定32。`-Configuration Debug`も指定できる。Dに専用データ、Fにソース/生成コードhash・実行receipt・各周期の資源/CPU・成功更新数・所要時間を記録する。現在値と旧値、反復回収、再オープンの整合性を確認する有限回の試験であり、無期限の連続運転やGatewayの長期運転、性能受入の完了とは扱わない。

行数は256のまま、共有メモリ予算は32MiBとする。Tsavoriteの既存の固定配列と、再オープン時に新旧allocatorが重なる費用を計上するためであり、32MiBの行データを確保する指定ではない。履歴保持とmetadataの個別上限は維持する。予約量と実メモリの違い、nativeの固定費は実装設計書の資源会計を参照する。

`-Workload Churn`を指定すると、同じキーについて更新・削除・再挿入を3周期で繰り返す。削除状態も独立して確定・回収・保存する。存在フラグを持つ256要素の独立モデルと、全行・点検索・Count/Sum・各group・欠けた外側キーを持つJoinを比較し、更新中の古い読取りも変更前の結果を保持することを確認する。

ChurnのStorageは8周期ごとの共通checkpoint後と最後に、その時点までの結果台帳を明示的に忘却する。忘却前のCommitted、忘却後と再起動後のUnknown、業務行の保持、台帳予約の減少を検証する。既定`-Workload Update`は結果を保持する。JSON v2にはWorkload、更新/削除/再挿入数、存在行数、回収の呼出し所要時間、忘却位置と前後の予約値、最終再起動後の状態を記録する。予約値はプロセスの実メモリ量と区別する。回収の内訳は共通Coordinatorから直接取得したReclaimDrainMilliseconds（受付から物理処理開始まで）、ReclaimPhysicalMilliseconds（物理回収・保存・再取得・共通後始末）、ReclaimInternalMilliseconds（両者の合計）を記録する。ReclaimMillisecondsは呼出し側で測る全体時間として残す。ReclaimSequenceは運転世代内の番号で、再オープンで新しいdomainになれば再び初期値から始まる。待機時間はTask開始待ちと開始前の準備を含み、CPU時間とは区別する。

```powershell
& .\TestSeamless\Invoke-MvccEndurance.ps1 `
    -TaskRoot 'D:\CodexTemp\Seamless\MvccImplementation\churn' `
    -LogRoot 'F:\CodexTemp\Seamless\MvccImplementation\churn' `
    -Workload Churn -Cycles 512 -Configuration Release
```
