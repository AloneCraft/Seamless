using Seamless.Collections.Tsavorite;

namespace Seamless.Collections.Tsavorite;

// KEYED-RECORD-CODEC-TODO: TRecord currently persists its TKey as well as the Tsavorite key. Revisit a generated keyed-record codec that constructs TRecord from TKey and deserializes only the remaining fields.
public class SetStore<TKey, TRecord>:CollectionStore<TKey,TRecord>
    where TRecord : IRecord<TKey> {
    internal SetStore(long indexSize,long pageSize,long segmentSize,long memorySize,double mutableFraction) :
        base(indexSize,pageSize,segmentSize,memorySize,mutableFraction,new MemoryOnlyDevice(segmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    internal SetStore(long indexSize,long pageSize,long segmentSize,long memorySize,double mutableFraction,IDevice logDevice,IDevice objectLogDevice,string? checkpointDir,double storageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long storageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes,long? objectLogSegmentSize = null) :
        base(indexSize,pageSize,segmentSize,memorySize,mutableFraction,logDevice,objectLogDevice,checkpointDir,OverflowMutableStrategyKind.Storage,storageLimitFraction,storageReserveBytes,objectLogSegmentSize) {
    }

    internal SetStore(TsavoriteLayout layout) : base(layout) {
    }

    /// <summary>
    /// シングル用の操作用オブジェクト。データ本体に同時に 1 つだけ作れる(2 つ目は InvalidOperationException)。
    /// 件数をデータ本体で管理し O(1)。複数スレッドから書くときは <see cref="BeginParallel"/> を使う。
    /// </summary>
    public Set<TKey,TRecord> Create() => new(this);
    /// <summary>
    /// 並列区間を開く。区間の <see cref="ParallelRegion{TAccessor}.Create"/> で、スレッドごとにマルチ用の操作用オブジェクトを作る。
    /// <paramref name="expectedCount"/> は区間を閉じたときの全体の見込み件数で、索引をその件数に合う大きさまで先に広げる。
    /// </summary>
    public ParallelRegion<ParallelSet<TKey,TRecord>> BeginParallel(long expectedCount = 0) =>
        this.BeginParallelCore(expectedCount,region => new ParallelSet<TKey,TRecord>(this,region));
}
