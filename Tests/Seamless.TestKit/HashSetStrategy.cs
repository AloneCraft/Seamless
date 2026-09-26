using Seamless.Collections;
using Seamless.Collections.Tsavorite;
namespace Seamless.TestKit;

public sealed class HashSetHandle<T>(IDisposable store,global::Seamless.Collections.Tsavorite.HashSet<T> set):IDisposable
    where T : IEquatable<T> {
    public global::Seamless.Collections.Tsavorite.HashSet<T> Set => set;
    public void Dispose() {
        set.Dispose();
        store.Dispose();
    }
}
public interface IHashSetValueProvider<T>
    where T : IEquatable<T> {
    static abstract T Create(int i);
}
public readonly struct Int32HashSetValueProvider:IHashSetValueProvider<int> {
    public static int Create(int i) => i;
}
public readonly struct StringHashSetValueProvider:IHashSetValueProvider<string> {
    public static string Create(int i) => i.ToString();
}
public readonly struct UnmanagedStructHashSetValueProvider:IHashSetValueProvider<UnmanagedStruct> {
    public static UnmanagedStruct Create(int i) => new() { Value=i };
}
public readonly struct ManagedStructHashSetValueProvider:IHashSetValueProvider<ManagedStruct> {
    public static ManagedStruct Create(int i) => new() { Name=$"N{i}" };
}
public readonly struct ManagedClassHashSetValueProvider:IHashSetValueProvider<ManagedClass> {
    public static ManagedClass Create(int i) => new() { Name=$"C{i}" };
}
/// <summary>
/// HashSetの動作を検証する。
/// HashSetの動作を検証する。
/// HashSetの動作を検証する。
/// </summary>
public interface IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    static abstract T Elem(int i);
    /// <summary>共通ストアと公開設定で作る。旧 API の正しさのテストとは独立した生成経路。</summary>
    static virtual TunedHashSetHandle<T> CreateTunedSet(TsavoriteTuning tuning,global::Seamless.Execution.StoreLimits limits)=>
        TunedHashSetHandle<T>.Create(tuning,limits);
    static abstract HashSetHandle<T> CreateMemorySet(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize);
}
public readonly struct MemoryPackHashSetStrategy<TProvider, T>:IHashSetTestStrategy<T>
    where TProvider : IHashSetValueProvider<T>
    where T : IEquatable<T> {
    public static T Elem(int i) => TProvider.Create(i);
    public static HashSetHandle<T> CreateMemorySet(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,IndexSize: indexSize,PageSize: pageSize,SegmentSize: segmentSize).Create<T>(new MemoryPackSerializerStrategy());
        return new HashSetHandle<T>(store,store.Create());
    }
}
public readonly struct BlittableHashSetStrategy<TProvider, T>:IHashSetTestStrategy<T>
    where TProvider : IHashSetValueProvider<T>
    where T : unmanaged, IEquatable<T> {
    public static T Elem(int i) => TProvider.Create(i);
    public static HashSetHandle<T> CreateMemorySet(
        long memorySize = global::Seamless.Collections.Tsavorite.Const.LogMemorySize,
        double mutableFraction = 1.0,
        long indexSize = global::Seamless.Collections.Tsavorite.Const.IndexSize,
        long pageSize = global::Seamless.Collections.Tsavorite.Const.PageSize,
        long segmentSize = global::Seamless.Collections.Tsavorite.Const.SegmentSize) {
        var store = Store.Create(MemorySize: memorySize,MutableFraction: mutableFraction,IndexSize: indexSize,PageSize: pageSize,SegmentSize: segmentSize).Create<T>();
        return new HashSetHandle<T>(store,store.Create());
    }
}
