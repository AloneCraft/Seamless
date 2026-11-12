# 遅延更新 MVCC の製品実装設計

## 背景

2026-11-02 の利用者との合意を、既存コードに接続するための設計案。並行方式は遅延更新＋MVCC、楽観的検証、Serializable とし、即時方式は比較実験に残す。通常の読み取り・更新と別行を更新する複数取引を並行実行する。集計中の物理更新待ちは許容する。性能は並行実行時の全体性能を優先し、FLinq のネイティブ走査と演算融合を活かす。

履歴予算不足時は、履歴保持を妨げる古い読み取り専用取引を明示的なエラーで中止する。実行中アクセスの終了前に保持を解放しない。TPC-E・Gateway の取引契約を共通化し、前レコード情報は当面接続層向け API に限定する。業務処理の自動再実行と既存領域の自動移行は行わない。

本書は現在の実装と設計理由を扱う。対応範囲は次の表、MV01～MV42の具体的な試験と検証の限界は[受入証拠対応](20261104-mvcc-acceptance-evidence.md)で区別する。コードと試験に基づく Fact、設計判断の Decision、守る条件の Invariant / Guardrail を区別する。初版の履歴回収は、新しい取引を待たせて既存の参照がなくなってから整理する案 A とする。回収中の新規取引の待機は利用者と合意済み。

## 現在の実装範囲 — Fact

| 段階 | 現在の範囲 |
|---|---|
| 生成器の接続 | 共通生成ソースによる解析用Compilation、同一コンパイルでのDB型とFLinq展開、using/await usingの寿命を実装。独立queryとif/while/do/switchの条件内queryをTask/ValueTaskのasync所有メソッドへ接続。条件は元位置で実行し、移動したBeginにもreadonly判定を反映。全体の無条件な再構成は避ける |
| 共通の版管理 | 明示Optimistic、ReadVersion、キー/不存在/表走査の依存、私有変更、検証、受付順、WAL、全参加先設置後のprefix公開を実装。既定Exclusiveと旧participant経路を維持 |
| Tsavorite接続 | `HistoryStore` / `VersionedTransactionalTable`。コピー更新、native前リンクのキー照合、論理削除、世代付きアドレス、Memory/Storage、版付き保存形式の拒否・復元を実装 |
| 回収と寿命 | 新規受付待ち、古い読取専用のHistoryPressure中止、実行中operationの排出、handle閉鎖、削除head整理、Compact、共通保存、再取得を実装。停止・障害状態を解除しない。中断中の列挙とGateway実行contextの作業予約も取引側で解放。完了callbackの登録・解除・一度だけの実行を予算化し、解放失敗でも後続の解放を続ける。共通保存失敗は全参加先へ通知し、回収中のグループも停止状態で廃棄できる |
| 論理DBと世代 | `VersionedTransactionParticipantGroup`で複数の型付き表/世代を一つの論理DBに登録。競合キーを世代/表prefixで分離し、WALはDB/表/codecで復旧先を照合。走査依存は保守的に論理DB単位。全参照排出と共通保存後の所属解除を`RetireVersionedParticipantsAsync`で実装。Gatewayの版付きカタログを共通scopeへ接続済み |
| DB/FLinq | 型付き開始、版付きDBのExclusive再オープン、`TransactionRows.Iterate`、既知の呼出し先を含む読取専用開始の静的判定を実装。通常のキー付きTable/MaterializedViewも`DatabaseRows`で所有Gateway取引または従来方式の読取保護へ自動接続。Nativeの独立ビルドにも設定済みFLinq analyzerのcompiler契約を渡す。不明な経路は更新可能。外部で開始された通常取引をdowngradeしない。対応しない型・演算列・制御構造は既存の実行経路または既存の診断に残す |
| 生成コードの説明 | アクセス判定、元ソース、既知/不明の理由、変形の前後、各文の処理、並列・キュー・資源・同期・不変条件をコメントへ出力。Debugでは静的計画定数と状態表示を追加 |
| CPUと全件件数 | host共通CPU枠、native pending待ちの枠返却、callback復帰を実装。callbackのないTransactionRowsのCount/LongCountをpage境界の有界chunkで並列走査。worker独立状態、全worker排出、予算不足時のworker削減/逐次fallbackを接続 |
| 結果と初期投入 | TransactionRowsのToArray/List/Dictionaryの結果構築を共通予算へ接続。生成ImportTablesAsyncで通常表と実体化ビューを一括確定し、入力例外・取消・明示した親取引への合流・Storage再オープンを検証。未公開の版付き候補には専用writerで初期行をnativeへ逐次投入し、基準版とsnapshotを保存する。使用後・復旧後の再投入、途中放棄した候補の利用を拒否 |
| Join/GroupBy | 取引行のGroupBy集計投影と、取引行同士のJoinを分割Lookupへ接続。Array/List/Dictionary、Count/LongCount/Any/First/Last/Sum、既定値引数なしのFirstOrDefault/LastOrDefaultに接続。前段のWhere/Select、後段のWhere/Select/Skip/Takeと、group投影前へ正規化されたSkip/Takeを扱う。producer上のcallback、有界queue、worker専用Lookup、予算付きPersisted退避、初出順/一致順の合成を実装。末尾取得は選ばれた値だけを投影する。型名を生成できる逃避group・非集計投影は予算付き所有配列へ接続。未知の比較/codecは逐次格納へ戻す。複数OrderBy/ThenBy・恒等キー・比較器factoryにも接続。未対応の型/hoist/演算列は判定情報を生成コメントへ残し、既存の実行経路/診断へ接続する |
| TPC-E | `VersionedTpcEDatabase`/`VersionedTpcDomain`で全33主表・18索引の見出し/chunkを共通scope/WAL/公開へ登録。明示比較variantのVL(標準LINQ)とVF(FLinq)を追加。driverの継続位置を行と同じprepared WALで確定し、初期投入・保存・再オープン・強制終了からの復旧を接続。既定TL/TF/PL/PFとallの選択は維持。41件の製品試験と、固定snapshotでの同条件性能比較を分離して扱う。現在の最終実行は受入状況と証拠対応を参照 |
| Gateway共通確定 | 現行のDBロック経路・初期世代公開・ID/rowversion予約を`TransactionCoordinator.CommitExternalAsync`へ接続。同じ取引ID・prepared WAL・metadataで耐久化し、LSNを受け取るadapterが全DBの反映後にDDL/Nativeを公開する。ID予約は業務rollbackでも戻さない独立取引のまま。最終commit未受付の準備失敗・取消は個別中止とし、健全なjournalと次の取引を停止しない。既定の現在値profileの経路として維持 |
| Gateway版付きの接続基盤 | 内部bindingによる共通coordinator所有、世代の全表一括登録、通常のdb.TableとFLinq用TransactionRowsを同じscopeへ接続。私有seedの逐次構築・封印、実際の生成DBのliveコピーと共通group登録、検証値付き共通checkpoint/WAL復旧を実装。独立metadata確定は、活動中の更新可能な親を保持して回収待ちを迂回でき、親のrollbackに合流しない。SQL/Native受付、版付き公開カタログ、保存点での世代復帰、共通WAL復旧、ID/rowversion独立予約を接続。整数主キーの点更新は異なる行の並行確定を検証。共通checkpoint後の旧世代・Native計画の退役、Store/予算の解放とファイル回収を実装。12停止点の強制終了、部分checkpoint4条件、履歴圧迫、並行方式切替えの復旧を検証 |
| 製品の代表測定 | `Invoke-MvccComparison.ps1`で1万/10万行×Memory/Storage、1万行の8版保持、通常native/取引/BCL LINQ、query worker上限1/2/4/8と独立取引同時数1/2/4/8を分けて各5反復。2,560 query・960 mixed・24資源標本を独立監査した。不要な私有Lookup退避を修正し同条件を再測定。結果と限界は[比較報告](20261103-product-mvcc-comparison.md)。旧96セルprobeと、256行ChurnのDebug/Release各512周期は別の証拠として維持。現在のGateway・反復回収・実プロセス終了の実行証拠は受入状況と証拠対応へ分ける |
| 受入状況 | 現行のDB81・共通取引166・Execution84が両構成で成功し、662実行と894入力を強制ビルド記録付きで監査済み。Gatewayは両構成361成功/外部比較4skip。256行Churnは両構成Memory/Storageの計8,192変更を監査済み。TPC-Eも両構成41件が成功。5プロジェクトの計1,466成功/外部比較8skip/失敗・ビルド警告0、1,349入力・36プロジェクトの一致を最終監査済み。詳細は受入証拠対応。NUMA高度化と専用履歴backendの製品採用は今回の実装範囲に含めない |

個々の段階の試験成功を、全体受入の代用にしない。検証は64行程度の小さい条件を中心に、Dの隔離snapshot/実データ、Fのreceipt/TRX/ログ、共通8論理CPU以内で行う。検証runnerは `TestSeamless/Invoke-MvccValidation.ps1`。

製品の代表測定は[実行方法](../../Benchmark/Experiments/ProductTransactions/README.md)に従う。入力は既定1万行、最大10万行とし、初期投入は旧probeでは4,096行、同条件比較では1,024行ずつに分けて依存情報のピークを抑える。測定のために単一取引のmetadata予算を引き上げない。取引セルの後に行う保存値の照合と回収は、そのセルの時間・割当から除く。全体プロセスのworking set、共通予算の予約ピーク、退避容量を別の数値として記録し、予算上限をCLR全体のworking set上限と呼ばない。方式と反復の実行順、JIT、キャッシュ、短い標本の影響があるため、この代表測定だけで性能の採否を確定しない。

分割Lookupは入力を全件配列へ複製せず、固定長batchをworkerごとの容量1のqueueへ渡す。各workerは独立したscopeとLookupを持ち、子予算で格納する。参照を含まない既知の値はPersistedへ退避できる。文字列キーは最初のキーのインスタンスを予算付きで保持し、退避後にも同じキーを返す。参照を持つ要素や未知のobject graphは退避で複製せずメモリ予算内で保持し、不足時は上限エラーを返す。安全性が不明な比較/codecはproducer上の逐次格納にする。GroupByは初出順、Joinは元の外側順と一致する内側順を保つ。キー/要素selectorとJoinの結果selectorはproducer上で呼び、集計は元のグループ投影式の評価位置を維持する。複数の集計callbackを行ごとに並べ替えず、数値の部分和を再結合しない。解析済みの集計には借用groupを渡す。型名を生成できる非集計投影とgroupを返す終端には、所有配列へ移したgroupを渡し、閉じるLookupを結果に保持させない。

TPC adapterでは既存の`Log.Begin/Commit/Rollback`を実行contextごとの共通scopeへつなぐ。主表hookによる索引の見出し/chunk更新も同じscopeに入る。索引hookの失敗では取引全体を中止し、途中の主表/索引変更を確定させない。取消は共有行へundoを書き戻さず私有変更を捨て、既存observerには逆順の取消通知を返す。並行取引で使うobserverは呼出し側が並行通知に対応させる。初期投入の`BeginBulkLoad/EndBulkLoad`も一つの取引にし、共通の私有変更で更新をまとめるため従来のscope外BulkCacheは使わない。型付き`Rows`は`TransactionRows`なのでFLinqのnative経路を維持できる。索引のInlineArrayは全32要素をunmanaged codecで保持し、field一つだけの符号化にしない。codecの識別子にはfield順序、layout、pack、InlineArray長とendiannessも含める。主表の保存形式はcodec識別子付きの新規領域で、既存variantを暗黙に切り替えない。保存は`Domain.CheckpointAsync`を通し、通常の`Store.Save`による共通境界を外れた保存を拒否する。Memoryは業務WALを作らず、Persistentは共通WALとLSN/CommitVersionのcheckpointから復元する。

## 現状の観察 — Fact

Gatewayの版付き接続では、論理DBを安定した参加先とし、その配下に型付きの表/世代を束ねる。子の所属は親が照合し、競合キーは世代・表のprefixと業務キーを組み合わせる。表走査の依存は初版では論理DB単位の安全側の上位集合にする。WAL復旧はpartition/table/codecを照合して子へ振り分け、別の世代のcodecで読み替えない。親の回収は全子を同じ静止境界で閉じ、共通保存後に再取得する。メンバー管理は親予算へ計上し、回収中の追加は拒否する。Gateway側の世代保持とsnapshot選択も共通scopeへ接続した。参加先groupの単体試験に加え、後から読むDB・DDL保存点・Native公開・実プロセス終了の製品試験で照合する。

Gateway接続の実装手順は、最初に既存のDBロックとprepared WALの形式を保ちながら共通Coordinatorの確定境界へ接続し、その後に版付き行と世代snapshotを接続する。外部で分離性を確立した変更集合を明示的な入口から受け取り、同じ取引ID・metadata・一つの共通WALで確定する。反映には耐久化済みLSNを渡し、全DB反映後にDDL/Nativeを公開する。確定だけの共通gateを取引開始時から保持しない。この既存方式に加え、明示した有限TransactionHistoryを持つ版付きGatewayを、同じjournalのCommitOwnedAsyncと版付きカタログ/Native公開へ接続する。

`Invoke-MvccCrash.ps1`は、64行のdurable commitが返った直後にプロセスをfinally/Dispose/checkpointなしで終了し、独立した次プロセスで全キー・値をWALから復元する試験を実装した。正常再オープン、確定後のプロセス終了、確定途中の障害注入を別の検証範囲として扱う。

GroupBy集計投影の後の`Take(int)`を分割Lookupへ接続した。単純な引数を一度評価し、0以下なら入力・selector・Lookup・workerを開始しない。正数では入力のグループ構築を完了してから要求件数だけ投影し、残りの集計callbackを呼ばない。Take/Skip引数と比較器式は元の呼出し順で一度保存してから空入力判定を行う。未対応の型・hoist・演算列には既存の実行経路/診断を使う。負数・0・一部・全件を通常LINQと比較し、callbackの回数と順序も検証する。

製品プローブはMemoryに加えてStorageを選択できる。結果v2へ保存方式とworker当たりの取引数を記録し、Storageの通常再オープンで行数・更新後の総和を検証する。小規模Persistentの成功を、長期運転・プロセス障害・Gatewayの全体受入へ流用しない。

初期投入は、生成する`ImportTablesAsync(source,cancellationToken)`から共通取引を一つ開始し、通常表と実体化ビューへの追加を同じ確定単位へ入れる。既存の同期`ImportTables`は単独SharedV2でこの入口の完了を待つ。既存キーへの追加は従来のAddと同じく無視し、全件配列や自動置換を導入しない。入力の例外・取消・確定時の競合では全参加先を中止する。既存取引へ合成する入口は明示した`TypedTransaction.ImportTablesAsync`で、子として合流する。DB入口の暗黙の合流は導入しない。ビューの変更能力は生成器用のprotected取込入口へ限定し、通常の型付きビューは読取り専用を維持する。これは既存ビュー定義にない自動再集計を追加する設計ではない。Gatewayの投入は共通Gateway domain接続で扱う。

**同期取込の既存API互換 — Decision（2026-11-04）:** 上記のDB入口の規則は新しい非同期取込についての規則である。既存の`BeginTransaction()`内で同期`ImportTables`を呼ぶ契約は維持し、従来の変更集合へ追加する。同期APIを無条件に非同期の開始へ転送すると既存scopeを拒否するため、生成器向けprotected状態検査で既存の同期writerを区別する。取引外のSharedV2では従来どおり取込全体の取引を開始する。新しい非同期APIによる処理合成はTypedTransaction側の明示入口を使う。Gateway全体回帰で古い取引外拒否の期待を検出し、期待を現行仕様へ修正した後、既存同期取引内での取込が`A legacy/read scope is already active`になる互換性不具合を確認した。独立取込と同期取引内の取込に別キーを使い、型付き表と事前登録表の共通確定・取消・再openを検証する。

生成器の接続は `Shared/Database.Generation` の共通生成処理から解析用 Compilation を作り、同じビルドで生成される取引ビューを FLinq が認識する。DB 生成器の公開型と生成ソースの所有者は維持し、解析用の型を FLinq の出力へ重複追加しない。取引を持つ非同期メソッドは本体を無条件に再構成せず、独立したquery文を持つTask/ValueTaskから接続する。再構成先にもasync修飾、using / await using、修飾子、型付きの省略引数を維持し、取引開始のreadonly判定を同じ境界解析から転送する。短絡や例外順序を変える条件式の先行評価は行わない。

改訂計画では、同じコンパイルでの生成・実行、using の解放、複数業務処理の一括確定・取消を最初の段階で検証する。続いて2表・少量データで MVCC の点操作と公開を通し、走査・回収、展開時解析、並列 Join / GroupBy、TPC-E / Gateway の順に接続する。各段階の成功を、未実装の後続段階の受入へ流用しない。

