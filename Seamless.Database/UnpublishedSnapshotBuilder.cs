using Seamless.Execution;
using Seamless.Transactions.Collections;
namespace Seamless.Database;

public abstract partial class DatabaseContainer{
    private UnpublishedSnapshotBuilder? unpublishedSnapshot;

    /// <summary>まだ公開・利用していない新規 canonical 領域を、全件 staging せず構築する。</summary>
    public UnpublishedSnapshotBuilder BeginUnpublishedSnapshot(){
        lock(this.gate){
            this.EnsureAvailable();
            if(this.Options.GatewayVersionedBinding is not null){throw new InvalidOperationException("Finish the private candidate snapshot before attaching it to the Gateway domain.");}
            if(this.Options.RecordSerialization!=DatabaseRecordSerialization.CanonicalV1){throw new InvalidOperationException("Unpublished snapshots require the canonical SharedV2 profile.");}
            this.EnsureTransactionSchema();this.EnsureSharedStorage();
            if(this.sharedReady||this.sharedStore!.CurrentCheckpoint is not null||this.sharedJournal is not null&&this.sharedJournal.Device.RecoveredLsn!=0){throw new InvalidOperationException("Only a fresh unpublished database can be bulk loaded.");}
            var builder=new UnpublishedSnapshotBuilder(this);this.unpublishedSnapshot=builder;return builder;
        }
    }

    public sealed class UnpublishedSnapshotBuilder:IDisposable{
        private readonly DatabaseContainer owner;
        private bool completed;
        private bool disposed;
        private bool failed;
        private long rowCount;
        private readonly Dictionary<string,SnapshotHandle> snapshots=new(StringComparer.Ordinal);
        internal UnpublishedSnapshotBuilder(DatabaseContainer owner){this.owner=owner;}
        public long RowCount=>this.rowCount;
        public void Insert<TKey,TRecord>(Table<TKey,TRecord> table,TRecord record) where TKey:struct where TRecord:struct{
            lock(this.owner.gate){
                this.Check();
                if(!this.owner.tables.TryGetValue(table.Name,out var registered)||!ReferenceEquals(registered,table)){throw new ArgumentException("The table does not belong to this snapshot.",nameof(table));}
                try{
                    bool added;
                    if(this.owner.Options.TransactionHistory is not null){
                        if(!this.owner.sharedParticipants.TryGetValue(table.Name,out var participant)||participant is not VersionedTransactionalTable<TKey,TRecord> typed){throw new InvalidOperationException("The snapshot table is not in the versioned schema.");}
                        added=this.Snapshot(typed,table.Name).Add(record);
                    }else{added=table.DirectAdd(table.DirectKey(record),record);}
                    if(!added){throw new InvalidDataException("Duplicate key in the unpublished snapshot: "+table.Name);}
                    this.rowCount=checked(this.rowCount+1);
                }catch{this.failed=true;throw;}
            }
        }
        public void Complete(){
            lock(this.owner.gate){
                this.Check();
                try{
                    foreach(var handle in this.snapshots.Values){handle.Complete();}this.ReleaseSnapshots();
                    // 空の候補にも基準版を残す。初期行の構築は業務WALのcommitではない。
                    if(this.owner.Options.TransactionHistory is not null){this.owner.sharedCheckpoint.CommitVersion=1;}
                    this.owner.unpublishedSnapshot=null;this.owner.EnsureSharedReady();
                    if(this.owner.RelationValidators.Length>0){var read=this.owner.BeginReadOnlyTransactionScopeAsync().AsTask().GetAwaiter().GetResult();try{this.owner.ValidateRelations(read);}finally{read.DisposeAsync().AsTask().GetAwaiter().GetResult();}}
                    this.completed=true;
                }
                catch{this.owner.unpublishedSnapshot=this;this.failed=true;throw;}
            }
        }
        private VersionedTransactionalTable<TKey,TRecord>.UnpublishedSnapshot Snapshot<TKey,TRecord>(VersionedTransactionalTable<TKey,TRecord> table,string name) where TKey:notnull{
            if(this.snapshots.TryGetValue(name,out var existing)){return (VersionedTransactionalTable<TKey,TRecord>.UnpublishedSnapshot)existing.Writer;}
            var memory=this.owner.executionResources.Budget.ReserveMemory(512);VersionedTransactionalTable<TKey,TRecord>.UnpublishedSnapshot? writer=null;
            try{writer=table.BeginUnpublishedSnapshot();this.snapshots.Add(name,new(writer,writer.Complete,writer,memory));return writer;}
            catch{try{writer?.Dispose();}finally{memory.Dispose();}throw;}
        }
        private void ReleaseSnapshots(){
            List<Exception>? errors=null;
            foreach(var handle in this.snapshots.Values){try{handle.Dispose();}catch(Exception error){(errors??=[]).Add(error);}}
            this.snapshots.Clear();if(errors is not null){throw new AggregateException("Unpublished snapshot cleanup failed.",errors);}
        }
        private sealed record SnapshotHandle(object Writer,Action Complete,IDisposable Cleanup,ResourceReservation Memory):IDisposable{
            public void Dispose(){try{this.Cleanup.Dispose();}finally{this.Memory.Dispose();}}
        }
        private void Check(){
            ObjectDisposedException.ThrowIf(this.disposed||this.completed,this);
            if(this.failed||!ReferenceEquals(this.owner.unpublishedSnapshot,this)){throw new InvalidOperationException("The unpublished snapshot is not writable.");}
        }
        public void Dispose(){
            lock(this.owner.gate){
                if(this.disposed){return;}this.disposed=true;
                try{this.ReleaseSnapshots();}catch{this.owner.faulted=true;throw;}
                finally{if(ReferenceEquals(this.owner.unpublishedSnapshot,this)){this.owner.unpublishedSnapshot=null;}if(!this.completed){this.owner.faulted=true;}}
            }
        }
    }
}
