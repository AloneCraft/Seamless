# Join・整列の専用経路と通常LINQの改善前の比較 — 2026-11-04

これはgroup所有化を改善する前の固定snapshotの測定報告である。現在の改善内容・性能・採否は[改善後の比較](20261104-group-materialization-results.md)を参照する。以下の「改善対象」はこの改善前snapshotについての観測として保持する。

結果は形状で分かれた。Join整列はmanaged割当を約36～42%削減したが、時間は恒等キーで通常LINQの0.94～1.15倍、複数整列で1.04～1.23倍だった。GroupBy集計後の整列は割当を約52～76%削減し、16,384行/worker1では約22～25%短い時間となった。group自体を返す複数整列は2.33～5.22倍の時間、1.12～3.10倍のmanaged割当となり、改善対象である。

## 条件と意味

- 4,096/16,384行、Memory/Storage、クエリ内worker上限1/4、FLinq/通常LINQの16条件。クエリ同士は同時実行しない。各条件を別プロセスで逐次実行し、方式の先後を巡回した。
- 同じ版付きTsavoriteのRows、Optimisticの読取り専用取引。通常LINQへはIEnumerableとして同じ保存先を渡す。比較用配列を通常LINQの入力にはしない。
- Joinは外側64行と内側全行を結び、結果を入力と同じ件数へ抑える。GroupByは1,024キー。投入順は固定の置換順、値は100+id%17。4形状のコードは[OrderedComparisonQueries.cs](../../Benchmark/Experiments/ProductTransactions/OrderedComparisonQueries.cs)。
- Release/.NET 10、共通affinity 0xFF（8論理CPU以内）、TieredCompilation=0。各形状2回warmup後10反復、中央値。初期投入・取引開始/enlist/Commit・結果照合は時間/割当から除く。全行が4MiBログに収まるwarm測定。Storageの結果を低温I/O性能と解釈しない。
- DB親予算64MiB、索引64KiB。行データを64MiB確保する設定ではない。全640時間標本と64メモリ観測が成功し、私有退避は全条件0バイト。
- 時間測定中のmanaged割当はプロセス全体の増分で、workerも含む。結果が返った後のGC観測増分は別の測定で、結果をKeepAliveする。結果以外の遅延初期化・cache・GC誤差も含み得る。
- 予算ピークはcaller所有の親ResourceBudgetのpeakカウンターだけを、他の操作がない時に現在値へ戻して測る。予約現在値・上限・製品APIは変更しない。通常LINQのLookup/整列/結果は共有予算へ登録されない。両方式ともRows走査buffer等の予約は含むため、通常LINQの予約は0ではない。
- 実行のたびに全結果の値/順序、group内の全行順序を照合し、取引終了後にもgroupを読む。独立モデルで入力全キー/値とJoinの件数/値を確認。CPU枠、取引/operation、DB廃棄後の親予算返却もassertする。

## 代表条件 — 16,384行・Memory・worker上限1

時間/割当は中央値。MiB=1,048,576バイト。「時間比」はFLinq/通常LINQで、小さい方が短い。

| 形状 | FLinq ms | LINQ ms | 時間比 | FLinq割当 MiB | LINQ割当 MiB |
|---|---:|---:|---:|---:|---:|
| JoinIdentity | 10.681 | 10.056 | 1.062 | 8.261 | 14.124 |
| JoinMultiple | 13.364 | 12.404 | 1.077 | 8.575 | 14.437 |
| GroupIdentity | 3.676 | 4.723 | 0.778 | 1.743 | 7.276 |
| GroupEscapeMultiple | 10.943 | 4.690 | 2.333 | 8.155 | 7.249 |

同じ代表条件のメモリ観測。保持増分はGC後のmanaged増分であり、query中の最大生存量ではない。予約は保守的な会計値であり、RSS/実割当とは異なる。

| 形状 | FLinq追加予約 MiB | LINQ追加予約 MiB | FLinq保持増分 KiB | LINQ保持増分 KiB |
|---|---:|---:|---:|---:|
| JoinIdentity | 9.735 | 16.008 | 129.180 | 129.180 |
| JoinMultiple | 10.423 | 16.008 | 128.203 | 128.203 |
| GroupIdentity | 0.954 | 8.004 | 16.125 | 16.234 |
| GroupEscapeMultiple | 5.338 | 8.004 | 320.094 | 344.203 |

