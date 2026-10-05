using Seamless.Collections.Tsavorite;
namespace Seamless.Collections.Tsavorite;

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

    internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes,long? ObjectLogSegmentSize = null) :
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes,keyUsesMemoryPackSerializer: true,valueUsesMemoryPackSerializer: true,ObjectLogSegmentSize) { }

    internal DictionaryStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,double StorageLimitFraction,long StorageReserveBytes,bool keyUsesMemoryPackSerializer,bool valueUsesMemoryPackSerializer,long? ObjectLogSegmentSize = null) :
        base(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind.Storage,StorageLimitFraction,StorageReserveBytes,ObjectLogSegmentSize) {
        this.keyUsesMemoryPackSerializer=keyUsesMemoryPackSerializer;
        this.valueUsesMemoryPackSerializer=valueUsesMemoryPackSerializer;
    }

    internal DictionaryStore(TsavoriteLayout layout) : base(layout) {
        this.keyUsesMemoryPackSerializer=RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        this.valueUsesMemoryPackSerializer=RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
    }

    public IDictionary<TKey,TValue> Create() {
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

    /// <summary>
    /// 件数を保持しない操作用オブジェクト。1 つの処理の中で並列化するときに使う。
    /// 増減は呼び出し側で集計し、最後に ApplyCountDelta で反映する。
    /// </summary>
    public IDictionary<TKey,TValue> CreateUncounted() {
        var dictionary = this.Create();
        ((IUncountableAccessor)dictionary).DisableCounting();
        return dictionary;
    }

    private IDictionary<TKey,TValue> CreateDictionary(Type dictionaryOpenType,Type keySerializerOpenType,Type valueSerializerOpenType) {
        var dictionaryType = dictionaryOpenType.MakeGenericType(
            typeof(TKey),
            typeof(TValue),
            keySerializerOpenType.MakeGenericType(typeof(TKey)),
            valueSerializerOpenType.MakeGenericType(typeof(TValue)));
        return (IDictionary<TKey,TValue>)Activator.CreateInstance(
            dictionaryType,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
            binder: null,
            args: [this],
            culture: null)!;
    }

}
