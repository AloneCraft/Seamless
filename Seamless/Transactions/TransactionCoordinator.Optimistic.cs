using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Seamless.Execution;
namespace Seamless.Transactions;

public sealed partial class TransactionCoordinator{
    public ValueTask ReclaimHistoryAsync(CancellationToken cancellationToken=default){
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Finish the current transaction before waiting for domain reclamation.");}
        return (this.versioned??throw new NotSupportedException("A versioned domain is required.")).Reclaim(cancellationToken);
    }
    public ValueTask CheckpointVersionedAsync(CancellationToken cancellationToken=default){
        if(this.HasCurrentTransaction){throw new InvalidOperationException("Finish the current transaction before waiting for a checkpoint.");}
        return (this.versioned??throw new NotSupportedException("A versioned domain is required.")).Reclaim(cancellationToken,false);
    }
    public void RecoverVersioned(long checkpointLsn,long checkpointVersion){
        ArgumentOutOfRangeException.ThrowIfNegative(checkpointVersion);
        lock(this.initialization){if(this.started){return;}(this.versioned??throw new NotSupportedException("A versioned domain is required.")).RestoreVersion(checkpointVersion);this.RecoverCore(checkpointLsn);}
    }
    public void ConfigureMaintenance(Func<ValueTask> checkpoint){
        ArgumentNullException.ThrowIfNull(checkpoint);if(this.started){throw new InvalidOperationException("Configure maintenance before starting transactions.");}
        (this.versioned??throw new NotSupportedException("A versioned domain is required.")).Checkpoint=checkpoint;
    }
    private void Replay(ITransactionParticipant participant,WalEntry entry,Guid id,long version,long lsn){
        if(this.versioned is not null){((IVersionedTransactionParticipant)participant).Replay(entry,new(id,version,lsn,this.versioned.Id));}
        else{participant.Replay(entry);}
    }
    internal sealed class ConflictKey: IEquatable<ConflictKey>{
        internal readonly IVersionedTransactionParticipant Participant;
        internal readonly byte[] Bytes;
        private readonly int hash;
        internal ConflictKey(IVersionedTransactionParticipant participant,ReadOnlySpan<byte> bytes){
            this.Participant=participant;this.Bytes=bytes.ToArray();
            var hash=new HashCode();hash.Add(RuntimeHelpers.GetHashCode(participant));hash.AddBytes(bytes);this.hash=hash.ToHashCode();
        }
        internal ConflictKey(IVersionedTransactionParticipant participant,ReadOnlySpan<byte> prefix,ReadOnlySpan<byte> bytes){
            this.Participant=participant;this.Bytes=new byte[checked(4+prefix.Length+bytes.Length)];
            global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(this.Bytes,prefix.Length);prefix.CopyTo(this.Bytes.AsSpan(4));bytes.CopyTo(this.Bytes.AsSpan(4+prefix.Length));
            var hash=new HashCode();hash.Add(RuntimeHelpers.GetHashCode(participant));hash.AddBytes(this.Bytes);this.hash=hash.ToHashCode();
        }
        public bool Equals(ConflictKey? other)=>other is not null&&ReferenceEquals(this.Participant,other.Participant)&&this.Bytes.AsSpan().SequenceEqual(other.Bytes);
        public override bool Equals(object? other)=>other is ConflictKey key&&this.Equals(key);
        public override int GetHashCode()=>this.hash;
        internal long MetadataBytes=>checked(256L+4L*this.Bytes.Length);
    }
    internal sealed partial class OptimisticDomain:IDisposable{
        private readonly TransactionCoordinator owner;
        private readonly object sync=new();
        private readonly SemaphoreSlim pending;
        private readonly ResourceBudget metadataBudget;
        private readonly Dictionary<IVersionedTransactionParticipant,long> epochs=[];
        private readonly Dictionary<ConflictKey,(long Version,ResourceReservation Memory)> versions=[];
        private readonly HashSet<TransactionContext> active=[];
        private readonly List<ResourceReservation> schemaMemory=[];
        private long published;
        private long assigned;
        private Task previousAcceptance=Task.CompletedTask;
        private Task previousPublication=Task.CompletedTask;
        private bool disposed;
        internal Func<ValueTask>? Checkpoint;
        internal Guid Id{get;}=Guid.NewGuid();
        internal void ThrowIfFailed()=>this.owner.failure.ThrowIfFailed();
        internal bool HasActive{get{lock(this.sync){return this.active.Count!=0||this.maintenance is not null;}}}
        internal OptimisticDomain(TransactionCoordinator owner,TransactionCoordinatorOptions options){
            this.owner=owner;this.pending=new(options.MaximumPendingCommits,options.MaximumPendingCommits);
            this.history=options.History!;this.parentBudget=options.ParentBudget;this.ReserveMaintenance();
            var limit=options.History!.MaxMetadataBytes;
            if(options.ParentBudget is not null){this.metadataBudget=options.ParentBudget.CreateChild(limit,0);}
            else{this.metadataBudget=new(limit,0);}
        }
        internal void Register(IVersionedTransactionParticipant participant){
            var memory=this.metadataBudget.ReserveMemory(256);
            try{this.epochs.Add(participant,0);this.schemaMemory.Add(memory);}catch{memory.Dispose();throw;}
        }
        internal void InitializeVersion(){foreach(var participant in this.epochs.Keys){participant.PrepareForTransactions();this.published=Math.Max(this.published,participant.RecoveredVersion);}this.assigned=this.published;}
        internal void RestoreVersion(long version){this.published=version;}
        internal long NextRecoveryVersion(){this.published=checked(this.published+1);this.assigned=this.published;return this.published;}
        internal async ValueTask Start(TransactionContext context,CancellationToken cancellationToken){
            using var waiting=this.metadataBudget.ReserveMemory(128);
            var timer=global::System.Diagnostics.Stopwatch.StartNew();var waited=false;
            while(true){
                Task<Exception?>? wait;
                lock(this.sync){
                    ObjectDisposedException.ThrowIf(this.disposed||this.owner.disposed||this.owner.stopping,this.owner);this.owner.failure.ThrowIfFailed();
                    cancellationToken.ThrowIfCancellationRequested();wait=this.maintenance;
                    if(waited&&timer.Elapsed>=this.history.MaintenanceWaitTimeout){throw new TimeoutException("The transaction admission deadline expired during maintenance.");}
                    if(wait is null){
                        if(this.active.Any(active=>active.Id==context.Id)){throw new InvalidOperationException("The transaction identity is already active.");}
                        context.Metadata=this.metadataBudget.ReserveMemory(1024);context.Domain=this;context.ReadVersion=this.published;this.active.Add(context);return;
                    }
                }
                waited=true;var remaining=this.history.MaintenanceWaitTimeout-timer.Elapsed;
                if(remaining<=TimeSpan.Zero){throw new TimeoutException("The transaction admission deadline expired during maintenance.");}
                var error=await wait.WaitAsync(remaining,cancellationToken).ConfigureAwait(false);
                if(error is not null){throw error;}
            }
        }
        internal void BeginOperation(TransactionContext context){
            lock(this.sync){
                context.ThrowIfUnavailable();if(!this.active.Contains(context)){throw new ObjectDisposedException(nameof(TransactionContext));}context.Operations++;
            }
        }
        internal void EndOperation(TransactionContext context){
            Action? retire=null;
            lock(this.sync){context.Operations--;if(context.Operations==0&&context.IsAborted){retire=context.Retire;}}
            retire?.Invoke();
        }
        internal void End(TransactionContext context){
            lock(this.sync){
                if(!this.active.Remove(context)){return;}
                context.Dependencies.Clear();context.Scans.Clear();context.Participants.Clear();context.Metadata?.Dispose();context.Metadata=null;
                var oldest=this.published;
                foreach(var other in this.active){if(!other.IsReadOnly){oldest=Math.Min(oldest,other.ReadVersion);}}
                foreach(var pair in this.versions.ToArray()){
                    if(pair.Value.Version<=oldest){this.versions.Remove(pair.Key);pair.Value.Memory.Dispose();}
                }
                Monitor.PulseAll(this.sync);
            }
        }
        internal void WaitForDrain(){lock(this.sync){while(this.active.Count!=0||this.maintenance is not null){Monitor.Wait(this.sync);}}}
        internal bool UseParticipant(TransactionContext context,ITransactionParticipant participant){
            lock(this.sync){
                context.ThrowIfUnavailable();if(context.Participants.Contains(participant)){return false;}
                try{context.Metadata!.Resize(checked(context.Metadata.Bytes+512));}
                catch(ResourceLimitException error){context.Abort(TransactionAbortReason.ResourceLimit);throw new TransactionAbortedException(context.Id,TransactionAbortReason.ResourceLimit,error);}
                context.Participants.Add(participant);return true;
            }
        }
        internal void UndoParticipant(TransactionContext context,ITransactionParticipant participant){lock(this.sync){if(context.Participants.Remove(participant)){context.Metadata!.Resize(context.Metadata.Bytes-512);}}}
        internal ResourceReservation ReserveMetadata(TransactionContext context,long bytes){
            lock(this.sync){
                context.ThrowIfUnavailable();
                try{return this.metadataBudget.ReserveMemory(bytes);}
                catch(ResourceLimitException error){context.Abort(TransactionAbortReason.ResourceLimit);throw new TransactionAbortedException(context.Id,TransactionAbortReason.ResourceLimit,error);}
            }
        }
        private void Verify(TransactionContext context,IVersionedTransactionParticipant participant){
            context.ThrowIfUnavailable();
            if(!this.active.Contains(context)||!this.owner.Owns(participant)){throw new InvalidOperationException("The participant does not belong to this active versioned transaction.");}
            if(context.Sealed){throw new InvalidOperationException("Transaction dependencies are sealed for commit.");}
        }
        internal void Observe(TransactionContext context,IVersionedTransactionParticipant participant,ReadOnlySpan<byte> bytes,bool write){
            lock(this.sync){
                this.Verify(context,participant);if(context.IsReadOnly){return;}
                ConflictKey key;
                if(participant is IRoutedVersionedTransactionParticipant{Routing:{} route}){key=new(route.Group,route.Prefix.Span,bytes);}else{key=new(participant,bytes);}
                if(context.Dependencies.TryGetValue(key,out var previous)){context.Dependencies[key]=previous||write;return;}
                try{context.Metadata!.Resize(checked(context.Metadata.Bytes+key.MetadataBytes));}
                catch(ResourceLimitException error){context.Abort(TransactionAbortReason.ResourceLimit);throw new TransactionAbortedException(context.Id,TransactionAbortReason.ResourceLimit,error);}
                context.Dependencies.Add(key,write);
            }
        }
        internal void ObserveScan(TransactionContext context,IVersionedTransactionParticipant participant){
            lock(this.sync){
                this.Verify(context,participant);
                if(participant is IRoutedVersionedTransactionParticipant{Routing:{} route}){participant=route.Group;}
                this.Verify(context,participant);if(context.IsReadOnly||context.Scans.Contains(participant)){return;}
                try{context.Metadata!.Resize(checked(context.Metadata.Bytes+128));}
                catch(ResourceLimitException error){context.Abort(TransactionAbortReason.ResourceLimit);throw new TransactionAbortedException(context.Id,TransactionAbortReason.ResourceLimit,error);}
                context.Scans.Add(participant);
            }
        }
        private void Validate(TransactionContext context){
            context.ThrowIfUnavailable();this.owner.failure.ThrowIfFailed();
            foreach(var pair in context.Dependencies){
                if(this.versions.TryGetValue(pair.Key,out var version)&&version.Version>context.ReadVersion){throw new TransactionAbortedException(context.Id,TransactionAbortReason.Conflict);}
            }
            foreach(var table in context.Scans){if(this.epochs[table]>context.ReadVersion){throw new TransactionAbortedException(context.Id,TransactionAbortReason.Conflict);}}
            context.Sealed=true;
        }
        private static void CheckCancellation(TransactionContext context,CancellationToken cancellationToken){
            if(cancellationToken.IsCancellationRequested){context.Abort(TransactionAbortReason.CallerCancellation);throw new TransactionAbortedException(context.Id,TransactionAbortReason.CallerCancellation,new OperationCanceledException(cancellationToken));}
        }
        internal async ValueTask<TransactionCommitResult> Commit(TransactionContext context,IReadOnlyList<ITransactionChanges> changes,ReadOnlyMemory<byte> metadata,CancellationToken cancellationToken,Action<TransactionInstallationContext>? installed=null){
            CheckCancellation(context,cancellationToken);
            if(this.owner.preparedJournal is{} prepared&&metadata.Length>prepared.MaximumMetadataBytes){context.Abort(TransactionAbortReason.ResourceLimit);throw new TransactionAbortedException(context.Id,TransactionAbortReason.ResourceLimit);}
            this.owner.failure.ThrowIfFailed();context.Freeze();
            if(context.IsReadOnly){lock(this.sync){context.ThrowIfUnavailable();CheckCancellation(context,cancellationToken);context.Sealed=true;return new(context.Id,TransactionCommitKind.NoChanges,0);}}
            var preparations=new List<(IVersionedTransactionChanges Changes,PreparedTransactionParticipant Prepared)>();
            var reserved=new Dictionary<ConflictKey,ResourceReservation>();CommitOrder? order=null;var accepted=false;var durable=false;var completed=false;var pending=false;long lsn=0;
            try{
                await this.pending.WaitAsync(cancellationToken).ConfigureAwait(false);pending=true;
                var walRequired=this.owner.journal is not null||this.owner.preparedJournal is not null;
                var maximum=this.owner.journal?.Writer.MaximumFrameBytes??int.MaxValue;
                foreach(var change in changes){
                    if(change is not IVersionedTransactionChanges versioned){throw new NotSupportedException("Every change in a versioned domain requires versioned preparation and installation.");}
                    var preparation=await versioned.PrepareAsync(new(context,walRequired,maximum),cancellationToken).ConfigureAwait(false);
                    preparations.Add((versioned,preparation));
                }
                var hasChanges=preparations.Any(static item=>item.Prepared.MutationCount!=0)||!metadata.IsEmpty;
                IEnumerable<WalEntry> Entries(){foreach(var item in preparations){foreach(var entry in item.Prepared.Entries){yield return entry;}}}
                WalDraft? draft=null;
                if(this.owner.journal is not null&&hasChanges){
                    var entries=new List<WalEntry>();var partitions=new SortedSet<int>();long bytes=checked(60L+metadata.Length);
                    foreach(var entry in Entries()){
                        bytes=checked(bytes+WalFormat.EntryPayloadBytes(entry.Codec,entry.Key.Length,entry.Row?.Length??0));if(partitions.Add(entry.Partition)){bytes=checked(bytes+4);}
                        if(bytes>maximum){throw new TransactionAbortedException(context.Id,TransactionAbortReason.ResourceLimit);}entries.Add(entry);
                    }
                    if(partitions.Count==0){partitions.Add(0);}draft=new(context.Id,partitions.ToArray(),entries,metadata.ToArray());
                }
                lock(this.sync){
                    CheckCancellation(context,cancellationToken);
                    this.Validate(context);
                    if(!hasChanges){return new(context.Id,TransactionCommitKind.NoChanges,this.owner.AppliedLsn);}
                    foreach(var pair in context.Dependencies){
                        if(pair.Value&&!this.versions.ContainsKey(pair.Key)){reserved.Add(pair.Key,this.metadataBudget.ReserveMemory(pair.Key.MetadataBytes));}
                    }
                    var version=checked(this.assigned+1);
                    order=new(version,this.previousAcceptance,this.previousPublication);
                    foreach(var pair in context.Dependencies){
                        if(!pair.Value){continue;}
                        if(this.versions.TryGetValue(pair.Key,out var old)){this.versions[pair.Key]=(version,old.Memory);}
                        else{this.versions.Add(pair.Key,(version,reserved[pair.Key]));reserved.Remove(pair.Key);}
                        this.epochs[pair.Key.Participant]=version;
                    }
                    this.assigned=version;this.previousAcceptance=order.Accepted.Task;this.previousPublication=order.Published.Task;
                }
                await order.PreviousAcceptance.WaitAsync(cancellationToken).ConfigureAwait(false);this.owner.failure.ThrowIfFailed();CheckCancellation(context,cancellationToken);
                if(this.owner.preparedJournal is not null){
                    var committed=await this.owner.preparedJournal.CommitAsync(context.Id,Entries(),metadata,cancellationToken).ConfigureAwait(false);lsn=committed.Lsn;accepted=true;durable=true;
                }else if(this.owner.journal is not null){
                    var receipt=await this.owner.journal.AppendAsync(draft!,cancellationToken).ConfigureAwait(false);accepted=true;lsn=receipt.Lsn;order.Accepted.TrySetResult();
                    await receipt.Durable.ConfigureAwait(false);durable=true;
                }
                order.Accepted.TrySetResult();
                if(accepted){await order.PreviousPublication.ConfigureAwait(false);}else{await order.PreviousPublication.WaitAsync(cancellationToken).ConfigureAwait(false);CheckCancellation(context,cancellationToken);}
                this.owner.failure.ThrowIfFailed();
                // Memoryの設置失敗も、途中まで適用した状態で続行させない。
                accepted=true;
                foreach(var item in preparations){item.Changes.Install(new(context.Id,order.Version,lsn,this.Id),item.Prepared);}
                installed?.Invoke(new(context.Id,order.Version,lsn,this.Id));
                lock(this.sync){
                    this.owner.failure.ThrowIfFailed();this.owner.AppliedLsn=Math.Max(this.owner.AppliedLsn,lsn);this.published=order.Version;
                }
                completed=true;return new(context.Id,TransactionCommitKind.Committed,lsn);
            }catch(Exception error){
                if(!accepted&&error is TransactionCommitException commitError&&commitError.Outcome!=TransactionOutcomeKind.Aborted){accepted=true;durable=commitError.Outcome==TransactionOutcomeKind.Committed;}
                if(!accepted&&this.owner.failure.Error is null&&(error is TransactionAbortedException||error is ResourceLimitException||error is OperationCanceledException||error is TransactionCommitException{Outcome:TransactionOutcomeKind.Aborted})){
                    var reason=TransactionAbortReason.ResourceLimit;if(error is TransactionAbortedException aborted){reason=aborted.Reason;}else if(error is OperationCanceledException){reason=TransactionAbortReason.CallerCancellation;}
                    context.Abort(reason);
                    if(error is TransactionAbortedException){throw;}
                    throw new TransactionAbortedException(context.Id,reason,error);
                }
                this.owner.failure.Fail(error);this.owner.journal?.Writer.Fail(error);
                var outcome=TransactionOutcomeKind.Unknown;if(durable){outcome=TransactionOutcomeKind.Committed;}
                throw new TransactionCommitException(context.Id,outcome,error);
            }finally{
                try{
                    foreach(var memory in reserved.Values){memory.Dispose();}
                    List<Exception>? cleanup=null;
                    foreach(var item in preparations){try{item.Prepared.Dispose();}catch(Exception error){cleanup??=[];cleanup.Add(error);}}
                    if(cleanup is not null){var error=new AggregateException("Versioned preparation cleanup failed.",cleanup);this.owner.failure.Fail(error);var outcome=TransactionOutcomeKind.Unknown;if(completed||durable){outcome=TransactionOutcomeKind.Committed;}throw new TransactionCommitException(context.Id,outcome,error);}
                }finally{
                    if(order is not null){
                        order.Accepted.TrySetResult();await order.PreviousPublication.ConfigureAwait(false);
                        lock(this.sync){if(this.owner.failure.Error is null){this.published=Math.Max(this.published,order.Version);}}
                        order.Published.TrySetResult();
                    }
                    if(pending){this.pending.Release();}
                }
            }
        }
        public void Dispose(){
            lock(this.sync){
                if(this.active.Count!=0||this.maintenance is not null){throw new InvalidOperationException("Finish active versioned transactions and maintenance before disposing the domain.");}
                this.disposed=true;
                foreach(var item in this.versions.Values){item.Memory.Dispose();}this.versions.Clear();
                foreach(var memory in this.schemaMemory){memory.Dispose();}this.schemaMemory.Clear();
                this.maintenanceMemory?.Dispose();this.maintenanceStorage?.Dispose();
                this.pending.Dispose();
            }
        }
        private sealed class CommitOrder(long version,Task previousAcceptance,Task previousPublication){
            internal long Version{get;}=version;
            internal Task PreviousAcceptance{get;}=previousAcceptance;
            internal Task PreviousPublication{get;}=previousPublication;
            internal TaskCompletionSource Accepted{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource Published{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
