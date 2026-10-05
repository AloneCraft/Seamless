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

    /// <summary>シングル用の操作用オブジェクト。件数をデータ本体で管理し O(1)。</summary>
    public IDictionary<TKey,TValue> Create() {
        if(this.keyUsesMemoryPackSerializer&&this.valueUsesMemoryPackSerializer) {
            return new Dictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(this);
        }
        return (IDictionary<TKey,TValue>)this.CreateAccessor(typeof(Dictionary<,,,>),[this]);
    }

    /// <summary>
    /// 並列区間を開く。区間の <see cref="ParallelRegion{TAccessor}.Create"/> で、スレッドごとにマルチ用の操作用オブジェクトを作る。
    /// <paramref name="expectedCount"/> は区間を閉じたときの全体の見込み件数で、索引をその件数に合う大きさまで先に広げる。
    /// </summary>
    public ParallelRegion<IParallelDictionary<TKey,TValue>> BeginParallel(long expectedCount = 0) =>
        this.BeginParallelCore(expectedCount,this.CreateParallel);

    private IParallelDictionary<TKey,TValue> CreateParallel(ParallelRegionState region) {
        if(this.keyUsesMemoryPackSerializer&&this.valueUsesMemoryPackSerializer) {
            return new ParallelDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(this,region);
        }
        return (IParallelDictionary<TKey,TValue>)this.CreateAccessor(typeof(ParallelDictionary<,,,>),[this,region]);
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

    // 実行時に決まるシリアライザの組み合わせで作る。コンストラクタの例外(作れないときの InvalidOperationException など)は包まずにそのまま投げる。
    private object CreateAccessor(Type dictionaryOpenType,object[] args) {
        Type keySerializerOpenType;
        if(this.keyUsesMemoryPackSerializer) {
            keySerializerOpenType=typeof(MemoryPackSerializer<>);
        } else {
            keySerializerOpenType=typeof(BlittableSerializer<>);
        }
        Type valueSerializerOpenType;
        if(this.valueUsesMemoryPackSerializer) {
            valueSerializerOpenType=typeof(MemoryPackSerializer<>);
        } else {
            valueSerializerOpenType=typeof(BlittableSerializer<>);
        }
        var dictionaryType = dictionaryOpenType.MakeGenericType(
            typeof(TKey),
            typeof(TValue),
            keySerializerOpenType.MakeGenericType(typeof(TKey)),
            valueSerializerOpenType.MakeGenericType(typeof(TValue)));
        try {
            return Activator.CreateInstance(
                dictionaryType,
                BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
                binder: null,
                args: args,
                culture: null)!;
        } catch(TargetInvocationException exception) when(exception.InnerException is not null) {
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

}
