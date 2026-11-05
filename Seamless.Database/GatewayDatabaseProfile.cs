using Seamless.Transactions;

namespace Seamless.Database;

/// <summary>Gateway の共通 WAL と DB の永続 ID。既存 SharedV2 領域には適用しない。</summary>
public sealed record GatewayDatabaseProfile(Guid WalId,int DatabaseId){public Guid GenerationId{get;init;}}
public readonly record struct GatewayTransactionOwner(Guid TransactionId,long SessionId);
public readonly record struct GatewayPreparedSnapshot(Guid Checkpoint,string DataSha256);

public interface IGatewayDatabaseTransaction:IDisposable{
    bool IsRollbackOnly{get;}
    void CreateSavepoint(string name);
    void RollbackToSavepoint(string name);
    void ReleaseSavepoint(string name);
    IEnumerable<WalEntry> PrepareGatewayCommit(GatewayTransactionOwner owner);
    void ApplyGatewayCommit(GatewayTransactionOwner owner,long lsn);
    GatewayPreparedSnapshot SealGatewaySnapshot(GatewayTransactionOwner owner)=>throw new NotSupportedException("This database transaction is not an unpublished generation.");
    void Commit();
}
