# MVCC履歴保存の現行評価と専用ストア案

## 結論と適用範囲

2026-11-03の現行コードを調査した。**当面はTsavorite版を維持し、索引・常駐ページ・配列コピー・資源会計を先に改善候補として測る。専用の追記型ストアは比較実装の候補であり、採用や置換は決めない。** 進行中のNative/FLinq接続を別の一貫性方式へ分岐させない。

本書は依頼された調査・検討結果で、製品の保存形式を変更する仕様ではない。最初の小規模比較では製品コードを変更せず、その後に資源会計・ページ返却・Garnet依存版を修正した。主データの索引やTsavorite全体の撤去は対象外である。

## 現行の構造 — Fact

- `VersionedTransactionalTable`は一つの`HistoryStore`を持つ。最新版用Tsavoriteと独立した履歴用Tsavoriteの二重構成ではない。現在値も旧版も同じハッシュ索引付きログに入り、キー検索から最新headを得る。そのため「履歴専用の索引を丸ごと削除」とは評価できない。専用化後も最新版をキーで引く索引が必要になる。
- `HistoryAccessor.ReadVisibleRecord`は最初にキー検索し、版が新しすぎれば`ReadRecordAtAddress`で遡る。native `PreviousAddress`は同一行の前版だけでなく、同じハッシュtagで衝突する別キーを含み得るため、現在のキー照合は正しさに必要である。
- 旧版の任意キー検索用に別の履歴索引を作るAPIはない。ただし現在の一つの索引はhead検索・キー存在・native lookup走査・compactにも使う。各表・実体化ビュー・TPCの索引見出し/chunkが持つ業務上の索引も削除対象ではない。
- 自己変更は私有`TransactionChangeSet`を先に読む。共通のキー/不存在/走査依存によるSerializable検証はCoordinatorが行う。保存層の置換だけでこれを変更する理由はない。
- 更新はRMWを使うが`InPlaceUpdater`は常にfalseで、コピー元を保全する。read-copyは`ReadCopyOptions.None`、read cacheも今回の全測定セルで無効だった。SpanByteは値をログ内に持ち、独立したobject payloadをobject logへ保存する方式ではない。これらを新たな削減効果に数えない。
- `HistoryFunctions`の値ヘッダーは21バイト。commit版、削除フラグ、値長、形式識別を持つ。各native版にはキーも保存する。RMWの汎用経路・索引探索・セッション/epoch/Pending処理は残る。
- 点読取はReaderでキーと値を配列へコピーし、`Record`で値からrowをさらに配列へコピーする。native走査は可視headをspanで処理できるが、新しいheadから旧版へ戻る場合は同じコピー経路を使う。

根拠: [HistoryStore.cs](../../Seamless.Collections.Tsavorite/Stores/HistoryStore.cs)、[HistoryIteration.cs](../../Seamless.Collections.Tsavorite/Stores/HistoryIteration.cs)、[VersionedTransactionalTable.cs](../../Seamless.Transactions.Collections/VersionedTransactionalTable.cs)、[CollectionStore.cs](../../Seamless.Collections.Tsavorite/Stores/CollectionStore.cs)。

## 会計修正前の小規模実測 — Factと限界

前回の532件検証で使ったRelease製品DLLを参照する独立ハーネスを作り、1,024キー×8版、キー8バイト・row32バイトを測定した。各セルのStore予算は32MiB、論理CPUは共通8個。専用ストアは未実装で比較していない。製品コードは変更せず、一時プロジェクトとデータはD、ログはFに保存した。

読取は各版について256件×3batchの中央値。Storageでは各batchの前に`FlushAndEvict`を実行する。OSのファイルキャッシュは排除していない。設定順序を交互にした統計試験ではなく、JIT・PGO・同居負荷を排除できていない。以下の遅延と所要時間は予備値で、倍率の採用根拠にはしない。追加・回収時間は各セル一回である。

