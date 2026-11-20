using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Transactions;

public enum TransactionAbortReason{Conflict=1,HistoryPressure,CallerCancellation,JoinedRollback,ResourceLimit,UndeclaredTableAccess}

public sealed class TransactionAbortedException(Guid id,TransactionAbortReason reason,Exception? inner=null)
    :InvalidOperationException("The transaction was aborted: "+reason,inner){
    public Guid TransactionId{get;}=id;
    public TransactionAbortReason Reason{get;}=reason;
}

/// <summary>旧版の選択と版付き設置に対応する保存先。通常のApplyによる上書きと区別する。</summary>
public interface IVersionedTransactionParticipant:ITransactionParticipant{
    /// <summary>未公開の初期投入を閉じ、通常取引の受付に備える。復旧版の照会とは分離する。</summary>
    void PrepareForTransactions(){}
    long RecoveredVersion{get;}
    IVersionedTransactionChanges CreateChanges(TransactionContext context);
    void Replay(WalEntry entry,TransactionInstallationContext context);
}

/// <summary>domainの全参照を排出した所有者が、通常のCompactとhandle再取得を行う能力。</summary>
public interface IReclaimableTransactionParticipant:IVersionedTransactionParticipant{
    ValueTask ReclaimAsync();
    void ReopenAfterMaintenance(){}
    /// <summary>物理回収開始後の共通保存・再取得失敗。受付を再開せず、所有者による廃棄だけを許す。</summary>
    void FaultMaintenance(Exception error){}
}

/// <summary>一つの論理参加先に属する表/世代。所属と復旧先を所有者が照合する。</summary>
public interface IVersionedTransactionParticipantGroup:IReclaimableTransactionParticipant{
    bool Owns(IVersionedTransactionParticipant participant);
    bool TryResolve(WalEntry entry,out IVersionedTransactionParticipant participant);
}
public interface IRoutedVersionedTransactionParticipant:IVersionedTransactionParticipant{
    VersionedParticipantRoute? Routing{get;}
}
public sealed class VersionedParticipantRoute{
    public VersionedParticipantRoute(IVersionedTransactionParticipantGroup group,ReadOnlyMemory<byte> prefix){
        ArgumentNullException.ThrowIfNull(group);if(prefix.Length is<1 or>4096){throw new ArgumentOutOfRangeException(nameof(prefix));}this.Group=group;this.Prefix=prefix.ToArray();
    }
    public IVersionedTransactionParticipantGroup Group{get;}
    public ReadOnlyMemory<byte> Prefix{get;}
}

public interface IVersionedTransactionChanges:ITransactionChanges{
    ValueTask<PreparedTransactionParticipant> PrepareAsync(TransactionPreparationContext context,CancellationToken cancellationToken);
    void Install(TransactionInstallationContext context,PreparedTransactionParticipant preparation);
}

/// <summary>受付前に凍結した変更と設置資源。Disposeは未使用の予約だけを返す。</summary>
public abstract class PreparedTransactionParticipant:IDisposable{
    public abstract long MutationCount{get;}
    public abstract IEnumerable<WalEntry> Entries{get;}
    public abstract void Dispose();
}

public readonly record struct TransactionPreparationContext(TransactionContext Transaction,bool WalRequired,int MaximumFrameBytes);
public readonly record struct TransactionInstallationContext(Guid TransactionId,long CommitVersion,long Lsn,Guid DomainId);
