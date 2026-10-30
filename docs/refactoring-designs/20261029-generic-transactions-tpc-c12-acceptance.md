# C12 実装・受入台帳

## 対象とソースの正本

2026-10-29 の改訂計画に対する実装。製品の正本は `G:\GitHub\Seamless`、TPC adapter と TPC-C の正本は `G:\GitHub\Seamless\Work\Orchestration\worktrees\tpcc`。専用 worktree の既存 Database と generator は置換していない。

確認時の主 checkout の HEAD は `481fe149f469751d654588de67740684f2d6d90b`、専用 worktree は `1846c8b70ed6411a4b273bbf5cf73316038e8bc8`。C12 の変更は差分として保持し、この作業では commit・push を行っていない。主 checkout の別作業による統合と C12 のソース identity は分け、各 stage の SHA256 manifest、`product-sync-06.json`、`final-native-source-identity.json` で実体を識別する。

製品に `Seamless.Transactions` と `Seamless.Transactions.Collections` を追加し、Database と generator を接続した。TPC-C は `Benchmark.TpcC.Server.TpcCPartitionedSut`、TPC-E は `Benchmark.TpcE.Compliance.TpcETransactionalSut` を追加した。既存 C10・TPC-E v1 runner は追加経路と分離して維持している。

## 確認した契約

- 宣言済み Table から型付き取引ビューを生成する。利用側の文字列登録は不要。同じデータ本体へ結び付き、変更集合だけを収集する。実体化ビューの取引ビューは読取専用。
- Serializable の初版として汎用 DB は全体排他、TPC-C は関係区画の所有権を使う。独立区画の実エンジン Payment は両方とも Sync 完了前に実行でき、応答は永続化を待つ。単一取引ビューの同時共有を拒否する。
- 子 Commit は Voted、未 Commit の破棄・子 Rollback は全体中止。未終了の子を持つ親の確定、迂回アクセス、不正な新旧混在を拒否する。旧同期 Commit は確定要求、Dispose が確定する。
- WAL 受付後のキャンセルで未実行へ戻さない。Sync 後の本体反映・後始末失敗は Committed を保ち、障害停止する。正常 Rollback の確認は運転中の有限キャッシュに置き、再起動後に記録がなければ Unknown。
- 結果台帳、forget watermark と保持台帳を保存する。整理後にも更新結果を照会でき、明示削除した履歴を復活させない。保持範囲より古い checkpoint と、整理済み WAL に対する結果台帳欠落を拒否する。
- WAL は長さ・CRC・版・catalog・codec・LSN・PAD 境界を検証する。最後の不完全 suffix だけを修復する。起動世代を切り替え、応答済み sector を書き直さず、一世代遅延で整理する。
- キューの件数・バイト数と符号化済み変更を共通予算へ計上する。永続取引の符号化は各表と取引全体の残りフレーム容量で制限する。既定の追加収集待ちは 0ms。
- 終了は受付停止・要求と受付済み checkpoint の排出・Sync・所有スレッド終了・資源破棄の順。障害ラッチは実行・応答許可・保存に共通。SUT を迂回した更新で Observer が失敗した場合も checkpoint を拒否する。
- TPC-C は4変種と P=1/P=2、複数 Store の部分 checkpoint を検証した。復旧で1変更を適用した直後に例外を注入して停止し、確定 checkpoint から開き直して全行・全索引・ITEM 複製の一致を確認した。TPC-E はネイティブ EGen の実更新と市場通知を共通 WAL に接続し、Tsavorite と Persisted の実 Store を繰り返し開き直して全表・全索引と確定結果 ID の一致を確認した。
- 監査モードは通常モードと catalog identity を分け、読取りと正常 Rollback も記録する。TPC-E のネイティブ要求には codec と DLL SHA256 を保存する。

## 検証結果とコマンド

証跡は `Work/Orchestration/c12/<stage>/` の `commands.json`、`stdout.log`、`stderr.log`、TRX、`result.json`、`source-before.json`、`source-after.json`。全件 SourceUnchanged=true の合格実行を採用する。