| 保存先 | 主索引指定 | 常駐ログ上限 | 実索引合計 | 最新版読取 µs | 第1版読取 µs | 8,192版追加 ms | 回収 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| Memory | 64 B | 4 MiB | 10,432 B | 3.657 | 28.762 | 56.325 | 19.959 |
| Memory | 32 KiB | 4 MiB | 33,792 B | 0.633 | 6.607 | 50.115 | 2.695 |
| Storage | 64 B | 64 KiB | 10,432 B | 96.479 | 699.954 | 770.948 | 1,037.237 |
| Storage | 32 KiB | 64 KiB | 33,792 B | 82.542 | 695.983 | 635.139 | 1,004.837 |
| Storage | 32 KiB | 1 MiB | 33,792 B | 92.946 | 720.919 | 2.769 | 103.741 |

確認できた容量・割当は次のとおり。

- ログ増分は全セル671,008 Bで、版当たり約81.91 B。これはrow32 B・key8 B・21 Bの値ヘッダーに、nativeヘッダー・長さ・alignment・ページ末尾を加えた測定値である。専用形式の節約率をこの値から断定しない。
- 主索引64 Bでもoverflow indexが10,368 B、162 bucket存在した。主索引32 KiBではoverflowが1,024 B、16 bucketだった。64 Bを索引全体の費用として報告してはならない。
- 汎用の自動索引拡大はparallel region中には保留される。Historyの容量入口はpayload長0で汎用予約処理へ入り、今回もregion終了・回収後の主索引は指定値のままだった。設定だけで自動拡大が進むとは仮定しない。なお生成DBのIndexSizeはbucket数から64倍して渡すため、今回の64 Bを全製品profileの設定として一般化しない。
- 点読取の割当は最新版約168 B/件、第1版約1,344 B/件で、8版の遡及に対応して増えた。設定を変えてもこのコピー費用は消えない。
- 最新版走査の割当はMemoryで約7.9 KiB/1,024行、第1版走査は約1.38 MB。Storageの退避後走査ではそれぞれ約1.14 MB、約2.52 MB。native走査の利益と、古い版を解決する費用を区別する必要がある。
- 静止回収後の論理ログは83,936 Bになり、全1,024行の最新版を再検証した。物理ファイルの即時削除と同義ではなく、checkpointや旧ファイルの保持・OSハンドルも別途必要である。
- 改修前はnativeの常駐ログ・使用中overflow indexと共通budgetの予約値が一致しなかった。例えばMemory/64 Bではnativeログ675,840 B＋使用中索引10,432 Bに対し予約671,744 Bだった。さらに使用中bucketの統計だけでは、以下の固定配列を含められていなかった。この旧予約値を現在のストアの全容量や実メモリ量として流用しない。専用案でも間接表・head索引・未完了I/Oを別途計上する。

Storageの大きい常駐ログでも、明示退避後のランダム旧版読取はほぼ改善しなかった。常駐設定を増やせばcold readが速くなるとはいえない。追加時間の差にはflush待ちが含まれるが、I/O回数・実転送量・OS cache missは未計測である。

これは**履歴単体の追加・読取・回収**の測定である。Serializable検証・業務WALのSync・複数表公開を含むcommit処理量、長期取引下の保持量、専用案の速度と保守費用は未測定である。以前のTransactionModesの数値も試作方式の比較であり、専用履歴ストアの実測値に流用しない。

証跡:

- F:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/history-costs-release-20261103.json
- F:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/history-costs-inputs-20261103.json
- 同ディレクトリのhistory-costs-release-build-20261103、history-costs-release-run-20261103
- 測定ソース: D:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/history-costs/Program.cs

## 比較案 — 未採用

**固定配列の追加観測 — Fact:** 採用DLLの`MallocFixedPageSize<HashBucket>`は、先読み用を含む2枚の`HashBucket[66048]`を最初に確保する。payloadだけで8,454,144バイトあり、使用中bucket数16（1,024バイト）とは別である。空のHistoryStoreでこの実配列と主索引・ログを合計すると少なくとも8,462,400バイトで、改修前の予約64バイトでは覆えていなかった。DLLのSHA256は`707B7517BBB8E638E7BB488B471BFCB6A845A8745BC0CC93DB10A350C024077F`で、同じローカルnupkg内のDLLとも一致した。ローカルGarnet checkoutを同じコードと仮定せず、このDLLの実配列と逆コンパイル結果を確認した。配列容量はRSSや物理常駐量と同義ではなく、疎なページやCLR heapのcommitと区別する。