| 対象 | 現在のコードと接続上の意味 |
|---|---|
| [TransactionCoordinator](../../Seamless.Transactions/TransactionCoordinator.cs) | 既定Exclusiveはroot終了までgateを保持。明示Optimisticはsnapshotと依存を登録し、準備後の短い順序境界で検証。WAL受付・耐久化・設置・公開を検証lockの外で進める |
| 同 `TransactionScope` | 同一取引への同時操作を `Busy` で拒否。合流型ネスト、保存点、親子の操作順、未確定破棄時の取消を持つ |
| [TransactionalTable](../../Seamless.Transactions.Collections/TransactionalTable.cs) | 現在値の read/write/remove delegate と変更集合を合成。旧版を選ぶ読み取り版は持たない。列挙は `IEnumerable` で行ごとに `scope.Use` を呼ぶ |
| [TransactionChangeSet](../../Seamless.Transactions.Collections/TransactionChangeSet.cs) | メモリ上限と一時索引への退避を持つ。保存点用の独立コピーと、履歴位置を使った復帰の仕組みがある |
| [WalWriter](../../Seamless.Transactions/WalWriter.cs) | 有界 queue と専用 pump、複数 frame の Sync、耐久化 receipt を持つ。受付待ちは非同期になり得る |
| [PreparedTransactionJournal](../../Seamless.Transactions/PreparedTransactionJournal.cs) | 準備チャンクと最終 commit を結ぶ。`CommitAsync` 全体を gate で直列化する。試作の単一 frame WAL と同じ費用ではない |
| [DatabaseTransactions](../../Seamless.Database/DatabaseTransactions.cs) | 旧SharedV2 profileと、TransactionHistoryで明示した版付きprofileを区別。版付きは新規領域、versioned adapter、静止境界の共通checkpointを使う。既存領域を自動変換しない |
| [GatewayTransactionManager](../../Seamless.Database.Gateway/Hosting/GatewayTransactionManager.cs) | 複数 DB の準備結果と世代 metadata を一つの WAL commit に束ね、全 DB 適用後に公開する。DB ごとに独立 COMMIT する構造ではない |
| [LogicalTransaction](../../Seamless.Database.Gateway/Hosting/LogicalTransaction.cs) | セッション所有権、複数 DB、保存点、非公開世代、Native クエリ世代を保持 |
| [TPC の TableCore](../../Benchmark/Benchmark.Tpc.Core/Storage/TableCore.cs) | `TxnLog` が本体の即時変更と Action undo を管理。書込み hook と observer によって索引・診断が接続されている |
| [TsavoriteTable](../../Benchmark/Benchmark.Tpc.Core/Storage/TsavoriteTable.cs) | 単一の `Set` handle を所有し、`Rows` の具体型が FLinq の起点。更新は Remove / Add。これをそのまま MVCC 書込みには使わない |
| [Tsavorite Compact](../../Seamless.Collections.Tsavorite/Stores/CollectionStore.cs) | `CompactCore` は全 accessor と parallel region の終了を要求する。最新値をコピーして旧領域を再利用可能にする。永続ファイルの解放は次の共通 Store 保存確定まで遅れる場合がある |
| [FLinq の TsavoriteParallelSetRoot](../../Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments/Tsavorite/TsavoriteParallelSetRoot.cs) | `.Iterate(ref strategy)` を生成できる。現在の `Contains` の現在値最適化や件数の仮定を、取引ビューへそのまま移せない |

調査時の製品コードには多数の未コミット変更がある。これらを破棄・置換せず、実装時は改めて差分を確認する。[試作結果](TransactionModesResults.md) の Debug / Release 各59件と性能628セルを、製品接続後の合格証拠にしない。

調査対象21ファイルの SHA256、観測時点、Git HEAD は `F:\CodexTemp\Seamless\DeferredMvccDesign\20261102-17c6fd114c3d49748ced1f203df93a52\source-hashes.json` に保存した。これは未コミット変更を含むコード調査の証跡であり、Git HEAD 全体のビルド・試験結果ではない。

## 過去設計から引き継ぐ仕様

- [C12 汎用基盤](20261029-generic-transactions-tpc-c12.md): 型付き明示ビュー、合流型ネスト、保存点、値の所有権分離、結果照会、受付後の取消境界、WAL と checkpoint の整合。
- [Gateway 型付き DDL](20261101-gateway-typed-ddl.md): 同一 Gateway 内の複数 DB 一括確定、接続所有権、世代検査、非公開 candidate、DDL / DML / Native の公開境界、ID 予約、collectible 型と codec の寿命。
- [並行化の合意](ConcurrentTransactions.md) と [採用試験の合意](TransactionModesAcceptance.md): 遅延方式、集計中の待機許容、全体性能優先、履歴不足時の古い読取取消、共通層への統一、高度 API の対象範囲。

Gateway の既存の複数 DB 原子性を弱めない。従来文書の「対象 DB の範囲で調整」は、単独 DB ならその DB、Gateway なら**同じ WAL と一括公開を共有する参加 DB 群（commit domain）**として具体化する。独立した domain の取引を一つの全体排他へ集約しない。

## 目的

目的は、既存の取引意味論を保ちつつ、独立した取引の読取り・計算・変更収集・耐久化待ちを並行させること。保存先内部の短い追記区間と公開は必要に応じて調停する。

## 非目的

初版の非目的は、無待機の全走査、分散取引、一般向けの長期履歴照会、古い形式の自動変換、同じ取引への複数の公開操作の同時実行、即時 MVCC の製品実装。集計中の更新待ちの許容を、無制限の回収停止の許容へ読み替えない。

## 提案設計

### 責務と依存方向 — Decision

| 配置 | 責務 |
|---|---|
| `Seamless.Transactions` | commit domain、scope の状態、snapshot 登録、依存関係の検証、予約、WAL への受付順、公開済み番号、結果、保持の調整、中止理由 |
| `Seamless.Transactions.Collections` | 型付きの変更集合、自己変更の合成、可視版の選択、主表・索引の参加、保存先の能力検査 |
| `Seamless.Collections.Tsavorite` | ヘッダー付き読取り、保持されたアドレスへの読取り、コピー更新、論理削除、アドレスの世代、ネイティブ走査、静止時の物理回収 |
| Persisted の私有作業領域 | セッション・取引・クエリ専用の変更集合、Join / GroupBy の退避を管理。共有業務表の MVCC 参加先にせず、DB 本体の保存先には追加しない |
| `Seamless.Database` / Generator | 明示オプション、型付きビュー、保存プロファイルの検査、checkpoint への接続 |
| FLinq Generator | 取引行ソースに対する provider root と融合。確定や WAL の判断は持たない |
| TPC adapter | `TxnLog` に代わる scope 接続、型付き Rows、主表・索引の変更収集、既存 observer との対応 |
| Gateway | 接続・SQL のネスト・DDL 世代・Native の寿命を管理し、最終確定は共通 domain へ委譲 |

Core に Tsavorite の型を参照させない。既存 `ITransactionParticipant` / `ITransactionChanges` は直列方式の互換経路として残す。並行方式には、snapshot を受け取り版を設置できる追加の participant 能力が必要。delegate だけの旧 participant や任意の `Enlist(ITransactionChanges)` を、能力検査なしで並行方式へ通さない。

## 用語と独立した設計軸

「並列モード」という呼び方だけで、次の項目をまとめない。

| 項目 | 意味と今回の扱い |
|---|---|
| 分離レベル | Serializable は、取引をある順番で一つずつ実行した場合と同じ結果になる保証。実際のスレッド数や同時実行の有無を指定するものではない |
| 複数取引の同時実行 | 異なる取引の読取り・計算・更新・待機を重ねる。今回合意した方式選択の軸 |
| 複数クエリの同時実行 | 別々のクエリを同時に進める。別取引での同時実行と、同じ取引に複数クエリを同時提出することを区別する。現行 scope の同時操作制約は後者の API 契約 |
| 一つのクエリ内部の並列化 | 一つの走査・集計等を複数ワーカーに分担させる。Serializable と両立する独立した設計項目。製品ではCountおよび対応するJoin/GroupByの形状を共通CPU枠へ接続し、MV20でworker上限ごとの結果を測定した。旧方式比較の試作はこの製品測定とは別である |

「取引全体排他方式」は、取引同士の実行を重ねないという意味であり、一つのクエリを単一スレッドで計算するという意味にはしない。「楽観的同時実行方式」は、複数取引を重ね、確定時に依存関係を検証する方式。FLinq の演算融合・ネイティブ走査と、クエリ内のワーカー並列化も別の項目として扱う。

初版のクエリ内並列化では、一つのクエリのワーカーが同じ snapshot を共有し、ワーカーごとの handle・strategy 状態・集計結果を持つ。中止時は全ワーカーのアクセス終了を確認して保持を解放する。同一 scope の独立した同時 API 操作を拒否する現在の制約を、そのまま内部ワーカーの禁止と読み替えない。

## API と互換性 — Decision

`BeginReadOnlyAsync`、`TransactionScope.IsReadOnly`、`TransactionContext` / `IContextualTransactionParticipant`、型付きDBの `BeginReadOnlyTransactionAsync`、明示Optimisticとversioned participant経路を実装した。設定省略時の取引全体排他は維持する。

以下のoptionsと追加participant能力はコードへ接続した。後続adapterを含む全契約の受入は継続中。既存の引数順と戻り値を維持し、既存の `BeginAsync(CancellationToken)` を残す。`BeginAsync(default)` の解決を変えないため、読取り専用開始は別名にする。

```csharp
public enum TransactionConcurrencyMode{Exclusive,Optimistic}

public sealed record TransactionCoordinatorOptions{
    public TransactionConcurrencyMode Concurrency{get;init;}
        =TransactionConcurrencyMode.Exclusive;
    public TransactionHistoryOptions? History{get;init;}
}

public sealed record TransactionHistoryOptions{
    public required long MaxRetainedMemoryBytes{get;init;}
    public required long MaxRetainedStorageBytes{get;init;}
    public required long MaxMetadataBytes{get;init;}
    public required long MaintenanceReserveMemoryBytes{get;init;}
    public required long MaintenanceReserveStorageBytes{get;init;}
    public required TimeSpan MaintenanceWaitTimeout{get;init;}
}

// 既存の3引数 constructor は、そのILシグネチャごと維持する。
public TransactionCoordinator(
    TransactionJournal? journal=null,
    TransactionFailure? failure=null,
    PreparedTransactionJournal? preparedJournal=null);

// options を指定する新しい overload。既存呼出しを曖昧にしない。
public TransactionCoordinator(
    TransactionJournal? journal,
    TransactionFailure? failure,
    PreparedTransactionJournal? preparedJournal,
    TransactionCoordinatorOptions options);

public ValueTask<TransactionScope> BeginAsync(
    CancellationToken cancellationToken=default);
public ValueTask<TransactionScope> BeginReadOnlyAsync(
    CancellationToken cancellationToken=default);
```

- `TransactionHistoryOptions` は履歴のメモリ・ストレージ上限、管理 metadata 上限、回収用作業領域、受付待ち上限を保持する。Optimistic で有限の予算を必須にする。試作の24 MiBを製品既定値として固定しない。
- `TransactionScope` の状態に読取り専用区分を保持する。読取り専用で開始した scope は最初から書込みを拒否し、後から更新 scope へ昇格させない。子は同じ区分・snapshot・中止状態を共有する。これは C# のメソッド属性の指定を必須にする意味ではない。
- 古い版の保持予算不足で中止する対象は、開始時に読取り専用と確定した取引だけとする。通常の取引は、まだ SELECT しか実行していなくても対象に含めない。生成器が取引全体のアクセスを解析し、読取り専用として開始した取引もこの対象に含む。
- 既存 `CommitAsync` / `RollbackAsync` / `DisposeAsync`、`Id`、`Voted` / `NoChanges` / `Committed` の意味を維持する。正常な競合を既存の成功 result に混ぜない。
- 追加する `TransactionAbortReason` の候補は `Conflict`、`HistoryPressure`、`CallerCancellation`、`JoinedRollback`。理由付きの取引中止例外を呼び出し側へ返す。`TransactionOutcomeKind` の3値は維持し、中止済み ID の照会を `Aborted` と整合させる。
- 同期 `EnterRead` は従来の排他 lease であり snapshot と同一ではない。Optimistic の利用経路を型付き snapshot へ接続するか、非対応操作として明示的に拒否する。checkpoint は専用の保守境界へ移す。
- 同一取引の別スレッドからの同時操作禁止は維持する。問い合わせ中に呼ばれる同期のネストした点検索と、副作用を持つ既存クエリは別途互換性試験を行い、scope の guard 変更で黙って意味を変えない。

`DatabaseContainerOptions` には取引基盤の選択とは別に Concurrency を明示する。既存 `DatabaseTransactionMode.Legacy` / `SharedV2` は互換識別子として維持し、説明では「共通トランザクション基盤（既存 API の SharedV2）」と呼ぶ。新しい SharedV3 等の名前は作らない。既存設定・新規 DB とも取引全体排他方式を既定とし、Optimistic と有限の資源予算を明示して有効にする。Legacy と Optimistic の組合せ、非対応保存プロファイル、参加先の能力不一致は開始前に拒否し、自動で別方式へ切り替えない。

上記のenumは、分離レベルとの混同を避けて `Serial` を `Exclusive` とした。これは取引全体排他の指定であり、分離レベルやクエリ内のワーカー数は変更しない。既存の実験結果の `Mode.Single` は取引全体排他の比較条件として読み、保存済みの結果名は書き換えない。

### 展開時のアクセス判定 — Decision

#### 生成コードへ残す情報 — 合意済み

利用者は、アクセス判定・最適化・変形・並列化・キュー・資源管理・同期を、展開コードの各処理を読む場所にコメントとして残すことを求めた。全体の説明だけを別文書へ分離せず、元ソース位置、入力/出力の対応、適用規則と前後の形、既知/不明の理由、所有者・解放位置、取消/例外/早期終了、順序・数値・公開の不変条件、生成器の版も記録する。

コメントは実際に生成される経路を説明する。逐次走査をworker並列と記載せず、呼出し先へ委譲した同期・予約・後始末を、生成メソッド内に実装したものとして書かない。TransactionRowsのToArray/List/Dictionaryは、構築・再確保中のコンテナと既知codecの値費用を共通予算へ予約し、完成時にも取引の中止を確認する。予約は生成されたusingで返し、返却後のmanaged結果はcaller所有とする。配列/辞書の戻り値を別型の退避結果へ変更しない。一般の利用者callbackが生成する任意のobject graphの全費用を計測できるとは約束せず、他providerの未接続経路にも予算付きと記載しない。

Debugでは、静的な計画を定数としてメタデータへ格納し、状態の表示に `DebuggerDisplay` を使う。生成メソッドの識別に標準の `GeneratedCode` を付ける。実行時の行・取引・codec・Typeをdebug用global cacheへ保持せず、自動ログ出力や追加同期を入れない。アクセスや並列化の契約を利用者が重複宣言する属性は追加しない。

各処理文には生成ソース内の手順番号を付け、メソッドの元ソース位置・入力ファイルchecksum・生成器MVIDを記載する。アセンブリ版が同じ開発ビルドも識別する。readonly判定と並列化の根拠、例外・短絡・数値の評価位置、結果の所有者、実装済みの予算/退避、逐次fallbackの条件を分けて記録する。コメントとDebug定数の追加で利用者コードの評価回数や後始末を変えない。ReleaseではDebug定数・DebuggerDisplayを含めず、生成ソースのコメントとGeneratedCodeは残す。

現在の並列件数計算は、共通のscope operationを親が全worker終了まで所有し、workerは公開scope APIを呼ばない。native session/epochはchunk内、走査構造の保護は親cursorに限定する。`MaximumQueryWorkers`はquery単位の上限、`CpuScheduler.Shared`はhost共通のCPU上限で、取引のExclusive/Optimisticとは独立する。小さい入力、私有変更、入れ子のCPU作業、旧adapterは逐次へ戻る。一般のstrategyに結合則・交換則や副作用なしを推定せず、接続層の`IParallelStrategy`契約を満たすものだけを分割する。

#### 取引範囲の判定

取引境界は利用者のソースにある明示的な `BeginTransactionAsync` と `CommitAsync` / `DisposeAsync` を基本とし、生成器が各業務メソッドの呼出しを勝手に別取引へ分けない。外側の一つの取引で `PlaceOrder(tx,...)` と `UsePoints(tx,...)` を順番に呼ぶ場合は、両方を一つの確定・中止単位として解析する。単一の業務メソッドを原則一取引にする生成入口は必須にしない。

利用者にメソッド属性で表の読み書きを重複宣言させず、展開対象の操作から表ごとの読取り・書込みの可能性を生成器が収集する。点検索・走査・Join の入力は読取り、追加・変更・削除は書込みとして扱い、分岐と解析可能な呼出し先の効果も合成する。同じ表への別名参照を別の表とみなさない。解析できない呼出し・動的アクセスは不明として扱い、更新の可能性を勝手に消さない。

解析できずアクセス情報もない呼出しがある場合は、それだけでビルドエラーにせず、取引全体を更新可能として共通取引経路で実行する。解析できた部分の最適化は維持し、不明な呼出しへの読取り専用判定・自動並列化は行わない。この方針は、外部処理も同じ scope の共通取引 API を通すことが前提。raw accessor 等による取引外の変更を許可する根拠にせず、既存 FLinq の未対応演算の扱いとも区別する。

明示した取引の開始から終了までを展開対象として解析できる場合は、全経路で DB 書込みがないと分かれば生成経路を読取り専用の開始へ接続する。ある表を読み、別の表へ書く場合は取引全体を更新可能で開始し、表ごとのアクセス情報は生成するビュー・実行経路・依存管理の最適化に使う。アクセス先が実行時まで未確定の経路も通常の取引として開始する。

利用者が外側で開始した通常の取引へ読取りだけのメソッドを呼んでも、その取引全体を読取り専用へ変更しない。後続の別メソッドが書く可能性があるため、その取引は古い版の予算不足による中止対象にしない。明示した読取り専用開始 API と、実行時に全 DB 書込みを拒否する検査は維持する。

静的な表アクセス情報と、実行時のキー・不存在・走査範囲の依存記録は別の役割を持つ。生成器の判定だけで楽観的検証を省略しない。書込み用変更集合・WAL 準備等は必要な経路へ限定するが、全表の版が同じ snapshot に一致する条件は維持する。読取りだけのメソッドでも外部状態を書き換える可能性はあるため、DB の読取り専用判定をクエリ内並列化の安全性判定と同一視しない。

この節は新しい生成機能の設計であり、現行の `[LinqIntercept]` が DB の読取り・書込み契約を指定するという意味ではない。現行の `ReadOnlyTransactionCollection` は実体化ビュー用であり、取引全体の読取り専用判定と区別する。

