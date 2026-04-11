
using Seamless.Collections;
namespace Seamless.Store;

public sealed class StorageUnmanagedDictionaryStore<TKey,TValue>:UnmanagedDictionaryStore<TKey,TValue>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    internal StorageUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir):
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir) {
    }

    public override UnmanagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,UnmanagedSerializer<TValue>> Create() => new StorageUnmanagedDictionary<TKey,TValue>(this);
}