上のDLL観測は2.2.1での修正前の証拠である。現在は正式版2.2.5へ更新した。2.2.5の`IndexOverflowSizeBytes`は使用中bucket数ではなく先読み・padding込みの確保済みpayloadを返すため、会計の照合にもこの公式APIを使う。初期overflow payloadの8,454,144バイトは引き続き存在する。page tableは固定4096要素から設定に応じた可変長へ変わっている。公式修正・回避処理の存廃・配布物のhashは[実装設計書のGarnet更新欄](DeferredMvccImplementation.md)を参照する。

現在の修正は、固定pool・索引alignment・ログの先行/返却ページ・リング管理領域を事前予約へ含める。通常の資源不足はnative生成前に返し、親budgetの残量も反映する。索引拡大と復旧で旧allocatorが重なる費用も予約する。大きな溢れpoolの境界、I/O/checkpointの全一時領域、破棄後にwrapperを保持した場合のmanaged配列の寿命を含む全ピークは、少量の配列観測だけで検証済みとは扱わない。

2.2.5の再検証は256行・64周期のChurnをDebug/Releaseで実行した。各構成のMemory/Storageそれぞれで更新704・削除672・再挿入672、最終256行を確認した。Memoryの予約量は9,785,728～9,802,112、最終9,793,920バイト。Storageの周期中予約量は8,741,248～8,776,064、最終再起動後8,749,440バイト。共通予算・CPU返却・忘却位置・再起動後の状態と最終入力784ファイルのhashを監査した。証跡はF:/CodexTemp/Seamless/g225-end-20261103/endurance-latest-20261103.json。これらはStoreの予約会計であり、プロセスRSS・速度比較・専用履歴backendの比較とは区別する。

この固定費は**最新版の索引にTsavoriteを残す専用案にも残る**。履歴ノードの形式だけを変えて、固定配列8MiB分が消えるとは見積もらない。head索引を共用・変更する案は、表別走査、所有権、回復、予算と性能の別のトレードオフを持つため、採用の前提にせず比較対象として明示する。

| 観点 | 現行Tsavorite | 設定・接続の調整 | 専用の追記型履歴＋head索引 |
|---|---|---|---|
| 最新版の検索 | 既存ハッシュ索引でheadを得る | distinct key数から索引を決め、overflowを観測 | 主索引を維持し、key→head参照を引く。余分なhead参照読取が増える可能性 |
| 旧版 | nativeの衝突チェーンをキー照合して遡る | 正しさの照合は削除しない。コピーの削減を別途試す | 同一行だけのprev参照。汎用ハッシュ探索・衝突キー照合を履歴ノードから外せる |
| 保存量 | 各版にキーとnative/値ヘッダー | 同じレコード形式なので基本保存量は変わらない | キー重複を減らせるが、head参照・checksum・node長・任意のrow ID・alignmentが増える。短いキーでは差が小さい可能性 |
| 割当・遅延 | 旧版ごとにコピーが増える | span/所有bufferの寿命を守る改善余地 | lease付きpage sliceで減らす余地。page miss・連続依存I/Oは残る |
| 追加・commit | RMWコピー更新と共通WAL | 索引/flush待ちを調整。業務確定は同じ | version append＋head更新になる。二つの資源の予約・障害処理が増える |
| 回収 | 静止後にlatestをcompact | 予算と索引の調整。現在の静止条件を維持 | live headと必要な基底版のtrace/再配置、参照更新、世代切替が必要 |
| 保守 | 既存のcheckpoint/回復/IOを利用 | 保存互換性を保ちやすいが、未測定の改善率は約束しない | page cache、破損検査、非同期IO、recovery、compactionを新規に保守 |

### 専用案の最小構成