### 参加先の追加能力

Core / Collections間には次の追加能力を実装した。context/preparationとversioned participantの契約を使い、旧Apply経路と区別する。保存先と製品全体の受入は現在の実装範囲に従う。

```csharp
public interface IVersionedTransactionParticipant:ITransactionParticipant{
    IVersionedTransactionChanges CreateChanges(TransactionContext context);
}
public interface IVersionedTransactionChanges:ITransactionChanges{
    ValueTask<PreparedTransactionParticipant> PrepareAsync(
        TransactionPreparationContext context,CancellationToken cancellationToken);
    void Install(TransactionInstallationContext context,
        PreparedTransactionParticipant preparation);
}
```

| 型 | 必須の内容・役割 |
|---|---|
| `TransactionContext` | domain・運転世代、取引 ID、ReadVersion、読取り専用属性、operation と保持の検査 |
| `TransactionPreparationContext` | 凍結と事前検証、親 budget への予約、取消の検査。保存先へはまだ変更を設置しない |
| `PreparedTransactionParticipant` | 凍結した変更と有界 WAL 列挙、変更不能な依存計画、設置と metadata の資源予約。処理終了まで所有する |
| `TransactionInstallationContext` | 検証済みの取引 ID・CommitVersion・WAL LSN・対象世代。耐久化後の設置にだけ渡す |

`PrepareAsync` が収集した依存計画を共通層が検証する。backend 独自の真偽値だけで検証を代行させない。通常の点操作はキー単位、全走査は表単位とする。大きな退避済み依存集合は事前に保守的な表依存へまとめる等、validation gate 内でディスク列挙をしない構成にする。後者による過剰競合は計測する。必要な依存を省いて高速化しない。

共通層の直近のキー変更・予約索引も有限の metadata 予算で持ち、有効な読み書き取引の horizon が必要とする項目を勝手に捨てない。安全に縮小できない場合は WAL 受付前の資源不足として扱う。Optimistic の versioned participant は旧 `Apply()` で現在値を上書きする経路へ流さず、`Install` を使う。

## 版の表現と可視性 — Decision

### 遅延方式専用の版

自己変更は `TransactionChangeSet` 内にだけ置く。検証と WAL の永続化後、`CommitVersion` と論理削除フラグを付けて保存先へ設置する。全参加先の設置前に公開しない。

即時方式と共通化した試作の `Owner -> State.Csn` 表を、そのまま製品の必須構造にはしない。遅延専用の初版案は、各設置版に運転世代内の `CommitVersion` を直接持たせる。未公開版は snapshot 番号より新しいため不可視であり、中止前の自己変更のために本体へ版を置く必要がない。

`CommitVersion` は運転中の可視性番号であり、Tsavorite の `ReadInfo.Version`、論理アドレス、外部の取引 ID と同一視しない。domain と運転世代に結び付け、再起動をまたぐ一般向け履歴照会には使わない。WAL の LSN と公開の番号も区別する。

Gateway の snapshot は、まだ参照していない参加可能 DB のカタログ・世代にも対応する。後から DB を参照した際に、その時点の最新世代へ読み替えず ReadVersion に対応する世代を取得する。世代の保持を個々の取得済み handle だけで決めず、domain の有効 snapshot も確認する。DDL の新世代公開を行の版公開と同じ境界に置く。

### 点検索

1. scope の状態と operation lease を検査する。
2. 自己の変更集合にキーがあれば、更新値または不存在を返す。
3. 保存先からキー・値・版・前リンクを一度の観測として取得する。
4. `CommitVersion <= ReadVersion` の同一キーの可視版を探す。新しい版と別キーは読み飛ばす。
5. 読み書き取引では不存在も含めキー依存を登録する。mutable な参照を直接返さず、既存 codec の所有権分離を維持する。

### 全走査と索引

全走査は構造安定化スコープを取得し、snapshot 時点の版を解決してから利用者の処理へ渡す。自己の更新・削除を反映し、本体の走査にない自己挿入も追加する。論理削除版を通常の engine tombstone として消さず、旧版を見つける起点を保つ。

読取り依存は初版では表単位を基本にする。任意の Where を正確な範囲へ自動変換できるとはしない。検証は「走査開始後の変更」だけではなく、**取引の ReadVersion より新しい競合変更**を検出する。主表だけでなく二次索引・UNIQUE / FK の検証に使う入力も同じ snapshot と公開単位へ参加させる。

複数入力の問い合わせと複数表の設置で、異なる順序の表ロックを保持して待つ構造を避ける。初版候補は domain 単位の走査用共有 gate と設置用排他 gate。点検索はこの走査 gate を取らず、設置中の新しい版を snapshot 条件で読み飛ばす。設置 gate 内で WAL の Sync は待たない。集計待ちが無関係 DB へ及ぶ範囲も計測し、後続の表単位化の判断材料にする。

## 確定の状態遷移 — Decision

メモリ DB は現在の契約どおり WAL を作らず、検証・全参加先への設置・一括公開を行う。永続 DB はその設置前に WAL の受付・耐久化を行う。以下の Accepted / Durable と WAL の手順は永続構成を示し、メモリ構成に再起動後の復旧や耐久化を約束しない。メモリ構成でも設置障害による部分状態を公開せず、安全に継続できなければ domain を停止する。

| 状態 | 許可すること・遷移 |
|---|---|
| `Active` | snapshot 読取りと私有変更。読取り専用は変更不可 |
| `Validating` | 変更集合を凍結し、依存検証と予約。競合なら WAL 受付前に `Aborted` |
| `Queued` | 検証済みの順序を共有受付 queue へ登録。全体の Sync 排他は保持しない |
| `Accepted` | WAL に確定記録が受け付けられた。呼出側の取消で確定を撤回しない |
| `Durable` | 結果は Committed。設置途中でも Aborted へ戻さない |
| `Installing` | 全参加先へ版と索引を設置。旧 snapshot には不可視 |
| `Published` | 先行の確定要求の設置完了も確認し、一つの公開境界を進める |
| `Completed` | 成功応答と後始末。後始末の失敗で確定済み結果を取り消さない |
| `AbortRequested` / `Aborted` | 新規アクセスと正常終了を拒否し、進行中アクセス終了後に保持を解放 |
| `RecoveryRequired` | 不明または耐久化後の設置・公開障害。同じ commit domain の受付・成功応答・保存を停止 |

概念上の確定手順は次のとおり。コメントは既存メソッドがこの名前で存在するという意味ではない。

```csharp
// 変更を凍結し、履歴の追加分・metadata・WAL作業領域を先に予約する。
using var reservation=await ReserveCommitResourcesAsync(tx);

// 短い順序境界で、確定済みと受付待ちの依存をともに検証する。
var request=ValidateAndQueue(tx,reservation);
var receipt=await request.Accepted;
await receipt.Durable;

// 保存先の旧値を壊さず設置する。自己変更はここまで私有のまま。
await InstallAllParticipantsAsync(request);
await WaitForEarlierPublicationsAsync(request);
PublishAllParticipants(request);
return Committed(tx.Id,receipt.Lsn);
```

短い validation gate 内で同期 I/O、Sync、queue 空き待ち、回収待ちを行わない。受付の queue は有界で、投入順と CommitVersion の順を一致させる。resource の取消と WAL に受け付けられたかどうかを、同じ受付境界で決める。受付前に中止した順序枠は明示的に退役させ、公開 frontier に穴を残さない。

競合・保持予算不足・通常の呼出側取消は、原則として WAL 受付前の正常な取引中止であり、共通 `TransactionFailure` を障害状態にしない。現在の `Commit` の catch 全体へ新しい競合例外をそのまま流してはならない。I/O・破損・設置失敗等は既存の Unknown / Committed の境界で扱う。結果照会に Unknown が返る場合に Aborted と推測しない。

### WAL の接続

共通の受付・結果・公開処理に、既存の単一 frame journal と prepared journal を接続する。Gateway は通常 DML・世代 metadata・Native metadata を同じ最終 commit に含め、DB ごとの独立 COMMIT と二重 WAL を禁止する。

prepared journal の gate と準備チャンクの非交錯という現在の前提は、初版では維持する。これにより Gateway の確定 I/O が直列化される部分は残る。試作の処理量を Gateway の予測値にしない。小さい取引の group commit 等を追加する場合は、準備チャンク・最終記録・復旧 parser の契約を別途検証し、単に gate を削除しない。

### 保存点とネスト

子 Commit は投票のみ、子 Rollback は root 全体を中止する現在の契約を維持する。保存点へは私有変更を戻し、snapshot 番号を進めない。読み取り依存は安全側の上位集合として保持し、復帰時に未追跡へ戻して競合を見逃さない。Gateway の保存点は既存の履歴位置と世代の保持を利用し、保存点ごとに全変更集合を複製する構造へ戻さない。

## 読取り専用取引の中止と履歴予算 — Decision / Invariant

### 予算の対象

履歴予算は、現行データとは別に保持する旧版と到達経路の費用を対象とする。試作で使った `Tail - Begin` は現行データも含むため、製品の履歴予算として流用しない。保存先のページ単位の保持・回収不能分を安全側に計上する。

次を親 `ResourceBudget` へ接続する。二重計上と計上漏れを検査する。

- 現行データ・native index / buffer の保存先予算。
- 旧版と保持経路のメモリ・ストレージ予算。
- snapshot 登録、依存集合、未公開予約、公開順 queue の管理予算。
- 私有変更集合・保存点・退避・WAL の予算。
- 物理回収に必要なコピー用ページ、保存確定まで残る旧ファイル、新旧世代の作業予算。

更新の受付後に回収用空き容量がなくなることを避ける。作業用の予備領域を事前予約し、満杯になってから無予算で Compact を呼ぶ方式にしない。

### 中止の手順

1. 追加予算が不足した時点で、旧版保持の下限を押し下げている読取り専用取引を古い順に候補とする。更新取引と耐久化済み取引をこの方針で取り消さない。
2. `HistoryPressure` を理由に状態を変更し、以後の新規アクセスと正常終了を拒否する。
3. 進行中 operation の終了を待つ。native の Pending 読取りと callback を含め、参照が残る間は保持を解放しない。
4. 最後の operation が解放されたら snapshot 登録を外し、保持の下限を再計算する。
5. 回収できた資源を確認してから更新を再受付する。空きが作れない場合や待機期限を超えた場合は WAL 受付前に資源不足を返し、予算を超えて書き進めない。

`AbortRequested` と operation の開始を同じ状態境界で調停する。中止前に開始した operation はカウントし、中止後の operation は入れない。最後の参照の解放・Dispose は一度だけ行う。プロセスやスレッドの強制終了を取消機構にしない。

FLinq の走査はチャンク境界と callback の前後で取消状態を検査する。任意の利用者 callback が戻らない場合の即時停止は保証しない。遅延列挙は中止後の `MoveNext` で例外を返し、集計や root の正常終了に中止済み状態を混ぜない。既に配信した部分行を撤回できないプロトコルでは、完了として返さず終了エラーを伝える。

## Tsavorite の高度 API と回収方式

### 接続層向けの API — Decision

提案する能力は、`ReadRecord(key,lease)`、`ReadRecordAtAddress(address,lease)`、`IterateRecords(ref strategy,lease)`、`AppendCommittedVersion(context,...)` に相当する操作。実際の型名は実装時に既存の generic 制約と照合する。

戻り値にはキー、所有権が分離された値、コピーした native ヘッダー、アドレスのストア・世代、接続層の CommitVersion / 削除フラグを含める。生ポインタと書換え可能なヘッダーを渡さない。現在値の不存在、保持切れ、世代不一致、取引の中止、I/O障害は区別する。

RMW は保持中の旧値を上書きせずコピー更新する。native の前リンクはハッシュ連鎖なのでキーを照合する。コピー callback 後にヘッダーが復元される点を守り、最終値になる前の `PreviousAddress` を保存しない。高度なネイティブ走査でヘッダーも渡せれば、試作の走査用リンク8バイトを省ける可能性があるが、保存形式とコストは実装・計測で確定する。

ハンドルは parallel region と現在の実行スレッドに従って取得する。Tx をまたぐ raw accessor の同時利用や、await を跨いだ native epoch の固定をしない。保持 lease の論理寿命と native epoch の短い保護を区別する。

### 初版の回収方式 A：新しい取引を待たせてまとめて回収 — 合意済み

取引1・2が実行中に整理が必要になった場合、これから始まる取引3を待たせる。取引1・2の終了を確認し、古いデータを整理してから取引3を開始する。実行中の更新を途中で強制終了する案ではない。整理そのものの間は対象の取引処理が動かない。停止する対象は commit domain 内であり、独立した domain は継続する。

- 対象 domain の新規取引受付を一時停止する。既存の更新取引は継続して終了できるようにする。
- 履歴予算不足の原因となる読取り専用取引を合意どおり中止し、すべての operation と取引が静止するのを待つ。更新取引を強制取消して静止を作らない。
- 受付前の資源不足を返す取引を含め、既存取引の排出を確認する。待機期限を超えたら参照を残したまま Compact を開始しない。
- キャッシュした accessor と parallel region を所有者が閉じる。最新版が公開済み確定版であることを検査し、通常の Compact と必要な共通保存を行う。
- 論理削除済みキーも整理する。通常の Compact は値に入れた削除フラグを理解しないため、静止確認後に不要な削除済み head を取り除く専用処理が必要。キーの取りこぼしを防ぐ有界走査・退避と、その作業予算を検証する。保持中の native Delete の流用で代替しない。
- ストア世代を進め、旧アドレスとキャッシュを失効させる。管理 metadata も到達性を検査して整理し、handle と受付を再開する。

利用者は案 A を選択し、**履歴回収中に新規取引を待たせることを許容した**。Compact 全体の実時間は表サイズ・I/Oに依存し、現在の API に実行全体を安全に期限取消する保証はない。排出待ち期限と回収実時間を混同せず、待機と回収にかかった時間を分けて測定する。

### 受付停止・排出・再開の契約 — Decision / Invariant

`TransactionCoordinator.StopAccepting()` は現在 `stopping` を設定する終了用の操作で、受付再開の対になる API はない。`EnterReadForShutdown()` も現在の取引全体の gate を待つ。履歴回収のためにこの永久停止状態を解除する実装にせず、commit domain ごとの一時的な受付状態と回収所有者を追加する。

一時状態は `Running → Draining → Reclaiming → Reopening → Running` とする。`Draining` は既存処理の終了待ち、`Reclaiming` は物理回収・必要な保存、`Reopening` は世代更新・handle 再取得と整合確認。終了・障害による停止は全状態に優先し、回収が終わっても永久停止を解除しない。同じ domain への複数の回収要求は一つにまとめ、独立した domain は継続する。

| 境界 | 契約 |
|---|---|
| 新規受付 | 新しい root 取引と独立した読取りスコープの開始を待たせる。受付許可と snapshot 登録を同じ状態境界で調停し、停止直前に待ち始めた開始要求も無条件には通さない。待機要求自体も有界な資源予算を持つ |
| 既存取引 | 読取り・更新・確定・中止・後始末を継続できる。既存 root に属する子スコープ・保存点は同じ受付許可を使い、回収待ちに入り直さない。既に中止した取引への操作は拒否する |
| 履歴圧迫 | 古い読取り専用取引を `HistoryPressure` で中止し、進行中 operation の終了後に保持と受付許可を一度だけ解放する。利用者の遅れた Dispose だけで履歴を保持し続けない。更新取引を一律に強制中止しない |
| 予算不足の更新 | 予約済み予算で完了できる既存更新は終了まで進める。完了に必要な予算を確保できない更新は WAL 受付前に資源不足で中止して参照を解放する。自身が排出対象に残ったまま回収完了を待つ経路を作らず、自動で業務処理を再実行しない |
| 静止の確認 | 活動中スコープ、operation、native Pending / callback、受け付け済みの耐久化・設置・公開・後始末の残りがないことを確認する。停止前に受け付けた確定処理を先に完了させる。初版のクエリ内の全 worker もこの参照管理に含める |
| 同期待ち | 排出・WAL・I/O を待つ間、進行中の取引が必要とする検証・公開 gate を保持しない。回収所有者の調整と、通常取引の終了に必要な操作を分離する |
| 物理回収 | 全参照の解放と作業予算を確認してから、所有者が accessor / parallel region を閉じる。論理削除の整理、Compact、必要な共通 checkpoint、世代更新・metadata 整理を行う。Gateway の保存は既存の共通保存経路に接続し、DB ごとに独立保存しない |
| 再開 | 全参加先の handle 再取得、公開済み状態と世代・保持の整合を確認してから受付を再開する。個々の handle の再取得成功だけで待機者を通さない |

待機中の開始要求は cancellation token と待機期限に従う。一つの待機者の取消は共通回収を中止せず、その要求が後から取引を開始することもない。`MaintenanceWaitTimeout` は排出待ちと開始側の待機に用いる上限であり、既に実行中の native Compact を強制停止する時間ではない。呼出し側が先に取消した場合はその要求の待機を終了する。

排出期限切れや物理変更前の予約不足では、参照が残ったまま回収へ進まない。安全性を確認できる場合は通常の資源不足として待機者へ結果を返し、永久停止の要求がなければ受付を戻す。受付を戻しても通常の予算検査は続ける。物理回収・保存・handle 再取得で障害が起き、安全な継続を確認できない場合は `RecoveryRequired` とし、`finally` で無条件に受付を再開しない。

### 回収案 B：取引を進めながら回収 — 将来の代替候補

取引1・2が実行中でも取引3を受け付け、動いている取引がまだ読む古い値を残しつつ、不要になった部分を整理する。受付待ちを減らすため、参照先が移動しても読める仕組みや整理用の領域を追加する。切替え時などの短い同期までなくなるという保証ではない。

