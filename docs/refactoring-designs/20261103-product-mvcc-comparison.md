# 製品MVCCの同条件比較とLookup改善

## 対象と条件

Garnet 2.2.5、.NET 10、Release、共通8論理CPUで測定した。通常nativeの`Set<int,Row>`と、同じキー・行・Auto符号化を使う版付き`TransactionRows`を比較する。通常nativeを通常LINQで読む条件、取引行を通常LINQで読む条件も含む。全件を配列に複製して基準にはしていない。

| 項目 | 条件 |
|---|---|
| 行数と保存先 | 1万・10万行 × Memory・Storage |
| 主索引 / page / log上限 | 2MiB / 64KiB / 32MiB、Fixed growth |
| 親予算 / 保持履歴 / metadata上限 | 128MiB / 24MiB / 48MiB |
| 取引クエリ内worker上限 | 1 / 2 / 4 / 8 |
| query | Where付きCount、Sum、64群のGroupBy、64キーを外側に持つJoin |
| 版 | 1版。1万行では古いsnapshotを保持して7回更新する8版条件も追加 |
| 独立取引の同時数 | 1 / 2 / 4 / 8 |
| 混在要求 | 読取100 / 95 / 50%、一様 / 1%集中、Exclusive / Optimistic |
| 測定 | ウォームアップ後5反復、方式の順を巡回、自動の業務再試行なし |

クエリ本体のKernelと、取引開始・commit・disposeを含むEndToEndは別標本とする。8版の古いsnapshotは既に開始したKernelだけを測る。worker値は取引クエリの上限であり、実worker数や通常nativeの並列方式と同一ではない。取引本体の重なりはBegin後からCommit前までの読取・変更準備区間で数える。

## 発見した問題と修正

**Fact:** 修正前の10万行・Memory・1 workerでは、取引FLinqのGroupByは中央値2,609.2815ms、Joinは2,243.1945msだった。DBの予約ピークは35,899,264バイトと128MiBの親予算内だったが、私有退避のピークが6,907,744バイトあった。

`ScopedLookup.Add`は参照を含まない値も毎行128バイトと符号化長の4倍等で見積もり、配列内にある値の費用を過大に加算していた。workerの子予算とその3分の1の退避閾値を適用すると、小さな実データでも早くPersistedへ退避した。並列batchでも固定のInput配列に加え、同じ値の符号化長を毎行見積もっていた。

比較器があり、キーと要素がともに参照を含まない経路だけ、グループ管理費と要素Listの実capacityに基づく予約へ変更した。拡大では新旧配列が重なる分を先に予約し、差替え後に旧配列分を返す。batchのインライン値は固定配列の予約へ含める。親子予算・退避閾値・Persisted形式・比較規則・順序・取引寿命は維持する。参照を含む型や比較器なしの符号化キー経路は従来の保守的な扱いを維持する。

8MiBのquery予算・8,192要素・64群で、順序と結果を保ち、予算内で退避しない試験を追加した。修正前はworker上限1/3の両条件で572,992/575,040バイトの不要な退避を検出し、修正後は両条件とも退避0となった。小予算で本当に退避する既存条件も維持した。

## 改善後の結果

以下は10万行、取引クエリ内worker上限1、Kernel、5反復の中央値。割当量はプロセスのmanaged累積割当であり、同時常駐量ではない。

| 保存先 | query | 修正前 ms | 修正後 ms | 修正前割当 B | 修正後割当 B |
|---|---|---:|---:|---:|---:|
| Memory | GroupBy | 2,609.2815 | 17.8828 | 210,407,288 | 8,788,448 |
| Memory | Join | 2,243.1945 | 26.3101 | 167,961,760 | 8,755,480 |
| Storage | GroupBy | 2,615.0540 | 17.3855 | 210,406,944 | 8,788,448 |
| Storage | Join | 2,242.9071 | 23.2883 | 167,961,080 | 8,755,480 |

