# 型付きトランザクションと共通 WAL v2

`TransactionCoordinator.BeginReadOnlyAsync()` と、生成された DB の `BeginReadOnlyTransactionAsync()` は、書込みを禁止した取引を開始します。子スコープにも同じ区分を引き継ぎ、読取り専用の確定では WAL を追加しません。設定省略時は取引全体排他を維持します。複数取引を同時に進める場合は `TransactionConcurrencyMode.Optimistic` と有限の `TransactionHistoryOptions` を明示します。

版付きDBは `DatabaseContainerOptions` の `Transactions=SharedV2`、`TransactionConcurrency`、`TransactionHistory` で選択します。保存先はTsavoriteのMemory/Storageで、Persistedは私有変更・クエリの作業領域です。既存の現在値profileは自動変換しません。版付きデータをExclusiveで開く場合も `TransactionHistory` を指定し、同じversioned adapterで読みます。

OptimisticではReadVersionに対応する可視版と自己変更を読み、キー・不存在・表走査の依存を確定時に検証します。競合は `TransactionAbortedException` のConflictとして返し、別取引を故障させません。MemoryはWALを作らず、全参加先の設置後に公開します。StorageはWAL耐久化後に設置します。設置・公開障害は共通domainを停止し、結果がUnknownかCommittedかを保持します。業務処理を自動再実行しません。

型付きコレクションの `.Rows` は `TransactionRows` を返します。FLinqのnative経路は `.Iterate(ref strategy)` を生成し、同じsnapshotと私有変更を合成します。遅延列挙中は構造保護により物理更新が待つ場合があります。確定・中止では取引自身の列挙保護とバッファも閉じ、外部の列挙子を閉じ忘れたことによる自己待機や履歴保持を避けます。中止後のMoveNextは正常完了として扱いません。

履歴回収は新規rootの入場を待たせ、実行中の参照を排出してから行います。履歴予算の圧迫では、開始時に読取り専用と確定した古い取引をHistoryPressureで中止します。まだ書いていない通常取引を読取り専用扱いにはしません。callback/native pendingの実行中には保持を返さず、静止後に削除headの整理、Compact、必要な共通checkpoint、世代とhandleの更新を行います。

複数の物理表・世代を一つの論理DBとして扱う接続層は、`VersionedTransactionParticipantGroup`をcoordinatorへ登録し、各表に`VersionedParticipantRoute`を渡します。同じキーでも表/世代のprefixが異なれば依存を分離し、走査依存は保守的に論理DB全体を対象にします。WALの復旧先はpartition・table ID・codecで照合するため、世代ごとに異なるcodec identityを使ってください。子の登録だけで世代は公開されません。接続層は取引のReadVersionに対応する世代を選び、その保存先の寿命も所有します。

不要になった子は`RetireVersionedParticipantsAsync(children)`へ渡します。新規rootを待たせ、既存取引の排出、全参加先の回収、共通checkpoint、再取得の後で所属を解除します。成功を待ってから所有者が表とStoreをDisposeしてください。取消は受付前まで有効で、受付後は結果確定まで待ちます。排出期限切れ・checkpoint失敗では所属を残し、物理回収開始後の障害はdomainを停止します。永続journalを使う場合は`ConfigureMaintenance`で全参加先の共通保存とWAL回収を実装してから退役を使います。

未公開の新規表には`VersionedTransactionalTable.BeginUnpublishedSnapshot(baseVersion)`で初期行を逐次追加できます。正の基準版を固定し、writerをCompleteしてから取引・復旧・回収を始めてください。使用中のgroupへ追加する表も、登録前に初期投入を完了させます。domain開始と所属登録では`PrepareForTransactions`で受付準備を行い、復旧版を照会するgetterだけでは未使用表の状態を変えません。使用開始後やcheckpoint復元後には再投入できません。初期行は業務WALへcommitせず、完成した候補のsnapshotとして保存します。途中放棄・投入失敗の表は再利用せず廃棄します。DBの`BeginUnpublishedSnapshot`もcanonicalの版付き表を扱い、初期行を通常取引の変更集合へ全件蓄積しません。完了前は通常取引を拒否します。

展開コードには、アクセス判定、呼出し先を含む既知/不明の理由、元ソースとchecksum、生成器の版とMVID、最適化/コンパイル設定、処理手順、変形、逐次/並列・キュー・資源・同期の実際の契約をコメントとして残します。Debugでは静的な計画定数とDebuggerDisplay、生成コードにはGeneratedCodeを使います。手動の表アクセス属性を要求せず、不明な呼出しは通常の更新可能取引を維持します。

版付き表の`GetCommittedSnapshot()`は、所有者が全取引を排出してnative regionを閉じた保存境界で使います。現在の確定行だけを逐次走査し、旧版・削除済み行を除いたcanonicalの検証値を計算します。全件配列を作らず、通常の取引中にsnapshot保持を省略して検証値を読むことも許可しません。