必要な版と到達経路を最古の有効 snapshot に基づいて残す。現在の Compact をそのまま使わず、旧アドレスの保持、リンクまたは世代の置換、copy 中の更新、公開切替、旧世代の解放を新設する。全件をメモリへ複製せず有界チャンクと予算付き退避を使う。新旧領域とコピー作業の同時存在を予約する。API の回収待ちを短くできる可能性があるが、形式・寿命・障害復旧・資源予算の実装が増える。性能は未実測。初版には採用せず、案 A の停止時間の実測後に必要性を判断する。

## FLinq の接続 — Decision

`TransactionCollection<TKey,TRecord>` に、生成器が認識する具体型 `TransactionRows<TKey,TRecord>` を追加する案。通常の列挙互換経路と、native 用の次の能力を用意する。

```csharp
public void Iterate<TStrategy>(ref TStrategy strategy)
    where TStrategy:struct,
        Seamless.Collections.Iteration.IStrategy<TStrategy,TRecord>;
```

既存 `IStrategy` の契約を使い、`false` で早期終了する。保存先への dispatch は問い合わせ境界で行い、行ごとの scope lock と boxing を増やさない。保存先の native loop の内側で可視性と私有変更を解決してから、融合した利用者処理へ渡す。

operation / 走査スコープの取得と解放は、この行ソースの通常の C# 実装の `try/finally` に置く。試作で `using var` が生成時に失われた問題を、利用者の各クエリに特別な書き方を要求して回避する設計にしない。

Count、Contains、Any、First、Join の全入力、GroupBy で同じ snapshot を使う。現在値の Count を KnownCount として宣言しない。キー点検索への最適化は transaction view の検索へ接続する。未対応の融合経路を、正しさ・性能を検証せず通常 LINQ と同等と数えない。

## クエリ内並列化と CPU 配分 — Decision

初版に走査・絞り込み・射影・集計に加え、Join と GroupBy の分割・合成を含める。既存の ParallelSet は並列区間でスレッドごとに使う handle であり、自動的なクエリ分割の実装済み証拠にしない。DB の取引ビュー向け provider root を追加し、共通の演算融合と Tsavorite の native 能力を利用する。新しい物理保存エンジンを追加する意味ではない。

- `Seamless.Execution` 側にホスト共通の CPU 配分を置き、取引の commit domain ごとに CPU 枠を重複確保しない。取引全体排他 / 楽観的同時実行の指定と、クエリ内の並列度を独立させる。
- 開いている取引数だけで配分せず、実行可能な仕事と CPU 枠の待機に応じて配分する。ワーカー数の計上は呼出し側で CPU 処理する分も含め、I/O / WAL 待ちで CPU 枠を保持し続けない。後続クエリへ処理の区切りで枠を返せる構成にする。
- 並列化できると確認した SQL の読取り演算等は自動で候補にする。任意のコールバック等、安全性が不明な処理は自動並列化せず、明示許可の経路と区別する。既存の結果・順序・比較・数値と例外の意味を変える再結合を無条件には行わない。
- 同じ取引への公開操作は一つずつ実行する。一つの公開クエリの内部だけで worker を分け、同じ snapshot と凍結した自己変更を共有する。worker ごとの実行コンテキスト、handle、strategy、部分結果を用意し、既存 ExecutionScope の同時操作禁止を解除するだけの実装にしない。
- Join / GroupBy には分割・偏り・重複キー・退避・結果合成を含める。worker の私有メモリ、バッファ、Persisted の退避を既存の親 budget へ計上し、全件配列の無制限な複製をしない。取消・例外・早期終了では全 worker の終了後に参照と作業領域を解放する。

CPU と作業メモリを一体に判断して同時実行数・並列度を調整する高度な制御は、利用者指示により NUMA 最適化の段階へ回す。初版の CPU 配分と既存のメモリ / 一時ストレージ上限検査はそれぞれ維持する。NUMA の配置、I/O 構成を考慮した制御も初版の完成条件に含めない。

選択の基準は、今の設計とともに実装しないと抜け落ちる接続・寿命・取消等を今含め、NUMA のように独立した課題として追えるものを後に回すこと。実装量の削減だけを根拠に、合意済みの Join / GroupBy の並列化を先送りしない。

## checkpoint・復旧・保存形式 — Decision

新しい版の保存形式は、通常行だけの既存保存形式と識別する。明示した新規プロファイルと新規領域で開始し、既存 Store / WAL / codec を暗黙に読み替えない。一般向けの履歴機能を増やさず、業務 WAL の row payload とその codec の意味は維持する。

初版案では CommitVersion は運転世代内の番号とし、既存 WAL の全 frame へ新しい番号を追加しない。checkpoint component に保存済み最大番号と対象 LSN を記録する。復旧時は全参加 Store の checkpoint 番号を確認し、その最大値より上から確定取引単位に番号を割り当てて再生する。保持済み checkpoint 行より新しい番号を使い、全参加先を整えてから snapshot を受け付ける。再起動前のアドレス・snapshot token は世代不一致として拒否する。

Gateway の DB ごとに checkpoint LSN が異なる場合も、既存の各 DB / 世代の適用済み境界を守り、現在の型へ旧世代の row を渡さない。新しい運転番号は取引 ID や WAL LSN の永続結果照会には使わない。

checkpoint は全設置・公開が完了した確定境界でだけ行う。Native handle の閉鎖・再取得を保守境界に接続する。WAL prefix の削除は全参加先の保存と outcome metadata の確定後とし、MVCC 旧版の回収とは別の判断にする。一般の GC や古い読取取消を理由に、復旧に必要な WAL を削除しない。

## 影響範囲

`TransactionCoordinator` / `TransactionScope`、追加 participant 能力、`TransactionalTable` / `TransactionCollection`、保存先の版読み取り・回収、Database の options と schema generator、FLinq の provider root、TPC の表・索引 adapter、Gateway の論理取引と共通確定・世代・checkpoint、および対応する既存の試験プロジェクトが対象。旧 WAL の row payload と既存プロファイルの通常利用は維持する。新しい版付き保存プロファイルは新規領域で明示導入する。既存データを新領域へ移す専用ツールは初版に含めない。

## 移行手順

| 段階 | 実装・確認内容 | 次へ進む条件 |
|---|---|---|
| 基盤 | options、scope 状態、snapshot 登録、正常中止、予約と公開順、追加 participant 能力。既存 serial の API は維持 | 同一取引・親子・保存点・結果の回帰と正常競合の非障害化 |
| Tsavorite | メモリ／ファイルの版付き保存、保持、論理削除、型付き Rows、選択した回収 | 旧版・自己変更・衝突・退避・取消・回収の安全性 |
| 共通確定 | 単一 frame / prepared journal の接続、全参加先の設置と公開、checkpoint / replay | 全境界の障害で Unknown / Aborted / Committed と復旧内容が整合 |
| Database / Generator | 明示オプション、型付きビュー、世代・索引、通常直アクセスとの境界 | 所有権と codec、既存 serial consumer、新規 Optimistic consumer が合格 |
| TPC-E | 新しい取引 adapter と具体型 Rows を接続。主表と OrderedIndex の双方を私有変更へ参加させる。旧 undo と observer を黙って流用しない | 取消後の本体・索引一致、同じ入力列による結果照合、計測内の診断費用の説明 |
| Gateway | `LogicalTransaction` を共通 domain へ接続し、一括 WAL・世代公開を維持。DML の DB 全体 write lock の役割を OCC と世代保護へ分離 | SQL / HTTP / Native の所有権、ネスト、保存点、DDL / DML 混在、複数 DB 強制終了復旧 |
| 私有作業領域 | Persisted を使う変更集合・並列 Join / GroupBy の退避、worker ごとの所有権・資源計上・破棄 | 共有業務表へ参加させず、取消・例外・正常終了で回収 |

Gateway の schema lease、世代所有権、ID 予約の契約は残す。DML の並行化のために既存 `DatabaseLockManager` の Read / Write / Schema の互換性を一括変更しない。参加先に旧能力が混在する場合は新方式で開始せず、取引途中に一部だけ直列確定へ戻さない。

実装で新しいソースや shared source を追加する場合は、所有する csproj / projitems、対応 shproj、`Seamless.slnx` を同時に更新する。既存の未コミット変更を別 checkout の古いコードで上書きしない。

## テスト計画

いずれも**今後の製品受入条件**。この文書作成によって試験済みになったとはしない。

| ID | 外部から観測する条件 |
|---|---|
| MV01 | 古い取引の複数行・複数表の値が一つの snapshot に一致し、現在値・未公開値・中止値が混ざらない |
| MV02 | 自己更新・削除・再挿入・新規キーを点検索、Count、Sum、Join 全入力、GroupBy が同じように扱う |
| MV03 | lost update、write skew、不存在キー、表走査への phantom、UNIQUE / FK 等の依存競合を検出する |
| MV04 | 別行を変更する取引が、読取り・計算・WAL 待ちの区間で実際に重なる。同一 scope の並列操作は拒否する |
| MV05 | 子 Commit の投票、子 Rollback の全体中止、保存点の退避・復帰・再利用、資源不足時の復帰を維持する |
| MV06 | mutable な返却値の変更で本体や他取引を書き換えられない。保存先を跨いでも所有権が分離される |
| MV07 | ReadOnly の書込み・昇格を拒否する。履歴圧迫で中止した scope の新しい読取り・正常終了も拒否する |
| MV08 | 実行中の native Pending 読取りや callback を障壁で停止し、中止要求後も参照がある間は回収されない |
| MV09 | 二重 Dispose・中止と終了の競合で保持や budget を二重解放しない。遅延列挙は途中結果を正常完了として返さない |
| MV10 | 小さい履歴予算で中止と回収を繰り返し、現行データ・metadata・作業領域を含む使用量が説明可能で有界になる |
| MV11 | 中止対象がない、callback が終了しない、作業領域が不足する場合に、予算超過や参照破壊で更新を進めない |
| MV12 | native ハッシュ衝突、論理削除、退避、世代切替、履歴切れを不存在と混同しない |
| MV13 | 競合・受付前の通常資源不足・取消後も別取引を受け付ける。I/O等の障害停止と混同しない |
| MV14 | Sync 前に成功や新しい版を公開しない。二表・二索引以上の設置途中も部分状態を返さない |
| MV15 | 先行取引の設置を障壁で止め、後続の耐久化済み取引だけで公開 frontier を飛び越えない |
| MV16 | WAL 受付前、準備のみ、最終受付後、Sync、設置、公開、応答、後始末の失敗で結果照会が整合する |
| MV17 | 実子プロセスを各境界で終了し、checkpoint と WAL から全参加先を復旧する。旧 scope / address は再利用できない |
| MV18 | 部分 checkpoint、WAL prune、outcome forget、復旧再停止で、確定済み結果とデータを失わない |
| MV19 | 生成されたコードに取引 Rows の `.Iterate` があり、現在値 Count / Contains への誤最適化がない。早期終了・例外でも lease を解放する |
| MV20 | 同条件の通常 native source / 新しい取引 source / 通常 LINQ を比較し、追加費用と並行時の改善を区別して報告する |
| MV21 | TPC-E の主表・索引と observer を同じ要求列で照合し、rollback と競合で不整合を残さない |
| MV22 | Gateway の複数 DB DML と DDL 世代、Native の公開が一つの結果に一致する。独立 WAL で別々に確定しない |
| MV23 | 既存 serial の API とコンパイル済み consumer、明示 nested scope、SQL の保存点・所有権・取消・結果照会、旧保存 profile の回帰を通す |
| MV24 | collectible な行型・codec・Native 計画・旧世代の保持と解放を検証し、global cache に寿命を延長させない |
| MV25 | DB A を読んだ後に別取引が DB B の世代を更新しても、後から B を読む同じ scope は ReadVersion に対応する世代と行を使う |
| MV26 | 回収要求後の新規 root / 独立読取りは待機し、既存 root の子スコープ・保存点・確定・中止は進められる。回収と全参加先の再取得が終わってから新規受付が進む |
| MV27 | 回収中に開始待機の取消・期限切れと再開を競合させても、取消済み要求から取引が始まらず、待機用予算を二重解放しない。他の待機者と共通回収は継続する |
| MV28 | 予算不足を起こした更新自身の参照で回収を待ち続けない。長い更新による排出期限切れでも、他の更新の強制中止・予算超過・参照が残る状態の Compact を行わない |
| MV29 | 同じ domain の同時回収要求を一つにまとめる。終了・障害停止が競合した場合は受付を再開しない。独立した domain の処理は進む |
| MV30 | 回収前の予約不足、Compact・共通保存・再取得の失敗を注入する。通常の資源不足と復旧必須の停止を区別し、部分再取得の状態で新しい取引を通さない |
| MV31 | 小さい予算で更新・削除・再挿入と回収を繰り返し、値・索引・世代と metadata / 削除済み head の回収を確認する。中止した読取 scope の遅い Dispose に回収を依存させず、排出待ち時間と回収実時間を別々に記録する |
| MV32 | 属性による読み書きの重複宣言なしで、明示した取引範囲全体が読取りだけと解析できれば生成経路は読取り専用となり、古い版の予算不足による中止対象になる |
| MV33 | 表 A を読み、表 B へ書く生成入口は更新可能な取引となる。A の古い版を読んでいても読取り専用取引として中止せず、自己変更・検証・公開を正しく扱う |
| MV34 | 分岐・別名・呼出し先の書込みを取りこぼさず、解析できないアクセスを読取り専用と推測しない。明示した読取り専用 scope では書込みを実行前に拒否する |
| MV35 | 外側の通常取引から読取りメソッドを呼んだ後でも、同じ取引で別の更新メソッドを呼べる。読取りメソッドを呼んだことだけで取引の区分や予算不足時の中止対象を変えない |
| MV36 | 一つの明示取引内の PlaceOrder と UsePoints を合成して解析する。後者の例外・競合で前者だけを確定せず、両処理の変更を同じ確定・中止単位で扱う |
| MV37 | ソースとアクセス情報のない外部呼出しも共通取引 API 経由なら通常の更新可能取引で実行でき、解析可能な部分の最適化を保つ。外部呼出しの変更も同じ中止・確定単位に入り、読取り専用化・自動並列化を誤って行わない |
| MV38 | CPU 枠を複数 domain / クエリで共有し、呼出し側を含む実行ワーカー数が設定上限を超えない。短い処理の追加、I/O 待ち、取消、再配分で枠の漏れや全体停止を起こさない |
| MV39 | 並列 Join / GroupBy を同じ入力の逐次実行と照合する。偏ったキー・重複・空入力・null・比較規則・自己変更・小さい退避予算でも結果と例外の意味が一致する |
| MV40 | 一クエリの複数 worker の読取りと終了を障壁で制御し、古い読取りの中止・回収が全 worker の参照解放を待つ。handle・buffer・私有 Persisted を二重解放しない |
| MV41 | 安全性を確認した演算だけを自動並列化し、不明なコールバックを勝手に並列化しない。1 worker と複数 worker の両方で FLinq の融合経路と結果を確認する |
| MV42 | 設定省略時は新規 DB でも取引全体排他。Optimistic は有限の予算付きで明示有効化する。同じ取引への二つの公開操作は拒否し、一クエリの内部 worker と混同しない |

Core / Collections の検証は既存の `Tests/Seamless.Transactions.Tests`、Database は `Tests/Seamless.Database.Transactions.Tests`、Gateway は `Tests/Seamless.Database.Gateway.Tests`、TPC は既存の Tpc.Tests / TpcE.Tests に追加する方針。実装を写すテストを増やさず、複数の内部経路を同じ不変条件で検証する。

各条件について確認すべきassertionと、確認済み/要照合を分けた[製品受入の証拠対応](20261104-mvcc-acceptance-evidence.md)を使用する。単にテスト名が存在することや狭い回帰試験の成功を、42項目全体の完了へ読み替えない。

規模は64～1万行で保持・取消・回収を再現し、10万行を代表性能条件に使う。予算を小さく設定して圧迫を作り、巨大な全件配列を作らない。Debug / Release は同じ source snapshot と失敗を区別した証跡を使う。8論理CPU、D の作業・実データ、F のログ・キャッシュ、共有 BGO を使わない規約とメモリ監視を維持する。

性能は成功処理量、p50 / p95 / p99、競合・中止率、割当、native / managed / metadata / WAL / 退避の容量、回収の待機・実行時間、復旧時間を分ける。回収前後だけの短い burst を継続運転の成績にしない。既存628セルは参考値として残し、この製品の成績へ読み替えない。

## リスクと対策

- 更新方式を遅延にしても、並行する古い読取りのための旧版保持は必要。変更集合だけで snapshot が完成したとみなさない。
- 正常な競合を既存の広い catch へ入れると domain 全体が故障する。受付前の中止と永続化障害の処理境界を分ける。
- 全走査の安定化と multi-input の lock 順で deadlock を作らない。domain gate の初版費用を計測する。
- 版を読むための metadata が実データ以上に増える可能性がある。依存集合・予約表も予算化し、必要な horizon より古い項目を安全に整理する。
- 静止回収では大きな表の停止が長くなり得る。回収時間を測定せず「短時間」と保証しない。
- 既存 prepared journal の直列性が group commit を制限する。試作の FileWal の数値から Gateway の速度を予測しない。
- native アドレスは再配置・再起動で変わる。保持と世代を検査し、WAL に永続的な業務参照として記録しない。

## 実装・試験で確定する事項

**回収方式の判断:** 案 A を利用者と合意済み。新規取引を待たせ、既存の処理・参照の終了を確認してから回収する。案 B の並行回収は初版の必須課題に置かない。

