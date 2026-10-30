# C12 実装・受入台帳

## 対象とソースの正本

2026-10-29 の改訂計画に対する実装。製品の正本は `G:\GitHub\Seamless`、TPC adapter と TPC-C の正本は `G:\GitHub\Seamless\Work\Orchestration\worktrees\tpcc`。専用 worktree の既存 Database と generator は置換していない。

作業開始・確認時の両 checkout の HEAD は `1846c8b70ed6411a4b273bbf5cf73316038e8bc8`。実装は未 commit の差分として保持し、push は行っていない。最終ソースの実体 identity は各 stage の SHA256 manifest と `product-sync-06.json` で識別する。

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
- TPC-C は4変種と P=1/P=2、複数 Store の部分 checkpoint を検証した。復旧で1変更を適用した直後に例外を注入して停止し、確定 checkpoint から開き直して全行・全索引・ITEM 複製の一致を確認した。TPC-E はネイティブ EGen の実更新と市場通知を共通 WAL に接続し、Persisted 実 Store の反復復旧で全表・全索引一致を確認した。
- 監査モードは通常モードと catalog identity を分け、読取りと正常 Rollback も記録する。TPC-E のネイティブ要求には codec と DLL SHA256 を保存する。

## 検証結果とコマンド

証跡は `Work/Orchestration/c12/<stage>/` の `commands.json`、`stdout.log`、`stderr.log`、TRX、`result.json`、`source-before.json`、`source-after.json`。全件 SourceUnchanged=true の合格実行を採用する。

| 対象 | Debug | Release | stage |
|---|---:|---:|---|
| 共通基盤・コレクション adapter・偽装置・プロセス終了 | 25 合格 | 25 合格 | bounded-core-debug-39 / bounded-core-release-40 |
| 型付き API・実 Store・既存同期 API・旧モード拒否 | 30 合格 | 30 合格 | mode-typed-debug-51 / mode-typed-release-52 |
| TPC-C 全回帰（C10 含む、境界追加前） | 62 合格 | 62 合格 | tpcc-final-debug-23 / tpcc-final-release-24 |
| TPC-C 最終新経路・監査・故障・独立区画並行実行・復旧途中停止 | 7 合格 | 7 合格 | interrupted-recovery-debug-55 / interrupted-recovery-release-56 |
| TPC-E 旧 runner 4変種・保存再 Open・サイズ設定 | 4 合格 | 4 合格 | tpce-runner-debug-43 / tpce-runner-release-44 |
| TPC-E 旧 v1 再実行・新旧ネイティブ出力・新経路復旧・ABI比較 | 4 合格 | 4 合格 | native-abi-debug-53 / native-abi-release-54 |

TPC-C 全回帰後に加えた変更は新しい SUT と共通基盤の境界に対するもの。既存 C10 は変更せず、最終新経路に対する7件を別途両構成で再実行した。復旧停止の観測用 callback は Benchmark adapter に置き、TPC-E が使う既存の4引数 constructor は維持した。TPC-E の最終照合は取引出力のバイト列と市場通知の全6 ABI フィールドを比較する。`TTradeRequest` は全長48 bytes、フィールドは0..43で、44..47は末尾 alignment。未初期化になり得るこの領域を業務出力として比較しない。フィールド差を検出し、末尾 alignment だけが異なる場合を許す独立試験も行う。

実行は `Run-Stage.ps1` から BGO を通した。各 stage は新しい `C:\tpcc-work\c12-<stage>` を使用し、既存データを上書きしない。キャッシュ済み NuGet を復元元にし、VSTest のローカル IPC のためホストコンテキストで実行した。コンパイルと試験に RAM 閾値と単一コア affinity を設定し、性能測定の結果としては扱わない。

再実行は既存 stage 名を再利用せず、上記スクリプトへ `-Name <新しい名前>`、`-Project <対象csproj>`、`-Configuration Debug|Release`、TPC 側では `-Root G:\GitHub\Seamless\Work\Orchestration\worktrees\tpcc` を渡す。ネイティブ試験は `-RequireEgen` を付ける。スクリプトが実行したコマンドの共通部は以下で、実際の filter と結果ディレクトリは各 `commands.json` に保持する。

```text
dotnet test <対象csproj> -c <構成> -m:1 -p:UseSharedCompilation=false --disable-build-servers -p:RestoreSources=C:\Users\kazuh\.nuget\packages -p:NuGetAudit=false --logger trx --results-directory <新しい証跡ディレクトリ> --filter <対象filter>
```

最終確認は全行合格、失敗0、スキップ0。`accepted-stages.json` に採用した stage と TRX 件数、構成、ソース不変確認をまとめる。ABI の不一致が出た `tpce-shared-release-50` は合格へ読み替えず、位置44の末尾 alignment に対する比較契約を修正し、独立試験を追加して53/54で再検証した。

## 同期と保全

`product-sync-01.json` から `product-sync-06.json` に、同期対象の送信元・宛先・更新前後 SHA256 を記録した。コピー前に宛先が前回同期から変わっていないことを検査し、製品 core の対象ファイルだけを同期した。

`final-source-identity.json` に最終ファイルの SHA256 を保持する。受入試験後は抽出した `TransactionChangeSet.cs` 末尾の余分な空行1行だけを除去した（BGO task `b189a623139d4fe98430b512646fd318`）。受入 manifest との差分がそのファイルだけであることを検査し、TPC 最終対象ファイルは受入時と一致することを確認した。最終 diff 検査はエラーなし。

`preserved-source-boundaries.json` は初期 TPC 試験の source manifest と最終確認時の manifest を比較する。Tsavorite Core の260ファイル、専用 worktree の既存 Database / generator、TPC-C の既存 Frames / Queries に変更がないことを確認する。新プロジェクトは各ソリューションへ登録し、focused solution はスクリプトから再生成した。主 checkout の別作業の Gateway・Query 等と、専用 worktree の C10・Core43・バックアップを巻き戻していない。

## 保証範囲と未採用の実行

偽装置では Sync 制御、Write/Sync/符号化失敗、背圧、排出を検証した。ファイル装置では CRC、長さ、LSN、世代、PAD、末尾修復、整理、応答済み sector 不再書込みを検証した。プロセス終了試験は Sync 後、通知・Dispose・checkpoint 前に子プロセスを `Environment.Exit(137)` で終了する。これは実電源断の認定ではない。

`native-recovery-17` の大規模 Tsavorite 永続 Store 反復復旧と、`tpce-final-debug-29` の全 Compliance 一括実行は長時間化したため停止した。両方とも未完了・Unavailable とし、合格件数に含めない。前者に代わる実 Store の全表・索引反復復旧は Persisted の新経路で完了した。後者に代わり、旧 runner の保存再 Open、v1 再実行と新旧ネイティブ出力を対象別に検証した。全 Compliance 一括の全件合格、Tsavorite＋大規模実 EGen 永続復旧の完了は主張しない。

HTTP/TDS 接続、同じ表の汎用取引並列化、他の分離レベル、TPC-E 区画並列化、旧形式の移行、C13/C14 の実 SUT 運転・監査・計測は今回の完了に含めない。WAL 復旧で市場への通知完了を意味させず、要求の自動再実行・重複排除も行わない。
