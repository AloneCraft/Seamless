# TPC-C 由来の不具合検査ランナー

TPC-C 由来・非監査・TPC-C の結果と比較不可。
HammerDB モードは TPROC-C v6.1 の SQL Server 向け規則を再現します。このランナーの速度は検査込みの直列実行の値です。HammerDB の結果でも、公式の性能結果でもありません。

4 変種は TL (Tsavorite / LINQ)、TF (Tsavorite / FLinq)、PL (Persisted / LINQ)、PF (Persisted / FLinq) です。既定では全4変種に同じ入力を流し、区画数が2以上なら区画1つのシャドーも比較します。

```powershell
dotnet run -c Release --project Benchmark/Benchmark.TpcC -- describe
dotnet run -c Release --project Benchmark/Benchmark.TpcC -- generate --warehouses 2 --items 200 --customers-per-district 30
dotnet run -c Release --project Benchmark/Benchmark.TpcC -- run --warehouses 4 --partitions 2 --transactions 20000
dotnet run -c Release --project Benchmark/Benchmark.TpcC -- crash --warehouses 2 --partitions 2 --items 200 --customers-per-district 30 --save-every 30 --cycles 12
dotnet run -c Release --project Benchmark/Benchmark.TpcC -- replay --from '<実行フォルダ>'
```

`run` は取引ごとに出力 (L1)、書き込み (L2)、読み戻し (L3) を比較します。初期投入、保存と開き直し、最後に、表と索引のダイジェスト・一貫性条件・ITEMの区画間複製 (L4) を確認します。`--save-every 0` は保存をせず、最後の状態だけを比較します。HammerDB モードの既知の差分は [規則書](docs/HAMMERDB_RULES.md) と [QUIRKS](docs/QUIRKS.md) を参照してください。

| 引数 | 既定値・意味 |
|---|---|
| `--seed` | `1`。入力と初期データを決める種 |
| `--warehouses`, `--partitions` | `4`, `2`。全区画を1 Storeに置く |
| `--items`, `--customers-per-district` | `1000`, `300`。不具合検査向け縮小規模 |
| `--mode` | `hammerdb` (`hammerdb-mssql` も可)、または `spec` |
| `--terminals` | `8`。入力生成器数。spec は倉庫あたり10以下 |
| `--transactions`, `--warmup` | `15000`, `1000`。warmup中も正しさを検査 |
| `--variants`, `--no-shadow` | `TL,TF,PL,PF`。シャドーを無効にするには `--no-shadow` |
| `--hop-threads` | `false`。`run`専用。`true`で各取引の全変種とL1〜L3を1本の専用thread Bへ直列移管 |
| `--save-every`, `--reopen-every` | `5000`, `2`。保存間隔と開き直し間隔 |
| `--memory-mb`, `--disk-gb` | メモリは規模から見積もる。ディスクは Store あたり16 GB |
| `--work`, `--keep-stores` | 出力先は既定 `Work/TpcC`。正常終了でもストアを保持する場合に指定 |
| `--stop-at`, `--trace-from` | 取引通し番号。直前停止と詳細トレース開始 |
| `--cpu`, `--priority` | 指定時だけ論理CPU固定。優先度は `belownormal` (`idle`, `normal` も可) |
| `--cycles` | crash の変種ごとの周期数。既定12 |
| `--inject-fault` | `ENGINE:TXN:KIND`。下記参照 |

`crash` の既定は TF/PF、保存300取引ごとです。保存直前、最初の子資源のSave、最後のFlush、マニフェスト確定後、保存直後、取引途中の書き込み、親による時間指定停止を種から選びます。各周期で復旧した運転状態と、同じ入力を最初から流した参照の表・索引を比較します。初期投入が保存される前の時間停止は未確定状態として扱います。これはStoreの保存原子性の検査で、WALやSUTの検査ではありません。

内部コマンド `crash-child` は共通引数に `--store PATH --variant TF --kill BeforeSave --kill-at 1 --kill-write 1` を加えます。`--kill` は `None`, `BeforeSave`, `InFirstSave`, `InLastFlush`, `AfterManifest`, `AfterSave`, `MidTransaction`, `Timed`。子の `--transactions` は保存済み位置から追加で流す件数です。

故障注入はハーネスだけで行います。`Output` は出力反転、`DropWrite` は書いた行の削除、`RouteWrong` は別区画への余分な書き込み、`SkipUndo` は巻き戻し済み挿入行の復活、`ReopenTamper` は開き直し直後の行削除です。書き込みのある取引を指定してください。`SkipUndo` は業務上の巻き戻しをするNew-Order、`RouteWrong` は2区画以上、`ReopenTamper` は保存と開き直しが必要です。指定した取引が条件を満たさない場合は注入されません。

`run` は実行ごとに `run.json`、`summary.json`、`summary.txt`、`txn-stats.csv` を保存します。失敗時はストアと `failure/failure.json`、`failure.txt`、再現引数を残します。`replay --from` は元の `run.json` の引数で新規ストアから再実行し、失敗取引まで詳しく記録します。故障注入も再現対象です。crash の replay は同じ種のキャンペーンを再実行します(時間停止の位置は非決定的です)。crash は `run.json`、`crash/cycles.csv` と失敗周期のストア複製・子のログを残します。

終了コードは一致 `0`、不一致 `1`、想定外例外 `2`、引数不正 `3`、資源上限 `4`、子の時間切れ `5`、Ctrl+Cによる取引境界での保存停止 `130` です。

`--hop-threads true` はrun owner Aを維持し、runごとに一度起動する専用thread Bで取引を実行します。各取引は全DBのA Release→B Acquire→既存ExecuteAllとL1〜L3→B Release→A Acquireの順です。入力生成、driver更新、保存、ダイジェスト、開き直し、正常終了のDisposeはAで行います。実行中のBのcleanupと応答が完了するまでAは次の操作へ進みません。部分Acquire/Releaseの失敗は停止扱いとし、BのReleaseにも失敗した場合はB上でterminal cleanupを試してから失敗を返し、未知の所有状態を再利用しません。

専用thread生成、dispatch待ち、handle移管は取引histogramの外です。histogramは従来どおり同じthreadのengine.Execute直前から直後までを測ります。sessionの`hop-acquire`/`hop-release` phaseには移管費用、run全体のWallSecondsには待機や移管も含まれます。GC.CollectionCountはprocess全体の値です。このflagは所有権とlifecycleの検査用で、falseとの性能優劣を示すものではありません。既定falseでは専用threadも移管も行わず、再現引数はtrueの場合だけflagを保持します。

workerの稼働とJoin後の終了をログに記録します。終了診断の出力が失敗してもJoin済みの事実を変更せず、残りのengine/store cleanupを続けます。元の取引例外、cleanup例外、診断出力例外は保持し、診断だけの失敗も正常終了には数えません。

C10全体のうち、Delivery比率を増やす運転、途中の予算超過・索引hook例外の追加キャンペーンは未実装です。現在の5種故障注入と区画1つのシャドー、保存・復旧検査と区別します。