同じ改善後ビルドの10万行Memory、1 workerの値は次のとおり。通常nativeと取引でログ形式・可視性確認・予算管理が異なるので、差のすべてをロック費用とは呼ばない。Sum等の短い標本で取引側が通常nativeを下回る値もあり、単一の差を一般的な優位性とは扱わない。

| query | 通常native FLinq ms | 取引FLinq ms | 取引行を通常LINQで読む ms |
|---|---:|---:|---:|
| Sum | 12.1666 | 9.6483 | 21.0636 |
| CountWhere | 11.4770 | 10.1751 | 19.6557 |
| GroupBy | 11.2770 | 17.8828 | 24.0853 |
| Join | 20.5702 | 26.3101 | 42.7240 |

worker上限8では取引FLinqのGroupByは17.8937ms、Joinは23.8408msだった。この規模のGroupByでworker数に比例する高速化は観測していない。1万行MemoryのSumは1版4.3105ms・8版9.4566ms、割当は4,888・10,884,224バイトで、古い版を辿る追加費用も残る。

独立取引の同時数8、10万行、一様、読取50%・更新50%での成功取引数/秒の中央値は次のとおり。各worker64要求、各5反復。成功だけを数え、競合した業務処理は再実行していない。

| 保存先 | 方式 | 成功取引/秒 | 各反復p95の中央値 ms | 全標本の競合率 | 最大の本体重なり |
|---|---|---:|---:|---:|---:|
| Memory | Exclusive | 127,309 | 0.0932 | 0% | 1 |
| Memory | Optimistic | 247,666 | 0.0587 | 0% | 7 |
| Storage | Exclusive | 6,477 | 2.0686 | 0% | 1 |
| Storage | Optimistic | 25,697 | 0.6526 | 0.0391% | 4 |

これらは今回の要求列の結果で、ホットキーや長い業務処理の一般的な速度保証ではない。RNG、latency配列、モデル更新、重なり計測等のハーネス費用を含む。

## メモリと保存容量

10万行Memory・worker上限1のDB予約ピークは35,899,264バイト、通常nativeの予約ピークは15,487,104バイトだった。DBのピークは初期構築・checkpoint等も含み、純粋なquery作業領域の大きさとは呼ばない。Memory条件の私有退避ピークはworker上限1/2/4/8すべて0となった。プロセスの累積ピークworking setは117,608,448バイトで、Storeの予約会計とは別である。

10万行Storage・worker上限1の終了後ファイル長の合計は、通常nativeが10,168,698バイト、DB Storeが25,665,317バイト、DB WALが30,006,052バイトだった。取引側には版ヘッダー、共通確定、保存・復旧のmetadataが含まれる。終了後の論理ファイル長であり、測定中のピーク、物理ディスク割当、全体の書込量とは異なる。StorageのStore予算の一時領域ピークにはcheckpoint/WAL等も含むため、これをすべてLookupの退避と呼ばない。

## 検証と証跡

`Test-MvccComparison.ps1`は4条件、query 2,560標本、混在取引960標本、資源24標本を監査した。terminal receipt、manifest/生成ソース/DLLのhash、全条件の網羅、反復と順序巡回、独立期待値、成功・競合件数、予算、CPU枠返却を確認した。query値を1だけ壊した隔離コピーを独立期待値で拒否する対照試験も実施した。

改善後ビルドの入力789ファイルは監査時の現行入力と一致した。source manifest SHA256は`A3725CF2A8D183C5F1C0FF85527722AB0F286AEB757C717A27B5F4977892FC8F`、ProductTransactions DLLは`A47F0668EB805A272798E9DC6AD830397825A1DF4ABDF7043528C1AF40376032`。Tsavorite DLLは`F1A3B1C0300C7DEEBD09D35AC42EEFFEDB6854E88711B18796A24231DFBC0E32`。

