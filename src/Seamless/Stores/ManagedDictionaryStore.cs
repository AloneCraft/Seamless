
using Seamless.Collections;
using Seamless.Devices;
namespace Seamless.Stores;

public class ManagedDictionaryStore<TKey, TValue>:ManagedCollectionStore<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>{
    internal ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory){ }

    internal ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage){ }

    private protected override long GetRecoveryEntryCount() {
        using var dictionary = this.Create();
        return dictionary.GetLongCount();
    }

}

//public class ManagedManagedDictionaryStore<TKey, TValue>
//    :ManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>
//    where TKey : IEquatable<TKey>
//    where TValue : IEquatable<TValue> {
//    internal ManagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    internal ManagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    public ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> Create() => new(this);
//}

//public class UnmanagedManagedDictionaryStore<TKey, TValue>
//    :ManagedDictionaryStore<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>
//    where TKey : unmanaged, IEquatable<TKey>
//    where TValue : IEquatable<TValue> {
//    internal UnmanagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    internal UnmanagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    public UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> Create() => new(this);
//}

//public class ManagedUnmanagedDictionaryStore<TKey, TValue>
//    :ManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>
//    where TKey : IEquatable<TKey>
//    where TValue : unmanaged, IEquatable<TValue> {
//    internal ManagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    internal ManagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    public ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> Create() => new(this);
//}

