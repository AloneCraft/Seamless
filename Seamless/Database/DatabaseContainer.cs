using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

using global::System.Linq;
/// <summary>
/// 宣言的データベースコンテナの基底。
/// 派生 partial クラスの表の宣言と、生成される IReader への拡張メンバー(ビュー・関数)から、
/// Seamless.Database の source generator がスキーマメタデータと取り込み配線を生成する。
/// Table 実体は Tsavorite ストアで、<see cref="DatabaseContainerOptions"/> により
/// メモリ範囲内運用とストレージ利用を切り替える。
/// </summary>
public abstract partial class DatabaseContainer:IDisposable {
    private static readonly global::Seamless.Database.System.SystemTable<global::Seamless.Database.System.Keys.DatabaseObject,global::Seamless.Database.System.Records.DatabaseObject> EmptyDatabaseObjects =
        new([],static record=>record.Key);

    private readonly global::System.Collections.Generic.Dictionary<string,IDisposable> tables= new(StringComparer.Ordinal);
    private readonly object                                                    gate  = new();
    private readonly DatabaseConcurrencyGate concurrencyGate=new();
    private readonly global::System.Threading.AsyncLocal<DatabaseTransactionState?> ambientTransaction=new();
    private readonly global::System.Threading.AsyncLocal<DatabaseReadSessionState?> ambientReadSession=new();
    private readonly global::System.Threading.AsyncLocal<int> ambientWriteDepth=new();
    private readonly global::Seamless.Execution.ExecutionScope executionResources;
    private volatile bool faulted;
    private          bool                                                      disposed;
    public           DatabaseContainerOptions                                  Options { get; }
    protected DatabaseContainer(DatabaseContainerOptions? options=null) {
        this.Options=ValidateOptions(options??DatabaseContainerOptions.InMemory);
        var temporaryDirectory=this.Options.TemporaryDirectory;
        if(string.IsNullOrWhiteSpace(temporaryDirectory)) {
            if(this.Options.StorageMode==DatabaseStorageMode.Storage) {
                temporaryDirectory=Path.Combine(this.ResolveStorageRoot(),".temporary");
            } else {
                temporaryDirectory=Path.Combine(Path.GetTempPath(),"Seamless","Database");
            }
        }
        global::Seamless.Execution.TemporaryResourceRecovery.RecoverAbandoned(temporaryDirectory);
        this.executionResources=new(new global::Seamless.Execution.ExecutionOptions {
            ParentBudget=this.Options.ParentBudget,
            MemoryLimitBytes=this.Options.ManagedMemoryLimitBytes,
            TemporaryStorageLimitBytes=this.Options.TemporaryStorageLimitBytes,
            TemporaryDirectory=temporaryDirectory,
            MaximumQueryWorkers=this.Options.MaximumQueryWorkers,
        });
        if(this.Options.RecordSerialization==DatabaseRecordSerialization.RecordGraphV1){
            this.executionResources.Codecs.UseRecordGraphSerialization(this.executionResources.Budget,this.executionResources.CancellationToken);
        }
    }
    /// <summary>実際のコンテナ型名。</summary>
    public string DatabaseName=>this.GetType().Name;
    /// <summary>DBが共有管理する実行時資源の現在値とピーク値。ディスクには永続ストアも含む。</summary>
    public DatabaseResourceUsage ResourceUsage=>new(
        this.executionResources.Budget.MemoryLimitBytes,
        this.executionResources.Budget.TemporaryStorageLimitBytes,
        this.executionResources.Budget.MemoryBytes,
        this.executionResources.Budget.TemporaryStorageBytes,
        this.executionResources.Budget.PeakMemoryBytes,
        this.executionResources.Budget.PeakTemporaryStorageBytes);
    internal global::Seamless.Execution.ExecutionScope ExecutionResources=>this.executionResources;
    // 共通ストアのメモリの上限は DB の上限(ManagedMemoryLimitBytes)にそろえる。渡さないとストアの既定の 256MB のままになり、
    // DB の上限を上げても表を開くときの先取り(表ごとのログのメモリ)が 256MB に収まらず、表の多い DB を開けなかった(2026-11-16)。
    private global::Seamless.Execution.StoreLimits SharedStoreLimits=>new(){MemoryBytes=this.Options.ManagedMemoryLimitBytes};
    /// <summary>DBの親予算と一時領域に属し、usingまたはDB終了時に解放する作業ストアを作る。</summary>
    public global::Seamless.Execution.Store CreateWorkStore(global::Seamless.Execution.StoreLimits? limits=null,global::Seamless.Execution.StoreOptions? options=null){
        this.EnsureAvailable();
        return this.executionResources.CreateWorkStore(limits,options);
    }
    /// <summary>生成する同期取込が、既存の同期writerの変更集合を維持するための状態検査。</summary>
    protected bool IsLegacyWriteTransactionActive=>this.ambientTransaction.Value is not null;

