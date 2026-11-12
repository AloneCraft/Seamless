namespace Seamless.Database;

// 暗黙の取引(DatabaseTransaction)の変更を、版付き方式の共通WALへ書き出すときの1行。
// 旧方式の専用WAL(database.wal)は廃止した。暗黙の取引を廃止するときに合わせて消す。
internal sealed record DatabaseWalOperation(
    int EncodingVersion,
    string TableName,
    bool Deleted,
    string KeyData,
    string? RecordData,
    global::Seamless.Execution.ResourceReservation? Reservation=null):IDisposable {
    public void Dispose()=>this.Reservation?.Dispose();
}