1. 主索引はkey→現在のheadを維持する。既存Tsavoriteを使う場合も、この索引とhead recordの費用は残る。
2. 履歴は不変ノードを追記する。概念フィールドはcommit版・同一行prev参照・削除フラグ・payload長・checksum・row。必要なら固定row IDを持ち、別行への誤参照を検出する。row IDを残す場合はその費用をキー削減から差し引く。
3. 参照はraw pointerでなく`(store/arena, generation, logical offset)`。固定ページ/segmentへ配置し、ページまたぎの可変長rowの規則を定める。公開済み参照がある間は同じ番号を再利用しない。跨segmentのprev参照を許可し、単なるsegment時刻順削除はしない。
4. 常駐ページ上限・dirty page・pin・read buffer・I/O descriptor・head索引/overflow・退避領域を共通budgetへ先に予約する。退避後はbuffer所有leaseを返す非同期読取とし、キャンセル受付だけでpage/deviceを解放しない。native相当のIO待ちではCPU枠を返し、実I/Oと全workerの終了を待つ。
5. 任意キー検索を履歴ノードに提供しない設計なら、prevは同一行だけを指す不変条件をappend入口で保証する。現在のpublic `ReadRecordAtAddress`が返すキーや`HistoryStore`の低層利用との互換性は、別adapterまたは新しい内部型として明示する。現行APIへ空のキーを返して偽装しない。

削除も通常の版ノードとして残し、古いsnapshotが必要な間はheadのキーを索引から外さない。再挿入では同じキーの版連鎖と削除境界を保持する。ノードの完成前にhead参照を見せず、head更新と読取の原子性・memory orderingを保証する。複数wordの参照を無保護で上書きしない。checkpointでは参照先ノードの耐久化を、参照を公開する共通manifestより先に終える。

### 一貫性・WAL・寿命

私有変更の優先読取、ReadVersion、キー/不存在/走査依存、Serializable検証、acceptance/publication順序は現在の共通層へ残す。変更集合を検証し、共通WALを耐久化してから全表の版・headを設置し、最後に共通prefixを公開する。新しいheadが先に物理的に見えても、古いsnapshotはprevへ戻れて、新しいprefixを先に公開しないことを守る。耐久化後の設置失敗はdomainを停止してWALから復旧し、業務を自動再実行しない。

Native/FLinqには引き続き同じTransactionRowsとoperation/worker寿命を見せる。保存先の選択で別の一貫性契約や独立したcommitを導入しない。現在の`VersionedTransactionalTable`は具象HistoryStoreへ結合しているため、専用化するならその内部境界を切り出す。先に既存型の外形だけ変えて専用化済みとは扱わない。

## 回収の安全条件

現行案Aは新規rootを待たせ、既存の取引・走査・worker・Pending/callbackを排出する。履歴不足では古い読取専用取引を中止するが、参照と実行中IOの終了前に解放しない。全participantを閉じ、current headだけを整理・保存し、共通checkpoint完了後にWAL prefixや旧保存世代を回収する。物理処理中の失敗はdomainを停止する。

活動中snapshotを残す別の回収方式を採る場合、各行について最古の活動中ReadVersion以下の最新版である**基底版**と、それより新しい必要な版・削除ノードを保護する。例えば版10→5→1に対してsnapshotが7なら、版5を消せない。`version<7`の全ノード削除は誤りである。read/write取引のReadVersion、私有変更が参照する基底、未公開の設置、跨segmentリンク、page pin、非同期IO、列挙子も保護対象となる。今回の依頼で非静止回収へ変更する判断はしていない。

専用案で静止後に再配置する場合も、headから到達可能な最新版・削除状態を新arenaへコピーし、head参照を一括で切替え、共通保存とIO完了を確認してから旧arenaを外す。回収中の新規取引待機は既存の合意どおりとする。

根拠: [TransactionCoordinator.Maintenance.cs](../../Seamless.Transactions/TransactionCoordinator.Maintenance.cs)、[HistoryStructureGate.cs](../../Seamless.Collections.Tsavorite/Stores/HistoryStructureGate.cs)、[VersionedTransactionalTable.Parallel.cs](../../Seamless.Transactions.Collections/VersionedTransactionalTable.Parallel.cs)。

## 再起動後に必要な履歴

