# 型付きトランザクションと共通 WAL v2

`Seamless.Transactions` は Benchmark に依存しない取引・所有権・WAL の基盤です。型付き保存先への接続と変更集合は `Seamless.Transactions.Collections`、テーブル宣言と取引ビューの生成は `Seamless.Database` / `Seamless.Database.Generator` が担当します。

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

初版の分離レベルは Serializable です。汎用 DB は読み取りセッション・保存と共通の DB 全体排他を取得します。取引ビューは一つの実行フローで使用してください。同時操作、未終了の子を残した親の確定、新旧スコープの混在、同じ取引からの `database.Accounts` による迂回を拒否します。独立した取引は開始時に排他を待ちます。同期インデクサーは保存先 I/O で呼出しスレッドを待たせる場合があります。

子は `await transaction.BeginTransactionAsync()` で開始します。親子は ID・排他・変更集合を共有します。子の Commit は `Voted`、最外側の Commit は `Committed` または `NoChanges` です。子の Rollback / 未 Commit の破棄は全体を中止します。既存の同期 `Commit()` は従来どおり確定要求を立て、スコープ破棄で確定します。

更新取引の成功通知を失った場合は、保存しておいた ID で `database.GetTransactionOutcomeAsync(id)` を照会します。`Committed` は確定済み、`Aborted` は運転中に中止を確認できた状態、`Unknown` は確定を判定できない状態です。不在を中止と判断してはいけません。`TransactionCommitException` は取引 ID と確定状態を持ちます。WAL 永続化後に本体反映・後始末が失敗した場合は `Committed` を保ち、DB を障害停止します。確定結果の照会は要求本体の重複排除や自動再実行を行いません。

更新結果は明示削除まで永続保持し、共通予算へ計上します。`ForgetTransactionOutcomesThroughAsync(lsn)` はその LSN 以下をまとめて削除します。削除 watermark も保存し、復旧で履歴を復活させません。正常 Rollback の履歴は永続化せず、運転中の有限キャッシュだけに置きます。再起動やキャッシュ失効後の結果は `Unknown` です。読取専用取引は通常 WAL へ記録しません。

WAL は長さ・版・CRC・LSN・区画・表・codec identity を検証します。専用スレッドが書込みと Sync を行い、Sync 成功前に応答を許可しません。キューは最大 256 件とバイト上限を持ちます。受付前のキャンセルは有効ですが、受付後にキャンセルしても確定を撤回しません。正常終了は受付停止・排出・Sync・スレッド終了・装置破棄の順です。

WAL・結果台帳・一時変更集合は ResourceBudget に参加します。結果台帳を確定してから checkpoint が覆う WAL を一世代遅延で削除します。Windows では `FILE_STORAGE_INFO` から論理・物理原子 sector を検査し、設定より大きい sector や不明な alignment を拒否します。標準は 4096 bytes です。詳細は [Microsoft の FILE_STORAGE_INFO 契約](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_storage_info) に従います。他 OS では装置に適合した SectorBytes の指定が必要です。

TPC adapter は本体更新＋既存 Undo を使い、共通の WAL・障害ラッチ・確定待ちへ接続します。TPC-C は最小区画 mailbox・昇順取得・逆順解放で関係区画を所有し、独立区画を並行実行できます。読み取り・正常 Rollback は関係区画の先行更新の永続化を待ちます。TPC-E の新経路は単一実行系です。市場向け通知は取引中に収集し、永続化後の応答に載せます。復旧だけで市場への配信完了とは扱いません。

HTTP/TDS 接続、同じ表の汎用取引並列化、他の分離レベル、TPC-E 区画並列化、旧データの移行ツールは後続工程です。Persisted のクエリ一時領域には業務 WAL を適用しません。