    /// <summary>DB全体writerを直列化し、変更をcommitまでstagingする。</summary>
    public DatabaseTransaction BeginTransaction(global::System.Threading.CancellationToken cancellationToken=default)=>this.BeginLegacyTransaction(null,cancellationToken);
    internal DatabaseTransaction BeginPlannedRowTransaction(string[] tables,global::System.Threading.CancellationToken cancellationToken)=>this.BeginLegacyTransaction(tables,cancellationToken);
    private DatabaseTransaction BeginLegacyTransaction(string[]? writeTables,global::System.Threading.CancellationToken cancellationToken) {
        if(this.Options.TransactionHistory is not null){throw new NotSupportedException("Use the generated BeginTransactionAsync or BeginReadOnlyTransactionAsync entry for the versioned profile.");}
        if(this.Options.GatewayProfile is not null){throw new InvalidOperationException("Use the owning Gateway transaction to update this database.");}
        this.EnsureAvailable();
        var state=this.ambientTransaction.Value;
        if(state is null) {
            if(this.ambientReadSession.Value is not null) {
                throw new InvalidOperationException("A read session cannot be promoted to a write transaction. Dispose the read session first.");
            }
            var shared=(writeTables is null?this.BeginTransactionScopeAsync(cancellationToken):
                this.BeginPlannedTransactionScopeAsync([],writeTables,cancellationToken)).AsTask().GetAwaiter().GetResult();
            state=new(this.Options.TransactionMemoryLimit,new SharedScopeLease(shared)){SharedScope=shared};
            this.ambientTransaction.Value=state;
        }
        state.Depth++;
        return new(this,state);
    }

    internal void ValidateTransactionContext(DatabaseTransactionState state){
        if(!ReferenceEquals(this.ambientTransaction.Value,state)||state.Depth<=0){throw new InvalidOperationException("The transaction scope does not belong to the current database context.");}
    }

    internal void EndTransaction(DatabaseTransactionState state,bool commitRequested) {
        if(!ReferenceEquals(this.ambientTransaction.Value,state)||state.Depth<=0) {
            throw new InvalidOperationException("The transaction scope does not belong to the current database context.");
        }
        if(!commitRequested) {
            state.Aborted=true;
        }
        state.Depth--;
        if(state.Depth!=0) {
            return;
        }
        try {
            if(!state.Aborted) {
                if(state.GatewayOwner is not null){
                    if(!state.GatewayApplied){throw new InvalidOperationException("The common Gateway WAL must commit before applying this database.");}
                    return;
                }
                try{this.ValidateRelationChanges(null,state);}catch{state.Aborted=true;throw;}
                if(state.SharedScope is not null){
                    state.SharedScope.EnlistLegacyChanges(new SharedLegacyChanges(this,state.Overlays.Values.ToArray()));
                    state.SharedScope.CommitAsync(state.CommitMetadata).AsTask().GetAwaiter().GetResult();
                    return;
                }
                throw new InvalidOperationException("The transaction has no shared scope.");
            }
        } catch(global::Seamless.Transactions.TransactionCommitException error)when(error.Outcome==global::Seamless.Transactions.TransactionOutcomeKind.Aborted&&state.SharedScope is{} scope&&error.TransactionId==scope.Id&&this.sharedCoordinator?.Failure.Error is null){
            state.Aborted=true;throw;
        } catch {
            if(!state.Aborted){this.faulted=true;}
            throw;
        } finally {
            this.ambientTransaction.Value=null;
            state.Dispose();
        }
    }

