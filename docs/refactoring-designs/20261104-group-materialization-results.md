# group所有化と複数整列の改善結果 — 2026-11-04

## 結果

同じ4,096/16,384行・1,024groupの条件で三段階の改善を採用した。旧版と改善版を同じ条件内で交互に実行した最終比較では、Memory/worker1のGroupEscapeMultipleは4,096行で5.728→1.148ms、managed割当6,191,072→897,880バイト。16,384行は9.892→3.299ms、8,550,832→2,028,936バイトとなった。時間は約80%/67%、割当は約85%/76%減少した。

通常LINQとの比較は、同じMemory/worker1で4,096行がほぼ同等、16,384行は約20%短い時間となった。割当は通常LINQより約56%/73%少ない。全条件で時間が優位になったわけではなく、4,096行のworker4、16,384行のStorage/worker4は通常LINQより長い。今回の小規模データではworker1よりworker4の方が長い条件が多い。

## 実装した内容と維持した契約

- groupごとのQueryResultBuffer/codec解決を、queryだけが所有する一つのMaterializerと集約予約に置き換えた。初めて所有groupが必要になった時だけ初期化する。DB rootへ型cacheを残さない。
- 内部Lookupの確定countを使い、配列を一回で確保する。未知の利用者groupのCount getterには触れず、既存の有界拡張を使う。内部countと実列挙数の一致も検証する。
- 既定codecの参照なし・nullableなしのfield値型に限り、値によらない符号化長をquery内で一度計算し、payloadをまとめて予約する。独自codec・参照・nullable・FixedTextは従来どおり値ごとの処理を維持する。
- 所有groupは標準LINQのgroupと同じ読取り専用IList操作を公開し、Count/CopyTo等の高速経路を利用できる。変異APIは拒否する。key/要素の参照同一性を維持する。
- worker1のLookup.Groupsは既に初出順なので、merge用PriorityQueueと各groupのFirst列挙を省いた。複数workerの順序mergeは維持する。
- scope/取消検査、割当前の予算予約、失敗したgroupだけの巻戻し、先行する正常groupの予約と利用、worker終了待ちを維持する。返すgroupはkeyと所有配列だけを保持し、取引/scope終了後も読める。static Materialize APIは互換入口として残す。
- 展開コードのコメントにもquery内共有、count/固定長条件、予約、所有権を記録した。

## 最終比較条件

[改善前の比較](20261104-ordered-join-comparison.md)と同じ4形状、行数4,096/16,384、Memory/Storage、query内worker上限1/4を使った。通常LINQも同じ版付きTsavoriteのRowsをIEnumerableとして読む。Join出力は入力と同じ件数。Optimistic読取り専用取引、ログ4MiBに収まるwarm状態、Release/.NET 10.0.12、TieredCompilation=0、共通affinity255である。親予算64MiBは行データの確保量ではない。

各queryは2回warmup後10回測定し、初期投入・Begin/enlist/Commit・結果照合を時間とmanaged割当から除いた。全結果、group内順序、取引終了後の寿命、CPU枠と親予算の返却を照合した。クエリ同士の同時実行試験ではない。

三段階それぞれ16プロセス/640時間標本/64メモリ観測を保存した。最終結果は別途、各行数/保存方式/worker条件の中で旧FLinq・新FLinq・旧LINQ・新LINQの順を巡回した32プロセス/1,280時間標本/128メモリ観測で確認した。旧版を現在ソースから再ビルドせず、保存済みのDLLを使用した。同居負荷による時間の揺れは残る。

## GroupEscapeMultiple — 最終の交互実行

単位はmsとMB（1MB=1,000,000バイト）。各欄は時間標本の中央値。LINQは改善版と同じセル内で実行した通常LINQ。

| 行数 | 保存方式 | worker上限 | 旧FLinq ms | 改善FLinq ms | LINQ ms | 旧FLinq割当 MB | 改善FLinq割当 MB | LINQ割当 MB |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 4,096 | Memory | 1 | 5.728 | 1.148 | 1.150 | 6.191 | 0.898 | 2.032 |
| 4,096 | Memory | 4 | 5.728 | 1.374 | 1.125 | 6.294 | 1.132 | 2.032 |
| 4,096 | Storage | 1 | 5.728 | 1.210 | 1.103 | 6.191 | 0.898 | 2.032 |
| 4,096 | Storage | 4 | 5.954 | 1.509 | 1.120 | 6.295 | 1.133 | 2.032 |
| 16,384 | Memory | 1 | 9.892 | 3.299 | 4.108 | 8.551 | 2.029 | 7.601 |
| 16,384 | Memory | 4 | 9.919 | 4.108 | 4.157 | 9.020 | 2.631 | 7.601 |
| 16,384 | Storage | 1 | 10.090 | 3.685 | 4.136 | 8.551 | 2.029 | 7.601 |
| 16,384 | Storage | 4 | 10.291 | 4.534 | 4.151 | 9.021 | 2.632 | 7.601 |

