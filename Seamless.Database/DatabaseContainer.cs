namespace Seamless.Database;

/// <summary>
/// 宣言的データベースコンテナの基底。
/// 派生 partial クラスの Table/View/TableFunction/スカラー関数宣言から、
/// Seamless.Database の source generator がスキーマメタデータと取り込み配線を生成する。
/// Table 実体は Tsavorite ストアで、<see cref="DatabaseContainerOptions"/> により
/// メモリ範囲内運用とストレージ利用を切り替える。
/// </summary>
public abstract class DatabaseContainer:IDisposable {
    private static readonly global::Seamless.Database.System.SystemTable<global::Seamless.Database.System.Keys.DatabaseObject,global::Seamless.Database.System.Records.DatabaseObject> EmptyDatabaseObjects =
        new([],static record=>record.Key);

    private readonly global::System.Collections.Generic.Dictionary<string,IDisposable> tables= new(StringComparer.Ordinal);
    private readonly global::System.Collections.Generic.Dictionary<string,object>      views = new(StringComparer.Ordinal);
    private readonly object                                                    gate  = new();
    private readonly DatabaseConcurrencyGate concurrencyGate=new();
    private readonly global::System.Threading.AsyncLocal<DatabaseTransactionState?> ambientTransaction=new();
    private readonly global::System.Threading.AsyncLocal<DatabaseReadSessionState?> ambientReadSession=new();
    private readonly global::System.Threading.AsyncLocal<int> ambientWriteDepth=new();
    private readonly global::Seamless.Execution.ExecutionScope executionResources;
    private DatabaseWriteAheadLog? writeAheadLog;
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
            MemoryLimitBytes=this.Options.ManagedMemoryLimitBytes,
            TemporaryStorageLimitBytes=this.Options.TemporaryStorageLimitBytes,
            TemporaryDirectory=temporaryDirectory,
        });
    }
    /// <summary>実際のコンテナ型名。</summary>
    public string DatabaseName=>this.GetType().Name;
    /// <summary>DBが共有管理する実行時資源の現在値とピーク値。</summary>
    public DatabaseResourceUsage ResourceUsage=>new(
        this.executionResources.Budget.MemoryLimitBytes,
        this.executionResources.Budget.TemporaryStorageLimitBytes,
        this.executionResources.Budget.MemoryBytes,
        this.executionResources.Budget.TemporaryStorageBytes,
        this.executionResources.Budget.PeakMemoryBytes,
        this.executionResources.Budget.PeakTemporaryStorageBytes);
    internal global::Seamless.Execution.ExecutionScope ExecutionResources=>this.executionResources;

    /// <summary>DB全体writerを直列化し、変更をcommitまでstagingする。</summary>
    public DatabaseTransaction BeginTransaction(global::System.Threading.CancellationToken cancellationToken=default) {
        this.EnsureAvailable();
        var state=this.ambientTransaction.Value;
        if(state is null) {
            if(this.ambientReadSession.Value is not null) {
                throw new InvalidOperationException("A read session cannot be promoted to a write transaction. Dispose the read session first.");
            }
            var lease=this.concurrencyGate.EnterWrite(cancellationToken);
            state=new(this.Options.TransactionMemoryLimit,lease);
            this.ambientTransaction.Value=state;
        }
        state.Depth++;
        return new(this,state);
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
                IEnumerable<DatabaseWalOperation> Operations() {
                    foreach(var overlay in state.Overlays.Values) {
                        foreach(var operation in overlay.CreateWalOperations()){yield return operation;}
                    }
                }
                this.GetWriteAheadLog()?.Append(Operations());
                foreach(var overlay in state.Overlays.Values) {
                    overlay.Apply();
                }
            }
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
        var state=this.ambientReadSession.Value;
        if(state is null) {
            IDisposable? lease=null;
            if(this.ambientTransaction.Value is null&&this.ambientWriteDepth.Value==0){lease=this.concurrencyGate.EnterRead(cancellationToken);}
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
        Table<TKey,TRecord> table,
        bool create)
        where TKey:struct
        where TRecord:struct {
        var state=this.ambientTransaction.Value;
        if(state is null) {
            return null;
        }
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
        Table<TRecord> table,
        bool create)
        where TRecord:notnull {
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
        if(this.ambientTransaction.Value is not null||this.ambientWriteDepth.Value>0) {
            return action();
        }
        this.EnsureAvailable();
        if(this.ambientReadSession.Value is not null) {
            throw new InvalidOperationException("A read session cannot be promoted to a write operation. Dispose the read session first.");
        }
        using var lease=this.concurrencyGate.EnterWrite(global::System.Threading.CancellationToken.None);
        this.ambientWriteDepth.Value++;
        try{return action();}
        finally{this.ambientWriteDepth.Value--;}
    }

    internal IEnumerator<T> Stream<T>(Func<IEnumerator<T>> getEnumerator) {
        var session=this.BeginReadSession();
        try{return new DatabaseSessionEnumerator<T>(getEnumerator(),session);}
        catch{session.Dispose();throw;}
    }

    private void EnsureAvailable() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(this.faulted){throw new DatabaseFaultedException("The database encountered an error while committing. Dispose and reopen it so committed WAL operations can be recovered.");}
    }

    /// <summary>generator が宣言メンバーから生成するオブジェクト一覧。生成前は空。</summary>
    public virtual global::Seamless.Database.System.SystemTable<global::Seamless.Database.System.Keys.DatabaseObject,global::Seamless.Database.System.Records.DatabaseObject> DatabaseObjects=>EmptyDatabaseObjects;

    /// <summary>source generatorが読み取り専用システムテーブルを構築する。</summary>
    protected static global::Seamless.Database.System.SystemTable<TKey,TRecord> CreateSystemTable<TKey,TRecord>(
        IEnumerable<TRecord> rows,Func<TRecord,TKey> keySelector)
        where TKey:struct
        where TRecord:struct=>new(rows,keySelector);

    /// <summary>宣言プロパティから呼ばれ、テーブル実体を宣言名単位で生成・共有する。</summary>
    protected Table<TRecord> Table<TRecord>([CallerMemberName] string name = "")
        where TRecord:notnull {
        return this.GetTable<TRecord>(name);
    }

    /// <summary>宣言プロパティから呼ばれ、候補キーで探索できるテーブルを生成・共有する。</summary>
    protected Table<TKey,TRecord> Table<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        [CallerMemberName] string name="")
        where TKey:struct
        where TRecord:struct=>this.GetTable(keySelector,name);

    internal Table<TKey,TRecord> GetTable<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        string name)
        where TKey:struct
        where TRecord:struct {
        ArgumentNullException.ThrowIfNull(keySelector);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.tables.TryGetValue(name,out var existing)) {
                return (Table<TKey,TRecord>)existing;
            }
            var table=this.CreateTable(keySelector,name);
            try {
                this.RecoverTable(table);
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
                this.RecoverMaterializedView(view);
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

    internal Table<TRecord> GetTable<TRecord>(string name)
        where TRecord:notnull {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.tables.TryGetValue(name,out var existing)) {
                return (Table<TRecord>)existing;
            }
            var table = this.CreateTable<TRecord>(name);
            try {
                this.RecoverTable(table);
                this.tables.Add(name,table);
                return table;
            } catch {
                table.Dispose();
                throw;
            }
        }
    }

    /// <summary>宣言プロパティから呼ばれ、ビューを宣言名単位で生成・共有する。</summary>
    protected View<TResult> View<TResult>(Func<IEnumerable<TResult>> definition,[CallerMemberName] string name = "") {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.views.TryGetValue(name,out var existing)) {
                return (View<TResult>)existing;
            }
            var view = new View<TResult>(definition,name);
            this.views.Add(name,view);
            return view;
        }
    }

    /// <summary>宣言メソッドから呼ばれ、引数をキャプチャしたテーブル関数を生成する。</summary>
    protected TableFunction<TResult> TableFunction<TResult>(Func<IEnumerable<TResult>> definition,[CallerMemberName] string name = "") {
        return this.CreateTableFunction(definition,name);
    }

    internal TableFunction<TResult> CreateTableFunction<TResult>(
        Func<IEnumerable<TResult>> definition,
        string name) {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        return new(definition,name);
    }

    /// <summary>スカラー関数を直ちに評価し、実行済み結果を返す。</summary>
    protected ScalarFunction<TResult> ScalarFunction<TResult>(
        Func<TResult> definition,
        [CallerMemberName] string name="")=>this.CreateScalarFunction(definition,name);

    internal ScalarFunction<TResult> CreateScalarFunction<TResult>(
        Func<TResult> definition,
        string name) {
        ArgumentNullException.ThrowIfNull(definition);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        return new(definition(),name);
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

    private Table<TRecord> CreateTable<TRecord>(string name)
        where TRecord:notnull {
        var sizing=this.ResolveTableStorage(name);
        var reservation=this.ReserveTableStoreMemory(sizing);
        var resources=new StackedDisposable(reservation);
        try {
        if(this.Options.StorageMode==DatabaseStorageMode.Memory) {
            var memoryStoreFactory = global::Seamless.Collections.Tsavorite.Store.Create(
                IndexSize:sizing.IndexSize,
                PageSize:sizing.PageSize,
                SegmentSize:sizing.SegmentSize,
                MemorySize:sizing.MemorySize);
            var memoryStore = memoryStoreFactory.Create<TRecord>();
            resources.Push(memoryStore);
            var rows=new global::Seamless.Collections.Tsavorite.HashSet<TRecord>(memoryStore,resources);
            return new(this,rows,name);
        }
        var directory = Path.Combine(this.ResolveStorageRoot(),SanitizePathSegment(name));
        Directory.CreateDirectory(directory);
        var checkpointToken = ReadCheckpointToken(directory);
        var store = global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize:sizing.IndexSize,
            PageSize:sizing.PageSize,
            SegmentSize:sizing.SegmentSize,
            MemorySize:sizing.MemorySize,
            CheckpointDir: Path.Combine(directory,"checkpoint"));
        if(checkpointToken is Guid token) {
            store=store.WithRecoveryIndexSize(token,sizing.IndexSize);
        }
        var logDevice = Devices.CreateLogDevice(Path.Combine(directory,"log"));
        resources.Push(logDevice);
        var objectDevice = Devices.CreateLogDevice(Path.Combine(directory,"obj"));
        resources.Push(objectDevice);
        var storageStore = store.Create<TRecord>(logDevice,objectDevice);
        resources.Push(storageStore);
        if(checkpointToken is Guid checkpoint) {
            storageStore.Load(checkpoint);
        }
        var checkpointedStore=new CheckpointedStore<HashSetStore<TRecord>>(
            storageStore,
            static store=>store.Save(),
            static store=>store.Address.TailAddress,
            directory);
        resources.Replace(storageStore,checkpointedStore);
        var storageRows=new global::Seamless.Collections.Tsavorite.HashSet<TRecord>(
            storageStore,
            resources);
        return new(this,storageRows,name);
        } catch(Exception creationException) {
            try{resources.Dispose();}
            catch(Exception cleanupException){throw new AggregateException("Table creation and cleanup both failed.",creationException,cleanupException);}
            throw;
        }
    }

    private Table<TKey,TRecord> CreateTable<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        string name)
        where TKey:struct
        where TRecord:struct {
        var sizing=this.ResolveTableStorage(name);
        var reservation=this.ReserveTableStoreMemory(sizing);
        var resources=new StackedDisposable(reservation);
        try {
        if(this.Options.StorageMode==DatabaseStorageMode.Memory) {
            var memoryStoreFactory=global::Seamless.Collections.Tsavorite.Store.Create(
                IndexSize:sizing.IndexSize,
                PageSize:sizing.PageSize,
                SegmentSize:sizing.SegmentSize,
                MemorySize:sizing.MemorySize);
            var dictionaryStore=memoryStoreFactory.Create<TKey,TRecord>();
            resources.Push(dictionaryStore);
            var dictionary=(global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>)dictionaryStore.Create();
            resources.Push((IDisposable)dictionary);
            return new(
                this,
                dictionary,
                keySelector,
                resources,
                name);
        }
        var directory=Path.Combine(this.ResolveStorageRoot(),SanitizePathSegment(name));
        Directory.CreateDirectory(directory);
        var checkpointToken=ReadCheckpointToken(directory);
        var store=global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize:sizing.IndexSize,
            PageSize:sizing.PageSize,
            SegmentSize:sizing.SegmentSize,
            MemorySize:sizing.MemorySize,
            CheckpointDir:Path.Combine(directory,"checkpoint"));
        if(checkpointToken is Guid token) {
            store=store.WithRecoveryIndexSize(token,sizing.IndexSize);
        }
        var logDevice=Devices.CreateLogDevice(Path.Combine(directory,"log"));
        resources.Push(logDevice);
        var objectDevice=Devices.CreateLogDevice(Path.Combine(directory,"obj"));
        resources.Push(objectDevice);
        var storageStore=store.Create<TKey,TRecord>(logDevice,objectDevice);
        resources.Push(storageStore);
        if(checkpointToken is Guid checkpoint) {
            storageStore.Load(checkpoint);
        }
        var storageDictionary=(global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>)storageStore.Create();
        resources.Push((IDisposable)storageDictionary);
        var checkpointedStore=new CheckpointedStore<DictionaryStore<TKey,TRecord>>(
            storageStore,
            static value=>value.Save(),
            static value=>value.Address.TailAddress,
            directory);
        resources.Replace(storageStore,checkpointedStore);
        return new(
            this,
            storageDictionary,
            keySelector,
            resources,
            name);
        } catch(Exception creationException) {
            try{resources.Dispose();}
            catch(Exception cleanupException){throw new AggregateException("Table creation and cleanup both failed.",creationException,cleanupException);}
            throw;
        }
    }

    private MaterializedView<TKey,TRecord> CreateMaterializedView<TKey,TRecord>(
        Func<TRecord,TKey> keySelector,
        string name)
        where TKey:struct
        where TRecord:struct {
        var sizing=this.ResolveTableStorage(name);
        var reservation=this.ReserveTableStoreMemory(sizing);
        var resources=new StackedDisposable(reservation);
        try {
        if(this.Options.StorageMode==DatabaseStorageMode.Memory) {
            var memoryStoreFactory=global::Seamless.Collections.Tsavorite.Store.Create(
                IndexSize:sizing.IndexSize,
                PageSize:sizing.PageSize,
                SegmentSize:sizing.SegmentSize,
                MemorySize:sizing.MemorySize);
            var dictionaryStore=memoryStoreFactory.Create<TKey,TRecord>();
            resources.Push(dictionaryStore);
            var dictionary=(global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>)dictionaryStore.Create();
            resources.Push((IDisposable)dictionary);
            return new(
                this,
                dictionary,
                keySelector,
                resources,
                name);
        }
        var directory=Path.Combine(this.ResolveStorageRoot(),SanitizePathSegment(name));
        Directory.CreateDirectory(directory);
        var checkpointToken=ReadCheckpointToken(directory);
        var store=global::Seamless.Collections.Tsavorite.Store.Create(
            IndexSize:sizing.IndexSize,
            PageSize:sizing.PageSize,
            SegmentSize:sizing.SegmentSize,
            MemorySize:sizing.MemorySize,
            CheckpointDir: Path.Combine(directory,"checkpoint"));
        if(checkpointToken is Guid token) {
            store=store.WithRecoveryIndexSize(token,sizing.IndexSize);
        }
        var logDevice=Devices.CreateLogDevice(Path.Combine(directory,"log"));
        resources.Push(logDevice);
        var objectDevice=Devices.CreateLogDevice(Path.Combine(directory,"obj"));
        resources.Push(objectDevice);
        var storageStore=store.Create<TKey,TRecord>(logDevice,objectDevice);
        resources.Push(storageStore);
        if(checkpointToken is Guid checkpoint) {
            storageStore.Load(checkpoint);
        }
        var storageDictionary=(global::Seamless.Collections.Tsavorite.IDictionary<TKey,TRecord>)storageStore.Create();
        resources.Push((IDisposable)storageDictionary);
        var checkpointedStore=new CheckpointedStore<DictionaryStore<TKey,TRecord>>(
            storageStore,
            static store=>store.Save(),
            static store=>store.Address.TailAddress,
            directory);
        resources.Replace(storageStore,checkpointedStore);
        return new(
            this,
            storageDictionary,
            keySelector,
            resources,
            name);
        } catch(Exception creationException) {
            try{resources.Dispose();}
            catch(Exception cleanupException){throw new AggregateException("Materialized view creation and cleanup both failed.",creationException,cleanupException);}
            throw;
        }
    }

    private void RecoverMaterializedView<TKey,TRecord>(MaterializedView<TKey,TRecord> view)
        where TKey:struct
        where TRecord:struct {
        var log=this.GetWriteAheadLog();
        if(log is null) {
            return;
        }
        foreach(var operation in log.Read(view.Name)) {
            if(operation.Deleted||operation.RecordData is null) {
                continue;
            }
            var key=this.DecodeWal<TKey>(operation,operation.KeyData);
            var record=this.DecodeWal<TRecord>(operation,operation.RecordData);
            view.DirectSet(key,record);
        }
    }

    private string ResolveStorageRoot() =>
        this.Options.StorageDirectory??Path.Combine(Environment.CurrentDirectory,this.DatabaseName);

    private TableStorageSizing ResolveTableStorage(string name) {
        if(this.Options.TableStorage.TryGetValue(name,out var sizing)) {
            return new(sizing.IndexSize,sizing.PageSize,sizing.SegmentSize,sizing.MemorySize);
        }
        return new(this.Options.IndexSize,this.Options.PageSize,this.Options.SegmentSize,this.Options.MemorySize);
    }

    private global::Seamless.Execution.ResourceReservation ReserveTableStoreMemory(TableStorageSizing sizing) {
        var indexBytes=checked(sizing.IndexSize*64L);
        var objectLogBytes=this.Options.StorageMode==DatabaseStorageMode.Memory
            ?global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity
            :0L;
        var bytes=checked(sizing.MemorySize+indexBytes+objectLogBytes);
        return this.executionResources.Budget.ReserveMemory(bytes);
    }

    private static DatabaseContainerOptions ValidateOptions(DatabaseContainerOptions value) {
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

    private DatabaseWriteAheadLog? GetWriteAheadLog() {
        if(this.Options.StorageMode==DatabaseStorageMode.Memory) {
            return null;
        }
        return this.writeAheadLog??=new(Path.Combine(this.ResolveStorageRoot(),"database.wal"));
    }

    private void RecoverTable<TRecord>(Table<TRecord> table)
        where TRecord:notnull {
        var log=this.GetWriteAheadLog();
        if(log is null){return;}
        foreach(var operation in log.Read(table.Name)) {
            var item=this.DecodeWal<TRecord>(operation,operation.KeyData);
            if(item is null){throw new InvalidDataException($"WAL operation for '{table.Name}' has a null record.");}
            if(operation.Deleted){_ = table.DirectRemove(item);}
            else{_ = table.DirectAdd(item);}
        }
    }

    private void RecoverTable<TKey,TRecord>(Table<TKey,TRecord> table)
        where TKey:struct
        where TRecord:struct {
        var log=this.GetWriteAheadLog();
        if(log is null) {
            return;
        }
        foreach(var operation in log.Read(table.Name)) {
            var key=this.DecodeWal<TKey>(operation,operation.KeyData);
            if(operation.Deleted) {
                _=table.DirectRemove(key);
                continue;
            }
            if(operation.RecordData is null) {
                throw new InvalidDataException($"WAL operation for '{table.Name}' has no record.");
            }
            var record=this.DecodeWal<TRecord>(operation,operation.RecordData);
            table.DirectSet(key,record);
        }
    }

    private T DecodeWal<T>(DatabaseWalOperation operation,string data) {
        if(operation.EncodingVersion==1) {
            var maximum=checked((data.Length/4+1)*3);
            using var reservation=this.executionResources.Budget.ReserveMemory(maximum);
            return global::MemoryPack.MemoryPackSerializer.Deserialize<T>(Convert.FromBase64String(data))!;
        }
        var length=checked((data.Length/4+1)*3);
        using var buffer=this.executionResources.Rent(length);
        if(!Convert.TryFromBase64String(data,buffer.Memory.Span,out var written)){
            throw new InvalidDataException("WAL contains invalid base64 data.");
        }
        return this.executionResources.Codecs.Get<T>().Decode(buffer.Memory.Span[..written])!;
    }


    private static string SanitizePathSegment(string value) {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach(var character in value) {
            if(Array.IndexOf(invalid,character)>=0) {
                builder.Append('_');
            } else {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }

    private const string CheckpointTokenFileName="checkpoint.token";

    private static Guid? ReadCheckpointToken(string directory) {
        var path=Path.Combine(directory,CheckpointTokenFileName);
        if(!File.Exists(path)) {
            return null;
        }
        var contents=File.ReadAllText(path);
        if(!Guid.TryParseExact(contents,"D",out var token)) {
            throw new InvalidDataException($"Invalid checkpoint token: {path}");
        }
        return token;
    }

    public void Dispose() {
        if(this.disposed){return;}
        if(this.ambientTransaction.Value is not null||this.ambientReadSession.Value is not null) {
            throw new InvalidOperationException("Dispose active database transactions and read sessions before disposing the database.");
        }
        using var writeLease=this.concurrencyGate.EnterWrite(global::System.Threading.CancellationToken.None);
        List<Exception>? exceptions=null;
        var checkpointedTables=new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        lock(this.gate) {
            if(this.disposed) {
                return;
            }
            this.disposed=true;
            checkpointedTables.UnionWith(this.tables.Keys);
            foreach(var table in this.tables.Values) {
                try {
                    table.Dispose();
                } catch(Exception exception) {
                    exceptions??=[];
                    exceptions.Add(exception);
                }
            }
            this.tables.Clear();
            this.views.Clear();
        }
        GC.SuppressFinalize(this);
        if(exceptions is null&&!this.faulted&&this.writeAheadLog is not null) {
            try {
                if(this.writeAheadLog.CanArchiveAfterCheckpoint(checkpointedTables)){
                    _=this.writeAheadLog.ArchiveAfterCheckpoint();
                }
            } catch(Exception exception) {
                exceptions??=[];
                exceptions.Add(exception);
            }
        }
        try{this.executionResources.Dispose();}
        catch(Exception exception){
            exceptions??=[];
            exceptions.Add(exception);
        }
        ThrowCollectedExceptions(exceptions);
    }

    private sealed class StackedDisposable(params IDisposable[] resources):IDisposable {
        private List<IDisposable>? resources=[..resources];

        public void Push(IDisposable resource) {
            ArgumentNullException.ThrowIfNull(resource);
            var current=this.resources??throw new ObjectDisposedException(nameof(StackedDisposable));
            current.Insert(0,resource);
        }

        public void Replace(IDisposable resource,IDisposable replacement) {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(replacement);
            var current=this.resources??throw new ObjectDisposedException(nameof(StackedDisposable));
            for(var i=0;i<current.Count;i++) {
                if(!ReferenceEquals(current[i],resource)){continue;}
                current[i]=replacement;
                return;
            }
            throw new InvalidOperationException("The resource to replace is not owned by this stack.");
        }

        public void Dispose() {
            var current=global::System.Threading.Interlocked.Exchange(ref this.resources,null);
            if(current is null){return;}
            List<Exception>? exceptions=null;
            foreach(var resource in current) {
                try {
                    resource.Dispose();
                } catch(Exception exception) {
                    exceptions??=[];
                    exceptions.Add(exception);
                }
            }
            ThrowCollectedExceptions(exceptions);
        }
    }

    private sealed class CheckpointedStore<TStore>(
        TStore store,
        Func<TStore,Guid> save,
        Func<TStore,long> getTailAddress,
        string directory):IDisposable
        where TStore:IDisposable {
        private readonly TStore store=store;
        private readonly Func<TStore,Guid> save=save;
        private readonly Func<TStore,long> getTailAddress=getTailAddress;
        private readonly string directory=directory;
        private readonly long initialTailAddress=getTailAddress(store);

        public void Dispose() {
            try {
                if(this.getTailAddress(this.store)!=this.initialTailAddress) {
                    var token=this.save(this.store);
                    var tokenPath=Path.Combine(this.directory,CheckpointTokenFileName);
                    var newTokenPath=tokenPath+".new";
                    File.WriteAllText(newTokenPath,token.ToString("D"));
                    File.Move(newTokenPath,tokenPath,overwrite:true);
                }
            } finally {
                this.store.Dispose();
            }
        }
    }

    private static void ThrowCollectedExceptions(List<Exception>? exceptions) {
        if(exceptions is null) {
            return;
        }
        if(exceptions.Count==1) {
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }
        throw new AggregateException(exceptions);
    }
}
