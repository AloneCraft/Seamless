using Seamless.Collections;
using Seamless.Collections.Tsavorite;

using Tsavorite.core;
namespace Seamless.TestKit;

public interface IDictTypeProvider<T>
    where T : IEquatable<T> {
    static abstract T Create(int i);
    static abstract T Wrong();
}
public readonly struct IntDictTypeProvider:IDictTypeProvider<int> {
    public static int Create(int i) => i;
    public static int Wrong() => -1;
}
public readonly struct StringDictTypeProvider:IDictTypeProvider<string> {
    public static string Create(int i) => i.ToString();
    public static string Wrong() => "!!";
}
public readonly struct UnmanagedStructDictTypeProvider:IDictTypeProvider<UnmanagedStruct> {
    public static UnmanagedStruct Create(int i) => new() { Value=i };
    public static UnmanagedStruct Wrong() => new() { Value=-1 };
}
public readonly struct ManagedStructDictTypeProvider:IDictTypeProvider<ManagedStruct> {
    public static ManagedStruct Create(int i) => new() { Name=$"N{i}" };
    public static ManagedStruct Wrong() => new() { Name="!!" };
}
public readonly struct ManagedClassDictTypeProvider:IDictTypeProvider<ManagedClass> {
    public static ManagedClass Create(int i) => new() { Name=$"C{i}" };
    public static ManagedClass Wrong() => new() { Name="!!" };
}
public interface IDictTestStrategy<TKey, TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    static abstract TKey K(int i);
    static abstract TValue V(int i);
    /// <summary>どの V(i) とも一致しない値。値不一致テスト用。</summary>
    static abstract TValue VWrong();
    /// <summary>共通ストアと公開設定で作る。アクセサは NewDict を呼んだスレッドで使う。</summary>
    static virtual TunedDictionaryHandle<TKey,TValue> CreateTunedStore(TsavoriteTuning tuning,global::Seamless.Execution.StoreLimits limits)=>
        TunedDictionaryHandle<TKey,TValue>.Create(tuning,limits);
    static abstract StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize);
    static abstract StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,
        IDevice objDevice,
        long memorySize = global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
        double mutableFraction = 1.0,
        string? checkpointDir = null,
        bool recoverDevice = false);
}
internal static class DictStoreHandleFactory {
    public static StoreHandle<TKey,TValue> Create<TKey, TValue>(
        IDisposable store,
        Func<global::Seamless.Collections.Tsavorite.IDictionary<TKey,TValue>> newDict,
        Action<bool> flushAndEvict,
        Func<Guid> save,
        Action<Guid> load,
        Func<(long,long,long,long)> address)
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(store,newDict,flushAndEvict,save,load,address);
}
public readonly struct ManagedStoreDictionaryStrategy<TKeyProvider, TValueProvider, TKey, TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider : IDictTypeProvider<TKey>
    where TValueProvider : IDictTypeProvider<TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    public static TKey K(int i) => TKeyProvider.Create(i);
    public static TValue V(int i) => TValueProvider.Create(i);
    public static TValue VWrong() => TValueProvider.Wrong();
    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,IndexSize: indexSize,PageSize: pageSize,SegmentSize: segmentSize).Create<TKey,TValue>(new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize = global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
        double mutableFraction = 1.0,
        string? checkpointDir = null,
        bool recoverDevice = false) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,CheckpointDir: checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
}
public readonly struct UnmanagedKeyDictionaryStrategy<TKeyProvider, TValueProvider, TKey, TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider : IDictTypeProvider<TKey>
    where TValueProvider : IDictTypeProvider<TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    public static TKey K(int i) => TKeyProvider.Create(i);
    public static TValue V(int i) => TValueProvider.Create(i);
    public static TValue VWrong() => TValueProvider.Wrong();
    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,IndexSize: indexSize,PageSize: pageSize,SegmentSize: segmentSize).Create<TKey,TValue>(new BlittableStrategy(),new MemoryPackSerializerStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize = global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
        double mutableFraction = 1.0,
        string? checkpointDir = null,
        bool recoverDevice = false) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,CheckpointDir: checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new BlittableStrategy(),new MemoryPackSerializerStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
}
public readonly struct UnmanagedValueDictionaryStrategy<TKeyProvider, TValueProvider, TKey, TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider : IDictTypeProvider<TKey>
    where TValueProvider : IDictTypeProvider<TValue>
    where TKey : IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    public static TKey K(int i) => TKeyProvider.Create(i);
    public static TValue V(int i) => TValueProvider.Create(i);
    public static TValue VWrong() => TValueProvider.Wrong();
    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,IndexSize: indexSize,PageSize: pageSize,SegmentSize: segmentSize).Create<TKey,TValue>(new MemoryPackSerializerStrategy(),new BlittableStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize = global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
        double mutableFraction = 1.0,
        string? checkpointDir = null,
        bool recoverDevice = false) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,CheckpointDir: checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new MemoryPackSerializerStrategy(),new BlittableStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
}
public readonly struct UnmanagedStoreDictionaryStrategy<TKeyProvider, TValueProvider, TKey, TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider : IDictTypeProvider<TKey>
    where TValueProvider : IDictTypeProvider<TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    public static TKey K(int i) => TKeyProvider.Create(i);
    public static TValue V(int i) => TValueProvider.Create(i);
    public static TValue VWrong() => TValueProvider.Wrong();
    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize
    ) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,IndexSize: indexSize,PageSize: pageSize,SegmentSize: segmentSize).Create<TKey,TValue>(new BlittableStrategy(),new BlittableStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize = global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity,
        double mutableFraction = 1.0,
        string? checkpointDir = null,
        bool recoverDevice = false
    ) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,CheckpointDir: checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new BlittableStrategy(),new BlittableStrategy());
        return DictStoreHandleFactory.Create(
            store,() => store.Create(),wait => store.Store.Log.FlushAndEvict(wait),
            () => store.Save(),token => store.Load(token),() => store.Address);
    }
}
