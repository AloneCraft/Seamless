using Seamless.Collections.Tsavorite;


namespace Seamless.Collections.Tsavorite;

public class HashSetStore<TKey>:CollectionStore<TKey,EqualityEmpty> {
    internal HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes,long? ObjectLogSegmentSize = null) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage,StorageLimitFraction,StorageReserveBytes,ObjectLogSegmentSize) { }

    internal HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) { }

    internal HashSetStore(TsavoriteLayout layout) : base(layout) { }

    /// <summary>件数を管理する操作用オブジェクト。スレッドごとに作る。追加・削除のたびにデータ本体の件数を排他的に増減する。</summary>
    public HashSet<TKey> Create() => new(this);
    /// <summary>
    /// 件数を保持しない操作用オブジェクト。1 つの処理の中で並列化するときに使う。
    /// 増減は呼び出し側で集計し、最後に <see cref="CollectionStore{TKey,TValue,TStoreFunctions,TAllocator}.ApplyCountDelta"/> で反映する。
    /// </summary>
    public HashSet<TKey> CreateUncounted() {
        var set = new HashSet<TKey>(this);
        ((IUncountableAccessor)set).DisableCounting();
        return set;
    }
    public Set<TKey> CreateSet() => new(this);
    /// <summary>
    /// 並列区間を開く。区間の <see cref="ParallelRegion{TAccessor}.Create"/> で、スレッドごとにマルチ用の操作用オブジェクトを作る。
    /// <paramref name="expectedCount"/> は区間を閉じたときの全体の見込み件数で、索引をその件数に合う大きさまで先に広げる。
    /// </summary>
    public ParallelRegion<ParallelHashSet<TKey>> BeginParallel(long expectedCount = 0) =>
        this.BeginParallelCore(expectedCount,region => new ParallelHashSet<TKey>(this,region));
    internal HashSet<TKey> CreateOwned() => new(this,this);
    internal Set<TKey> CreateOwnedSet() => new(this,this);
}
