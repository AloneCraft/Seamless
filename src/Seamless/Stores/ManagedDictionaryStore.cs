using System.Reflection;
using Seamless.Collections;
using Seamless.Devices;
namespace Seamless.Stores;

public class ManagedDictionaryStore<TKey, TValue>:ManagedCollectionStore<TKey,TValue> {
    private readonly bool keyUsesManagedSerializer;
    private readonly bool valueUsesManagedSerializer;

    internal ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,keyUsesManagedSerializer: true,valueUsesManagedSerializer: true) { }

    internal ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,bool keyUsesManagedSerializer,bool valueUsesManagedSerializer) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
        this.keyUsesManagedSerializer=keyUsesManagedSerializer;
        this.valueUsesManagedSerializer=valueUsesManagedSerializer;
    }

    internal ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes,keyUsesManagedSerializer: true,valueUsesManagedSerializer: true) { }

    internal ManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction,long StorageReserveBytes,bool keyUsesManagedSerializer,bool valueUsesManagedSerializer) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage,StorageLimitFraction,StorageReserveBytes) {
        this.keyUsesManagedSerializer=keyUsesManagedSerializer;
        this.valueUsesManagedSerializer=valueUsesManagedSerializer;
    }

    public Collections.IDictionary<TKey,TValue> Create() {
        if(this.keyUsesManagedSerializer) {
            if(this.valueUsesManagedSerializer) {
                return new ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>(this);
            }

            return CreateDictionary(
                typeof(ManagedUnmanagedDictionary<,,,>),
                typeof(ManagedSerializer<>),
                typeof(UnmanagedSerializer<>));
        }

        if(this.valueUsesManagedSerializer) {
            return CreateDictionary(
                typeof(UnmanagedManagedDictionary<,,,>),
                typeof(UnmanagedSerializer<>),
                typeof(ManagedSerializer<>));
        }

        throw new InvalidOperationException("ManagedDictionaryStore cannot create an unmanaged/unmanaged dictionary.");
    }

    private protected override long GetRecoveryEntryCount() {
        using var dictionary = this.Create();
        return dictionary.GetLongCount();
    }

    private Collections.IDictionary<TKey,TValue> CreateDictionary(Type dictionaryOpenType,Type keySerializerOpenType,Type valueSerializerOpenType) {
        var dictionaryType = dictionaryOpenType.MakeGenericType(
            typeof(TKey),
            typeof(TValue),
            keySerializerOpenType.MakeGenericType(typeof(TKey)),
            valueSerializerOpenType.MakeGenericType(typeof(TValue)));
        return (Collections.IDictionary<TKey,TValue>)Activator.CreateInstance(
            dictionaryType,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
            binder: null,
            args: [this],
            culture: null)!;
    }

}

//public class ManagedManagedDictionaryStore<TKey, TValue>
//    :ManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>
//// {
//    internal ManagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    internal ManagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    public ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> Create() => new(this);
//}

//public class UnmanagedManagedDictionaryStore<TKey, TValue>
//    :ManagedDictionaryStore<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>
//    where TKey : unmanaged
// {
//    internal UnmanagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    internal UnmanagedManagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    public UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> Create() => new(this);
//}

//public class ManagedUnmanagedDictionaryStore<TKey, TValue>
//    :ManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>
////    where TValue : unmanaged {
//    internal ManagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction){ }

//    internal ManagedUnmanagedDictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) :
//        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir){ }

//    public ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> Create() => new(this);
//}

