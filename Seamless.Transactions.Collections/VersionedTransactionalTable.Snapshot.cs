using Seamless.Collections.Tsavorite;
using Seamless.Execution;
namespace Seamless.Transactions.Collections;

public sealed partial class VersionedTransactionalTable<TKey,TRecord> where TKey:notnull{
    private readonly object snapshotGate=new();
    // 0:未使用、1:通常使用、2:初期投入、3:未完了/失敗、4:破棄済み。
    private int snapshotState;
    private UnpublishedSnapshot? snapshot;
    /// <summary>公開前の新規表だけに、変更集合と業務WALを作らず初期行を逐次追加する。</summary>
    public UnpublishedSnapshot BeginUnpublishedSnapshot(long baseVersion=1){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseVersion);
        lock(this.snapshotGate){
            if(this.snapshotState!=0||this.data.HasRecoveredCheckpoint||this.data.MaximumCommittedVersion!=0){throw new InvalidOperationException("Only a fresh unused table can build an unpublished snapshot.");}
            this.snapshotState=2;
            try{return this.snapshot=new(this,baseVersion);}catch{this.snapshotState=3;throw;}
        }
    }
    private void SealUnpublishedSnapshot()=>this.CheckUnpublishedSnapshot(true);
    private void CheckUnpublishedSnapshot(bool seal){
        if(Volatile.Read(ref this.snapshotState)==1){return;}
        lock(this.snapshotGate){
            ObjectDisposedException.ThrowIf(this.snapshotState==4,this);
            if(this.snapshotState==2){throw new InvalidOperationException("Finish the unpublished table snapshot before using or maintaining the table.");}
            if(this.snapshotState==3){throw new InvalidOperationException("The unpublished table snapshot was not completed; discard its dataset.");}
            if(seal){this.snapshotState=1;}
        }
    }
    public sealed class UnpublishedSnapshot:IDisposable{
        private readonly VersionedTransactionalTable<TKey,TRecord> owner;
        private readonly long baseVersion;
        private readonly ResourceReservation working;
        private HistoryAccessor? access;
        private bool completed,disposed;
        private long rowCount;
        internal UnpublishedSnapshot(VersionedTransactionalTable<TKey,TRecord> owner,long baseVersion){
            this.owner=owner;this.baseVersion=baseVersion;this.working=owner.resources.Budget.ReserveMemory(checked(owner.data.PageBytes*4+512));
            try{this.access=owner.Access();}catch{this.working.Dispose();throw;}
        }
        public long RowCount=>Interlocked.Read(ref this.rowCount);
        public bool Add(TRecord row){
            lock(this.owner.snapshotGate){
                this.Check();
                try{
                    var key=this.owner.keyOf(row);if(this.owner.ReadStored(this.access!,key).HasValue){return false;}
                    // 基準版の行はまだ他取引へ公開しない。旧版保持の予約ではなく、単行のnative容量を予約する。
                    using var capacity=this.owner.data.ReserveAppend(this.owner.EstimateAppend(key,row,false));
                    this.owner.Append(this.access!,key,row,this.baseVersion,false);this.rowCount=checked(this.rowCount+1);return true;
                }catch{this.owner.snapshotState=3;throw;}
            }
        }
        public void Complete(){
            lock(this.owner.snapshotGate){
                this.Check();
                try{this.Close();this.completed=true;this.owner.snapshotState=1;}
                catch{this.owner.snapshotState=3;throw;}
                finally{this.owner.snapshot=null;}
            }
        }
        private void Check(){
            ObjectDisposedException.ThrowIf(this.disposed||this.completed,this);
            if(this.owner.snapshotState!=2||!ReferenceEquals(this.owner.snapshot,this)){throw new InvalidOperationException("The unpublished table snapshot is no longer writable.");}
        }
        private void Close(){try{this.access?.Dispose();}finally{this.access=null;this.working.Dispose();}}
        public void Dispose(){
            lock(this.owner.snapshotGate){
                if(this.disposed){return;}this.disposed=true;
                try{this.Close();}finally{if(!this.completed){this.owner.snapshotState=3;}if(ReferenceEquals(this.owner.snapshot,this)){this.owner.snapshot=null;}}
            }
        }
    }
}
