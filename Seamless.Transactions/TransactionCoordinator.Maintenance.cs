using System.Diagnostics;
using Seamless.Execution;
namespace Seamless.Transactions;

public enum TransactionMaintenancePhase{None,Draining,Physical,Completed,Failed}
public enum TransactionMaintenanceStep{None,ReclaimParticipants,Checkpoint,ReopenParticipants,Finalize}
/// <summary>単一の共通回収の経過時間。待機者ごとに数えず、終了後は直近の結果を保持する。</summary>
public readonly record struct TransactionMaintenanceDiagnostics(long Sequence,TransactionMaintenancePhase Phase,TimeSpan DrainWait,TimeSpan PhysicalWork,TimeSpan Total,bool PhysicalStarted){
    public TransactionMaintenanceStep Step{get;init;}
}
public readonly record struct TransactionDomainDiagnostics(long PublishedVersion,int ActiveTransactions,int ActiveOperations,long HistoryMemoryBytes,long HistoryStorageBytes,bool Reclaiming){
    public TransactionMaintenanceDiagnostics Maintenance{get;init;}
}

/// <summary>保存先が既に親budgetへ計上した領域の履歴部分を制限する。native領域を二重予約しない。</summary>
public sealed class TransactionHistoryReservation:IDisposable{
    private readonly TransactionCoordinator.OptimisticDomain owner;
    private readonly long memory,storage;
    private int state;
    internal TransactionHistoryReservation(TransactionCoordinator.OptimisticDomain owner,long memory,long storage){this.owner=owner;this.memory=memory;this.storage=storage;}
    public void Retain(){if(Interlocked.CompareExchange(ref this.state,1,0)!=0){throw new InvalidOperationException("The history reservation has already been completed.");}}
    public void Dispose(){if(Interlocked.CompareExchange(ref this.state,2,0)==0){this.owner.ReleaseHistory(this.memory,this.storage);}}
}

public sealed partial class TransactionContext{
    public TransactionHistoryReservation ReserveHistory(long memoryBytes,long storageBytes){
        this.ThrowIfUnavailable();if(this.IsReadOnly){throw new InvalidOperationException("A read-only transaction cannot reserve an installation.");}
        return (this.Domain??throw new InvalidOperationException("A versioned domain is required.")).ReserveHistory(this,memoryBytes,storageBytes);
    }
}

