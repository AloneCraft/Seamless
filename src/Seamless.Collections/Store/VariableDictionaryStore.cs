using Seamless.Devices;

using Tsavorite.core;

namespace Seamless.Collections;

public abstract class ManagedCollectionStore<TKey,TValue>:CollectionStore<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    private protected ManagedCollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
}

public abstract class ManagedDictionaryStore<TKey,TValue>:ManagedCollectionStore<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
{
    private protected ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}

    public abstract ManagedDictionary<TKey,TValue> Create();
}