    public DatabaseReadSession BeginReadSession(global::System.Threading.CancellationToken cancellationToken=default) {
        this.EnsureAvailable();
        this.EnsureGatewayRecovered();
        var state=this.ambientReadSession.Value;
        if(state is null) {
            IDisposable? lease=null;
            if(this.ambientTransaction.Value is null&&this.ambientWriteDepth.Value==0&&this.gatewayVersionedExecution.Value is null){
                if(this.Options.GatewayProfile is null){this.EnsureSharedReady();lease=this.sharedCoordinator!.EnterRead(cancellationToken);}
                else{lease=this.concurrencyGate.EnterRead(cancellationToken);}
            }
            state=new(lease);
            this.ambientReadSession.Value=state;
        }
        state.Depth++;
        return new(this,state);
    }

    internal void EndReadSession(DatabaseReadSessionState state) {
        if(!ReferenceEquals(this.ambientReadSession.Value,state)||state.Depth<=0) {
            throw new InvalidOperationException("The read session does not belong to the current database context.");
        }
        state.Depth--;
        if(state.Depth!=0){return;}
        this.ambientReadSession.Value=null;
        state.Lease?.Dispose();
    }

    internal TableTransactionOverlay<TKey,TRecord>? GetTransactionOverlay<TKey,TRecord>(
        StoredTable<TKey,TRecord> table,
        bool create)
        where TKey:struct
        where TRecord:struct {
        this.ValidateOverlayWrite(create);
        var state=this.ambientTransaction.Value;
        if(state is null) {
            return null;
        }
        if(state.SharedScope?.HasTablePlan==true){state.SharedScope.CheckTableAccess(this.ResolveParticipant(table.Name),create);}
        if(state.Overlays.TryGetValue(table,out var existing)) {
            return (TableTransactionOverlay<TKey,TRecord>)existing;
        }
        if(!create) {
            return null;
        }
        var overlay=new TableTransactionOverlay<TKey,TRecord>(table,this.executionResources,this.Options.TransactionMemoryLimit);
        state.Overlays.Add(table,overlay);
        return overlay;
    }

    internal UnkeyedTableTransactionOverlay<TRecord>? GetTransactionOverlay<TRecord>(
        StoredTable<TRecord> table,
        bool create)
        where TRecord:notnull {
        this.ValidateOverlayWrite(create);
        var state=this.ambientTransaction.Value;
        if(state is null){return null;}
        if(state.Overlays.TryGetValue(table,out var existing)){
            return (UnkeyedTableTransactionOverlay<TRecord>)existing;
        }
        if(!create){return null;}
        var overlay=new UnkeyedTableTransactionOverlay<TRecord>(table,this.executionResources,this.Options.TransactionMemoryLimit);
        state.Overlays.Add(table,overlay);
        return overlay;
    }

    internal MaterializedViewTransactionOverlay<TKey,TRecord>? GetTransactionOverlay<TKey,TRecord>(
        MaterializedView<TKey,TRecord> view,
        bool create)
        where TKey:struct
        where TRecord:struct {
        this.ValidateOverlayWrite(create);
        var state=this.ambientTransaction.Value;
        if(state is null) {
            return null;
        }
        if(state.Overlays.TryGetValue(view,out var existing)) {
            return (MaterializedViewTransactionOverlay<TKey,TRecord>)existing;
        }
        if(!create) {
            return null;
        }
        var overlay=new MaterializedViewTransactionOverlay<TKey,TRecord>(view,this.executionResources,this.Options.TransactionMemoryLimit);
        state.Overlays.Add(view,overlay);
        return overlay;
    }

    internal TResult WithReadGate<TResult>(Func<TResult> action) {
        using var session=this.BeginReadSession();
        return action();
    }

    internal TResult WithWriteGate<TResult>(Func<TResult> action) {
        this.ValidateOverlayWrite(true);
        if(this.ambientTransaction.Value is not null||this.ambientWriteDepth.Value>0) {
            return action();
        }
        this.EnsureAvailable();
        throw new InvalidOperationException("Use a transaction to update the database.");
    }

    internal IEnumerator<T> Stream<T>(Func<IEnumerator<T>> getEnumerator) {
        var session=this.BeginReadSession();
        try{return new DatabaseSessionEnumerator<T>(getEnumerator(),session);}
        catch{session.Dispose();throw;}
    }