## 実装との対応と判断

Joinの内側はnative producer、外側はsnapshotの行列挙を使い、Lookup・整列・結果を同じquery scopeで保護する。通常LINQとの比較には、この走査とLookupの構築・予算検査の差も含む。整列アルゴリズム単体の速度差ではない。恒等キーの小さい時間差を一律の高速化とは判断しない。

groupを返す場合、専用経路はLookupが閉じた後にも結果を読めるよう、各groupを`QueryGrouping.Materialize`で所有配列へ移す。ここではgroupごとにQueryResultBuffer・予約・CodecRegistry.Forkとcodec解決を行う。1,024個の小さいgroupではこの固定費とコピーが目立つ構造である。これはコード上の確認であり、各処理へのCPU/割当の寄与率はprofilerで分離していない。4,096行/worker1のGroupEscapeMultipleは約6.0～6.3ms、通常LINQは約1.2ms、割当は約3.05倍。16,384行でも約2.3～2.6倍の時間となった。

返却後の保持増分は代表条件でFLinq約320KiB、通常LINQ約344KiBだった。構築中の累積割当が大きいことと、最終結果が保持する量は別である。groupの所有権・寿命・予算保証を維持しつつ、query scope内でのcodec解決共有とgroup構築の固定費削減が次の改善候補になる。今回の測定ではこの性能改善を加えていない。

worker上限4による一貫した短縮は、このデータ規模では確認できなかった。通常LINQ自体は逐次で、worker設定は比較DBの条件を揃えたもの。小さいqueryに対して常にworkerを増やす根拠にはしない。10反復は安定したtail分布や他負荷との同時実行性能を示さない。

## OSメモリの観測

各プロセスは同じ方式の4形状を実行する。累積peak working setはDB初期化・warmup・GC未回収領域も含む。以下は16,384行/Memory/worker1の全プロセスの値で、個別queryのpeakではない。最後のprivate bytesとpeak working setは異なる指標であり、足し合わせない。

| 方式 | 累積peak working set MiB | 終了前private bytes MiB |
|---|---:|---:|
| FLinq | 78.879 | 49.473 |
| Linq | 76.984 | 53.914 |

## 全条件の中央値