## メモリ指標の区別

managed割当は全workerを含むプロセスの累積割当増分で、最大生存量ではない。GC後の保持増分は別の観測で、Memory/worker1の改善版は4,096行で約131KB、16,384行で約328KB。通常LINQは約156KB/344KBだった。返す全行を保持する必要があるので、旧版に対する保持量の大幅削減は主張しない。観測にはGCや遅延初期化の差も含まれる。

親予算の追加予約ピークは4,096行/Memory/worker1で旧2,008,736→新2,012,832バイト、16,384行では旧5,596,832→新5,600,928バイト。query-local codec初期化用の4,096バイト分を明示計上するため増加した。実際のmanaged割当が減ったことと、保守的な予約量が減ることは同じではない。通常LINQのLookup/整列/結果はこの予算へ登録されない。

全条件の私有退避は0バイト。Storageもwarm測定であり、低温I/Oや大規模Spillの性能を示さない。各report.jsonにOS private bytes/working setの前後値とプロセス累積ピークを保存した。DB/runtime/他queryも含むので、各queryの厳密なOSピークや所有group単独のメモリ量とは呼ばない。

## 反復改善の採否と停止理由

第一段階のquery-local codec/予約共有で、Memory/worker1の割当は4,096行で6.191→1.414MB、16,384行で8.551→3.773MBへ減った。第二段階のList操作・確定count・固定長処理では1.029MB/2.160MBへ減った。最後のworker1 merge省略はさらに各query約131KBを削減したが、独立した第二/第三段階の時間中央値の差はMemory/worker1で1.30→1.28ms、3.44→3.35msと約1～3%だった。これは交互実行による因果分離ではなく、小さい追加効果と測定変動が重なる範囲として扱う。

| 残る候補 | 判断 |
|---|---|
| 複数workerのmergeで先頭ordinalだけを直接読む内部API | 第一要素の列挙固定費を減らす余地はあるが、今回の1,024groupでは同種の固定費はquery当たり約0.13MB。追加の時間改善は未測定。残った小さな割当削減として今回は採用しない |
| 借用groupの要素を一括コピーして列挙器/leaseを省く | 取消・借用寿命・scopeの保護境界の再設計を伴う。安全な現行の逐次検査を維持する。今回の大きなcodec/予約固定費は既に除去した |
| 全group共通slabや共有pool | 小さいgroup一つの保持で全groupの領域を残す等の寿命/保持量の変化があるため不採用 |
| DB rootのcodec cacheへ解決型を残す | collectible型の寿命を延ばすので不採用 |
| 所有化を最終整列の後まで遅らせる | 未知callback/comparerへのgroup逃避の分析が必要。この全groupを返す条件では最終的な所有配列は全部必要。今回の単純な固定費改善の範囲を超える |
| 小さい入力でのworker数の自動選択、NUMA、Lookup/Rows全体の設計変更 | 共通処理の別の最適化課題。利用者の既存方針に従い後続へ回す |

この同条件で大きな固定費の削減を終え、追加の単純変更による時間改善も小さくなったため、利用者の停止条件に従い反復改善を終了した。全データ形状・将来の実装で改善不能という意味ではない。

## 正確性回帰と証拠

Execution.Testsの94件、DB展開の19件、Nativeの通常/版付きテーブル統合2件がDebug/Releaseとも成功し、各ビルドの新規警告は0。新しい所有化試験では多数group、参照同一性、scope終了後、読取り専用List、未知Count getter、nullable長、Memory/Spillとworker1/3、独自codecの呼出し順/producer thread、例外・取消・予算不足と巻戻しを確認した。List操作の追加前にIList非互換で失敗する試験も保存した。最後に生成コメントを追加し、最終Release比較の強制ビルドと生成物監査を通した。

- 設計: [group所有化の改善設計](20261104-group-materialization-optimization.md)。
- 最終16条件: F:/CodexTemp/Seamless/group-optimization-C-20261104/ordered-comparison-audit.json（640標本/64観測）。現在入力manifestは1E4E7BDB9EB93B9AA0288E6D4D57524DF5F017A78332BDD4376414F82A30AC45。
- 最終交互実行: F:/CodexTemp/Seamless/group-optimization-paired-20261104/revision-audit.json（32条件/1,280標本/128観測）。全32比較行、生の各10反復、OS値、PID/生成時刻、affinity、receiptを保存。
- revision-builds.jsonで両版の全runtime DLLと生成コード、revision-input-changes.jsonでソース差分7ファイルを記録。うち1ファイルはREADME、残り6ファイルは今回のruntime/生成接続。workloadコードは同一。旧snapshotの全入力と、新snapshot/現在入力の全hashを照合した。
- 正確性証跡はF:/CodexTemp/Seamless/group-optimization-tests-20261104、group-optimization-db-20261104、group-optimization-gateway-20261104。これは今回の関連回帰であり、別snapshotの全ソリューション受入件数を今回の成功件数へ混ぜない。

