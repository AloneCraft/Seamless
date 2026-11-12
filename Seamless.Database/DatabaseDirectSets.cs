namespace Seamless.Database;

using global::Seamless.Collections.Tsavorite;

// 直接使う表（Set<T>・Set<TKey,T>）。Tsavorite の集合そのものを返し、取引・WAL・版の管理・DB全体のゲートを通さない。
// 書き込みは取り消せず、保存はDBを閉じたときのチェックポイントだけで行う。異常終了後の復旧は保証しない。
// 宣言はトランザクションで使う表（Table<T>）と分け、同じ表を両方の書き方で使えないようにする（生成器が入口を分ける）。
// 設計：docs/refactoring-designs/20261112-0017-database-access-model.md の I1・I6。
public abstract partial class DatabaseContainer {
    private const string DirectSetDirectoryName="direct";
    private readonly global::System.Collections.Generic.Dictionary<string,IDisposable> directSets=new(StringComparer.Ordinal);

    /// <summary>
    /// 宣言プロパティから呼ばれ、キーのない直接使う表を宣言名単位で生成・共有する。
    /// 返す集合はシングル用の操作用オブジェクトで、同時に使えるのは1スレッドだけ。
    /// </summary>
    protected global::Seamless.Collections.Tsavorite.Set<TRecord> Set<TRecord>([global::System.Runtime.CompilerServices.CallerMemberName] string name="")
        where TRecord:notnull=>
        this.GetDirectSet(name,static store=>store.Create<TRecord>(),static(store,log,obj)=>store.Create<TRecord>(log,obj),static owned=>owned.CreateSet(),
            static(store,token)=>store.Load(token),static store=>store.Save(),static store=>store.Address.TailAddress);

    /// <summary>宣言プロパティから呼ばれ、キーで探索できる直接使う表を宣言名単位で生成・共有する。</summary>
    protected global::Seamless.Collections.Tsavorite.Set<TKey,TRecord> Set<TKey,TRecord>([global::System.Runtime.CompilerServices.CallerMemberName] string name="")
        where TRecord:global::Seamless.Collections.IRecord<TKey> =>
        this.GetDirectSet(name,static store=>store.CreateSet<TKey,TRecord>(),static(store,log,obj)=>store.CreateSet<TKey,TRecord>(log,obj),static owned=>owned.Create(),
            static(store,token)=>store.Load(token),static store=>store.Save(),static store=>store.Address.TailAddress);

    private TSet GetDirectSet<TStore,TSet>(
        string name,
        Func<global::Seamless.Collections.Tsavorite.Store,TStore> memory,
        Func<global::Seamless.Collections.Tsavorite.Store,IDevice,IDevice,TStore> storage,
        Func<TStore,TSet> open,
        Action<TStore,Guid> load,
        Func<TStore,Guid> save,
        Func<TStore,long> tail)
        where TStore:IDisposable
        where TSet:class {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ObjectDisposedException.ThrowIf(this.disposed,this);
        lock(this.gate) {
            if(this.directSets.TryGetValue(name,out var existing)) {
                return ((DirectSet<TSet>)existing).Rows;
            }
            var created=this.CreateDirectSet(name,memory,storage,open,load,save,tail);
            this.directSets.Add(name,created);
            return created.Rows;
        }
    }

    private DirectSet<TSet> CreateDirectSet<TStore,TSet>(
        string name,
        Func<global::Seamless.Collections.Tsavorite.Store,TStore> memory,
        Func<global::Seamless.Collections.Tsavorite.Store,IDevice,IDevice,TStore> storage,
        Func<TStore,TSet> open,
        Action<TStore,Guid> load,
        Func<TStore,Guid> save,
        Func<TStore,long> tail)
        where TStore:IDisposable
        where TSet:class {
        var sizing=this.ResolveTableStorage(name);
        var resources=new StackedDisposable(this.ReserveTableStoreMemory(sizing));
        try {
            if(this.Options.StorageMode==DatabaseStorageMode.Memory) {
                var memoryStore=memory(global::Seamless.Collections.Tsavorite.Store.Create(
                    IndexSize:sizing.IndexSize,PageSize:sizing.PageSize,SegmentSize:sizing.SegmentSize,MemorySize:sizing.MemorySize));
                resources.Push(memoryStore);
                return new(open(memoryStore),resources);
            }
            // 取引の保存領域と混ざらないよう、直接使う表は専用の下位ディレクトリに置く。
            var directory=Path.Combine(this.ResolveStorageRoot(),DirectSetDirectoryName,SanitizePathSegment(name));
            Directory.CreateDirectory(directory);
            var checkpointToken=ReadCheckpointToken(directory);
            var factory=global::Seamless.Collections.Tsavorite.Store.Create(
                IndexSize:sizing.IndexSize,PageSize:sizing.PageSize,SegmentSize:sizing.SegmentSize,MemorySize:sizing.MemorySize,
                CheckpointDir:Path.Combine(directory,"checkpoint"));
            if(checkpointToken is Guid token) {
                factory=factory.WithRecoveryIndexSize(token,sizing.IndexSize);
            }
            var logDevice=Devices.CreateLogDevice(Path.Combine(directory,"log"));
            resources.Push(logDevice);
            var objectDevice=Devices.CreateLogDevice(Path.Combine(directory,"obj"));
            resources.Push(objectDevice);
            var store=storage(factory,logDevice,objectDevice);
            resources.Push(store);
            if(checkpointToken is Guid checkpoint) {
                load(store,checkpoint);
            }
            // 閉じるときに変更があればチェックポイントを書き、トークンを差し替える。
            resources.Replace(store,new CheckpointedStore<TStore>(store,save,tail,directory));
            return new(open(store),resources);
        } catch(Exception creationException) {
            try{resources.Dispose();}
            catch(Exception cleanupException){throw new AggregateException("Direct set creation and cleanup both failed.",creationException,cleanupException);}
            throw;
        }
    }

    // 直接使う表のチェックポイントは旧方式のDBの印(checkpoint.token)と同じ名前なので、旧方式の検出から外す。
    private static bool IsDirectSetPath(string root,string path)=>
        Path.GetRelativePath(Path.Combine(root,DirectSetDirectoryName),path) is var relative&&!relative.StartsWith("..",StringComparison.Ordinal)&&!Path.IsPathRooted(relative);

    /// <summary>DBを閉じるときに直接使う表を閉じる（変更があればチェックポイントを書く）。gate を持った状態で呼ぶ。</summary>
    private void DisposeDirectSets(List<Exception> errors) {
        foreach(var set in this.directSets.Values) {
            try{set.Dispose();}catch(Exception error){errors.Add(error);}
        }
        this.directSets.Clear();
    }

    private sealed class DirectSet<TSet>(TSet rows,IDisposable resources):IDisposable where TSet:class {
        public TSet Rows{get;}=rows;
        public void Dispose() {
            try{(this.Rows as IDisposable)?.Dispose();}
            finally{resources.Dispose();}
        }
    }
}