現行の共通トランザクション契約は、再起動を越えて活動中snapshotを再開しない。HistoryAddressのstore IDはopenごとに変わり、回収でもgenerationが変わる。業務WALも物理prevアドレスではなくkey/rowの変更を持つ。したがって共通checkpoint時点の可視行・削除状態と、その後の耐久化済みWALから、起動時に必要なhead/版を再構築する案は可能である。

ただし、checkpoint中・生成世代の公開中の停止では、旧snapshotや保存途中Store、共通WAL、私有seedの組合せから正しく戻す必要がある。新arenaの完成前に旧arenaや必要WALを消さない。任意の過去版のtime-travelや低層HistoryStoreの保存済み旧版読取を永続的に保証する別契約まで削除してよいという意味ではない。

根拠: [TransactionCoordinator.cs](../../Seamless.Transactions/TransactionCoordinator.cs)、[DatabaseContainer.GatewayVersioned.cs](../../Seamless.Database/DatabaseContainer.GatewayVersioned.cs)、[GatewayTransactionManager.VersionedGenerations.cs](../../Seamless.Database.Gateway/Hosting/GatewayTransactionManager.VersionedGenerations.cs)。

## 比較試験と採用基準 — 提案

**利用者要求（2026-11-03）:** 履歴を専用化した時点で、その性能・メモリ・ストレージを比較する。専用版の完成だけで採用完了とは扱わず、現行Tsavorite・設定調整版Tsavorite・専用版の同条件比較を実行し、増減を報告する。小規模から始め、比較のために大きなメモリを確保しない。

比較表は各方式の絶対値と現行・調整版に対する増減を併記する。メモリは索引/overflow/履歴/page/bufferの実量、共通予算の予約ピーク、managed割当、プロセスのprivate bytes/working setを区別する。ストレージは履歴本体とhead索引、WAL、checkpoint、私有退避を分け、合計のファイル長と取得可能な物理確保量、測定区間の書込み量を記録する。論理アドレスのtailだけを実ディスク使用量と呼ばず、予算の予約値だけを実メモリ量と呼ばない。性能は成功commit/s、最新/旧版読取と取引の分位点、回収停止・保存・復旧時間を同じ行数/版数/保持期間/耐久化条件で比較する。OS cacheを消していない読取りを物理diskのcold性能と呼ばない。

まず現行と設定調整版を同じRelease入力で比較する。次に同じ外部契約を通る専用prototypeを加える。1K/10K程度のdistinct key、版数1/8/64、短い整数キーと長い複合キー、row32/512 B、均一/集中更新、Memory/Storage、常駐/退避後、長期readerあり/なしを段階実行する。設定順序を交替し、warmup後の複数反復と分布を記録する。NUMAの最適化はこの採否の前提に追加しない。

測るものは、主索引＋overflow＋履歴＋常駐pageの実量と予約peak、managed割当、最新版/旧版のp50/p95/p99、履歴追加、**共通WAL Syncと複数表公開込みの成功commit/s**、実I/O回数/byte、長期取引の保持量とwriter中止、回収停止時間・移動量・checkpoint/再起動費用である。ヒット率を揃えない冷温比較や、単体append/sを業務commit/sへ読み替える比較はしない。

採用の必須gateは、自己変更・削除/再挿入・競合・phantom・複数表公開・基底版・segment境界・満杯・取消と遅延IO・回収中の新規受付・checkpoint/設置途中のプロセス停止の正確性、保存形式/世代の検証、実資源を覆う予算、Native/FLinqの同じ結果・順序・寿命である。これを通した上で、調整済みTsavoriteに対する成功commit/sとtail latency、容量・割当・回収時間に反復して再現する利益があり、通常読取の退行と保守増分を許容できる場合に採用候補とする。速度の数値的な許容幅は同条件の比較結果を得てから判断する。

## 推奨する次の順序