public sealed partial class TransactionCoordinator{
    public TransactionDomainDiagnostics GetDomainDiagnostics()=>(this.versioned??throw new NotSupportedException("A versioned domain is required.")).Diagnostics();
    /// <summary>全取引の排出と共通 checkpoint 後に所属を解除する。保存先の Dispose は成功後に所有者が行う。</summary>
    /// <remarks>取消は受付前まで有効。受付後は結果が確定するまで待ち、寿命の不明な状態を返さない。</remarks>
    public ValueTask RetireVersionedParticipantsAsync(IReadOnlyList<IRoutedVersionedTransactionParticipant> participants,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(participants);cancellationToken.ThrowIfCancellationRequested();
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Finish the current transaction before retiring generations.");}
        var domain=this.versioned??throw new NotSupportedException("A versioned domain is required.");this.Recover();return domain.Retire(participants,cancellationToken);
    }
    internal sealed partial class OptimisticDomain{
        private readonly TransactionHistoryOptions history;
        private ResourceReservation? maintenanceMemory,maintenanceStorage;
        private readonly ResourceBudget? parentBudget;
        private Task<Exception?>? maintenance;
        private long maintenanceSequence,maintenanceStartedTimestamp,maintenancePhysicalTimestamp;
        private TransactionMaintenanceStep maintenanceStep;
        private TransactionMaintenanceDiagnostics lastMaintenance;
        private bool cancelReaders;
        private long historyMemory,historyStorage;
        private readonly List<Retirement> retirements=[];
        private sealed record Retirement(IRoutedVersionedTransactionParticipant[] Participants,ResourceReservation Memory);
        internal async ValueTask Retire(IReadOnlyList<IRoutedVersionedTransactionParticipant> participants,CancellationToken cancellationToken){
            Task<Exception?> task;
            lock(this.sync){
                cancellationToken.ThrowIfCancellationRequested();this.owner.failure.ThrowIfFailed();ObjectDisposedException.ThrowIf(this.owner.stopping||this.disposed,this.owner);
                if((this.owner.journal is not null||this.owner.preparedJournal is not null)&&this.Checkpoint is null){throw new InvalidOperationException("Persistent generation retirement requires a common checkpoint callback.");}
                var reservation=this.metadataBudget.ReserveMemory(checked(128L+participants.Count*64L));Retirement? request=null;
                try{
                    var copy=new IRoutedVersionedTransactionParticipant[participants.Count];
                    for(var index=0;index<copy.Length;index++){
                        var child=participants[index];
                        if(child is null||child.Routing?.Group is not VersionedTransactionParticipantGroup||!this.owner.Owns(child)){throw new ArgumentException("Every retired child must belong to a registered logical database group.",nameof(participants));}copy[index]=child;
                    }
                    if(copy.Length==0){reservation.Dispose();return;}
                    // 個別待機者の取消に依存しない共通回収へ予約の所有権を渡す。
                    // 通常の世代退役はreaderも排出まで待つ。履歴不足の要求が合流した場合だけ中止へ切り替わる。
                    request=new(copy,reservation);this.retirements.Add(request);task=this.StartReclaim(false);
                }catch{if(request is not null){this.retirements.Remove(request);}reservation.Dispose();throw;}
            }
            var error=await task.ConfigureAwait(false);if(error is not null){throw error;}
        }
        internal TransactionDomainDiagnostics Diagnostics(){lock(this.sync){return new(this.published,this.active.Count,this.active.Sum(static context=>context.Operations),this.historyMemory,this.historyStorage,this.maintenance is not null){Maintenance=this.GetMaintenanceDiagnostics()};}}
        // sync内だけで呼ぶ。同じ時点で区間を切り、合計と各区間の丸め誤差を生じさせない。
        private TransactionMaintenanceDiagnostics GetMaintenanceDiagnostics(TransactionMaintenancePhase phase=TransactionMaintenancePhase.None){
            if(this.maintenance is null){return this.lastMaintenance;}
            var now=Stopwatch.GetTimestamp();var physical=TimeSpan.Zero;TimeSpan drain;var started=this.maintenancePhysicalTimestamp!=0;
            if(started){drain=Stopwatch.GetElapsedTime(this.maintenanceStartedTimestamp,this.maintenancePhysicalTimestamp);physical=Stopwatch.GetElapsedTime(this.maintenancePhysicalTimestamp,now);}
            else{drain=Stopwatch.GetElapsedTime(this.maintenanceStartedTimestamp,now);}
            if(phase==TransactionMaintenancePhase.None){if(started){phase=TransactionMaintenancePhase.Physical;}else{phase=TransactionMaintenancePhase.Draining;}}
            return new(this.maintenanceSequence,phase,drain,physical,drain+physical,started){Step=this.maintenanceStep};
        }
        internal TransactionHistoryReservation ReserveHistory(TransactionContext context,long memory,long storage){
            ArgumentOutOfRangeException.ThrowIfNegative(memory);ArgumentOutOfRangeException.ThrowIfNegative(storage);
            lock(this.sync){
                context.ThrowIfUnavailable();this.owner.failure.ThrowIfFailed();
                if(!this.active.Contains(context)){throw new ObjectDisposedException(nameof(TransactionContext));}
                if(memory>this.history.MaxRetainedMemoryBytes-this.historyMemory||storage>this.history.MaxRetainedStorageBytes-this.historyStorage){
                    context.Abort(TransactionAbortReason.HistoryPressure);this.StartReclaim();throw new TransactionAbortedException(context.Id,TransactionAbortReason.HistoryPressure);
                }
                var reservation=new TransactionHistoryReservation(this,memory,storage);this.historyMemory=checked(this.historyMemory+memory);this.historyStorage=checked(this.historyStorage+storage);return reservation;
            }
        }
        internal void ReleaseHistory(long memory,long storage){lock(this.sync){this.historyMemory-=memory;this.historyStorage-=storage;}}
        internal async ValueTask Reclaim(CancellationToken cancellationToken,bool cancelReaders=true){
            Task<Exception?> task;lock(this.sync){task=this.StartReclaim(cancelReaders);}
            var error=await task.WaitAsync(cancellationToken).ConfigureAwait(false);if(error is not null){throw error;}
        }
        private Task<Exception?> StartReclaim(bool cancelReaders=true){
            this.cancelReaders|=cancelReaders;if(this.maintenance is not null){return this.maintenance;}
            this.owner.failure.ThrowIfFailed();ObjectDisposedException.ThrowIf(this.owner.stopping||this.disposed,this.owner);
            if(this.epochs.Keys.Any(static participant=>participant is not IReclaimableTransactionParticipant)){throw new NotSupportedException("All participants must support quiescent reclamation.");}
            this.maintenanceSequence=checked(this.maintenanceSequence+1);this.maintenanceStartedTimestamp=Stopwatch.GetTimestamp();this.maintenancePhysicalTimestamp=0;this.maintenanceStep=TransactionMaintenanceStep.None;
            var completion=new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);this.maintenance=completion.Task;
            _=Task.Run(async()=>{
                Exception? result=null;var physical=false;
                try{
                    var timer=Stopwatch.StartNew();
                    while(true){
                        List<Action> retire=[];var drained=false;
                        lock(this.sync){
                            this.owner.failure.ThrowIfFailed();ObjectDisposedException.ThrowIf(this.owner.stopping,this.owner);
                            foreach(var context in this.active.OrderBy(static context=>context.ReadVersion)){
                                if(!this.cancelReaders||!context.IsReadOnly||context.Sealed){continue;}
                                context.Abort(TransactionAbortReason.HistoryPressure);if(context.Operations==0&&context.Retire is not null){retire.Add(context.Retire);}
                            }
                            drained=this.active.Count==0;
                        }
                        foreach(var action in retire){action();}
                        if(drained){break;}
                        if(timer.Elapsed>=this.history.MaintenanceWaitTimeout){throw new TimeoutException("Active transaction references did not drain before the reclamation deadline.");}
                        await Task.Delay(10).ConfigureAwait(false);
                    }
                    this.owner.failure.ThrowIfFailed();ObjectDisposedException.ThrowIf(this.owner.stopping,this.owner);
                    this.maintenanceMemory?.Dispose();this.maintenanceMemory=null;this.maintenanceStorage?.Dispose();this.maintenanceStorage=null;
                    physical=true;
                    lock(this.sync){this.maintenancePhysicalTimestamp=Stopwatch.GetTimestamp();this.maintenanceStep=TransactionMaintenanceStep.ReclaimParticipants;}
                    foreach(var participant in this.epochs.Keys){await ((IReclaimableTransactionParticipant)participant).ReclaimAsync().ConfigureAwait(false);}
                    if(this.Checkpoint is not null){
                        lock(this.sync){this.maintenanceStep=TransactionMaintenanceStep.Checkpoint;}
                        await this.Checkpoint().ConfigureAwait(false);
                    }
                    lock(this.sync){this.maintenanceStep=TransactionMaintenanceStep.ReopenParticipants;}
                    foreach(var participant in this.epochs.Keys){((IReclaimableTransactionParticipant)participant).ReopenAfterMaintenance();}
                    lock(this.sync){this.maintenanceStep=TransactionMaintenanceStep.Finalize;}
                    this.ReserveMaintenance();
                    lock(this.sync){
                        foreach(var item in this.versions.Values){item.Memory.Dispose();}this.versions.Clear();this.historyMemory=0;this.historyStorage=0;
                    }
                }catch(Exception error){
                    result=error;if(physical){
                        this.owner.failure.Fail(error);this.owner.journal?.Writer.Fail(error);
                        foreach(var participant in this.epochs.Keys){try{((IReclaimableTransactionParticipant)participant).FaultMaintenance(error);}catch(Exception cleanup){result=new AggregateException(result,cleanup);}}
                    }
                }
                finally{lock(this.sync){
                    try{
                        if(result is null){foreach(var request in this.retirements){foreach(var child in request.Participants){((VersionedTransactionParticipantGroup)child.Routing!.Group).Retire(child);}}}
                    }catch(Exception error){result=error;this.owner.failure.Fail(error);this.owner.journal?.Writer.Fail(error);}
                    finally{
                        foreach(var request in this.retirements){request.Memory.Dispose();}this.retirements.Clear();
                        var phase=TransactionMaintenancePhase.Completed;if(result is not null){phase=TransactionMaintenancePhase.Failed;}
                        this.lastMaintenance=this.GetMaintenanceDiagnostics(phase);
                        this.maintenance=null;this.cancelReaders=false;completion.TrySetResult(result);Monitor.PulseAll(this.sync);
                    }
                }}
            });
            return completion.Task;
        }
        private void ReserveMaintenance(){
            if(this.parentBudget is null){return;}
            this.maintenanceMemory=this.parentBudget.ReserveMemory(this.history.MaintenanceReserveMemoryBytes);
            try{this.maintenanceStorage=this.parentBudget.ReserveTemporaryStorage(this.history.MaintenanceReserveStorageBytes);}
            catch{this.maintenanceMemory.Dispose();this.maintenanceMemory=null;throw;}
        }
    }
}
