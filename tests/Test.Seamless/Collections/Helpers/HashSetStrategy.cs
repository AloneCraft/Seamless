using Seamless;
using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;
public sealed class HashSetHandle<T>(IDisposable store, ISeamlessHashSet<T> set) : IDisposable
    where T : IEquatable<T>{
    public ISeamlessHashSet<T> Set => set;
    public void Dispose()
    {
        set.Dispose();
        store.Dispose();
    }
}
public interface IHashSetValueProvider<T>
    where T : IEquatable<T>{
    static abstract T Create(int i);
}
public readonly struct Int32HashSetValueProvider : IHashSetValueProvider<int>{
    public static int Create(int i) => i;
}
public readonly struct StringHashSetValueProvider : IHashSetValueProvider<string>{
    public static string Create(int i) => i.ToString();
}
/// <summary>
/// HashSet テストの生成戦略。
/// Dictionary の IDictTestStrategy と同様に、値生成とストア生成のみを担う。
/// 操作は ISeamlessHashSet&lt;T&gt; を直接呼び出す。
/// </summary>
public interface IHashSetTestStrategy<T>
    where T : IEquatable<T>{
    static abstract T Elem(int i);
    static abstract HashSetHandle<T> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0);
}
public readonly struct ManagedHashSetStrategy<TProvider, T> : IHashSetTestStrategy<T>
    where TProvider : IHashSetValueProvider<T>
    where T : IEquatable<T>{
    public static T Elem(int i) => TProvider.Create(i);
    public static HashSetHandle<T> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0)
    {
        var store = Factory.Create(MemorySize: memorySize, MutableFraction: mutableFraction).Create<T>(new ManagedTag());
        return new HashSetHandle<T>(store, store.CreateManagedUnmanaged());
    }
}
public readonly struct UnmanagedHashSetStrategy<TProvider, T> : IHashSetTestStrategy<T>
    where TProvider : IHashSetValueProvider<T>
    where T : unmanaged, IEquatable<T>{
    public static T Elem(int i) => TProvider.Create(i);
    public static HashSetHandle<T> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0)
    {
        var store = Factory.Create(MemorySize: memorySize, MutableFraction: mutableFraction).Create<T>();
        return new HashSetHandle<T>(store, store.Create());
    }
}