1. native overflow/pageと共通budgetの会計差を調べ、実資源と設定の観測を揃える。
2. distinct key数に応じた索引と表ごとの常駐量を比較する。すべての表へ一律に大容量を予約しない。
3. 旧版読取での不要な配列コピーを、所有bufferとcallback寿命を維持して削減できるか試す。
4. それでも残る同一行prev以外の連鎖・汎用RMW・ヘッダー費用を専用prototypeで比較する。
5. 既存のNative/FLinq実装はTransactionRowsの境界を維持して進め、専用化の採否を先取りして保存形式を置換しない。

## 比較prototypeの実装範囲 — 2026-11-03

利用者の比較実装指示に従い、`Benchmark/Experiments/HistoryStoreModes`へ独立した実験を追加した。製品の共通WAL、Gateway/Native/FLinqの保存先を切り替える機能ではない。比較中に見つけた現行HistoryStoreのアドレス接続不具合は別途修正し、性能値には修正済みの現行方式を使う。初段は1,024キー、キー8/32バイト、行32/512バイト、版1/8と32バイト行の64版を使う。現行の小索引64バイトと調整済み32KiB索引を、同じGarnet 2.2.5の固定ビルドで比較する。Memoryは8MiBのログ/arena上限、Storageは64KiBと8MiBの常駐条件を扱い、親予算64MiBへhead索引とarenaを両方計上する。専用版のheadログとarena cacheは別なので、合計の実量も報告し、同じ名前の設定値だけで常駐量が同一とは主張しない。

専用版はTsavoriteのkey→head offset索引と、同一行prevだけを持つ固定ページの追記arenaを使う。40バイトのnodeヘッダーはmagic、版、prev、行長、削除フラグ、非暗号学的checksumを持つ。payload先頭のrow IDと版も照合する。公開参照はopenごとのIDと回収世代に結び付け、回収/再open後に使えない。ページcacheは有界、全件の行配列は作らず、拡張・I/O buffer・新旧回収領域を割当前に共通budgetへ予約する。

保存はarenaをStore componentとしてheadと同じmanifestへ登録し、参照先をflushしてから共通manifestを確定する。保存済み末尾ページへ次の追記を重ねない。静止回収は現在のheadだけを新世代へコピーし、削除headを外し、共通保存の確定後に旧arenaを回収する。古い保存を無効にすることもStoreへ通知する。初段のAPIは単一owner・同期の実験用で、共通取引、WAL、並行Pending、製品採用gateを実装済みとは扱わない。

先に正確性入口を追加し、版選択・不存在・削除/再挿入・ページ境界・満杯・checksum・短いファイル・別open/回収世代の参照拒否・保存/再open・静止回収を確認する。性能はウォームアップ後5回、方式順を巡回し、append、最新/旧版読取、保存、再open、回収を別に測る。Storageでは明示的なアプリcache排出後も測り、OS cacheは消さない。headとarenaを合わせた容量・共通予約・managed割当・観測したプロセスメモリと転送量を報告する。単体append/sを共通WAL込みのcommit/sとは呼ばず、この初段だけで採用を決めない。

**比較条件の追加 — Fact/Decision:** 修正後の600標本は完了したが、既定NativeデバイスとBuffered arenaのI/O差が混在する。履歴形式だけの利益とみなさず、元のsnapshotとrunner・監査を`default-io-evidence`へ保存する。追加の`-BufferedControl`では既存の`TsavoriteTuning.Storage`を使い、現行HistoryStoreと専用版headをともにBuffered/RandomAccessへ揃える。Memoryを除く同じ5形状・キー2種・常駐2種・索引2種・方式2種・5反復の400標本を別snapshotで測る。OS file cacheはowner予算とprocess private bytesの外にあるため、その費用も解釈に含め、単なるNative→Bufferedの改善を専用化の利益へ足さない。

**比較結果 — Fact/Decision:** 追加の400標本・80条件と、現行prototypeの39検証項目のDebug/Release実行は完了した。回収中を含む予約ピークは再open前後のownerの最大値から求める。小キャッシュ・多版では専用版に利点があり、1版の常駐条件では追加時間と予約量が増える例もあった。製品の保存先は現行を維持し、共通確定込みの採用gateを通したとは扱わない。数値、trade-off、再実行入口と証跡は[比較結果](20261103-history-store-comparison.md)に集約する。
