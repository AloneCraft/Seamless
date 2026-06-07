using Seamless.Collections;
using Seamless.Devices;
namespace Seamless.Stores;

public class DictionaryStore<TKey, TValue>:CollectionStore<TKey,TValue> {
    private readonly bool keyUsesMemoryPackSerializer;
    private readonly bool valueUsesMemoryPackSerializer;

    internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) :
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,keyUsesMemoryPackSerializer: true,valueUsesMemoryPackSerializer: true) { }

    internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,bool keyUsesMemoryPackSerializer,bool valueUsesMemoryPackSerializer) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
        this.keyUsesMemoryPackSerializer=keyUsesMemoryPackSerializer;
        this.valueUsesMemoryPackSerializer=valueUsesMemoryPackSerializer;
    }

    internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes) :
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes,keyUsesMemoryPackSerializer: true,valueUsesMemoryPackSerializer: true) { }

    internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction,long StorageReserveBytes,bool keyUsesMemoryPackSerializer,bool valueUsesMemoryPackSerializer) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage,StorageLimitFraction,StorageReserveBytes) {
        this.keyUsesMemoryPackSerializer=keyUsesMemoryPackSerializer;
        this.valueUsesMemoryPackSerializer=valueUsesMemoryPackSerializer;
    }

    public Collections.IDictionary<TKey,TValue> Create() {
        if(this.keyUsesMemoryPackSerializer) {
            if(this.valueUsesMemoryPackSerializer) {
                return new Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(this);
            }

            return this.CreateDictionary(
                typeof(Dictionary<,,,>),
                typeof(MemoryPackSerializer<>),
                typeof(BlittableSerializer<>));
        }

        if(this.valueUsesMemoryPackSerializer) {
            return this.CreateDictionary(
                typeof(Dictionary<,,,>),
                typeof(BlittableSerializer<>),
                typeof(MemoryPackSerializer<>));
        }

        return this.CreateDictionary(
            typeof(Dictionary<,,,>),
            typeof(BlittableSerializer<>),
            typeof(BlittableSerializer<>));
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
