
using Seamless.Collections;
namespace Seamless.Store;

public abstract class ManagedDictionaryStore<TKey,TValue,TKeySerializer,TValueSerializer>:ManagedCollectionStore<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>
{
    private protected ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base                        (     IndexSize,     PageSize,     SegmentSize,     MemorySize,       MutableFraction,        LogDevice,        ObjectLogDevice,        CheckpointDir){}

    public abstract ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> Create();
}