## 全形状の最終比較

以下も同じ交互実行の改善版と通常LINQ。単位はmsとMB、中央値。

| 行数 | 保存方式 | worker上限 | 形状 | FLinq ms | LINQ ms | FLinq割当 MB | LINQ割当 MB |
|---:|---|---:|---|---:|---:|---:|---:|
| 4096 | Memory | 1 | JoinIdentity | 2.257 | 2.201 | 2.205 | 3.718 |
| 4096 | Memory | 1 | JoinMultiple | 2.816 | 2.557 | 2.288 | 3.800 |
| 4096 | Memory | 1 | GroupIdentity | 0.971 | 1.051 | 0.762 | 2.060 |
| 4096 | Memory | 1 | GroupEscapeMultiple | 1.148 | 1.150 | 0.898 | 2.032 |
| 4096 | Memory | 4 | JoinIdentity | 2.398 | 2.214 | 2.358 | 3.718 |
| 4096 | Memory | 4 | JoinMultiple | 3.023 | 2.528 | 2.441 | 3.800 |
| 4096 | Memory | 4 | GroupIdentity | 1.220 | 1.160 | 0.997 | 2.060 |
| 4096 | Memory | 4 | GroupEscapeMultiple | 1.374 | 1.125 | 1.132 | 2.032 |
| 4096 | Storage | 1 | JoinIdentity | 2.304 | 2.160 | 2.205 | 3.718 |
| 4096 | Storage | 1 | JoinMultiple | 2.820 | 2.537 | 2.288 | 3.800 |
| 4096 | Storage | 1 | GroupIdentity | 1.024 | 1.071 | 0.762 | 2.060 |
| 4096 | Storage | 1 | GroupEscapeMultiple | 1.210 | 1.103 | 0.898 | 2.032 |
| 4096 | Storage | 4 | JoinIdentity | 2.520 | 2.276 | 2.358 | 3.718 |
| 4096 | Storage | 4 | JoinMultiple | 3.023 | 2.606 | 2.441 | 3.800 |
| 4096 | Storage | 4 | GroupIdentity | 1.251 | 1.102 | 0.997 | 2.060 |
| 4096 | Storage | 4 | GroupEscapeMultiple | 1.509 | 1.120 | 1.133 | 2.032 |
| 16384 | Memory | 1 | JoinIdentity | 9.390 | 9.187 | 8.663 | 14.811 |
| 16384 | Memory | 1 | JoinMultiple | 11.894 | 10.950 | 8.992 | 15.139 |
| 16384 | Memory | 1 | GroupIdentity | 3.458 | 4.080 | 1.696 | 7.630 |
| 16384 | Memory | 1 | GroupEscapeMultiple | 3.299 | 4.108 | 2.029 | 7.601 |
| 16384 | Memory | 4 | JoinIdentity | 10.052 | 9.092 | 9.190 | 14.811 |
| 16384 | Memory | 4 | JoinMultiple | 12.427 | 10.885 | 9.518 | 15.139 |
| 16384 | Memory | 4 | GroupIdentity | 3.815 | 4.020 | 2.298 | 7.630 |
| 16384 | Memory | 4 | GroupEscapeMultiple | 4.108 | 4.157 | 2.631 | 7.601 |
| 16384 | Storage | 1 | JoinIdentity | 9.413 | 9.021 | 8.663 | 14.811 |
| 16384 | Storage | 1 | JoinMultiple | 12.249 | 10.723 | 8.992 | 15.139 |
| 16384 | Storage | 1 | GroupIdentity | 3.099 | 4.256 | 1.696 | 7.630 |
| 16384 | Storage | 1 | GroupEscapeMultiple | 3.685 | 4.136 | 2.029 | 7.601 |
| 16384 | Storage | 4 | JoinIdentity | 10.133 | 9.142 | 9.190 | 14.811 |
| 16384 | Storage | 4 | JoinMultiple | 12.919 | 11.375 | 9.519 | 15.139 |
| 16384 | Storage | 4 | GroupIdentity | 3.974 | 4.320 | 2.299 | 7.630 |
| 16384 | Storage | 4 | GroupEscapeMultiple | 4.534 | 4.151 | 2.632 | 7.601 |