**コードと試験から確定する事項:** 型名と配置、既存 consumer の互換性、親 budget への各費用の計上、具体的な予算と排出待ち上限、native / managed callback の取消検査間隔、エラーの SQL / Native への表現、依存集合の退避・整理、性能の許容幅。

## 合意状態

方式・必要な並行性・全体性能優先・履歴不足時の古い読取取消・TPC-E / Gatewayの共通化・高度APIの対象範囲・初版の回収方式A・新規DBの既定排他方式は合意済み。DB本体はTsavoriteのMemory/Storageを対象とし、Persistedは私有作業領域に限定する。Join/GroupByを含むクエリ内並列化とCPU配分は初版に含め、NUMA・I/O構成とCPU/メモリを一体に扱う高度な制御は後に回す。明示した取引範囲の静的解析では、不明な呼出しを更新可能な共通経路へ残す。生成コードへ詳細な判断・処理・寿命・同期のコメントを残し、必要なDebug情報も埋め込む。現在の実装範囲は冒頭の表、受入の実行結果と限界はMV条件表と証拠対応で示す。
### 世代参加先の退役

公開から外れた世代や破棄した候補の物理表を、その場で論理DBから除去しない。共通 coordinator の退役要求へ渡し、新規 root の受付停止、既存 root と worker の排出、全参加先の回収、共通 checkpoint、handle 再取得を完了してから所属を解除する。checkpoint 失敗・排出期限切れなら所属は維持する。物理表・Store・collectible 型の Dispose は成功を受け取った所有者が行う。永続 journal を使う場合は共通 checkpoint callback の設定を必須とする。

退役要求のコピー・待機情報も metadata budget に計上する。取消は要求の受付前まで有効とし、受付後は退役結果が確定するまで待つ。取消だけが返って実際には所属解除が進行する曖昧な寿命契約を作らない。同じ表の同時要求は同じ回収にまとめる。退役 API は保存先の削除や業務処理の再実行を行わない。

### Gateway候補を版付き保存先へ接続するときの境界 — Guardrail

現行の`GatewayGenerationStore.Seal`は、現在値overlayを候補へ反映して保存した後、候補DBを閉じ、seedをliveへコピーして別のDBインスタンスを開く。この手順を、活動中の共通MVCC scopeが参加先や列挙子を保持したまま流用してはいけない。論理DBへの子の登録、候補の封印、WALが指す保存先、公開後の物理handleを同じ寿命契約へそろえてから、Gatewayの版付き設定を有効化する。

候補の行を共通WALへ含める場合は、再生に必要な全世代のdescriptorとcodecを復旧前に用意する。最終公開世代だけのdescriptorを保存して、未公開の中間候補向け行をWALへ残さない。保存点が参照する世代・型・Native計画は保存点の寿命まで保持し、公開カタログの行と業務行は同じReadVersionで選ぶ。子の登録やメモリ上のcurrent差替えだけを公開完了とみなさない。

### 未公開の版付き初期データ — Decision

新しい候補の初期データは、通常取引の変更集合へ全件stagingせず、`VersionedTransactionalTable.BeginUnpublishedSnapshot`の専用writerで逐次投入する。対象は使用開始前かつ未復旧の保存先だけとし、固定した正の基準版を全行へ付ける。DBの候補構築は基準版1を使い、空の候補もcheckpointのCommitVersionに1を残す。初期行は候補のsnapshotとして保存し、業務WALへ個別commitしない。

writerのnative accessorと作業領域を共通予算へ予約し、単行のnative容量を検査してから追加する。重複キーは上書きしない。構築中の通常アクセス・復旧・回収を拒否し、完了後は初期投入を再開できない。未完了で破棄した表は使用不能とし、DB builderの失敗・破棄でも候補DB全体を使用不能にする。保存済みの空表にも後から初期行を混入できないよう、保存先のcheckpoint復元済み状態を照合する。

受付準備は`PrepareForTransactions`で明示し、復旧版を照会するgetterだけで未使用表の状態を変えない。初回のdomain開始では全表を準備し、使用中のgroupへ後から参加する表も所属登録前に準備する。投入中の表を登録して未完成の世代を使える状態にしない。

### Gateway接続で残る所有権の実装順序 — Decision

DBコンテナの内部bindingで、共通coordinator・論理DB group・WAL ID・世代ID・受付可否を渡す。通常の単独DBの入口は維持し、Gatewayが所有するbindingのない版付きGateway設定は引き続き拒否する。共通coordinatorを各DBから独立してRecover・保存・Disposeしない。codecと競合prefixに世代IDを含め、全表の生成が成功してからgroupへ一括登録する。一括登録は失敗時に部分所属を残さず、回収との間でも原子的に扱う。

候補は共通scopeへ表を登録する前に初期snapshotを完成・保存し、実際に使うlive側の保存先を開く。候補への業務差分と公開カタログはその後の共通scope/WALで確定する。保存点が保持する中間世代の差分をWALへ含めるなら、その世代も復旧可能なdescriptor集合へ含める。所属解除と物理Store・型の解放は、共通scope・保存点・workerの参照を排出した後だけ行う。

ID/rowversion予約は、業務rollbackから独立したmetadataの確定として維持する。版付きscopeの代用に`CommitExternalAsync`を使わない。業務取引の途中に独立した予約scopeを開始する場合、回収待ちが既存の親取引の終了を待っている間でも予約を進められる内部経路が必要になる。通常の新規root受付へ並べると、親が予約を待ち、回収が親を待つ循環になる。親が活動中であることと書込み権限を検証した保持を使い、業務取引への合流・自動再実行・親の中止による予約の撤回をしない。

**実装済みの境界 — Fact:** 内部`GatewayVersionedBinding`と世代一括登録、共通scopeを使う通常の`db.Table`アクセスを実装した。複数DBの読取り時点、世代別の競合、保存点、共通WAL復旧、退役後の物理解放を小規模試験で確認した。`GetGatewayTransactionRows`は同じscopeをFLinqの既存native走査へ渡し、生成コードでもWhere/Sumの融合と詳細コメントを確認した。`GatewayGenerationStore`の内部owner経路で私有seedの封印とliveへの接続を実装し、256行の生成DBでcheckpointの有無それぞれの共通WAL復旧を検証した。Gateway管理者のSQL受付・版付き公開カタログ・Native公開も共通scopeへ接続した。所有GatewayDatabaseHostは有限のTransactionHistoryを渡して開始でき、所有bindingなしで個別DBだけを開始する経路は引き続き拒否する。

資源会計と旧ページ返却の変更前の関連回帰は、共通取引116件・DB取引62件・Gateway関連54件・TPC-E全体41件をDebug/Releaseで実行し、計546件が成功した。失敗・スキップ・ビルド警告は0件。当時の両構成の入力hashと1,251ファイルを照合した。根拠はF:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/transaction-tail-latest-20261103.jsonと各セルのTRX・入力一覧。変更後の検証状況は本書末尾のnative常駐領域の節を参照する。Gatewayには版付き方式と既定方式の12停止点ずつ、部分checkpoint4条件、8KiBの履歴圧迫、並行方式切替えの独立予約復旧、2DBのDDLとNative再構築の確定前・1DB反映後・公開後の障害復旧、実行中Native操作の排出と新規取引の受付待ちを含む。TPC-Eには従来4方式の比較・故障検出、VL/VFとの比較、driverと業務行の同時復旧、metadataなしに行だけ進んだ場合の再開拒否、4停止点×2variantの実プロセス終了を含む。WALの回収後に共通checkpointを復旧する最小試験は修正前の失敗と修正後の成功を確認した。結果台帳の稼働中照会でWALを再走査しないことも少量データの割当上限で検証する。生成FLinqのnative Iterate・融合・詳細コメントの検証は維持する。長期連続運転・残るクエリ形状・全体性能の受入完了とは扱わない。

**版付きsnapshotの検証値 — Decision:** 候補の封印と共通checkpointでは、全root/workerを排出してnative regionを閉じた状態で、可視headだけを逐次走査する。旧版と削除済み行は数えない。キー・行をcanonical表現にし、既存の行hashの和・xor・件数による順序非依存の検証値と同じ規則で集計する。作業領域は予算化し、全件配列や新しい業務scopeを作らない。検証用accessorを閉じてからStoreを保存する。

**私有seedの能力 — Decision:** 共通domainへ参加する前の候補だけに内部`GatewayVersionedSeed`を渡す。WAL ID・DB ID・世代ID・構築所有者を照合し、他のDBへの使い回しを拒否する。保存形式・codecの世代識別はliveと同じにする。初期snapshot以外の業務更新を許可せず、基準版1の構築完了後に全handleを閉じ、検証値とcheckpointを保存して封印する。共通WALもDB別WALも書かない。封印済みseedを閉じてliveへコピーし、liveは別のbindingで開いてから共通groupへ登録する。公開Gatewayが利用するカタログの初期版も1以上にし、候補の基準版が所有取引のReadVersionを越えないようにする。

**親に保持された独立metadata確定 — Decision:** Gateway内部専用の`CommitIndependentMetadataAsync`は、同じcoordinatorが所有する活動中かつ更新可能な親scopeを保持している間だけ使える。親の通常操作と同時に進めず、独立したID・ReadVersion・metadata予約を持つcontextを共通domainへ登録する。新しい業務rootの受付は回収待ちに残すが、このcontextは既存の親が回収を止めている証拠を使って受付できる。行へのアクセスや公開scopeを返さず、metadataだけを通常の確定順序と耐久化へ通す。親がExclusiveのgateを保持しているときも二度gateを待たない。確定後は親のrollbackや保存点で予約を撤回しない。durable後の取消は確定を取り消さず、contextの排出まで親の保持を返さない。未受付の取消・引数不正は共通domainを故障させない。

**自動中止時の外部実行資源 — Decision:** Gatewayの実行contextが予約したmetadataを利用者の遅いDisposeまで残さない。共通rootへ内部の完了callbackを予算付きで登録し、通常終了・rollback・読取りの自動中止で一度だけ解放する。先に実行contextを閉じたときは登録を解除する。回収側から元のAsyncLocalを書き換えず、残ったcontextからDB所有者への参照を切る。捕捉済みcontextの再利用は取引の中止理由または終了状態で拒否する。callbackは行や計画の利用を再開せず、資源解放だけを行う。

### Gateway管理者の結線で維持する境界 — Decision

- 接続が指定した取引IDを共通scopeにも使い、接続間で開始・操作が混ざらない内部入口を用意する。共通scopeの受付やschema lockを待っている間は管理者のgateを保持しない。確定側が同じgateで公開を進めるため、待ちながら保持すると循環待ちになる。
- DB名→世代参照とNativeモジュール名→計画参照を、基準版1以上から始まる版付きカタログへ保存する。SQL/Nativeの参照先は所有scopeのReadVersionで選び、そのキーの読取りもOCCの依存にする。現在のregistry辞書や最初に触れたDBのコピーだけで全DBのsnapshotを決めない。
- 私有seedをliveとして開いた候補は、最終公開候補だけでなく、同じ取引のWALが参照し得る中間世代もdescriptor集合へ保持する。保存点から候補を戻しても、残った変更集合の復旧先を失わない。全世代の保持はmetadata/保存領域の予算へ計上し、共通排出・保存後に所属と物理保存先を解放する。
- 全行の設置後、共通prefixを公開する前に、確定済みのDDL/Native参照と必要なID予約値を反映する内部の設置完了処理を用意する。Commitの戻り後に現在の辞書だけを差し替えて、その間に新しいsnapshotが古い参照を選ぶ隙間を作らない。設置完了処理の失敗はdurableの有無を保ってdomainを停止し、業務処理を再実行しない。
- 復旧は共通checkpointと保持されたWALのmetadataから必要な全descriptorを集め、全codec/参加先を登録してから行を再生する。途中まで保存された各Storeが共通checkpointより進んでいても、共通の保存完了までは旧WALの必要部分を削除しない。現在公開中の一世代だけを開いて全WALを再生しない。
- DMLのDB全体write lockを共通OCCへ置き換える一方、DDL/Nativeのschema保護と取引の順次操作は維持する。論理取引、保存点、SQL計画、Native計画の保持もroot完了に接続し、古い読取りの自動中止が利用者の後片付け待ちで回収を止め続けないようにする。

### Gateway旧世代とNative計画の回収 — Decision

公開を終えた旧世代と放棄した候補だけを退役対象にする。活動中の取引がなくなった時点で自動回収を開始し、明示`Checkpoint()`も同じ直列化した経路を使う。共通domainは新規rootを待たせ、既存scopeとworkerを排出する。通常の退役は既存readerを中止せず、履歴不足の回収要求が合流したときだけ読取専用取引の中止へ切り替える。複数世代の退役要求は一括受付し、共通checkpointが対象のdescriptorを復旧集合から除外した後に、所属と物理Storeを解放する。保存済み範囲の保持WALもCRC・形式を検証し、共通checkpoint後の準備チャンクとcommitの対応はhashで検証する。保存済み行の古いcodecは解決しない。未保存の行には引き続き正しいcodecを要求する。

Nativeの旧計画は古いsnapshotから選択され得る間は解放しない。保持するsource・依存・manifestの作業予約を共有予算へ計上し、全取引の排出後に旧計画と一緒に返す。SQLの古い計画が保持する行ソースもこの回収境界で解放する。自動回収workerは終了した接続のExecutionContextを引き継がず、hostの終了時には所有する回収workerの終了を待つ。回収の失敗は診断へ残し、物理回収開始後の共通domain障害を解除しない。

ファイルは公開中と一つ前の参照を保持し、削除対象の絶対パスを新規受付停止中に固定する。受付再開後の候補を後から列挙して削除しない。ALCのUnload後もWindowsがDLLを保持する場合、ファイル削除は`LastGenerationCollection.Deferred`へ残して次回に再試行する。製品コードは強制GCを実行しない。Storeと予算の解放、ALCの収集、物理ファイル削除の完了は区別して検証する。

### Gateway SQL点更新 — Decision

単一かつ非null・非計算列の整数主キーと生成キー構造の対応を証明できる表では、等値UPDATE/DELETEをnativeの点操作へ接続する。主キーの重複検査は保存先の一意性で保証し、全件主キー検証による不要な走査依存を作らない。WHEREの元の述語は取得行へ評価する。型の損失がない整数キーだけを渡し、範囲外・不明な式・複合キーなどは既存走査へ戻す。UNIQUE・CHECK・FKなど他の制約検証は維持する。同じ行の競合は中止結果を返し、業務処理を自動再実行しない。

### Gatewayプロセス停止試験 — Decision

既存の現在値方式の試験workerを版付きprofileにも接続し、候補構築前・構築後・コピー後・封印後、WAL記録前・準備チャンク耐久化後・共通commit耐久化後、DB反映後・公開前・公開後・全DB反映後・保存途中で自分自身を強制終了する。各条件は少数行・2DB・互換なNative計画を使う。停止点へ到達した証拠を取引ID・PID・生成時刻・profileと共にflushしてから終了し、親は所有する子の終了と記録を照合する。無関係な異常終了を成功として扱わない。再起動後は業務行・型・Native計画・rowversionの独立予約値を照合し、耐久化前は旧状態、耐久化後は全参加先の新状態であることを確認する。保存途中の失敗でも共通checkpoint完了前の必要なWALを失わないことを検証する。

### TPC-E比較入口への接続 — Decision

既存TL/TF/PL/PFは比較の対照として維持し、版付きTsavoriteの標準LINQをVL、FLinqをVFとして明示登録する。別アセンブリから同じ共有取引ソースを型別名でコンパイルし、VFのRowsをTransactionRowsのまま展開する。Persistedを共有業務表のMVCC保存先に追加しない。既存の既定選択と`--variants all`は従来の4variantを維持し、VL/VFは明示選択する。

検証・digest・読み戻しには共通の読取り専用snapshotを用意し、保存は版付きdomainの共通checkpointを通す。大きな初期投入も単一の確定単位を保つため、TPCの版付き永続profileをprepared WALへ接続する。既存の実験用v1とは異なるv2識別子で新規領域を要求し、自動変換しない。予算とTsavoriteの作業量はStoreの上限と規模から有限に決める。

比較driverの運転状態は業務DBとは別の外部所有情報だが、再起動の継続位置として業務行と一致しなければならない。版付きvariantでは、取引結果から作った次の運転状態を共通WALのmetadataへ入れ、行と同時に耐久化する。初期投入もGenerationResultから作った初期状態と一緒に確定する。Trade-Orderが業務rollbackを選んだ場合は、業務行を戻した後に進行状態だけを独立確定する。読み取り結果に応じたdriver更新も含め、既存の保守的な更新可能scopeを維持する。比較用のdriver状態保存の費用を製品queryそのものの性能と混同しない。

共通checkpointはmetadataの最新確定値も保存し、その完了後だけWALを回収する。復旧ではcheckpointとその後の共通WALからdriver状態を復元し、古いdriver状態を使って確定済み業務を再実行しない。既存方式の「最後の保存点」契約と版付き方式の「最後のdurable commit」契約をcrash campaignで区別し、子の取引開始・完了の記録と復旧した通し番号・全表digestを照合する。

比較用metadataなしの行確定が後続した場合は、保存とWALの両方に継続位置の不整合を残して検出する。比較driverの再開を拒否し、既存の古い位置から処理を再実行しない。一般の版付きDBでmetadataを使わない利用自体は維持する。checkpointのmetadata上限はprepared WALの同じprofileの上限と一致させる。

### 結果台帳と復旧境界 — Decision

prepared WALの復旧にも共通checkpoint LSNを渡し、回収済みprefixを要求しない。保持WALのCRC・形式検証は続け、checkpoint後の準備チャンクと最終commitをhashで結び付ける。checkpoint以前の古い行のcodecを復旧へ要求しない。

