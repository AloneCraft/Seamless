using System.Text.Json;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
using Store=Seamless.Execution.Store;
namespace Benchmark.Tpc.Core;

/// <summary>TPCの主表と索引を、一つの共通MVCC/WAL/公開境界に登録する明示adapter。</summary>
public sealed class VersionedTpcDomain:IDisposable{
    private readonly Store store;
    private readonly ExecutionScope resources;
    private readonly TransactionJournal? journal;
    private readonly Checkpoint checkpoint=new();
    private readonly List<(Action Close,Action Open)> handles=[];
    private readonly List<ExecutionScope> participantResources=[];
    private readonly TxnLog log;
    private int nextId;
    private bool started,disposed;
    public VersionedTpcDomain(Store store,TxnLog log,TransactionCoordinatorOptions options,ExecutionOptions execution){
        ArgumentNullException.ThrowIfNull(options.History);this.store=store;this.log=log;
        this.resources=new(execution with{ParentBudget=store.Budget});
        try{
            if(store.Kind==StoreKind.Persistent){
                this.journal=new(Path.Combine(store.DirectoryPath!,"tpc-mvcc-wal-v1"),new WalOptions{CatalogIdentity="tpc-deferred-mvcc-v1"},this.resources.Budget);
                store.Register(this.checkpoint);
                if(this.checkpoint.Loaded&&this.checkpoint.WalId!=this.journal.Device.WalId){throw new InvalidDataException("TPC store and WAL identity mismatch.");}
                if(store.CurrentCheckpoint is not null&&!this.checkpoint.Loaded){throw new InvalidDataException("A fresh versioned TPC dataset is required.");}
                this.checkpoint.WalId=this.journal.Device.WalId;
            }
            this.Coordinator=new(this.journal,null,null,options with{ParentBudget=this.resources.Budget});
            this.Coordinator.ConfigureMaintenance(this.Save);log.UseShared(this.Coordinator,this.resources);
        }catch{this.journal?.DisposeAsync().AsTask().GetAwaiter().GetResult();this.resources.Dispose();throw;}
    }
    public TransactionCoordinator Coordinator{get;}
    public TransactionScope Current=>this.log.SharedScope;
    public ResourceBudget Budget=>this.resources.Budget;
    public VersionedTransactionalTable<TKey,TRow> CreateTable<TKey,TRow>(string name,Func<TRow,TKey> keyOf,TsavoriteTuning tuning,long changeMemoryLimit=1<<20,Action<CodecRegistry>? configure=null) where TKey:notnull{
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.started){throw new InvalidOperationException("Declare all TPC rows and indexes before recovery.");}
        var child=this.resources.CreateChild();VersionedTransactionalTable<TKey,TRow>? participant=null;
        try{
            configure?.Invoke(child.Codecs);
            var identity=name+"|"+child.Codecs.Get<TKey>().FormatId+"|"+child.Codecs.Get<TRow>().FormatId;
            var data=this.store.CreateTsavoriteHistory(name,tuning,VersionedTransactionalTable<TKey,TRow>.StorageIdentity(identity,VersionedRecordEncoding.Canonical));
            participant=new(0,checked(++this.nextId),identity,data,child,changeMemoryLimit,keyOf);
            this.Coordinator.Register(participant);this.handles.Add((participant.CloseForMaintenance,participant.ReopenAfterMaintenance));this.participantResources.Add(child);return participant;
        }catch{participant?.Dispose();child.Dispose();throw;}
    }
    public void Recover(){
        if(this.started){return;}
        if(this.store.Kind==StoreKind.Persistent&&this.store.CurrentCheckpoint is null){
            if(this.journal!.Device.RecoveredLsn!=0){throw new InvalidDataException("Committed TPC WAL requires its initial store checkpoint.");}
            this.Close();try{this.checkpoint.AllowSave=true;this.store.Save();}finally{this.checkpoint.AllowSave=false;this.Open();}
        }
        this.Coordinator.RecoverVersioned(this.checkpoint.Lsn,this.checkpoint.Version);this.started=true;
    }
    public ValueTask CheckpointAsync(CancellationToken token=default)=>this.Coordinator.CheckpointVersionedAsync(token);
    private void Close(){foreach(var handle in this.handles){handle.Close();}}
    private void Open(){foreach(var handle in this.handles){handle.Open();}}
    private async ValueTask Save(){
        if(this.store.Kind!=StoreKind.Persistent){return;}
        var lsn=this.Coordinator.AppliedLsn;
        try{
            await this.journal!.Writer.WaitForDurableAsync(lsn).ConfigureAwait(false);this.checkpoint.Lsn=lsn;this.checkpoint.Version=this.Coordinator.GetDomainDiagnostics().PublishedVersion;
            // 共通保守境界が全participantを閉じてから呼ぶ。Store.Saveで勝手に再開しない。
            try{this.checkpoint.AllowSave=true;this.store.Save();}finally{this.checkpoint.AllowSave=false;}await this.journal.CheckpointAndPruneAsync(lsn).ConfigureAwait(false);
        }catch(Exception error){this.Coordinator.Failure.Fail(error);this.journal!.Writer.Fail(error);throw;}
    }
    public void Dispose(){
        if(this.disposed){return;}if(this.Coordinator.GetDomainDiagnostics().ActiveTransactions!=0){throw new InvalidOperationException("Finish all TPC transactions before disposing the domain.");}this.disposed=true;
        try{this.Coordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();}
        finally{try{this.Close();}finally{foreach(var child in this.participantResources){child.Dispose();}this.journal?.DisposeAsync().AsTask().GetAwaiter().GetResult();this.resources.Dispose();}}
    }
    private sealed class Checkpoint:IStoreComponent{
        public string Name=>"tpc-mvcc-checkpoint-v1";
        internal Guid WalId;internal long Lsn,Version;internal bool Loaded,AllowSave;
        public string Save(){if(!this.AllowSave){throw new InvalidOperationException("Use the common TPC domain checkpoint rather than Store.Save.");}return JsonSerializer.Serialize(new Data(1,this.WalId,this.Lsn,this.Version));}
        public void Load(string state){var data=JsonSerializer.Deserialize<Data>(state)??throw new InvalidDataException("Missing TPC checkpoint.");if(data.Format!=1||data.WalId==Guid.Empty||data.Lsn<0||data.Version<0){throw new InvalidDataException("Invalid TPC checkpoint.");}this.WalId=data.WalId;this.Lsn=data.Lsn;this.Version=data.Version;this.Loaded=true;}
        public void Flush(){}public void Dispose(){}
        private sealed record Data(int Format,Guid WalId,long Lsn,long Version);
    }
}
