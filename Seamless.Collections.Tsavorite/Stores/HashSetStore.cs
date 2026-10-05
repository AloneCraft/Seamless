using Seamless.Collections.Tsavorite;


namespace Seamless.Collections.Tsavorite;

public class HashSetStore<TKey>:CollectionStore<TKey,EqualityEmpty> {
    internal HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes,long? ObjectLogSegmentSize = null) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage,StorageLimitFraction,StorageReserveBytes,ObjectLogSegmentSize) { }

    internal HashSetStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) { }

    internal HashSetStore(TsavoriteLayout layout) : base(layout) { }

    /// <summary>
    /// シングル用の操作用オブジェクト。データ本体に同時に 1 つだけ作れる(2 つ目は InvalidOperationException)。
    /// 件数をデータ本体で管理し O(1)。索引は混んだら自動で広げる。スレッドには縛られないが、同時に使えるのは 1 スレッドだけ。
    /// 複数スレッドから書くときは <see cref="BeginParallel"/> を使う。
    /// </summary>
    public HashSet<TKey> Create() => new(this);
    /// <summary>シングル用の操作用オブジェクト(<see cref="Set{TRecord}"/>)。<see cref="Create"/> と合わせて、同時に 1 つだけ作れる。</summary>
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