| 対象 | Debug | Release | stage |
|---|---:|---:|---|
| 共通基盤・コレクション adapter・偽装置・プロセス終了 | 25 合格 | 25 合格 | bounded-core-debug-39 / bounded-core-release-40 |
| 型付き API・実 Store・既存同期 API・旧モード拒否 | 30 合格 | 30 合格 | mode-typed-debug-51 / mode-typed-release-52 |
| TPC-C 全回帰（C10 含む、境界追加前） | 62 合格 | 62 合格 | tpcc-final-debug-23 / tpcc-final-release-24 |
| TPC-C 最終新経路・監査・故障・独立区画並行実行・復旧途中停止 | 7 合格 | 7 合格 | interrupted-recovery-debug-55 / interrupted-recovery-release-56 |
| TPC-E 旧 runner 4変種・保存再 Open・既定／明示サイズ設定 | 8 合格 | 8 合格 | tpce-legacy-debug-83 / tpce-legacy-release-84 |
| TPC-E 旧 v1 再実行・新旧ネイティブ出力・新経路復旧・ABI比較 | 4 合格 | 4 合格 | native-abi-debug-53 / native-abi-release-54 |
| TPC-E Compliance 全件（実 EGen、Tsavorite／Persisted 反復復旧を含む） | 37 合格 | 37 合格 | compliance-all-debug-86 / compliance-all-release-87 |

TPC-C 全回帰後に加えた変更は新しい SUT と共通基盤の境界に対するもの。既存 C10 は変更せず、最終新経路に対する7件を別途両構成で再実行した。復旧停止の観測用 callback は Benchmark adapter に置き、TPC-E が使う既存の4引数 constructor は維持した。TPC-E の最終照合は取引出力のバイト列と市場通知の全6 ABI フィールドを比較する。`TTradeRequest` は全長48 bytes、フィールドは0..43で、44..47は末尾 alignment。未初期化になり得るこの領域を業務出力として比較しない。フィールド差を検出し、末尾 alignment だけが異なる場合を許す独立試験も行う。

実行は `Run-Stage.ps1` から共通 BGO を通す。現在の stage データ・TEMP/TMP は新規の `D:\SeamlessTestData\C12\c12-<stage>`、CLI home・NuGet package／HTTP cache は同領域の `RuntimeCache` を使う。BGO state は `D:\CodexBackgroundOps`、file transport の専用 mailbox は `D:\SeamlessTestData\C12\BgoMailbox`。インストール済みツールと既存 NuGet feed の C パスは読取り元で、作業用出力は D とする。VSTest のローカル IPC のため通常ユーザーのホストコンテキストで実行し、RAM 閾値・単一コア affinity を設定している。以下の時間を性能測定結果としては扱わない。過去 stage の実行時 C パスは証跡に保持し、そのデータは下記の D/F へ移行済みである。

再実行は既存 stage 名を再利用せず、上記スクリプトへ `-Name <新しい名前>`、`-Project <対象csproj>`、`-Configuration Debug|Release`、TPC 側では `-Root G:\GitHub\Seamless\Work\Orchestration\worktrees\tpcc` を渡す。ネイティブ試験は `-RequireEgen` を付ける。スクリプトが実行したコマンドの共通部は以下で、実際の filter と結果ディレクトリは各 `commands.json` に保持する。 全件実行は `-Filter` を渡さず、`dotnet test` にも `--filter` を付けない。下記の filter 指定は対象を絞る回帰の場合だけ追加する。

```text
dotnet test <対象csproj> -c <構成> -m:1 -p:UseSharedCompilation=false --disable-build-servers -p:RestoreSources=C:\Users\kazuh\.nuget\packages -p:NuGetAudit=false --logger trx --results-directory <新しい証跡ディレクトリ> [--filter <対象filter>]
```

最終確認は全行合格、失敗0、スキップ0。`accepted-stages.json` に採用した stage と TRX 件数、構成、ソース不変確認をまとめる。ABI の不一致が出た `tpce-shared-release-50` は合格へ読み替えず、位置44の末尾 alignment に対する比較契約を修正し、独立試験を追加して53/54で再検証した。

## 同期と保全

`product-sync-01.json` から `product-sync-06.json` に、同期対象の送信元・宛先・更新前後 SHA256 を記録した。コピー前に宛先が前回同期から変わっていないことを検査し、製品 core の対象ファイルだけを同期した。

`final-source-identity.json` に最終ファイルの SHA256 を保持する。受入試験後は抽出した `TransactionChangeSet.cs` 末尾の余分な空行1行だけを除去した（BGO task `b189a623139d4fe98430b512646fd318`）。その時点の変更は受入 manifest と比較して識別した。追加の TPC-E 物理サイズ指定・検証コードは86/87の同一ソースで受け入れ、主 checkout の C12 対象49ファイルと、専用 worktree の既存保全範囲は `final-native-source-identity.json` で再確認する。