Gatewayは`GatewayDatabaseHost.OpenAsync`へ有限の`TransactionHistory`を渡すと共通の版付きdomainを使います。既定の現在値profileとの自動変換は行いません。私有seedを封印してからliveの物理表を共通groupへ登録し、各DBの独立した復旧・保存・破棄を防ぎます。接続の取引IDを共通scopeにも使い、SQL/Nativeが触れる全DBとNative計画を同じReadVersionのカタログから選びます。DDL/Nativeの参照は全行の設置後・共通prefix公開前に反映します。通常の`db.Table`とFLinq用の`GetGatewayTransactionRows()`も同じscopeへ接続します。独立したID/rowversion予約は活動中の更新可能な親scopeを保持して共通WALへ確定し、回収待ちとの循環待ちを避け、親のrollbackでは戻しません。実行contextの予約はroot完了でも解放します。

SQLの単一・非null整数主キーを証明できる等値UPDATE/DELETEはnativeの点アクセスを使い、主キー重複の確認も保存先で行います。異なる行を更新する取引は並行して確定でき、同じ行の競合は`TransactionCommitException`のAbortedと内部のConflictで返します。その他の述語・主キー・制約は従来の走査・検証を維持し、競合判定が保守的になる場合があります。

不要なDB世代とNative計画は、活動中の取引がなくなった時点か明示`Checkpoint()`で回収します。新規rootを待たせ、既存の読取りも中止せず終了を待ちます。履歴不足の回収要求が合流した場合だけ読取専用取引の中止へ切り替えます。共通checkpointで必要な復旧descriptorを絞ってから所属・Store・型・予約を解放します。再生時は保存済みWALのCRC/hashを検証しつつ、その行の旧codecを要求しません。公開中と一つ前の世代ファイルは保持し、候補ファイルの削除対象は受付停止中に固定します。WindowsがDLLをまだ保持している場合は`LastGenerationCollection.Deferred`に残し、次回に再試行します。製品コードから強制GCは行いません。

callbackのない `.Rows.Count()` / `.Rows.LongCount()` は、同じsnapshotをpage境界の有界chunkに分けて処理できます。query単位の `MaximumQueryWorkers` とhost共通の `CpuScheduler.Shared` の枠へ従い、workerの状態・handle・予約を分離します。native pending待ちではCPU枠を返します。全workerの終了を待ってから走査保護と予約を解放し、失敗を呼出し側へ伝えます。私有変更、小さい入力、worker上限1、入れ子のCPU作業、旧adapterは逐次走査へ戻り、workerの予約が不足する場合はworker数を減らします。取引のExclusive/Optimisticとは独立した設定です。並列Join/GroupByの残りの形状、TPC-Eの比較variantへの適用、全体の障害・性能受入は実装継続中です。

参加先が読取り専用取引を扱う場合は `IContextualTransactionParticipant` を実装し、受け取った `TransactionContext` の寿命と `EnsureWritable()` を各操作で検査します。対応しない参加先と任意の `Enlist` は読取り専用取引では拒否します。

`Seamless.Transactions` は Benchmark に依存しない取引・所有権・WAL の基盤です。型付き保存先への接続と変更集合は `Seamless.Transactions.Collections`、テーブル宣言と取引ビューの生成は `Seamless.Database` / `Seamless.Database.Generator` が担当します。

`TransactionScope.CreateSavepoint(name)` はその時点の変更集合を保持し、`RollbackToSavepoint(name)` は後続の変更と後続の保存点を取り消します。同名は最後の保存点を使用し、大文字小文字を区別します。復帰先の保存点は再利用できます。内部の文単位保存点は `ReleaseSavepoint(name)` で解放します。`ITransactionChanges` の独自参加者は `CreateSnapshot()` を実装する必要があり、未対応の参加者がある場合は保存点の作成を失敗させます。変更集合のコピーも共有予算とディスク退避を使います。

新規領域には `PreparedTransactionJournal` と `new TransactionCoordinator(preparedJournal:journal)` を選択できます。通常の WAL と保存プロファイルの識別情報を分け、既存領域を自動変換しません。変更は上限内の preparation frame (type 3) に分け、最後の commit に LSN 範囲・行数・連鎖 hash・利用者 metadata を保存します。準備だけの ID を確定結果として返しません。metadata だけの DDL 用確定も記録します。既存の単一 frame プロファイルの動作は維持します。

Gatewayのように分離性・変更集合・復旧を外部で所有する現在値adapterは、`CommitExternalAsync`に明示IDと`ICommittedTransactionChanges`を渡せます。行をstreamして耐久化した後、正のLSN付きで反映します。最終commit未受付でjournalも健全なら準備失敗・取消はその要求のAbortedとなり、次の取引は進められます。耐久化後の反映失敗はCommittedを保って停止します。この入口は版付きscopeの代用には使えません。

準備プロファイルは `ITransactionChanges.EnumerateEncoded()` を使用します。`TransactionalTable` の実装は退避済みの変更を逐次 encode します。独自参加者の既定実装は従来の `Encode()` に委譲するため、その参加者の有界性は別途確認が必要です。単一 mutation と利用者 metadata には frame 上限を適用します。

