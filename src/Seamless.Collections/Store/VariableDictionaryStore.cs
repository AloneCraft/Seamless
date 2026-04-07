
using Seamless.Devices;

using Tsavorite.core;

namespace Seamless.Collections;
public abstract class ManagedDictionaryStore<TKey,TValue>:CollectionStore<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
{
    private protected ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                         (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}
    //private otected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction): 

    public abstract ManagedDictionary<TKey,TValue> Create();
}