| 行数 | 保存 | worker上限 | 形状 | FLinq ms | LINQ ms | 時間比 | FLinq割当 MiB | LINQ割当 MiB |
|---:|---|---:|---|---:|---:|---:|---:|---:|
| 4096 | Memory | 1 | JoinIdentity | 2.475 | 2.408 | 1.028 | 2.103 | 3.545 |
| 4096 | Memory | 1 | JoinMultiple | 3.077 | 2.845 | 1.081 | 2.182 | 3.624 |
| 4096 | Memory | 1 | GroupIdentity | 1.078 | 1.151 | 0.937 | 0.852 | 1.965 |
| 4096 | Memory | 1 | GroupEscapeMultiple | 6.246 | 1.248 | 5.006 | 5.904 | 1.938 |
| 4096 | Memory | 4 | JoinIdentity | 2.752 | 2.499 | 1.101 | 2.249 | 3.545 |
| 4096 | Memory | 4 | JoinMultiple | 3.346 | 2.988 | 1.120 | 2.328 | 3.624 |
| 4096 | Memory | 4 | GroupIdentity | 1.378 | 1.262 | 1.092 | 0.951 | 1.965 |
| 4096 | Memory | 4 | GroupEscapeMultiple | 6.588 | 1.268 | 5.197 | 6.003 | 1.938 |
| 4096 | Storage | 1 | JoinIdentity | 2.338 | 2.476 | 0.945 | 2.103 | 3.545 |
| 4096 | Storage | 1 | JoinMultiple | 2.976 | 2.730 | 1.090 | 2.182 | 3.624 |
| 4096 | Storage | 1 | GroupIdentity | 1.107 | 1.142 | 0.969 | 0.852 | 1.965 |
| 4096 | Storage | 1 | GroupEscapeMultiple | 6.014 | 1.152 | 5.221 | 5.904 | 1.938 |
| 4096 | Storage | 4 | JoinIdentity | 2.526 | 2.585 | 0.977 | 2.249 | 3.545 |
| 4096 | Storage | 4 | JoinMultiple | 3.105 | 2.994 | 1.037 | 2.328 | 3.624 |
| 4096 | Storage | 4 | GroupIdentity | 1.354 | 1.230 | 1.101 | 0.951 | 1.965 |
| 4096 | Storage | 4 | GroupEscapeMultiple | 6.177 | 1.259 | 4.906 | 6.003 | 1.938 |
| 16384 | Memory | 1 | JoinIdentity | 10.681 | 10.056 | 1.062 | 8.261 | 14.124 |
| 16384 | Memory | 1 | JoinMultiple | 13.364 | 12.404 | 1.077 | 8.575 | 14.437 |
| 16384 | Memory | 1 | GroupIdentity | 3.676 | 4.723 | 0.778 | 1.743 | 7.276 |
| 16384 | Memory | 1 | GroupEscapeMultiple | 10.943 | 4.690 | 2.333 | 8.155 | 7.249 |
| 16384 | Memory | 4 | JoinIdentity | 10.175 | 9.365 | 1.087 | 8.764 | 14.124 |
| 16384 | Memory | 4 | JoinMultiple | 12.514 | 11.126 | 1.125 | 9.078 | 14.437 |
| 16384 | Memory | 4 | GroupIdentity | 4.020 | 4.119 | 0.976 | 2.192 | 7.276 |
| 16384 | Memory | 4 | GroupEscapeMultiple | 10.534 | 4.265 | 2.470 | 8.603 | 7.249 |
| 16384 | Storage | 1 | JoinIdentity | 9.832 | 9.943 | 0.989 | 8.262 | 14.124 |
| 16384 | Storage | 1 | JoinMultiple | 12.416 | 11.339 | 1.095 | 8.575 | 14.437 |
| 16384 | Storage | 1 | GroupIdentity | 3.391 | 4.505 | 0.753 | 1.743 | 7.276 |
| 16384 | Storage | 1 | GroupEscapeMultiple | 10.486 | 4.416 | 2.374 | 8.154 | 7.249 |
| 16384 | Storage | 4 | JoinIdentity | 10.846 | 9.445 | 1.148 | 8.764 | 14.124 |
| 16384 | Storage | 4 | JoinMultiple | 13.888 | 11.294 | 1.230 | 9.078 | 14.437 |
| 16384 | Storage | 4 | GroupIdentity | 4.387 | 4.327 | 1.014 | 2.192 | 7.276 |
| 16384 | Storage | 4 | GroupEscapeMultiple | 11.762 | 4.485 | 2.622 | 8.604 | 7.249 |

## 証拠と回帰

監査結果: F:/CodexTemp/Seamless/ordered-comparison-20261104/ordered-comparison-audit.json。全32比較行の時間min/max・割当・予算・保持増分はordered-comparison-summary.csv。各プロセスのreport.jsonに生の10反復とOS前後値を保持する。source manifest: 24DC3BA0FC8A0F7A38822147439453E49D3D5C69692277ECBA91202D2A4D7F3F、802入力は監査時の現在ソースに一致。生成コードと実行DLLのhash、全16条件、巡回順序、PID/creation time、affinity、強制ビルドとビルド警告0を照合済み。

予備Debugは4,096行/Memory/worker1の両方式で24時間標本・8メモリ観測が成功。最終Releaseは640時間標本・64メモリ観測が成功。測定を開始する前に、窓のないGroupBy整列が`if(!(true&&true)){yield break;}`を生成してCS0162を出すことを再現した。常に真の継続条件を生成しない最小修正を行った。53種類の終端の通常LINQ比較へCS0162不在を追加し、従来/MVCCの2条件がDebug/Releaseで成功した。証跡はF:/CodexTemp/Seamless/ordered-comparison-regression-20261104のDebug-test-7839ed8b4c794cc09152eb4a2a54f0a7とRelease-test-122568fba37246bf8f7872d989611cd4。警告を抑制した成功ではない。
