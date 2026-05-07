
using Seamless.Collections;
using Seamless.Devices;
namespace Seamless.Stores;

public class UnmanagedDictionaryStore<TKey, TValue>:UnmanagedCollectionStore<TKey,TValue>
    where TKey : unmanaged
    where TValue : unmanaged {
    internal UnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    internal UnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage,StorageLimitFraction,StorageReserveBytes) {
    }

    private protected override long GetRecoveryEntryCount() {
        using var dictionary = this.Create();
        return dictionary.GetLongCount();
    }

    public Seamless.Collections.Dictionary<TKey,TValue,UnmanagedSerializer<TKey>,UnmanagedSerializer<TValue>> Create() => new(this);
}

//public class UnmanagedUnmanagedDictionaryStore<TKey, TValue>:UnmanagedDictionaryStore<TKey,TValue>
//    where TKey : unmanaged
//    where TValue : unmanaged {
//    internal UnmanagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    internal UnmanagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    public Seamless.Collections.Dictionary<TKey,TValue,UnmanagedSerializer<TKey>,UnmanagedSerializer<TValue>> Create() => new(this);
//}