証跡ルートは`F:/CodexTemp/Seamless/mvcc-comparison-20261103`。改善後は`matrix-results.json`と`comparison-audit.json`、改善前は`before-lookup/`に保存した。修正前に失敗した2条件は`Release-test-a25fc975d1ec4ca9aeed022f93223fca`。改善後の最終回帰は次のとおり。

| 対象 | Debug | Release |
|---|---:|---:|
| 共通取引 | 128 | 128 |
| DB取引 | 65 | 65 |
| Gatewayの版付き/共通確定・Native関連 | 65 | 65 |
| Execution / Lookup / 既存Store回帰 | 77 | 77 |
| TPC-E全件 | 41 | 41 |
| 合計 | 376 | 376 |

計752実行の失敗・スキップ・対象プロジェクトのビルド警告は0。現行の実行入力1,284ファイル、複数実行で重なる入力の一致、各TRXとreceiptを監査した。Lookupの修正検出2条件、実際に退避する小予算条件、全workerの終了障壁、Gatewayの12停止点・複数DDL/Native再構築・部分checkpoint4条件・通常TableからのNative/FLinq展開が両構成で実行されたことも確認した。証跡は`validation-latest.json`、`audit-regressions.ps1`、`final-regression-results.json`、`lookup-regression-results.json`、`tpce-regression-results.json`。性能比較の入力との差はREADMEの追記だけで、実行入力は一致している。

## この結果から判断できる範囲

通常nativeの演算融合を取引へ接続した効果と、独立取引を重ねる効果を分けて観測できた。不要な私有退避は修正した。一方、古い版を辿る配列コピー、native基準より大きい取引Lookupの割当、query内のworker配分は引き続き改善候補である。

今回の測定はログに収まるウォームデータであり、冷たいディスクI/O、メモリを超えるデータ、無期限の運転、専用履歴バックエンドとの比較を実施したものではない。Gateway・TPC-E・障害復旧を含む実装設計の全受入条件の完了とも区別する。共通の取引方式や既定Exclusive、明示Optimisticという合意は変更しない。

## 履歴アドレス修正後の再確認 — 2026-11-03

衝突キーの実アドレスを完了metadataから取得する修正後、同じ4条件を別のsnapshotで再実行した。query 2,560標本・混在960標本・資源24標本は独立監査を通り、791入力が監査時の製品ソースと一致した。証跡は`F:/CodexTemp/Seamless/mvcc-history-buffered-20261103/matrix-results.json`と`comparison-audit.json`。source manifestは`E8DD226D2DF2AF77C0FDF79EAD20A24D51DFD0592F6745CF7C710198C7D2DF0F`、製品比較DLLは`6871BB07A0AB806C85330B9D57A2445B61C75D16A966756A13F545698020013B`。

10万行・Memory・1 workerのKernel中央値は次のとおり。開始・終了を含む標本は別に測っているので、この表との差分を取引開始・終了の費用とは呼ばない。

| クエリ | 通常native FLinq ms | 取引FLinq ms | 取引通常LINQ ms | 取引FLinq managed割当 B | 取引通常LINQ managed割当 B |
|---|---:|---:|---:|---:|---:|
| GroupBy | 13.7810 | 20.1268 | 26.9023 | 8,788,416 | 45,949,056 |
| Join | 23.5731 | 28.1908 | 47.4677 | 8,755,448 | 87,679,024 |

Memory全条件で私有退避ピークは0、DB予約ピークは35,899,264バイト、通常nativeの予約ピークは15,487,104バイトだった。プロセス全体のworking setピークは162,988,032バイトで、owner予算やStoreの予約量とは同じ指標ではない。前回の固定標本との差だけで、コード変更による速度・working setの増減原因を断定しない。

この再確認は製品の共通取引を通す測定である。[専用履歴backendの比較](20261103-history-store-comparison.md)は共通WALを通しておらず、相互のappend時間・commit/sを取り違えない。
