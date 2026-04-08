
namespace Seamless.Collections;

public abstract class UnmanagedDictionaryStore<TKey,TValue>:UnmanagedCollectionStore<TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    private protected UnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir) {
    }

    public abstract UnmanagedDictionary<TKey,TValue> Create();
}