稼働中の結果台帳は、writerが返したreceiptのLSNを予約済みの受付項目へ登録し、DurableLsn以下になった項目だけCommittedへ進める。照会のたびにWALの既存行を再読込みしない。起動時は保持WALと結果snapshotから台帳を再構成する。受付失敗、忘却と受付の重なり、snapshot保存失敗時の停止、既知の確定結果の保持という契約は維持する。

### 共通checkpoint後に追加されたGateway世代 — Decision

共通checkpointに含まれる世代には、そのLSN以上のStore snapshotを要求する。その後の共通WALで追加された世代は、検証済みの封印seedとcheckpoint後のWALから復旧する。新しいseedへ、作成前の共通checkpoint LSNのsnapshotを要求しない。checkpointに含まれた既存世代の欠落検査は維持する。

### VF生成物の確認 — Fact

VFをDebugで生成ソース付きビルドし、14ファイルに6,696行の手順・計画説明と、TransactionRows.Iterateの実呼出しを確認した。WhereとCountのnative融合ではworkerを追加しないこと、checkedの範囲とcallbackの順序を維持することも生成コメントに残る。全queryがnative走査や並列経路を使うという意味ではない。入力の型・形状に応じた既存の展開経路を維持する。証跡はF:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/tpc-vf-generated-20261103.jsonと、その参照先の生成ソース・ビルドログに保存した。

### 通常のTableからNative/FLinqへの接続 — Decision

生成器はキー付きTableとMaterializedViewを型で認識し、入力式を一度評価して内部向けGetQueryRowsへ接続する。DatabaseRowsは入口で版付きGatewayの所有scopeを捕捉し、その場合は既存TransactionRowsのnative走査・並列Count・予算・operationへ委譲する。版付きCountへ現在値の格納件数を流用しない。従来方式は既存の読取sessionとoverlay列挙を維持し、callbackのない単純Count/LongCountだけは読取保護下の既存の論理件数を利用して不要な全走査を避ける。従来方式の単純走査は逐次とし、版付きsnapshotやworker並列と誤記しない。結果bufferはDBの共通予算へ計上する。Join/GroupByの対応形状は同じ行ソースとして再利用し、sourceの再束縛でも動的な接続の説明を維持する。

この入口で取引を暗黙に開始したり、独立したcoordinatorを作ったりしない。版付きGatewayの所有contextなし・終了後の利用は既存の拒否を保つ。standalone SharedV2は既存の型付きtx.Table.Rowsを利用する契約を維持する。履歴ストアの評価・専用化とは独立した境界とし、保存形式を変更しない。

**生成物と実行の確認 — Fact:** 通常TableのArray/List/Dictionary、自己変更・削除を反映した件数、rollback、捕捉済み行ソースの旧版保持と終了後の拒否、実体化ビューとJoin/GroupByをDB試験で確認した。Nativeの独立ビルドは従来方式/MVCCをDebug/Releaseの4ケースで実行し、私有更新後の値とrollback後の値を照合した。試験ごとの専用ビルド領域を使い、インストールしたDLLのhashで対象のartifactを選ぶ。最終4ケースのDLLと生成コードのhash、GetQueryRows/Iterate/予算付き結果/件数経路の証跡はF:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/native-rows-generated-20261103.jsonへ保存した。

### 小規模な連続運転の検証入口 — Decision

既存ProductTransactionsへ独立したenduranceコマンドを追加する。256行、更新worker4、各worker8取引を一周期とし、Memory/Storageで指定周期数だけ繰り返す。最初の更新取引を全workerで準備してから確定へ進め、別行の取引が実際に重なる条件を作る。業務処理の再実行は行わない。古い読取専用取引を更新中も保持し、更新後も旧値・Count・GroupBy・Joinが同じ読取り時点を使うことを検証する。現在値は保存先とは独立した小さい配列モデルと全行照合する。各周期で履歴回収・共通checkpointを通し、Storageは定期的に閉じて再オープンする。資源使用量・CPU枠・成功数・所要時間とソースhashを記録する。有限回の反復の成功は、無期限運転・Gatewayの長期運転・性能受入の完了とは区別する。

**検証値 — Fact:** 現在の同一入力で、既定UpdateはDebug/Release各8周期、後述のChurnはDebug/Release各512周期と終了境界2/3周期の計6条件が成功した。各周期で全行・点検索を独立モデルと照合し、古い読取りとFLinq Count/Sum/GroupBy/Joinも検証する。ビルド警告は0。両構成と現在の784入力を照合した。根拠はF:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/endurance-phase15/endurance-latest-20261103.jsonと各report/spec/receipt/source-manifest。256行は維持し、ストア生成・allocator交換の既存固定費を覆う共有予算を32MiBとする。記録するResourceUsageは共通予算の計上値であり、プロセスやOS全体の実メモリ量とは区別する。専用履歴版の比較測定値として流用しない。

**結果台帳の保持 — Fact:** 既定Updateは取引結果の忘却を要求せず、Storageの結果台帳を保持する。`TransactionJournal`は確定結果1件につき96バイトを共通予算へ計上するため、履歴回収後も台帳分の予約は更新数に応じて増える。予約の増分をそのまま履歴ノードの増分やメモリ解放漏れと呼ばない。一方、台帳を忘却した条件でも増えるnativeページは別の問題であり、以下の反復回収試験で区別する。Churnの忘却条件を業務システムの保持期間や自動忘却の既定仕様にはしない。予約値は実際のCLR heap/プロセスのメモリ量とは区別する。

### Join/GroupByの逐次終端と後段の接続 — Decision

**Join後の整列の接続 — Decision（2026-11-04）:** Join→OrderByが一般の整列へ落ち、16,384件の128バイト値（結果配列だけで2MiB）を残予算1.5MiBでも返すことを確認した。同じ残予算のJoin→Countは成功しており、入力走査そのものの不足ではない。負の対照はF:/CodexTemp/Seamless/mvcc-join-order-20261104/Debug-test-dda401659e234ac49f19c7028580e41f/results.trx（5条件中Join整列だけ失敗）。整列に共通のInputBudget/キー予約/BCL接続をGroupByと共有する。内側は既存のnative producerと有界worker Lookupを維持する。外側はsnapshotの有界な行列挙から一件ずつ照合し、結果selectorと整列キーの交互評価を保つ。結果を先に全件配列へ置き換えない。整列前のWhere/Select/Skip/Takeは通常LINQの列の能力を維持して接続し、同じoperation/query scopeがLookup・整列・結果の全解放まで保持する。通常の整列なしJoinのnative融合経路と一般Array/Listは変更しない。複数整列、nullキー、明示比較器、短絡、callbackの順序、予算不足と返却を通常LINQへ照合する。

**Nativeの恒等キー整列 — Fact/Decision（2026-11-04）:** Nativeの通常Tableから始まるJoinは、両入力がDatabaseRowsRootであるにもかかわらず専用経路へ入らなかった。生成したfallbackコメントで、入力・終端・型引数・hoistは適合し、後段OrderByDescending/ThenByだけが不適合となることを確認した。IRは`x => x`を`Func=null`の恒等キーへ正規化するが、取引用整列が関数不在を未対応と判定していた。恒等キーもIRのKeyType/InputTypeから型を決め、InputBudgetとKeyBudgetを通す。Join/GroupByの恒等主キー・副キーを通常LINQと比較し、Nativeでは従来/MVCCの両方で`[Join/整列/取引]`と予算付き整列の生成・実行をDebug/Releaseで確認した。未接続の場合は入力・終端・後段の判定情報を生成コメントへ残す。

**Join・整列の改善前の追加性能比較 — Fact（2026-11-04、固定snapshot）:** 4,096/16,384行・Memory/Storage・query worker上限1/4で通常LINQと比較した。16の独立プロセス条件、640時間標本・64メモリ観測を監査し、802入力が現在ソースに一致した。Joinはmanaged割当を約36～42%削減したが、時間は形状により通常LINQの0.94～1.23倍だった。GroupBy集計後の整列は割当を約52～76%削減した。groupを返す複数整列は2.33～5.22倍の時間、1.12～3.10倍の割当となり、groupごとの所有配列・QueryResultBuffer/codec解決の固定費が改善候補である。現在は[所有化の改善報告](20261104-group-materialization-results.md)の三段階を採用済み。同条件内で旧/新を交互に実行した32条件・1,280標本では、Memory/worker1のgroupを返す複数整列が4,096行で5.728→1.148ms・割当6.191→0.898MB、16,384行で9.892→3.299ms・8.551→2.029MBへ改善した。Execution94件、DB展開19件、Native統合2件が両構成で成功。追加効果が小さくなったため反復改善を終了した。上記の改善前の数値を現在の性能とは扱わない。CPU寄与率は未分離。予約量・累積割当・GC後の保持量・OS値を区別し、全条件と限界は[追加測定報告](20261104-ordered-join-comparison.md)に残した。測定時に窓のないGroupBy整列が常に偽の終了分岐を生成するCS0162を再現したため、その不要な分岐を生成しない修正を加えた。53種類の終端比較へ警告不在を追加し、従来/MVCCの2条件がDebug/Releaseで成功した。
**検証snapshotのビルド更新時刻 — Fact/Guardrail（2026-11-04）:** 既存ファイルへ内容Bをコピーして元の古い更新時刻を残すと、MSBuildの増分ビルドが以前のAのDLLを再利用する最小再現を得た。同じ内容Bの更新時刻を進めると実行結果がBへ変わった。証跡はF:/CodexTemp/Seamless/mvcc-input-time-probe-20261104/result.json。検証runnerはコピー直後のSHA256を記録値と照合し、内容をコピーしたファイルの更新時刻を進める。最終検証のForceCompileはsnapshot内の全C#入力を更新して再コンパイルし、要求とrunnerのSHA256をvalidation-specへ記録する。ソースmanifest一致だけで旧DLLを排除したと判断しない。過去の実行が全て旧DLLだったという推測にも置き換えない。
**複数整列の先行段階検証 — Fact（2026-11-04）:** 一般融合の複数OrderBy制限を、専用rendererへ接続できる取引用終端だけで解除した。39種類の終端と24種類の逃避結果を、空/自己変更・5個のTake値・従来/MVCC・worker上限1/3で通常LINQと比較し、各構成1,740組、計3,480組で値・例外型・callback列・結果寿命が一致した。比較器factoryによる引数変数の変更、複数整列の間の投影、groupからの配列投影も含む。1MiBの要素配列を残予算768KiBで要求する4条件は、全て通常の予算不足として拒否し、予約を返す。後続整列の作業領域も最初の入力を渡す前に予約する低レベル試験を追加した。

同段階の最終入力のDB73・Execution84・共通取引155件がDebug/Releaseで成功した。Nativeの従来/MVCCの独立ビルド4条件も成功し、複数の整列呼出しと2つのInputBudgetが同じqueryにあることを生成構文で確認した。名前の正規化やprivateメソッドのUnsafeAccessor化で変わる文字列を、生成経路の検査条件にしない。業務処理の合成試験はOrdersへの追加も含め、後者の例外と競合で注文だけが残らないことを確認した。計628実行の失敗・スキップ・ビルド警告は0。25プロジェクト・1,218入力の構成間、追加ファイル、監査時の現在ソースとの一致を確認した。証跡はF:/CodexTemp/Seamless/mvcc-group-execution-20261104/multisort-acceptance-audit.json。Gateway全体とTPC-E全体の再検証はこの628実行とは分ける。以下の664実行などは、それぞれ記録された先行段階の証拠である。

**複数整列と比較器式の接続 — Decision（2026-11-04）:** 初回の入力件数は後続のWhere/Select/Skip/Takeと整列にも上界となるため、後続OrderByの入力作業領域、全キー、整列に保持される投影値を最初のInputBudgetへ登録する。後続整列の間に列挙ラッパーは追加せず、通常LINQのCount/Any/First/Lastの最適化と評価順を維持する。比較器式はグループ用から各整列用まで元の順で一度評価し、Take(0以下)でも式の評価は省かない。キーselectorや比較をworkerへ移す範囲は広げない。型を表現できない投影の既存診断契約は維持する。初回の多段配列試験では既存fallbackの配列生成構文エラーも見えたため、groupを直接返す形へ切り分けた。16,384個の64バイト値を持つgroupが残予算768KiBでも返る2条件を確認し、順序なし/単一整列の2条件は予算不足になった。修正前の証跡はF:/CodexTemp/Seamless/mvcc-access-acceptance-20261103/Debug-test-177b40a123f3423381a4880449e57653/results.trx。複数整列、比較器factory、途中投影、空入力、窓、短絡、結果の寿命と予約返却を通常LINQと比較する。

**group後の整列の現在の範囲 — Decision:** GroupBy後のOrderBy/OrderByDescendingとThenBy/ThenByDescendingを、同じ取引operationとquery scopeへ接続する。独立した複数の整列も、一つへ融合せず通常LINQの各ブロックを保つ。前後・途中のWhere/Select/Skip/Takeは元の評価位置を保つ。順序キーの評価、安定整列、既定/明示比較器と終端の短絡は通常LINQへ委ね、その内部の入力コピー・map・キー配列を件数から割当前に予約する。キーが参照を持つときはselectorの返却時にpayloadも予約し、全整列と結果構築が終了するまでscopeが保持する。入力groupは必要時に所有配列へ移し、整列後の遅延結果へLookupを逃がさない。型名を表現できないキーなどの既存診断は維持する。一般のArray/Listの複数整列の融合制限は維持し、専用rendererと同じ判定で取引用終端へ接続できる場合だけその制限を外す。

**引数の評価位置 — Invariant:** グループ比較器、投影前のSkip/Take、後続各段の比較器とSkip/Take、終端の辞書比較器を元の呼出し順で一度保存する。Takeが0以下でも後続の比較器式の評価・例外は省かず、引数を全て評価してから入力の短絡を決める。比較器factoryが同じ変数を書き換えても、先行するTakeの保存値を変えない。比較器式を全て先に評価した実装では、Take(1)後のfactoryが引数を0へ変えると入力callbackを全て省いてしまうことを通常LINQとの比較で確認した。修正前の4条件の証跡はF:/CodexTemp/Seamless/mvcc-access-acceptance-20261103/Debug-test-e3ac2866f284471a8b5cb3153ca06ce2/results.trx。生成コメントにも引数の保存順と早期終了の位置を残す。

入力を先に配列へ置き換えるとFirst/Lastで投影とキーを交互に評価する挙動や、Anyの件数取得可否まで変わるため、`InputBudget.Read`から通常LINQへ逐次渡す。要素を渡す前に入力・map・全キー・最終結果の配列上界を予約し、入力列挙が終わっても予約をscope終了まで保持する。DB行ソースのGroupByではCount前のOrderBy除去とFirst→MinByへの変形を抑え、予算付きの整列経路へ渡す。通常Array/Listの変形条件は維持する。BCLの1要素時のキー省略も保つため、ToArray/ToListは直接呼ぶ。配列上界は.NET 10の[倍増するSegmentedArrayBuilder](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Linq/src/System/Linq/SegmentedArrayBuilder.cs)と[整列・終端の実装](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Linq/src/System/Linq/OrderedEnumerable.SpeedOpt.cs)を確認し、要素用に`4N+32`枠、位置mapと各キーのN枠を予約する。これらは稼働中の作業領域の保守的な上界であり、プロセスRSSや返却後のCLR共有poolの使用量ではない。ランタイム更新時には上界と終端の挙動を再検証する。

**整列と取込互換の検証 — Fact:** 36種類の終端の比較と16種類の逃避結果の比較を、空/自己変更・5個のTake値・従来/MVCC・worker上限1/3で実行した。各構成1,360組、Debug/Release合計2,720組で値・例外型・callback列・取引終了後の列挙が一致した。明示した比較器、ThenBy、窓のないFirst/Last、Any/LongCount、キーが例外を投げるCount/Firstも含む。予算不足の2条件、入力・キーpayloadの事前予約と解放も検証した。DB71・Execution83・共通取引141・Gateway取込/Native4・TPC-E機能33件が両構成で成功し、計664実行の失敗・スキップ・ビルド警告は0。35プロジェクトの閉包とSharedの1,332入力について、構成間、監査時のソース、追加ファイルの有無を照合した。証跡はF:/CodexTemp/Seamless/mvcc-group-execution-20261104/ordered-group-acceptance-audit.json。

Gateway全体の先行Debug回帰は360成功・1失敗・4スキップだった。失敗は同期取込の期待と互換性に関するもので、修正後の上記Gateway4件に含めて両構成で検証した。全365件を修正後に再実行した結果ではない。TPC-Eは整列接続の変更前にプロセス終了8条件を含む全41件が両構成で成功し、変更後は機能33件を両構成で再確認した。プロセス終了条件まで変更後の入力で再実行済みとは扱わない。

**2026-11-04 逃避groupの接続 — Decision:** 既存の有限予算・operation寿命・初出順の仕様を維持し、型名を生成できる非集計投影とgroup自体を返す終端も分割Lookupへ接続する。非集計の投影に渡すgroupは、要素をQueryResultBufferで事前予約して所有配列へ移したIGroupingとする。配列とkey/group管理費の予約はquery scopeが結果構築終了まで所有し、返却後のgroupは取引・Lookup・Persisted・scopeを参照しない。解析済みの非逃避集計は従来どおり借用groupを使い、コピーを追加しない。型名を表現できない匿名型などの既存診断契約は変更しない。前段のselector、後段のcallback、Skip/Takeと末尾取得の評価順を保持する。通常Array/Listの無予算builderは変更しない。