    internal void ValidateQueryAccess()=>this.EnsureAvailable();
    private void EnsureAvailable() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.unpublishedSnapshot is not null){throw new InvalidOperationException("The unpublished database snapshot is still being built.");}
        if(this.faulted){throw new DatabaseFaultedException("The database encountered an error while committing. Dispose and reopen it so committed WAL operations can be recovered.");}
    }

    /// <summary>generator が宣言メンバーから生成するオブジェクト一覧。生成前は空。</summary>
    public virtual global::Seamless.Database.System.SystemTable<global::Seamless.Database.System.Keys.DatabaseObject,global::Seamless.Database.System.Records.DatabaseObject> DatabaseObjects=>EmptyDatabaseObjects;

    /// <summary>generator が <c>IReader</c> への拡張メンバーから作るビュー・関数の入口(名前で呼ぶ Manager・Gateway 用)。生成前は空。</summary>
    public virtual IReadOnlyList<DatabaseQuery> Queries=>[];

    /// <summary>source generatorが読み取り専用システムテーブルを構築する。</summary>
    protected static global::Seamless.Database.System.SystemTable<TKey,TRecord> CreateSystemTable<TKey,TRecord>(
        IEnumerable<TRecord> rows,Func<TRecord,TKey> keySelector)
        where TKey:struct
        where TRecord:struct=>new(rows,keySelector);

    /// <summary>宣言プロパティから呼ばれ、テーブル実体を宣言名単位で生成・共有する。</summary>
    protected StoredTable<TRecord> Table<TRecord>([CallerMemberName] string name = "")
        where TRecord:notnull {
        return this.GetTable<TRecord>(name);
    }

    /// <summary>宣言プロパティから呼ばれ、候補キーで探索できるテーブルを生成・共有する。</summary>
    protected StoredTable<TKey,TRecord> Table<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        [CallerMemberName] string name="")
        where TKey:struct
        where TRecord:struct=>this.GetTable(keySelector,name);

    internal StoredTable<TKey,TRecord> GetTable<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        string name)
        where TKey:struct
        where TRecord:struct {
        ArgumentNullException.ThrowIfNull(keySelector);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.tables.TryGetValue(name,out var existing)) {
                return (StoredTable<TKey,TRecord>)existing;
            }
            var table=this.CreateTable(keySelector,name);
            try {
                this.tables.Add(name,table);
                return table;
            } catch {
                table.Dispose();
                throw;
            }
        }
    }

    /// <summary>宣言プロパティから呼ばれ、キー探索可能な実体化ビューを宣言名単位で生成・共有する。</summary>
    protected MaterializedView<TKey,TRecord> MaterializedView<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        [CallerMemberName] string name=default!)
        where TKey:struct
        where TRecord:struct=>this.GetMaterializedView(keySelector,name);

    internal MaterializedView<TKey,TRecord> GetMaterializedView<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        string name)
        where TKey:struct
        where TRecord:struct {
        ArgumentNullException.ThrowIfNull(keySelector);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.tables.TryGetValue(name,out var existing)) {
                return (MaterializedView<TKey,TRecord>)existing;
            }
            var view=this.CreateMaterializedView(keySelector,name);
            try {
                this.tables.Add(name,view);
                return view;
            } catch {
                view.Dispose();
                throw;
            }
        }
    }

    /// <summary>generatorが取込中に実体化ビューへ行を追加する。</summary>
    protected static bool AddMaterializedViewRow<TKey,TRecord>(
        MaterializedView<TKey,TRecord> view,
        TRecord record)
        where TKey:struct
        where TRecord:struct=>view.Add(record);

    internal StoredTable<TRecord> GetTable<TRecord>(string name)
        where TRecord:notnull {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.tables.TryGetValue(name,out var existing)) {
                return (StoredTable<TRecord>)existing;
            }
            var table = this.CreateTable<TRecord>(name);
            try {
                this.tables.Add(name,table);
                return table;
            } catch {
                table.Dispose();
                throw;
            }
        }
    }

    /// <summary>ストアドプロシージャを直ちに実行し、実行済み結果を返す。</summary>
    protected StoredProcedure<TResult> StoredProcedure<TResult>(
        Func<TResult> definition,
        [CallerMemberName] string name="")=>this.CreateStoredProcedure(definition,name);

    internal StoredProcedure<TResult> CreateStoredProcedure<TResult>(
        Func<TResult> definition,
        string name) {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        return new(definition(),name);
    }

    private StoredTable<TRecord> CreateTable<TRecord>(string name)
        where TRecord:notnull=>this.CreateSharedTable<TRecord>(name);

    private StoredTable<TKey,TRecord> CreateTable<TKey,TRecord>(Func<TRecord,TKey> keySelector,string name)
        where TKey:struct
        where TRecord:struct=>this.CreateSharedTable(keySelector,name);

    private MaterializedView<TKey,TRecord> CreateMaterializedView<TKey,TRecord>(Func<TRecord,TKey> keySelector,string name)
        where TKey:struct
        where TRecord:struct=>this.CreateSharedMaterializedView(keySelector,name);

    private string ResolveStorageRoot() =>
        this.Options.StorageDirectory??Path.Combine(Environment.CurrentDirectory,this.DatabaseName);

    private TableStorageSizing ResolveTableStorage(string name) {
        if(this.Options.TableStorage.TryGetValue(name,out var sizing)) {
            return new(sizing.IndexSize,sizing.PageSize,sizing.SegmentSize,sizing.MemorySize);
        }
        return new(this.Options.IndexSize,this.Options.PageSize,this.Options.SegmentSize,this.Options.MemorySize);
    }

    private static DatabaseContainerOptions ValidateOptions(DatabaseContainerOptions value) {
        ArgumentNullException.ThrowIfNull(value);
        if(value.TransactionMetadataConsumer is not null){
            if(value.StorageMode!=DatabaseStorageMode.Storage||value.GatewayProfile is not null){throw new ArgumentException("Metadata retention requires persistent standalone SharedV2 transactions.",nameof(value));}
            new global::Seamless.Transactions.WalOptions{MetadataConsumer=value.TransactionMetadataConsumer}.Validate();
        }
        if(!Enum.IsDefined(value.TransactionConcurrency)){throw new ArgumentException("Versioned concurrency requires the common transaction profile.",nameof(value));}
        if(value.TransactionConcurrency==global::Seamless.Transactions.TransactionConcurrencyMode.Optimistic&&value.TransactionHistory is null){throw new ArgumentException("Optimistic transactions require finite history budgets.",nameof(value));}
        if(value.GatewayProfile is not null&&value.TransactionHistory is not null&&value.GatewayVersionedBinding is null&&value.GatewayVersionedSeed is null){throw new NotSupportedException("The versioned Gateway domain adapter must own this profile; standalone configuration is not supported.");}
        value.GatewayVersionedBinding?.Validate(value);
        value.GatewayVersionedSeed?.Validate(value);
        if(!Enum.IsDefined(value.RecordSerialization)){throw new ArgumentException("Explicit record serialization requires the new SharedV2 storage profile.",nameof(value));}
        ArgumentNullException.ThrowIfNull(value);
        if(value.TransactionMemoryLimit<0) {
            throw new ArgumentOutOfRangeException(nameof(value.TransactionMemoryLimit));
        }
        if(value.ManagedMemoryLimitBytes<=0) {
            throw new ArgumentOutOfRangeException(nameof(value.ManagedMemoryLimitBytes));
        }
        if(value.TransactionMemoryLimit>value.ManagedMemoryLimitBytes) {
            throw new ArgumentOutOfRangeException(nameof(value.TransactionMemoryLimit));
        }
        if(value.TemporaryStorageLimitBytes<0) {
            throw new ArgumentOutOfRangeException(nameof(value.TemporaryStorageLimitBytes));
        }
        ArgumentNullException.ThrowIfNull(value.TableStorage);
        var tableStorage=new Dictionary<string,DatabaseTableStorageOptions>(StringComparer.Ordinal);
        foreach(var pair in value.TableStorage) {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            ArgumentNullException.ThrowIfNull(pair.Value);
            if(pair.Value.IndexSize<=0||pair.Value.PageSize<=0||pair.Value.SegmentSize<=0||pair.Value.MemorySize<=0) {
                throw new ArgumentOutOfRangeException(nameof(value.TableStorage),$"Storage sizes for '{pair.Key}' must be positive.");
            }
            tableStorage.Add(pair.Key,pair.Value);
        }
        return value with {
            TableStorage=new global::System.Collections.ObjectModel.ReadOnlyDictionary<string,DatabaseTableStorageOptions>(tableStorage),
        };
    }

    private readonly record struct TableStorageSizing(long IndexSize,long PageSize,long SegmentSize,long MemorySize);


    public void Dispose() {
        if(this.disposed){return;}
        if(this.unpublishedSnapshot is not null){throw new InvalidOperationException("Dispose the unpublished snapshot builder before disposing its database.");}
        this.DisposeShared();
    }
}
