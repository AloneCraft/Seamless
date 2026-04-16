
using Seamless.Collections;
using Seamless.Devices;
namespace Seamless.Stores;

public class UnmanagedDictionaryStore<TKey, TValue>:UnmanagedCollectionStore<TKey,TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    internal UnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    internal UnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage) {
    }


}

public class UnmanagedUnmanagedDictionaryStore<TKey, TValue>:UnmanagedDictionaryStore<TKey,TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    internal UnmanagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction) { }

    internal UnmanagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir) { }

    public UnmanagedUnmanagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,UnmanagedSerializer<TValue>> Create() => new(this);
}