**修正前の観測 — Fact:** 16,384行から64バイト値へ要素投影し、group.ToArrayの結果配列だけで1MiBになる条件で、残予算768KiBでも正常終了した。通常の逐次Countは同じ残予算内で成功し、fixture投入・走査の失敗ではない。証跡はF:/CodexTemp/Seamless/mvcc-access-acceptance-20261103/Debug-test-ec9118df497f4914a35151000bfa861c/results.trx。修正後は予算不足でResourceLimitExceptionを返し、十分な予算では同じ値・順序を返すこと、groupを取引終了後にも列挙できること、例外後に予算が戻ることを検証する。

**逃避groupの検証 — Fact:** DB全70件とExecution全80件がDebug/Releaseで成功した。逃避groupのArray/List/Dictionary/First/Last、非集計投影とresult selectorを、従来方式/MVCC・worker上限1/3・空入力/自己変更・Takeの5値で通常LINQと比較した。値・例外型・callback順序を照合し、queryと取引終了後にgroupと遅延IEnumerableを列挙した。予算不足後・正常終了後の予約返却、参照型要素と最初のkeyの同一性、要素列挙中の例外と取消も確認した。300実行の失敗・スキップ・ビルド警告は0、856入力が両構成と監査時のソースに一致した。証跡はF:/CodexTemp/Seamless/mvcc-group-execution-20261104/group-regression-audit.json。

Gatewayの独立Nativeビルドでも、通常Tableからgroupと要素配列を返し、query終了後の集計・自己更新・rollbackを検証した。従来方式/MVCCのDebug/Release計4実行が成功し、生成物のQueryGrouping.MaterializeとPartitionedLookupBuilderを照合した。1,144入力の構成間・監査時ソース一致と警告0を含む証跡は同rootのnative-group-regression-audit.json。この追加の範囲をGateway全体の再回帰と同一視しない。

既存の分割Lookupとproducer上のcallbackを維持し、Count/LongCount/Any/First/FirstOrDefault/Last/LastOrDefault/Sumと予算付きToDictionaryへ接続を広げる。Where/Select/AsEnumerableと単純なint引数のSkip/Takeを後段へ融合し、整数の件数と和はchecked、floatの和はdouble accumulatorを使い、nullableのnullは加算しない。未知の数値型・複雑な引数は既存経路を維持する。逃避groupは上記の所有配列への接続を使う。元の順序で一要素ずつ終端へ渡し、スカラーを返すための全件配列を作らない。Take(0以下)は入力やcallbackを開始せず、Any/FirstとTakeの終了では余分な結果selectorを呼ばない。全worker/Lookup/結果の解放まで所有取引のoperationを保持する。通常LINQとの結果・callback列・空入力・例外順序の比較を先に置き、生成物でも専用終端とnative走査への接続を確認する。

正規化でSkip/Takeが投影前へ移った場合も、非逃避の集計候補を保持し、捨てるgroupの投影を評価しない。Last/LastOrDefaultでは、述語のないgroup投影と最後の述語以降のSelectを、選ばれた最後の値にだけ実行する。入力のgroupやLookupをquery外へ返さず、所有operation内で選択・投影・解放を完了する。終端の処理は直接呼ぶlocal functionへ出し、結果一件ごとに追加のdelegateを呼ぶ形にしない。比較器引数はTake(0)でも元の位置で一度評価する。

nullableの条件式で、colon直前のラムダ引数をnamed argument/tupleラベルと混同して未置換にする不具合を修正した。従来置換しなかったcolon直前の候補だけを構文で照合し、ラベル・文字列・メンバー名へ置換を広げない。既存の配列/List回帰から見えた、直接比較へ特殊化した内部比較器の未使用引数と、必ず最初の一致で終了するループの到達不能な増分も除いた。custom comparer、引数評価、predicate/counter/indexの評価順は維持する。

**検証方法:** 25メソッドを各5個のTake値、空/自己変更あり、従来/MVCCで通常LINQと比較し、値・例外型・callback列と各メソッドの専用終端生成を検査する。通常の配列/Listの既存29回帰と比較器4組合せの1ケースもRoslynで生成・実行する。意図的な匿名型のFLINQ001を1件だけ期待診断として検証し、他のコンパイル警告を許容しない。外側のビルド警告の抑制設定は追加しない。

**結果 — Fact:** 上記をDebug/Releaseで実行し、各構成500組、合計1,000組の通常LINQ比較が成功した。一般配列/Listの回帰も各構成30ケースが成功し、期待したFLINQ001以外の生成コンパイル警告は0件。構成と従来/MVCCの4ケースで25メソッドずつ、計100メソッドの生成物に取引終端・PartitionedLookupBuilder・native Iterateへの接続を確認した。生成ファイルとhashの記録はF:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/transaction-tail-generated-20261103.json、実行と入力の照合はtransaction-tail-latest-20261103.jsonを参照する。

### 削除・再挿入の反復条件 — Decision

MV31の更新だけでは確認できない境界を、既存の256行enduranceへ明示的なChurn条件として追加する。4workerが別のキーを更新・削除・再挿入する3周期を繰り返す。同じ取引で削除してすぐ戻すだけにはせず、削除状態も確定・回収・保存する。各周期の古い読取scopeは変更前の存在と値を保持し、現在値は存在フラグを持つ独立モデルと全行照合する。欠けた外側キーを含むJoin、空になり得るgroup、点検索、Count/Sumも照合する。Storageは8周期ごとと最後に再起動し、削除済みheadと再挿入を取り違えないことを確認する。

Churnは結果台帳の保持を無期限に増やす試験とは分ける。8周期ごとの共通checkpoint完了後と最後に、その時点までの結果を明示的に忘却し、直前の結果が忘却前にはCommitted、忘却後と再起動後にはUnknownでも業務行は保持されることを確認する。既定Update条件の結果保持は変更しない。忘却位置・更新/削除/再挿入数・行数・回収後の予約値を記録し、予約値を実メモリ量と呼ばない。少量反復の正確性試験であり、専用履歴の比較や全体性能受入へ流用しない。

**結果 — Fact:** ChurnのDebug/Release各512周期は、Memory/Storage各16,384変更、各構成の合計32,768変更が成功した。保存先ごとの内訳は更新5,472・削除5,472・再挿入5,440で、最後は224行。Storageの途中再オープンと結果忘却は各64回。Memoryの回収後の予約値は9,851,232～9,867,616バイトで、周期に比例して増えなかった。Storageの忘却後は8,806,752～8,814,944バイト、最終再起動後は8,814,944バイトだった。固定配列を計上する前の約150KiBという予約値を現在のストア全容量とは扱わない。既定Update各8周期では最新結果が再起動後もCommittedであることを照合した。ChurnのDebug2周期では削除状態224行、Release3周期では再挿入後256行を最終再起動でも照合し、8周期未満での最終忘却も検証した。各周期の操作数・行数・忘却位置・CPU枠の返却を外部の監査スクリプトでも確認した。証跡は上記endurance-phase15の最終6条件。私有Persisted workerの障壁は後節のMV40試験で確認した。排出待ち時間と物理回収時間の個別計測も後節のMV31実装で追加した。これらの小規模試験だけで無期限の連続運転やMV20の性能比較を完了とは扱わない。

### nativeの常駐領域の事前予約 — Decision

採用DLLの実配列を観測すると、空の履歴ストアで予約64バイトに対して、溢れ索引とログだけで少なくとも8,462,400バイトを確保していた。溢れ索引の初期配列は先読み用を含む2枚であり、公開された使用中bucket数では確保量を表せない。まず共通Tsavorite接続の既知の固定配列、索引alignment、ログの先行ページと返却pool、リングの管理領域を生成前に予約する。ログと溢れ索引の拡大も割当て前に予約し、回収・全削除・復旧で既存の配列が残る分を予約から外さない。索引拡大は新旧の索引と溢れpoolが重なる費用を先に確保する。小さい予算が実際の初期費用を満たせない場合は、巨大な配列を確保してから検出せず、生成前の通常の資源不足とする。

これは共有予算を持つStoreの会計修正であり、従来の予算なしAPIや保存形式は変更しない。採用DLLの配列構造に基づく算定は回帰試験で検証し、依存DLLの更新時にも再検証する。予約値はプロセス全体のRSS・GC未回収領域やcaller所有の結果とは区別する。I/O中・checkpointの一時領域を含む全ピークの確認は、空の配列を覆う試験だけで完了としない。履歴保持量の上限と、Storeの常駐配列の費用も区別する。

**旧ページ返却 — Fact/Decision:** 固定費の計上後、MemoryのChurn512周期は、結果台帳を持たないのに予約量が9,916,768から28,168,544バイトへ増えていた。1行×128更新を24周期回収する最小試験でも、修正前は11周期目にnativeログのpayload統計が86,016バイトになり、設定64KiB＋返却pool4ページの上界を超えた。採用allocatorの`FreePage`はtrackerがない場合に旧ページの配列を保持するためだった。参照排出後のCompact中だけ未起動の`LogSizeTracker`を設定し、旧ページを返却させる。背景の退避は開始せず、Memoryの有効な旧版をNullDeviceへ追い出さない。finallyで解除する。修正後は最小試験と上記512周期が成功した。初期費用の不足と、この反復回収の増加は別々の再現試験で確認している。

**回帰条件の固定費 — Decision:** 既存の行数と履歴/metadata圧迫の閾値は増やさない。複数表・世代の試験予算は、それぞれの既存native poolを覆う32～64MiBへ調整する。TPC-Eは多数の主表と索引の既存固定費を含む試験予算を明示し、小さいMemory試験では将来用の巨大なリングを作らない固定設定を使う。従来の辞書・集合の保存/回収試験もExecution.Testsから同じソースを実行し、短い専用パスと有限の件数で検証する。これらの予算は実メモリの測定値ではない。

**2.2.1での会計修正後の回帰 — Fact:** 共通取引121件、DB取引62件、Execution/従来Store回帰75件、TPC-E全41件がDebug/Releaseで成功し、計598件の失敗・スキップ・ビルド警告は0件。Gateway Debugは52件成功・2件失敗だった。失敗はNative展開の生成DLLをコピーするMSB3030で、Garnet更新前に発生した。Gateway Releaseはこの実行では未実施。全10セル合格とは扱わない。証跡はF:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/native-budget-phase15/native-budget-matrix-20261103.jsonとDebug-test-6172e87b99574baf9f11460ccaf09cc9のTRX。以降の更新版の検証と混合しない。

### Garnetの更新で置き換えられる対処 — 2026-11-03

**Decision（利用者確認済み）:** 正式配布版の`Microsoft.Garnet`を2.2.1から2.2.5へ更新する。正式版2.2.5を標準とし、小さい索引のI/O回避は対応する次の正式版で再検証するまで維持する。直接参照する製品・テスト・ベンチマークの7プロジェクトを同じ版に揃え、共有NuGetキャッシュや既存Garnet checkoutは書き換えない。依存関係は固定版を使用し、実行時に最新版へ自動変更しない。