`preserved-source-boundaries.json` は初期 TPC 試験の source manifest と最終確認時の manifest を比較する。Tsavorite Core の260ファイル、専用 worktree の既存 Database / generator、TPC-C の既存 Frames / Queries に変更がないことを確認する。新プロジェクトは各ソリューションへ登録し、focused solution はスクリプトから再生成した。主 checkout の別作業の Gateway・Query 等と、専用 worktree の C10・Core43・バックアップを巻き戻していない。

## 大規模実 EGen 検証の条件と運用

顧客1,000、初期取引1営業日、MarketDays=1305、NewsItemBytes=100000を維持し、入力・取引・全表／全索引の照合回数を減らしていない。`HarnessWriteTransactions` だけで58回の全 DB ダイジェストを実行する。全 stage の壁時計時間は Debug 7923.89秒、Release 6035.70秒で、データ移行などの同時稼働条件も異なるため速度比較には用いない。

旧4 KiB segment では復旧用 Store に113,680ファイルを観測し、途中停止した全件実行では約66,500ハンドルと LocalStorageDevice.Dispose のスタックを観測した。新規試験領域には `TpcEScale.TsavoriteSegmentBytes=16*1024*1024` を明示する。表／索引の segment だけを指定し、log memory の予算は変えない。省略時の従来値と null の JSON 省略を維持し、既存領域は作成時と同じ値で開く。自動変換は行わない。3回の再 Open 時の Tsavorite Store ファイル数は Debug [354, 627, 900]、Release [354, 627, 900]。

開始・終了・CPU時間・ハンドル・使用メモリ・checkpoint／再 Open は `tpce-phases.jsonl` に記録する。EGen の CLI 子プロセスは CreateNoWindow=true。BGO IPC 障害の根本原因を特定済みとは扱わず、正式 D state と専用 D mailbox を使い、大容量 F アーカイブ作成と全件試験の同時実行を分離した。CPU枠はDebug全件終了後に先行限定実行へ引き渡し、その終端確認後にReleaseを通常の調停順で投入した。相手側の試験結果はC12の合格件数に含めない。

## C 作業領域の移行

`C:\tpcc-work` の321領域、297,731ファイル、元ファイル容量212,303,876,832 bytes（約197.72 GiB）は、全ファイルSHA256照合後にC側を削除し、空の親も除去した。

| 保存先 | 対象 |
|---|---|
| `D:\tpcc-work` | Store／WAL／SQLiteを含む再利用可能な65領域、208,530,897,763 bytes |
| `F:\SeamlessTestData\historical-tpc-20261029\historical-cold-*` | HDDでよい208領域、1,401,287,026 bytesを領域別ZIP＋SHA256 manifestで保存 |
| `D:\SeamlessTestData\C12` と `F:\SeamlessTestData\C12\archive-20261029` | C12所有の48領域、2,371,692,043 bytes。実行用／保存用の区分は `data-relocations-20261029.json` に保持 |

`storage-completion-20261029.json` が現在の保存先・元データ不存在・manifest・削除receiptをまとめる。コピー／照合と削除を分け、対象一覧をハッシュで固定し、Store／SQLiteの非使用を確認して、個別ファイルと空ディレクトリだけを除去した。中断処理の未完成ZIP約5.11 GBも、Dの完全な代替を確認して削除した。物理ドライブ全体の空き容量の変化を、この作業だけの回収量とは扱わない。旧コマンド中のCパスは過去の実行証跡であり、今後の再実行はDの入口を使う。

## 保証範囲と未採用の実行

偽装置では Sync 制御、Write/Sync/符号化失敗、背圧、排出を検証した。ファイル装置では CRC、長さ、LSN、世代、PAD、末尾修復、整理、応答済み sector 不再書込みを検証した。プロセス終了試験は Sync 後、通知・Dispose・checkpoint 前に子プロセスを `Environment.Exit(137)` で終了する。これは実電源断の認定ではない。

途中停止した17/29と、BGO IPC 障害で中断した61/72/81/82は合格へ数えない。条件を維持した全件実行86/87は、実 EGen を必須にして Debug／Release 各37件、失敗0・スキップ0・SourceUnchanged=true で完了した。`native-completion-20261029.json` が各 TRX、全ケース、段階計測と native binary identity をまとめる。

HTTP/TDS 接続、同じ表の汎用取引並列化、他の分離レベル、TPC-E 区画並列化、旧形式の移行、C13/C14 の実 SUT 運転・監査・計測は今回の完了に含めない。WAL 復旧で市場への通知完了を意味させず、要求の自動再実行・重複排除も行わない。