再オープン後は受付前にReplayが必要です。全commitと準備hashを検証してから、確定済みの行だけを再生します。callbackはその呼出し中に全行を列挙し、参照を保持しないでください。途中で止めた再生を完了として扱いません。commit索引は共有メモリ予算内に保持し、確定単位の再生でWALを再走査します。`PreparedTransactionJournal.CheckpointAsync` は、所有者が全参加先を保存するcallbackの完了後に共通WALを回収します。各DBを独立COMMITするAPIではありません。Gatewayの既存の複数DB原子性と、追加中の版付きdomain接続は区別します。

既存 DB の既定動作は Legacy のままです。新経路は新しい保存領域で明示的に選択します。

```csharp
var options=DatabaseContainerOptions.OnStorage(newDirectory) with{
    Transactions=DatabaseTransactionMode.SharedV2
};
using var database=new BankDatabase(options);

await using var transaction=await database.BeginTransactionAsync();
var transactionId=transaction.Id;
var source=transaction.Accounts[fromId];
var destination=transaction.Accounts[toId];
if(source.Balance<amount){
    await transaction.RollbackAsync();
    return;
}
transaction.Accounts[fromId]=source with{Balance=source.Balance-amount};
transaction.Accounts[toId]=destination with{Balance=destination.Balance+amount};
var result=await transaction.CommitAsync();
```

`Accounts` は通常の `Table<TKey,TRecord>` 宣言から生成します。利用側で表名の文字列登録、Store の作成、Tsavorite のハンドル切替えを行う必要はありません。取引ビューは同じデータ本体を使用し、変更した行だけを staging します。実体化ビューには読取専用の取引ビューを生成します。

分離レベルはSerializableです。既定の現在値profileは、読み取りセッション・保存と共通のDB全体排他を取得します。取引ビューへの公開操作は一つずつ実行してください。同時操作、未終了の子を残した親の確定、新旧スコープの混在、同じ取引からの `database.Accounts` による迂回を拒否します。Exclusiveの独立取引は開始時に排他を待ち、明示Optimisticでは依存を検証して並行実行します。同期インデクサーは保存先I/Oで呼出しスレッドを待たせる場合があります。

子は `await transaction.BeginTransactionAsync()` で開始します。親子は ID・排他・変更集合を共有します。子の Commit は `Voted`、最外側の Commit は `Committed` または `NoChanges` です。子の Rollback / 未 Commit の破棄は全体を中止します。既存の同期 `Commit()` は従来どおり確定要求を立て、スコープ破棄で確定します。

更新取引の成功通知を失った場合は、保存しておいた ID で `database.GetTransactionOutcomeAsync(id)` を照会します。`Committed` は確定済み、`Aborted` は運転中に中止を確認できた状態、`Unknown` は確定を判定できない状態です。不在を中止と判断してはいけません。`TransactionCommitException` は取引 ID と確定状態を持ちます。WAL 永続化後に本体反映・後始末が失敗した場合は `Committed` を保ち、DB を障害停止します。確定結果の照会は要求本体の重複排除や自動再実行を行いません。

更新結果は明示削除まで永続保持し、共通予算へ計上します。`ForgetTransactionOutcomesThroughAsync(lsn)` はその LSN 以下をまとめて削除します。削除 watermark も保存し、復旧で履歴を復活させません。正常 Rollback の履歴は永続化せず、運転中の有限キャッシュだけに置きます。再起動やキャッシュ失効後の結果は `Unknown` です。読取専用取引は通常 WAL へ記録しません。

WAL は長さ・版・CRC・LSN・区画・表・codec identity を検証します。専用スレッドが書込みと Sync を行い、Sync 成功前に応答を許可しません。キューは最大 256 件とバイト上限を持ちます。受付前のキャンセルは有効ですが、受付後にキャンセルしても確定を撤回しません。正常終了は受付停止・排出・Sync・スレッド終了・装置破棄の順です。

WAL・結果台帳・一時変更集合は ResourceBudget に参加します。結果台帳を確定してから checkpoint が覆う WAL を一世代遅延で削除します。Windows では `FILE_STORAGE_INFO` から論理・物理原子 sector を検査し、設定より大きい sector や不明な alignment を拒否します。標準は 4096 bytes です。詳細は [Microsoft の FILE_STORAGE_INFO 契約](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_storage_info) に従います。他 OS では装置に適合した SectorBytes の指定が必要です。

TPC adapter は本体更新＋既存 Undo を使い、共通の WAL・障害ラッチ・確定待ちへ接続します。TPC-C は最小区画 mailbox・昇順取得・逆順解放で関係区画を所有し、独立区画を並行実行できます。読み取り・正常 Rollback は関係区画の先行更新の永続化を待ちます。TPC-E の新経路は単一実行系です。市場向け通知は取引中に収集し、永続化後の応答に載せます。復旧だけで市場への配信完了とは扱いません。

HTTP/TDS 接続、同じ表の汎用取引並列化、他の分離レベル、TPC-E 区画並列化、旧データの移行ツールは後続工程です。Persisted のクエリ一時領域には業務 WAL を適用しません。