**Fact:** 公式[2.2.3の修正 #2229](https://github.com/microsoft/garnet/pull/2229)はoverflow領域の確保済みページを公開統計へ含め、allocatorの上限超過を例外にし、索引拡大時の予約と上限を導入している。空の64バイト索引による同一プログラムで、`IndexOverflowSizeBytes`は2.2.1では1,024、2.2.5では8,454,144バイトとなった。実配列payloadの下界は両者とも8,454,144バイト。2.2.5の統計は反射による実配列の合計とも照合する。byte allocatorの2ページだけで131,056件を追加し、その先の拒否、繰り返した拒否での件数不変、最後の値の保持、解放したアドレスの再利用も試験する。

**Decision:** 確保済みoverflow payloadの照合には公式`IndexOverflowSizeBytes`を使う。固定4096要素のpage tableを前提にする旧算定は、公式の上限解決APIと可変長tableに合わせる。将来の割当前予約、親Storeとの共有予算、CLR配列header、索引alignment、ログ管理領域、新旧索引が重なる費用は引き続きSeamlessが扱う。上流のログ予算補償はbest effortであり、Seamlessの予算保証を置き換えない。

| 対処 | 更新版での判断 |
| --- | --- |
| 使用中bucket数から確保量を読み替える対処 | 2.2.5の確保済みpayload統計へ移行。事前予約は維持 |
| 小さい索引のcheckpointをバッファ付きI/Oにする対処 | 2.2.5では必要。64バイト索引の回避なし条件でWin32エラー87を再現 |
| 同I/O対処に対応するmainの修正 | [#2303](https://github.com/microsoft/garnet/pull/2303)を含む`8a76c2e2c79e219f95b5b8be20a5a52138456719`を隔離ビルド。回避なしで保存・復旧成功。正式版採用とソース版採用を区別 |
| Compact中とRecover中の未起動tracker | 2.2.5にもtracker依存のページ返却条件が残るため維持。隔離コピーでCompactのtrackerだけを外すと、反復回収の11周期目に86,016バイトとなる元の増加を再現 |
| checkpoint状態機械と索引拡大の排他、保存後flush、Clearの世代切替、墓標保持 | 更新だけを根拠に削除しない。元の障害・データ寿命・同時実行の条件が異なる |
| MVCCのcopy-only更新・旧版保全・衝突キー照合 | 不具合回避ではなく正しさの条件。維持 |

公式2.2.5のソースcommitは`8d007138208ce030eb10a50c80e636b1678b83e4`、nupkg SHA256は`81823363A0034935154AB9CCC4F250EAB444CCC05C2B72AC44DE181F4B184917`、配布Tsavorite DLLは`F1A3B1C0300C7DEEBD09D35AC42EEFFEDB6854E88711B18796A24231DFBC0E32`。mainの再現ビルドはSDK 10.0.401を明示した。実装ソースは変更せず、SDK選択ファイルのみ隔離コピー内で変更した。配布DLLとソースビルドDLLはAssemblyVersionが同じでも別物なのでSHAとcommitで区別する。

比較の証跡はF:/CodexTemp/Seamless/MvccImplementation/20261102-1793607081/garnet-updateの`probe-2.2.1`、`probe-2.2.5`、`main-core-build`、`sector-225-buffered`、`sector-225-unbuffered`、`sector-main-unbuffered`。更新後の製品回帰はF:/CodexTemp/Seamless/garnet225-20261103、反復回収はF:/CodexTemp/Seamless/g225-end-20261103へ分離する。性能比較や専用履歴ストアの比較とは扱わない。

trackerなしの対照条件はF:/CodexTemp/Seamless/g225-negative-20261103/results.trxに保存した。製品ファイルは変更せずDの隔離コピーだけで一時trackerの設定2箇所を除去した。期待した回収上界違反の1件失敗であり、製品回帰の失敗数とは分ける。

**更新後の検証 — Fact:** 共通取引122件×2構成、DB取引62件×2、Execution/従来Store回帰75件×2、GatewayのDebug Native展開2件とRelease関連54件、TPC-EのRelease全41件、計615件が成功した。失敗・スキップ・対象プロジェクトのビルド警告は0。最終ソース1,282入力と各TRX、両構成の入力一致を監査した。証跡はF:/CodexTemp/Seamless/g225-final-20261103/validation-latest.json。2.2.5でのTPC-E Debug全件やGateway Debug全件まで実行済みとは扱わない。

保存互換性は、2.2.1の製品成果物で32キー×3版を保存し、2.2.5の製品成果物で96件の版読取と削除フラグを照合した。更新版で版4を追記・保存し、別プロセスで再度開いてその値と既存版を照合した。小規模のSpanByte履歴形式の確認であり、すべての既存データ形式の互換検証とは扱わない。証跡はF:/CodexTemp/Seamless/g225-compat-20261103の`221-write-v2`、`225-read-v2`、`225-reopen-v2`。

### Garnet側の部品の利用範囲 — 利用者合意と現行評価

Tsavorite以外のGarnet部品も、独自実装を減らし、既存の一貫性・保存形式・資源所有権を維持できる場合は利用してよい。正式版2.2.5の公開APIを優先し、Garnetのサーバー全体を必要条件にはしない。

2.2.5の`CacheSizeTracker`はGarnet固有の`StoreFunctions`/`StoreAllocator`を持つKVを受け取るため、現在の汎用SpanByte接続へそのまま代入できない。汎用の`LogSizeTracker`は引き続き直接利用する。`LimitedFixedBufferPool`は公開された利用候補だが、予算は`NetworkBufferBudget`であり、Seamlessの親子`ResourceBudget`への事前予約・返却と接続する必要がある。既存の予算付きbufferを無条件に置き換えない。Garnetの`TransactionManager`は`RespServerSession`/`StorageSession`へ結合し、`GarnetAppendOnlyFile`もGarnetの設定・ログ構成を受け取るため、共通確定や現在のWALを置換する判断はしていない。

### 私有Persisted workerの終了障壁 — MV40

`VersionedTableTests.PrivateWorkers`は実際の`PartitionedLookupBuilder<int,int>`へnativeの取引Rowsから入力する。64行から1,024要素を作り、query子予算256KiBで私有LookupをPersistedへ退避する。各workerを個別の障壁で止め、読取専用取引の履歴圧迫による中止とworker例外を別条件で確認する。producerの行走査が終了した後も、他workerが残っている間はqueryと私有領域の後始末を完了せず、回収も待機する。全worker解放後に一時ディスクとCPU枠を返し、障害停止せず別の更新を受け付ける。

試験だけで比較器に障壁と例外を注入する。製品で副作用付き比較器を安全に自動並列化できるという契約は増やさない。Fixtureの行数・履歴予算・native固定費は維持し、この試験だけquery worker数を最大4にする。共通CPU上限は引き続き8。巨大データやNUMA最適化は追加しない。

**Fact:** 追加した履歴中止/worker例外の2条件を含む共通取引124件がDebug/Releaseで成功した。248実行の失敗・スキップ・ビルド警告は0。`ExecuteQuery`の取引operation保護だけを隔離コピーで外すと、producer終了後にworkerが残る状態の活動中操作の参照不足を履歴中止条件が検出した（2条件中1失敗・1成功）。正規コードのTRXと最終入力hash、両構成の入力一致を監査済み。証跡はF:/CodexTemp/Seamless/mvcc-workers-20261103/validation-latest.json、対照条件はF:/CodexTemp/Seamless/mvcc-workers-negative-20261103/results.trx。MV40の私有Lookupに関する終了障壁を確認したもので、残る性能比較や他の未完了条件を合格へ読み替えない。

### 回収の排出待ちと物理処理の計測 — MV31実装方針

共通Coordinatorの単一回収処理に計測番号を付け、合流した待機者で二重に数えない。受付から物理処理開始までを`DrainWait`、物理回収・共通保存・handle再取得・共通後始末の完了までを`PhysicalWork`とする。両者の和が`Total`。単調時計を使用し、待機中の観測値は経過時間、終了後は直近結果の固定値を返す。待機にはTask開始待ちと物理処理前の準備も含むため、純粋なsleep時間やCPU時間とは呼ばない。

`TransactionDomainDiagnostics`の既存6引数コンストラクターと分解を維持し、追加プロパティ`Maintenance`で番号・状態・時間・物理処理開始有無を返す。初期状態はNone。排出期限切れはFailed/PhysicalStarted=false、物理変更後の失敗はFailed/trueとし、既存の正常資源不足と復旧必須停止の区別を変更しない。最後の結果は成功・失敗のいずれも保持し、次の回収中は新しい番号を見せる。履歴や個々の取引ごとの計測一覧は保持しない。

Databaseの診断入口はGateway共有bindingがあれば共通ownerを参照し、独立の回収器を作らない。製品耐久試験は全体の呼出し時間を残し、同時に各周期の排出待ち・物理処理・内部合計を記録する。計測のための待機時間追加やデータ量増加は行わない。障壁で両区間を分け、排出期限切れと保存/再取得失敗、同時要求の合流を検証する。

**実装と検証 — Fact:** `TransactionDomainDiagnostics.Maintenance`と`DatabaseContainer.GetTransactionDomainDiagnostics()`を追加した。診断では破棄済みDBを拒否し、業務停止中も既存ownerの状態は取得可能とする。停止状態のDBで従来の業務用利用可否チェックを通すと診断まで拒否されたため、この診断入口だけを既存の結果照会と同様の扱いにした。業務行へのアクセスを許可する変更は行わない。

両区間を障壁で分ける条件、同時要求の合流、排出期限切れ後の再受付、共通保存失敗、handle再取得失敗、二つのGateway DBが同じ診断を返す条件、停止したDBからの診断取得を確認した。共通取引128件とDB63件がDebug/Releaseで成功し、計382件の失敗・スキップ・ビルド警告は0。最終814入力と両構成の一致を監査した。証跡はF:/CodexTemp/Seamless/mvcc-timing-20261103/validation-latest.json。

256行・64周期のChurnをDebug/Release、Memory/Storageで再実行し、操作数・再起動後の256行・資源・CPU返却に加え、全周期の区間時間が有限かつ非負で、区間合計が内部合計となり、呼出し全体時間を越えないことを監査した。最終784入力と両構成の一致も確認した。Releaseの中央値はMemoryが排出0.01225ms・物理0.47970ms、Storageが排出0.01310ms・物理6.78935ms。各区間の中央値は別々に算出したため、中央値同士の和を全体の中央値とは扱わない。これは小規模な正確性試験での観測であり、長い更新の排出や性能受入MV20の結果ではない。証跡はF:/CodexTemp/Seamless/mvcc-timing-endurance-20261103/endurance-latest-20261103.jsonとphase-summary.json。

診断追加前のDLLを参照して別consumerをコンパイルし、そのconsumerのSHA256を変えずに新しいTransactions DLLへ差し替えた。既存6引数コンストラクター、6項目の分解、Coordinatorからの返却値の受け取りが旧/新ランタイムの双方で成功した。証跡はF:/CodexTemp/Seamless/mvcc-timing-compat-20261103/compatibility-latest.json。

### 同条件比較コマンド — MV20

既存probeの3回固定順・取引経路だけの計測を受入比較へ読み替えず、ProductTransactionsに独立した比較入口を追加する。同じunmanaged行・intキー・Auto符号化・初期値・索引/ページ/ログ設定を使い、通常Tsavoriteのキー付き`Set<int,Row>`と版付きTransactionRowsについて、FLinqと通常Enumerableを比較する。Where付きCount、Sum、64群のGroupBy、64キーを外側に持つJoinを測る。比較メソッド自身の展開と、通常nativeのSumが`.Iterate`へ接続されたことを検査し、通常Enumerableはinterceptを無効にする。全行を配列化して基準にする経路は追加しない。

クエリ本体だけのKernelと、取引の開始・commit・disposeを含むEndToEndを区別する。各条件をウォームアップし、5回の測定で方式の順を巡回させる。クエリ内worker数1/2/4/8と、独立取引の同時数1/2/4/8を別の項目として記録する。後者は同じ分布・乱数seedの要求をExclusive/Optimisticへ交互に与え、100/95/50%読取、一様/1%集中、競合中止、成功更新との最終値の一致を確認する。業務処理の再実行を行わない。

1万行では、読取専用snapshotを保持して全キーを7回更新する8版条件も比較する。この条件は開始済みの古いsnapshotのKernelだけを測り、毎回の新規取引開始を含むEndToEndとは呼ばない。古い値の結果と、snapshot終了後の最新版を別々に照合する。10万行は1版条件とする。独立取引の重なりは、Begin後からCommit前までの読取・変更準備区間の活動数として記録する。RNG・latency標本・モデル更新・重なり計測は混在取引の所要時間へ含まれる。

1万/10万行、Memory/Storage、Release、共通8論理CPUを使用する。ログ32MiB・保持履歴24MiBを上限とし、初期固定費とmetadataを含むDBの共有上限は128MiB（行を128MiB確保する指定ではない）。通常nativeの業務領域にも同じ上限を設定する。既存native FLinq内部の私有lookupに共有予算が適用されない箇所は資源上限が同じとは主張せず、プロセス割当・RSSと保存先の予算を区別する。読み取り比較はウォーム状態で、メモリを超えるデータや冷たいディスクI/Oの結果ではない。nativeと版付きログのヘッダー・可視版探索・診断等の違いを含む追加費用として報告し、差のすべてをロック費用とは呼ばない。

DBの`IndexSize`はbucket数、TsavoriteTuningはbyte数なので、主索引2MiBには前者32,768、後者2,097,152を指定する。`DatabaseContainerOptions.SharedStoreOptions`で既存Storeのprofile/growth設定をSharedV2業務Storeへ渡せるようにし、比較では両方式にFixedを指定する。既定値nullは従来動作を維持し、Legacyでの指定は拒否する。小さなMemoryログを超えるcommitが全行未公開のまま通常のResourceLimit中止になり、その後も既存行を読めることを試験する。私有query作業領域の設定とは分ける。

`Invoke-MvccComparison.ps1`は初回のビルドを保存し、`-UseExistingBuild`ではrunnerとDLLのhashを照合して同じ成果物を再利用する。`Test-MvccComparison.ps1`は4条件のterminal receipt、ソース/生成コード/DLLのhash、条件の直積・5反復・順序巡回・独立した期待値・成功数・資源/CPU返却を監査し、中央値を出力する。現行の実行入力との差は拒否し、Markdownだけの差は明示する。保存終了後のファイル長は別途集計し、測定中のピークや物理ディスク割当量とは呼ばない。worker値は取引クエリの上限であり、実際のworker数やnative側の方式指定と同一とは主張しない。

### 比較で見つかった私有Lookupの早すぎる退避 — MV20の改善

**Fact:** 固定ビルドの4条件（1万/10万行×Memory/Storage）が完了し、2,560 query・960 mixed・24資源標本を独立監査した。10万行Memory、1 workerの取引FLinqではGroupByが中央値2,609ms、Joinが2,243msで、通常LINQの22ms/40msを大きく上回った。私有退避のピークは約6.9MB、DB予約ピークは約35.9MBで、128MiBの親予算に余裕があっても退避していた。証跡はF:/CodexTemp/Seamless/mvcc-comparison-20261103/comparison-audit.json。測定後に別作業で共有生成器が変わったため、`-FrozenInputs`で測定snapshot自体を監査し、現在入力との差を列挙した。現在の作業ツリーの測定と同一とは主張しない。

**Fact:** `ScopedLookup.Add`は、参照を含まない値でも毎行128バイトと符号化長の4倍等を加算する。分割workerの子予算からさらに3分の1を退避閾値にするため、インライン配列に収まる値が早く退避する。並列batchも既に確保したInput配列に加えて、同じ値の符号化長を毎行見積もっていた。

**Decision:** 比較器を持ち、キーと要素の両方が参照を含まない経路に限り、グループ管理費とListの実capacityを予約する。配列拡大は新旧が重なる分を割当前に予約し、差替え後に旧配列の予約を返す。既存の退避閾値・親子予算・codec/比較の契約は変えず、不足時は同じPersisted退避へ進む。固定batch配列に収まる同条件の値は追加のobject graph予約を重ねない。参照を含む型、比較器なしの符号化キー経路、未知のcodecは従来の保守的会計を維持する。小さい予算での退避・例外・順序・所有権・終了障壁の回帰を通し、十分な予算で収まる8,192要素が退避しない条件を追加して修正前の失敗を確認する。

**改善後 — Fact:** 追加2条件は修正前に不要な退避を検出し、修正後は両条件とも退避0。ExecutionのDebug/Release各77件・警告0を確認した。新しい固定ビルドで4条件を再測定し、2,560 query・960 mixed・24資源標本と現行入力789ファイルの一致を監査した。10万行Memory・1 workerのGroupBy/Joinは中央値17.8828ms/26.3101ms、managed割当8,788,448/8,755,480バイトになり、Memoryの私有退避ピークは全worker上限で0だった。全結果、独立取引の成功処理量、通常nativeとの追加費用、保存容量、残る制約は[比較報告](20261103-product-mvcc-comparison.md)を参照する。合意した全体受入を、この一つの比較成功だけで完了とは扱わない。

最終回帰は共通取引128・DB65・Gateway関連65・Execution77・TPC-E41件がDebug/Release各成功し、計752件の失敗・スキップ・ビルド警告は0。各TRX/receipt、必須シナリオの実行、現行実行入力1,284ファイルと構成間の一致を監査した。Gatewayには12停止点のプロセス終了、複数DDLとNative再構築の共通復旧、部分checkpoint、Native操作の排出、通常TableからのFLinq展開を含む。証跡はF:/CodexTemp/Seamless/mvcc-comparison-20261103/validation-latest.json。実装設計全体の要求別受入と、専用履歴バックエンドとの実測比較は、この回帰件数だけで完了へ読み替えない。

### 衝突したキーの履歴アドレス — 専用履歴比較で検出した接続不具合

**Fact:** 1,024個の32バイトキー・8版の比較で、現行HistoryStoreの回収後に削除headが一つ残る条件を再現した。値と削除フラグは正しいが、`HistoryFunctions.Reader`が採用していた`ReadInfo.Address`は、Garnet 2.2.5のMemory/常駐読取ではhash-tag衝突の探索前に設定される。該当キーへ辿った後の実アドレスとは異なる場合がある。公開HistoryAddressを読み直す製品回帰試験で、Memory/Storage常駐の2条件が別キーを返して失敗し、Storage退避後の条件は成功した。証跡はF:/CodexTemp/Seamless/mvcc-comparison-20261103/Release-test-0a367edd432842cc80ef53be662eff52。値を誤って返した事実と、アドレス/回収の不具合は区別する。

**Decision:** レコード本体と前リンクは同じReader callbackから取得し、アドレスは同期Readの返却metadata、またはPending完了出力のRecordMetadataから確定する。callbackのAddressは物理アドレストークンへ採用しない。保存形式・前リンク・公開APIの型は変更しない。衝突時のアドレス往復、古い版の照合、削除head回収をMemory/Storage常駐/Storage退避後で検証する。この修正前に途中終了した専用履歴比較の180標本は最終性能値へ含めない。

### Gatewayの制約依存と複数DB取消 — MV03 / MV22

`GatewayVersionedTransactions.ConstraintDependenciesAbortTheWholeMultiDatabaseTransaction`で、別の主キーを同じUNIQUE値へ変更する競合と、子行追加・参照先削除の競合を両方の確定順序で照合する。どちらも別DBの異なる行を同じscopeで変更し、制約依存で負けた取引の変更が別DBにも残らないことを確認する。同一行のwrite/write競合では代用できない条件である。通常の競合後も新しい取引を受け付け、再open後の全DBの行と勝者の確定結果を照合する。データは各表1～3行とし、試験のために予算を拡大しない。

**Fact:** 追加した4条件を含むGateway関連69件がDebug/Releaseで成功した。138実行の失敗・スキップ・ビルド警告は0。証跡はF:/CodexTemp/Seamless/mvcc-history-comparison-20261103/regression-results.jsonのGateway両構成と各TRX。これは合意済みの制約依存・共通確定の検証を補うもので、UNIQUE/FKを独立した確定へ移したり、業務処理を自動再実行したりする仕様変更ではない。

### 可変payloadの所有権 — MV06

`VersionedTableTests.MutablePayloadsCannotChangeStagedCommittedOrOtherTablesRows`は、2表へ同じ`byte[]`を入力し、入力元・点読取の返却値・native走査のcallback内の配列を書き換えても、私有変更・確定行・古いsnapshot・他表へ変更が伝わらないことを照合する。後続writerでの変更と保存点の復帰も同じ規則で扱う。Memory/Storage各1行の試験で、Storageはログを明示的に退避してから読む。退避読取での所有権を確認するもので、Pendingを意図的に停止する障壁試験とは区別する。

**Fact:** 可変payloadの2条件に加え、回収の受付待ちを追加した共通取引134件がDebug/Releaseで成功した。268実行の失敗・スキップ・ビルド警告は0。証跡はF:/CodexTemp/Seamless/mvcc-mutability-20261103/regression-results.jsonと各TRX。共有生成ソースはこのCoreのProjectReference閉包へ含まれず、Gateway/DBの検証は別に扱う。

### 回収中の受付と既存writerの終了 — MV26 / MV29

`OptimisticTransactionTests.NewRootsWaitThroughDrainCheckpointAndHandleReopeningWhileTheExistingWriterFinishes`は、排出待ち・共通checkpoint・handle再取得を別の障壁で止め、各段階で開始した新規rootが待機することを確認する。既存writerは回収の排出待ち中にも子の投票・保存点からの復帰・更新・確定を完了できる。全handleの再取得後に3待機者が同じ確定値を読み、活動中取引が0へ戻ることを照合する。checkpoint/再取得失敗の既存2条件にも、失敗後の新規読取を受け付けない照合を追加した。

### 実ディスクのPending完了と回収の排出 — MV08

`VersionedTableTests.NativePendingReadKeepsItsOperationUntilTheRealIoCallbackIsReleased`は、1行をStorageへ書き、ログを退避した後、そのfixtureの実デバイスの読取完了通知だけを保留する。データのI/Oは元のデバイスへ渡し、正常完了の転送バイト数も確認する。回収要求の開始flagだけで判断せず、取引の寿命検査がHistoryPressureを返すまで待ち、その間も操作数が残り、物理回収が始まらず、履歴世代が変わらないことを照合する。通知を解放した後は元の読取りが中止を返し、全操作の排出後に世代が進み、独立した新しい読取りが元の値を読める。finallyで通知と試験用の包みを解放する。製品への試験専用hook、I/Oの模倣、待ち時間の水増しは追加しない。

この条件を含む共通取引の全141件がDebug/Releaseで成功した。TRXはF:/CodexTemp/Seamless/mvcc-mutability-20261103のDebug-test-f009804ed74e420892a9fa458ffc2182とRelease-test-f8bfb2b4a5044971ac191bd0e01ed20f。通常の退避読取・mutable payloadの確認や、worker callbackを停止する既存試験と別の証拠である。

### 今回の回帰と入力一致の境界 — 2026-11-03

記録したsnapshotでは、共通取引134・DB65・Gateway関連69・Execution77・TPC-E41件をDebug/Releaseで実行し、計772実行の失敗・スキップ・ビルド警告は0だった。必須のプロセス停止、複数DDL/Native復旧、部分checkpoint、通常Tableからの展開、可変payload、制約依存、回収受付の3段階が含まれることをTRXで照合した。1,311入力を照合し、記録snapshot間の重複入力も一致した。証跡はF:/CodexTemp/Seamless/mvcc-history-comparison-20261103/validation-recorded-snapshots.json。

ただし、実行中に別作業で`Seamless.Database.Gateway.csproj`へ`Seamless.Security`参照が追加された。通常の現行入力一致監査はこの差を拒否しており、上記は`PassedForRecordedSnapshots`として保存した。現在のワークツリー全体を同じ入力で回帰済みとは扱わない。参照追加後のビルドでは、生成型の解放・移行・キーなし行コピーと、旧module参照を保持したNativeコードの解放の4件をDebug/Releaseで確認した。追加8実行の失敗・スキップ・警告は0。こちらの証跡は同rootのlifetime-results.jsonとlifetime-audit.jsonである。

履歴アドレス修正後の製品4条件比較は2,560 query・960混在・24資源標本を監査し、791入力が監査時の製品ソースと一致した。[製品比較](20261103-product-mvcc-comparison.md)を参照する。専用履歴はI/Oを揃えた400標本と別の既定I/O600標本を比較し、製品保存先の切替えは行っていない。[専用履歴の比較結果](20261103-history-store-comparison.md)に数値と判断をまとめた。これらの成功を、未照合の受入条件や現在のGateway参照追加後の全体回帰へ流用しない。
