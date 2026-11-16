using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
using ExecutionStore=Seamless.Execution.Store;
namespace Seamless.Database;
public abstract partial class DatabaseContainer{
    private ExecutionStore? sharedStore;
    private TransactionJournal? sharedJournal;
    private PreparedTransactionJournal? preparedSharedJournal;
    // 転記元の登録・参照では、利用者のスキーマgetterや業務行の復旧を実行しない。
    internal TransactionJournal? MetadataJournal{get{lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);if(this.Options.TransactionMetadataConsumer is null){return null;}this.EnsureSharedStorage();return this.sharedJournal;}}}
    private TransactionCoordinator? sharedCoordinator;
    private readonly SharedCheckpoint sharedCheckpoint=new();
    private readonly Dictionary<string,ITransactionParticipant> sharedParticipants=new(StringComparer.Ordinal);
    private readonly List<(Action Release,Action Acquire)> sharedHandles=[];
    private bool sharedReady;
    // 試験用：閉じるときに保存しない(落ちたことにする)。WAL から当て直す経路を確かめる。
    internal bool DiscardOnDispose{get;set;}
    // 使う表だけを開く：保存の仕組みをまだ開いていないかもしれない表(名前→開いているか)。
    private readonly Dictionary<string,Func<bool>> lazyTables=new(StringComparer.Ordinal);
    // 素の WAL・版付きの保存なし・Gateway なし・書き込みを1つずつ通す方式の永続の DB で、最初の取引の前に作る表だけを遅らせる。
    private bool CanOpenTablesLazily=>!this.sharedReady&&this.sharedStore?.Kind==StoreKind.Persistent&&this.sharedJournal is not null&&this.preparedSharedJournal is null
        &&this.Options.TransactionHistory is null&&this.Options.GatewayProfile is null&&this.Options.GatewayVersionedBinding is null
        &&this.sharedCoordinator is{SupportsDeferredReplay:true,Concurrency:TransactionConcurrencyMode.Exclusive};
    // 表が当て直し済みの WAL の位置。前の保存のときに開いていなかった表は、その表の位置(保存点に記録)。ほかは保存点の位置。
    private long TableCheckpointLsn(string name)=>this.sharedCheckpoint.TableLsn is{} lsns&&lsns.TryGetValue(name,out var own)?own:this.sharedCheckpoint.Lsn;
    protected virtual string TransactionCatalogIdentity=>this.GetType().FullName??this.DatabaseName;
    private void CheckRecordGraphContract(string name,string identity){
        if(this.Options.RecordSerialization!=DatabaseRecordSerialization.RecordGraphV1){return;}
        if(this.sharedCheckpoint.Loaded){
            if(!this.sharedCheckpoint.RecordContracts.TryGetValue(name,out var saved)||saved!=identity){throw new InvalidDataException("The saved record graph contract differs. Explicit migration is required.");}
        }else{this.sharedCheckpoint.RecordContracts[name]=identity;}
    }
    protected string TransactionCodecIdentity<TKey,TRecord>(string name){
        var identity=name+"|"+this.executionResources.Codecs.Get<TKey>().FormatId+"|"+this.executionResources.Codecs.Get<TRecord>().FormatId;
        if(this.Options.TransactionHistory is not null){identity+="|DeferredMvccV1";}
        if(this.Options.RecordSerialization==DatabaseRecordSerialization.CanonicalV1){identity+="|CanonicalStoreV1";}
        if(this.Options.RecordSerialization==DatabaseRecordSerialization.RecordGraphV1){identity+="|RecordGraphStoreV1";}
        if(this.Options.TransactionHistory is not null&&this.Options.GatewayProfile is{} profile){identity+="|Generation="+profile.GenerationId.ToString("N");}
        return identity;
    }
    protected virtual void EnsureTransactionSchema(){throw new NotSupportedException("SharedV2 requires a generated typed transaction schema.");}
    protected ValueTask<TransactionScope> BeginTransactionScopeAsync(CancellationToken cancellationToken=default)=>this.BeginTransactionScopeCore(false,cancellationToken);
    protected ValueTask<TransactionScope> BeginReadOnlyTransactionScopeAsync(CancellationToken cancellationToken=default)=>this.BeginTransactionScopeCore(true,cancellationToken);
    private ValueTask<TransactionScope> BeginTransactionScopeCore(bool readOnly,CancellationToken cancellationToken){
        this.EnsureAvailable();
        if(this.Options.GatewayProfile is not null){throw new InvalidOperationException("Use the owning Gateway transaction rather than a standalone transaction scope.");}
        if(this.ambientReadSession.Value is not null||this.ambientTransaction.Value is not null){throw new InvalidOperationException("A legacy/read scope is already active.");}
        this.EnsureSharedReady();
        if(readOnly){return this.sharedCoordinator!.BeginReadOnlyAsync(cancellationToken);}
        return this.sharedCoordinator!.BeginAsync(cancellationToken);
    }
    private void EnsureSharedReady(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate){
            if(this.sharedReady){return;}
            this.EnsureTransactionSchema();this.EnsureSharedStorage();
            if(this.Options.RecordSerialization==DatabaseRecordSerialization.RecordGraphV1&&this.sharedCheckpoint.Loaded&&this.sharedParticipants.Count!=this.sharedCheckpoint.RecordContracts.Count){throw new InvalidDataException("The saved record graph table set differs. Explicit migration is required.");}
            if(this.sharedStore!.Kind==StoreKind.Persistent&&this.sharedStore.CurrentCheckpoint is null){
                if((this.sharedJournal?.Device.RecoveredLsn??this.preparedSharedJournal?.DurableLsn??0)!=0){throw new InvalidDataException("Committed WAL requires the original initialized Store checkpoint.");}
                try{
                    foreach(var handle in this.sharedHandles){handle.Release();}
                    this.sharedStore.Save();foreach(var handle in this.sharedHandles){handle.Acquire();}
                }catch(Exception error){if(this.Options.GatewayVersionedBinding is null){this.sharedCoordinator!.Failure.Fail(error);this.sharedJournal?.Writer.Fail(error);}else{this.faulted=true;}throw;}
            }
            if(this.Options.GatewayVersionedBinding is not null){this.sharedReady=true;return;}
            if(this.Options.TransactionHistory is not null){this.sharedCoordinator!.RecoverVersioned(this.sharedCheckpoint.Lsn,this.sharedCheckpoint.CommitVersion);}
            else{this.sharedCoordinator!.Recover(this.sharedCheckpoint.Lsn);}this.sharedReady=true;
        }
    }
    private void EnsureSharedStorage(){
        if(this.sharedStore is not null){return;}
        if(this.Options.PreparedTransactionWal&&(this.Options.StorageMode!=DatabaseStorageMode.Storage||this.Options.GatewayProfile is not null
            ||this.Options.GatewayVersionedBinding is not null||this.Options.TransactionHistory is not null||this.Options.TransactionMetadataConsumer is not null)){
            throw new InvalidOperationException("The standalone prepared WAL requires persistent current-value storage without a Gateway or metadata consumer.");
        }
        this.Options.GatewayVersionedBinding?.Bind(this);
        this.Options.GatewayVersionedSeed?.Bind(this);
        var failure=new TransactionFailure();
        try{
            var versioned=this.Options.TransactionHistory is not null;this.sharedCheckpoint.Versioned=versioned;
            if(this.Options.StorageMode==DatabaseStorageMode.Storage){
                var root=this.ResolveStorageRoot();
                var walDirectory="wal-v2";var storeDirectory="store-v2";var catalog=this.TransactionCatalogIdentity;
                if(this.Options.PreparedTransactionWal){
                    if(Directory.Exists(Path.Combine(root,"wal-v2"))){throw new InvalidOperationException("Explicit migration is required before selecting the prepared WAL.");}
                    walDirectory="wal-prepared-v1";
                }else if(Directory.Exists(Path.Combine(root,"wal-prepared-v1"))){
                    throw new InvalidOperationException("This dataset requires the prepared WAL profile.");
                }
                if(versioned){
                    if(Directory.Exists(Path.Combine(root,"wal-v2"))||Directory.Exists(Path.Combine(root,"store-v2"))){throw new InvalidOperationException("Create the versioned profile in a fresh dataset; the current-value profile is not automatically converted.");}
                    walDirectory="wal-mvcc-v1";storeDirectory="store-mvcc-v1";catalog+="|DeferredMvccV1";
                }else if(Directory.Exists(Path.Combine(root,"wal-mvcc-v1"))||Directory.Exists(Path.Combine(root,"store-mvcc-v1"))){throw new InvalidOperationException("A versioned dataset requires TransactionHistory options even when opened with Exclusive concurrency.");}
                if(this.Options.GatewayProfile is not null&&Directory.Exists(Path.Combine(root,"wal-v2"))){throw new InvalidOperationException("A standalone SharedV2 WAL cannot be opened as a Gateway database.");}
                if(this.Options.GatewayProfile is not null&&Directory.Exists(Path.Combine(root,"wal-mvcc-v1"))){throw new InvalidOperationException("A standalone versioned WAL cannot be attached to a common Gateway domain.");}
                if(File.Exists(Path.Combine(root,"database.wal"))||Directory.Exists(root)&&Directory.EnumerateFiles(root,"checkpoint.token",SearchOption.AllDirectories).Any(path=>!IsDirectSetPath(root,path))){
                    throw new InvalidOperationException("Legacy database detected. Create SharedV2 in a separate new directory; automatic migration is disabled.");
                }
                if(this.Options.GatewayProfile is null){
                    var walOptions=new WalOptions{CatalogIdentity=catalog,MetadataConsumer=this.Options.TransactionMetadataConsumer};
                    if(this.Options.PreparedTransactionWal){
                        this.preparedSharedJournal=new(Path.Combine(root,walDirectory),walOptions,this.executionResources.Budget);
                        failure=this.preparedSharedJournal.Failure;
                    }else{this.sharedJournal=new(Path.Combine(root,walDirectory),walOptions,this.executionResources.Budget,failure);}
                }
                this.sharedStore=ExecutionStore.Open(Path.Combine(root,storeDirectory),this.SharedStoreLimits,this.Options.SharedStoreOptions,this.executionResources.Budget);
                this.sharedCheckpoint.DatabaseId=this.Options.GatewayProfile?.DatabaseId??0;
                this.sharedCheckpoint.GenerationId=this.Options.GatewayProfile?.GenerationId??Guid.Empty;
                this.sharedCheckpoint.GraphProfile=this.Options.RecordSerialization==DatabaseRecordSerialization.RecordGraphV1;
                this.sharedStore.Register(this.sharedCheckpoint);
                var walId=this.Options.GatewayProfile?.WalId??this.preparedSharedJournal?.WalId??this.sharedJournal!.Device.WalId;
                if(this.sharedCheckpoint.Loaded&&this.sharedCheckpoint.WalId!=walId){throw new InvalidDataException("Store and WAL belong to different datasets.");}
                if(this.sharedStore.CurrentCheckpoint is not null&&!this.sharedCheckpoint.Loaded){throw new InvalidDataException("SharedV2 checkpoint component is missing.");}
                this.sharedCheckpoint.WalId=walId;
            }else{this.sharedStore=ExecutionStore.CreateMemory(options:this.Options.SharedStoreOptions,parentBudget:this.executionResources.Budget);}
            if(this.Options.GatewayVersionedBinding is{} binding){this.sharedCoordinator=binding.Coordinator;}
            else{this.sharedCoordinator=new(this.sharedJournal,failure,this.preparedSharedJournal,new TransactionCoordinatorOptions{Concurrency=this.Options.TransactionConcurrency,History=this.Options.TransactionHistory,ParentBudget=this.executionResources.Budget});this.sharedCoordinator.RegisterCommitValidator(this.ValidateRelations);if(versioned){this.sharedCoordinator.ConfigureMaintenance(()=>this.SaveShared(false));}}
        }catch(Exception error){
            var errors=new List<Exception>{error};
            void Close(Action close){try{close();}catch(Exception cleanup){errors.Add(cleanup);}}
            Close(()=>this.sharedStore?.Dispose());
            Close(()=>this.sharedJournal?.DisposeAsync().AsTask().GetAwaiter().GetResult());
            Close(()=>this.preparedSharedJournal?.DisposeAsync().AsTask().GetAwaiter().GetResult());
            this.sharedStore=null;this.sharedJournal=null;this.preparedSharedJournal=null;
            if(errors.Count>1){throw new AggregateException("Shared storage initialization and cleanup failed.",errors);}
            throw;
        }
    }
    private Table<TKey,TRecord> CreateSharedTable<TKey,TRecord>(Func<TRecord,TKey> keySelector,string name) where TKey:struct where TRecord:struct{
        if(this.Options.TransactionHistory is not null){var versioned=this.CreateVersionedParticipant(keySelector,name);return new(this,keySelector,versioned,name);}
        this.EnsureSharedStorage();var sizing=this.ResolveTableStorage(name);
        var identity=this.TransactionCodecIdentity<TKey,TRecord>(name);this.CheckRecordGraphContract(name,identity);
        var tuning=new TsavoriteTuning{IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize};
        global::Seamless.Collections.Tsavorite.DictionaryStore<TKey,TRecord>? data=null;
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> Create(){
            data??=this.sharedStore!.CreateTsavorite<TKey,TRecord>(name,tuning);
            if(this.Options.RecordSerialization==DatabaseRecordSerialization.RecordGraphV1){return data.Create<global::Seamless.Execution.Codecs.RecordGraphSerializer<TKey>,global::Seamless.Execution.Codecs.RecordGraphSerializer<TRecord>>();}
            if(this.Options.RecordSerialization==DatabaseRecordSerialization.CanonicalV1){return data.Create<global::Seamless.Execution.Codecs.CanonicalSerializer<TKey>,global::Seamless.Execution.Codecs.CanonicalSerializer<TRecord>>();}
            return data.Create();
        }
        var id=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)))&int.MaxValue;
        if(id==0){id=1;}
        // 使う表だけを開く(設計 20261116-使う表だけを開く.md)：条件に合えば、保存の仕組み(メモリを取り置く)は初めて使うときに開き、
        // そのときに、この表の分の変更の記録だけを当て直す。登録(取引の調停への参加)は今までどおり最初の取引の前に行う。
        TransactionalTable<TKey,TRecord>? participant=null;
        Table<TKey,TRecord> table;
        if(this.CanOpenTablesLazily){
            table=new Table<TKey,TRecord>(this,keySelector,new SharedEmptyLease(),name,Create,()=>this.sharedCoordinator!.OpenDeferred(participant!),Create);
            this.sharedStore!.KeepUnopened([name]);this.lazyTables.Add(name,()=>table.IsStorageOpen);
        }else{
            table=new Table<TKey,TRecord>(this,Create(),keySelector,new SharedEmptyLease(),name,Create);
        }
        participant=new TransactionalTable<TKey,TRecord>(0,id,identity,this.executionResources,this.Options.TransactionMemoryLimit,
            key=>{var found=table.DirectTryGetValue(key,out var row);return(found,row);},table.DirectSet,key=>table.DirectRemove(key),keySelector,()=>table.DirectRows);
        this.sharedCoordinator!.Register(participant);this.sharedParticipants.Add(name,participant);
        if(this.lazyTables.ContainsKey(name)){this.sharedCoordinator.DeferReplay(participant,this.TableCheckpointLsn(name));}
        this.sharedHandles.Add((table.ReleaseSharedHandle,table.AcquireSharedHandle));return table;
    }
    public TransactionCollection<TKey,TRecord> GetTransactionCollection<TKey,TRecord>(TransactionScope scope,Table<TKey,TRecord> table) where TKey:struct where TRecord:struct{
        this.EnsureAvailable();
        this.CheckGatewayVersionedAdmission();
        if(!this.tables.TryGetValue(table.Name,out var own)||!ReferenceEquals(own,table)||!this.sharedParticipants.TryGetValue(table.Name,out var participant)||participant is not ITransactionalTable<TKey,TRecord> typed){throw new InvalidOperationException("Table is not part of this typed transaction schema.");}
        return typed.For(scope);
    }
    private MaterializedView<TKey,TRecord> CreateSharedMaterializedView<TKey,TRecord>(Func<TRecord,TKey> keySelector,string name) where TKey:struct where TRecord:struct{
        if(this.Options.TransactionHistory is not null){var versioned=this.CreateVersionedParticipant(keySelector,name);return new(this,keySelector,versioned,name);}
        this.EnsureSharedStorage();var sizing=this.ResolveTableStorage(name);
        var identity=this.TransactionCodecIdentity<TKey,TRecord>(name);this.CheckRecordGraphContract(name,identity);
        var data=this.sharedStore!.CreateTsavorite<TKey,TRecord>(name,new TsavoriteTuning{IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize});
        global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord> Create(){
            if(this.Options.RecordSerialization==DatabaseRecordSerialization.RecordGraphV1){return data.Create<global::Seamless.Execution.Codecs.RecordGraphSerializer<TKey>,global::Seamless.Execution.Codecs.RecordGraphSerializer<TRecord>>();}
            if(this.Options.RecordSerialization==DatabaseRecordSerialization.CanonicalV1){return data.Create<global::Seamless.Execution.Codecs.CanonicalSerializer<TKey>,global::Seamless.Execution.Codecs.CanonicalSerializer<TRecord>>();}
            return data.Create();
        }
        var view=new MaterializedView<TKey,TRecord>(this,Create(),keySelector,new SharedEmptyLease(),name,Create);
        var id=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)))&int.MaxValue;if(id==0){id=1;}
        var participant=new TransactionalTable<TKey,TRecord>(0,id,identity,this.executionResources,this.Options.TransactionMemoryLimit,
            key=>{var found=view.DirectTryGetValue(key,out var row);return(found,row);},view.DirectSet,static _=>throw new InvalidOperationException("Materialized views are read-only."),keySelector,()=>view.DirectRows);
        this.sharedCoordinator!.Register(participant);this.sharedParticipants.Add(name,participant);this.sharedHandles.Add((view.ReleaseSharedHandle,view.AcquireSharedHandle));return view;
    }
    public ReadOnlyTransactionCollection<TKey,TRecord> GetTransactionCollection<TKey,TRecord>(TransactionScope scope,MaterializedView<TKey,TRecord> view) where TKey:struct where TRecord:struct{
        this.EnsureAvailable();
        this.CheckGatewayVersionedAdmission();
        if(!this.tables.TryGetValue(view.Name,out var own)||!ReferenceEquals(own,view)||!this.sharedParticipants.TryGetValue(view.Name,out var participant)||participant is not ITransactionalTable<TKey,TRecord> typed){throw new InvalidOperationException("View is not part of this typed transaction schema.");}
        return new(typed.For(scope));
    }
    /// <summary>生成された取込処理だけが使うビュー構築能力。利用者の通常ビューは読取り専用のまま。</summary>
    protected TransactionCollection<TKey,TRecord> GetMaterializedViewImportCollection<TKey,TRecord>(TransactionScope scope,MaterializedView<TKey,TRecord> view) where TKey:struct where TRecord:struct{
        this.EnsureAvailable();
        this.CheckGatewayVersionedAdmission();
        if(!this.tables.TryGetValue(view.Name,out var own)||!ReferenceEquals(own,view)||!this.sharedParticipants.TryGetValue(view.Name,out var participant)||participant is not ITransactionalTable<TKey,TRecord> typed){throw new InvalidOperationException("View is not part of this typed transaction schema.");}
        return typed.For(scope);
    }
    private Table<TRecord> CreateSharedTable<TRecord>(string name) where TRecord:notnull{
        if(this.Options.GatewayVersionedBinding is not null||this.Options.GatewayVersionedSeed is not null){throw new NotSupportedException("Gateway tables require stable keys; generate an internal row ID for SQL tables without primary keys.");}
        if(this.Options.TransactionHistory is not null){var versioned=this.CreateVersionedParticipant<TRecord,TRecord>(static row=>row,name);return new(this,versioned,name);}
        if(this.Options.RecordSerialization!=DatabaseRecordSerialization.Default){throw new NotSupportedException("Explicit typed storage requires a stable key; generate an internal row ID for keyless SQL tables.");}
        this.EnsureSharedStorage();var sizing=this.ResolveTableStorage(name);
        var data=this.sharedStore!.CreateTsavoriteHashSet<TRecord>(name,new TsavoriteTuning{IndexSize=checked(sizing.IndexSize*64),PageSize=sizing.PageSize,SegmentSize=sizing.SegmentSize,LogMemorySize=sizing.MemorySize});
        var table=new Table<TRecord>(this,data.Create(),name,data.Create);
        var id=BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(name)))&int.MaxValue;if(id==0){id=1;}
        var participant=new TransactionalTable<TRecord,TRecord>(0,id,this.TransactionCodecIdentity<TRecord,TRecord>(name),this.executionResources,this.Options.TransactionMemoryLimit,
            key=>(table.DirectContains(key),key),(key,row)=>table.DirectAdd(row),key=>table.DirectRemove(key),static row=>row,()=>table.DirectRows.Select(static row=>new KeyValuePair<TRecord,TRecord>(row,row)));
        this.sharedCoordinator!.Register(participant);this.sharedParticipants.Add(name,participant);this.sharedHandles.Add((table.ReleaseSharedHandle,table.AcquireSharedHandle));return table;
    }
    public TransactionCollection<TRecord,TRecord> GetTransactionCollection<TRecord>(TransactionScope scope,Table<TRecord> table) where TRecord:notnull{
        this.EnsureAvailable();
        this.CheckGatewayVersionedAdmission();
        if(!this.tables.TryGetValue(table.Name,out var own)||!ReferenceEquals(own,table)||!this.sharedParticipants.TryGetValue(table.Name,out var participant)||participant is not ITransactionalTable<TRecord,TRecord> typed){throw new InvalidOperationException("Table is not part of this typed transaction schema.");}
        return typed.For(scope);
    }
    public ValueTask<TransactionOutcome> GetTransactionOutcomeAsync(Guid id){
        if(this.Options.GatewayProfile is not null){throw new InvalidOperationException("Query the common Gateway transaction outcome.");}
        this.EnsureSharedReady();return this.sharedCoordinator!.GetOutcomeAsync(id);
    }
    public ValueTask ForgetTransactionOutcomesThroughAsync(long lsn){
        if(this.Options.GatewayVersionedBinding is not null){throw new InvalidOperationException("Use the common Gateway outcome owner.");}
        this.EnsureSharedReady();
        if(this.preparedSharedJournal is not null){return this.preparedSharedJournal.ForgetThroughAsync(lsn);}
        if(this.sharedJournal is null){return ValueTask.CompletedTask;}
        return this.sharedJournal.ForgetThroughAsync(lsn);
    }
    public async ValueTask CheckpointTransactionsAsync(){
        if(this.Options.GatewayVersionedBinding is not null){throw new InvalidOperationException("Use the common Gateway checkpoint owner.");}
        this.EnsureSharedReady();
        if(this.Options.TransactionHistory is not null){await this.sharedCoordinator!.CheckpointVersionedAsync().ConfigureAwait(false);return;}
        using var lease=this.sharedCoordinator!.EnterRead();
        await this.SaveShared().ConfigureAwait(false);
    }
    public ValueTask ReclaimTransactionHistoryAsync(CancellationToken cancellationToken=default){if(this.Options.GatewayVersionedBinding is not null){throw new InvalidOperationException("Use the common Gateway history owner.");}this.EnsureSharedReady();return this.sharedCoordinator!.ReclaimHistoryAsync(cancellationToken);}
    /// <summary>Gateway接続時は共通owner、それ以外はこのDBの版付きdomainの診断を返す。障害停止中も既存ownerの状態を取得できる。</summary>
    public TransactionDomainDiagnostics GetTransactionDomainDiagnostics(){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.Options.GatewayVersionedBinding is{} binding){return binding.Coordinator.GetDomainDiagnostics();}
        this.EnsureSharedReady();return this.sharedCoordinator!.GetDomainDiagnostics();
    }
    private async ValueTask SaveShared(bool reopenHandles=true){
        if(this.Options.GatewayProfile is not null){throw new InvalidOperationException("Only the Gateway common checkpoint may save this profile.");}
        if(this.sharedStore!.Kind!=StoreKind.Persistent){return;}
        this.sharedCoordinator!.Failure.ThrowIfFailed();var lsn=this.sharedCoordinator.AppliedLsn;var pruneLimit=long.MaxValue;
        try{
            void Save(long fence){
                // 開いていない表は前の位置のまま記録する(その表の分の記録は、開くまで当てていない)。WAL もその位置より前は消さない。
                Dictionary<string,long>? unopened=null;
                foreach(var (name,isOpen) in this.lazyTables){if(isOpen()){continue;}var own=this.TableCheckpointLsn(name);(unopened??=new(StringComparer.Ordinal))[name]=own;pruneLimit=Math.Min(pruneLimit,own);}
                this.sharedCheckpoint.TableLsn=unopened;
                this.sharedCheckpoint.Lsn=fence;
                if(this.Options.TransactionHistory is not null){this.sharedCheckpoint.CommitVersion=this.sharedCoordinator!.GetDomainDiagnostics().PublishedVersion;}
                foreach(var handle in this.sharedHandles){handle.Release();}
                this.sharedStore.Save();
                if(reopenHandles){foreach(var handle in this.sharedHandles){handle.Acquire();}}
            }
            if(this.preparedSharedJournal is not null){
                await this.preparedSharedJournal.CheckpointAsync((walId,fence)=>{
                    if(walId!=this.sharedCheckpoint.WalId){throw new InvalidDataException("Store and prepared WAL belong to different datasets.");}
                    Save(fence);return ValueTask.CompletedTask;
                }).ConfigureAwait(false);
            }else{
                await this.sharedJournal!.Writer.WaitForDurableAsync(lsn).ConfigureAwait(false);
                Save(lsn);
                await this.sharedJournal.CheckpointAndPruneAsync(Math.Min(lsn,pruneLimit)).ConfigureAwait(false);
            }
        }catch(Exception error){this.sharedCoordinator.Failure.Fail(error);this.sharedJournal?.Writer.Fail(error);throw;}
    }
    private void DisposeShared(){
        if(this.Options.GatewayVersionedBinding is not null){this.DisposeGatewayVersionedStorage();return;}
        if(this.ambientTransaction.Value is not null||this.ambientReadSession.Value is not null||this.sharedCoordinator?.HasCurrentTransaction==true){throw new InvalidOperationException("Finish active transactions/read sessions before disposing the database.");}
        var errors=new List<Exception>();
        this.sharedCoordinator?.StopAccepting();
        if(this.sharedCoordinator is not null){
            using var lease=this.sharedCoordinator.EnterReadForShutdown();
            if(this.sharedReady&&!this.DiscardOnDispose&&this.sharedCoordinator.Failure.Error is null&&this.Options.GatewayProfile is null){try{this.SaveShared(this.Options.TransactionHistory is null).AsTask().GetAwaiter().GetResult();}catch(Exception error){errors.Add(error);}}
            this.disposed=true;
            foreach(var table in this.tables.Values){try{table.Dispose();}catch(Exception error){errors.Add(error);}}
            this.tables.Clear();
        }else{this.disposed=true;}
        lock(this.gate){this.DisposeDirectSets(errors);}
        try{this.sharedCoordinator?.DisposeAsync().AsTask().GetAwaiter().GetResult();}catch(Exception error){errors.Add(error);}
        try{this.sharedStore?.Dispose();}catch(Exception error){errors.Add(error);}
        try{this.sharedJournal?.DisposeAsync().AsTask().GetAwaiter().GetResult();}catch(Exception error){errors.Add(error);}
        try{this.preparedSharedJournal?.DisposeAsync().AsTask().GetAwaiter().GetResult();}catch(Exception error){errors.Add(error);}
        try{this.executionResources.Dispose();}catch(Exception error){errors.Add(error);}
        if(errors.Count>0){throw new AggregateException("Shared transaction database cleanup failed.",errors);}
    }
    private sealed class SharedEmptyLease:IDisposable{public void Dispose(){}}
    private sealed class SharedScopeLease(TransactionScope scope):IDisposable{public void Dispose()=>scope.DisposeAsync().AsTask().GetAwaiter().GetResult();}
    private sealed class SharedLegacyChanges(DatabaseContainer owner,ITransactionOverlay[] overlays):ITransactionChanges{
        private readonly ResourceReservation walMemory=owner.executionResources.Budget.ReserveMemory(0);
        public IReadOnlyList<WalEntry> Encode()=>this.EncodeBounded(int.MaxValue);
        public IEnumerable<WalEntry> EnumerateEncoded(){
            try{
                foreach(var overlay in overlays){foreach(var operation in overlay.CreateWalOperations()){using(operation){
                    var participant=owner.sharedParticipants[operation.TableName];var key=Convert.FromBase64String(operation.KeyData);byte[]? row=null;var kind=WalMutation.Upsert;
                    if(operation.Deleted){kind=WalMutation.Delete;}else{row=Convert.FromBase64String(operation.RecordData!);}
                    this.walMemory.Resize(checked(key.Length+(row?.Length??0)+128L));yield return new(participant.Partition,participant.TableId,kind,participant.CodecIdentity,key,row);
                }}}
            }finally{this.walMemory.Resize(0);}
        }
        public IReadOnlyList<WalEntry> EncodeBounded(int maximumBytes){
            var entries=new List<WalEntry>();long bytes=0;this.walMemory.Resize(0);
            foreach(var overlay in overlays){
                foreach(var operation in overlay.CreateWalOperations()){
                    using(operation){
                        var participant=owner.sharedParticipants[operation.TableName];byte[]? row=null;var kind=WalMutation.Upsert;
                        if(operation.Deleted){kind=WalMutation.Delete;}else{row=Convert.FromBase64String(operation.RecordData!);}
                        var key=Convert.FromBase64String(operation.KeyData);bytes=checked(bytes+WalFormat.EntryPayloadBytes(participant.CodecIdentity,key.Length,row?.Length??0));
                        if(bytes>maximumBytes){throw new InvalidDataException("Transaction exceeds the WAL frame bound.");}
                        this.walMemory.Resize(checked(this.walMemory.Bytes+key.Length+(row?.Length??0)+128L));
                        entries.Add(new(participant.Partition,participant.TableId,kind,participant.CodecIdentity,key,row));
                    }
                }
            }
            return entries;
        }
        public void Apply(){foreach(var overlay in overlays){overlay.Apply();}}
        public void Dispose()=>this.walMemory.Dispose();
    }
    private sealed class SharedCheckpoint:IStoreComponent{
        public string Name=>"~transaction-checkpoint-v2";
        public Guid WalId;
        public long Lsn;
        public int DatabaseId;
        public Guid GenerationId;
        public string? DataSha256;
        public bool Loaded;
        public bool Versioned;
        public long CommitVersion;
        public bool GraphProfile;
        public Dictionary<string,string> RecordContracts=new(StringComparer.Ordinal);
        // 使う表だけを開く：保存のときに開いていなかった表と、その表が当て直し済みの WAL の位置。無ければ全部の表が Lsn まで当て直し済み。
        public Dictionary<string,long>? TableLsn;
        public string Save(){
            var version=2;if(this.DatabaseId!=0){version=3;}if(this.GenerationId!=Guid.Empty){version=4;}if(this.DataSha256 is not null){version=5;}if(this.Versioned){version=6;}
            Dictionary<string,string>? contracts=null;if(this.GraphProfile){version=7;contracts=this.RecordContracts;}
            return global::System.Text.Json.JsonSerializer.Serialize(new CheckpointData(version,this.WalId,this.Lsn,this.DatabaseId,this.GenerationId,this.DataSha256,this.Versioned,this.CommitVersion,contracts){TableLsn=this.TableLsn});
        }
        public void Load(string state){
            var value=global::System.Text.Json.JsonSerializer.Deserialize<CheckpointData>(state)??throw new InvalidDataException("Missing transaction checkpoint.");
            if(value.TableLsn is{} lsns&&lsns.Any(pair=>string.IsNullOrWhiteSpace(pair.Key)||pair.Value<0||pair.Value>value.Lsn)){throw new InvalidDataException("Invalid table WAL positions in the transaction checkpoint.");}
            this.TableLsn=value.TableLsn is null?null:new(value.TableLsn,StringComparer.Ordinal);
            if(value.Version==7){
                if(!this.GraphProfile||value.RecordContracts is null||value.Versioned!=this.Versioned||value.WalId==Guid.Empty||value.Lsn<0||value.CommitVersion<0||!this.Versioned&&value.CommitVersion!=0||value.DatabaseId!=this.DatabaseId||value.GenerationId!=this.GenerationId
                    ||value.DataSha256 is not null&&(value.DataSha256.Length!=64||!value.DataSha256.All(static character=>Uri.IsHexDigit(character)))
                    ||value.RecordContracts.Any(static pair=>string.IsNullOrWhiteSpace(pair.Key)||string.IsNullOrWhiteSpace(pair.Value))){throw new InvalidDataException("Invalid record graph checkpoint or storage profile.");}
                this.RecordContracts=new(value.RecordContracts,StringComparer.Ordinal);this.WalId=value.WalId;this.Lsn=value.Lsn;this.DataSha256=value.DataSha256;this.CommitVersion=value.CommitVersion;this.Loaded=true;return;
            }
            if(this.GraphProfile||value.RecordContracts is not null){throw new InvalidDataException("The saved record serialization profile differs. Explicit migration is required.");}
            if(value.Version==6){
                if(!this.Versioned||!value.Versioned||value.WalId==Guid.Empty||value.Lsn<0||value.CommitVersion<0||value.DatabaseId!=this.DatabaseId||value.GenerationId!=this.GenerationId){throw new InvalidDataException("Invalid versioned transaction checkpoint or dataset identity.");}
                this.WalId=value.WalId;this.Lsn=value.Lsn;this.DataSha256=value.DataSha256;this.CommitVersion=value.CommitVersion;this.Loaded=true;return;
            }
            if(this.Versioned||value.Versioned||value.CommitVersion!=0){throw new InvalidDataException("The transaction checkpoint belongs to a different storage profile.");}
            if(value.Version is not (2 or 3 or 4 or 5)||value.WalId==Guid.Empty||value.Lsn<0||value.DatabaseId!=this.DatabaseId||value.GenerationId!=this.GenerationId||value.Version>=3&&value.DatabaseId<=0||value.Version==2&&value.DatabaseId!=0||value.Version<4&&value.GenerationId!=Guid.Empty||value.Version==4&&value.GenerationId==Guid.Empty||value.Version==5&&(value.DataSha256 is not{Length:64}||!value.DataSha256.All(static character=>Uri.IsHexDigit(character)))){throw new InvalidDataException("Invalid transaction checkpoint or Gateway database/generation identity.");}
            this.WalId=value.WalId;this.Lsn=value.Lsn;this.DataSha256=value.DataSha256;this.Loaded=true;
        }
        public void Flush(){}
        public void Dispose(){}
        private sealed record CheckpointData(int Version,Guid WalId,long Lsn,int DatabaseId=0,Guid GenerationId=default,string? DataSha256=null,bool Versioned=false,long CommitVersion=0,
            [property:global::System.Text.Json.Serialization.JsonIgnore(Condition=global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]Dictionary<string,string>? RecordContracts=null){
            [global::System.Text.Json.Serialization.JsonIgnore(Condition=global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public Dictionary<string,long>? TableLsn{get;init;}
        }
    }
}
