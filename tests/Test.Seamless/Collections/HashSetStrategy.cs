using Seamless;
using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

public sealed class HashSetHandle<TSet, T>(IDisposable store, TSet set) : IDisposable
    where TSet : class, ICollection<T>, IDisposable
    where T : IEquatable<T>
{
    public TSet Set => set;

    public void Dispose()
    {
        set.Dispose();
        store.Dispose();
    }
}

/// <summary>
/// 具体的な HashSet 型に対するテスト生成戦略。
/// 要素生成と concrete 型専用メソッドの呼び分けを担う。
/// </summary>
public interface IHashSetTestStrategy<TSet, T>
    where TSet : class, ICollection<T>, IDisposable
    where T : IEquatable<T>
{
    static abstract T Elem(int i);
    static abstract HashSetHandle<TSet, T> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0);
    static abstract bool Add(TSet set, T item);
    static abstract void ExceptWith(TSet set, TSet other);
    static abstract void IntersectWith(TSet set, TSet other);
    static abstract bool IsSubsetOf(TSet set, TSet other);
    static abstract bool IsSupersetOf(TSet set, TSet other);
    static abstract bool IsProperSubsetOf(TSet set, TSet other);
    static abstract bool IsProperSupersetOf(TSet set, TSet other);
    static abstract bool Overlaps(TSet set, TSet other);
    static abstract bool SetEquals(TSet set, TSet other);
    static abstract void SymmetricExceptWith(TSet set, TSet other);
    static abstract void UnionWith(TSet set, TSet other);
    static abstract bool IsDisposed(TSet set);
}

public readonly struct ManagedStringHashSetStrategy : IHashSetTestStrategy<global::Seamless.Collections.ManagedHashSet<string>, string>
{
    public static string Elem(int i) => i.ToString();
    public static HashSetHandle<global::Seamless.Collections.ManagedHashSet<string>, string> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0)
    {
        var store = Factory.Create(MemorySize: memorySize, MutableFraction: mutableFraction).Create<string>(new ManagedTag());
        return new HashSetHandle<global::Seamless.Collections.ManagedHashSet<string>, string>(store, store.Create());
    }
    public static bool Add(global::Seamless.Collections.ManagedHashSet<string> set, string item) => set.Add(item);
    public static void ExceptWith(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.ExceptWith(other);
    public static void IntersectWith(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.IntersectWith(other);
    public static bool IsSubsetOf(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.IsSubsetOf(other);
    public static bool IsSupersetOf(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.IsSupersetOf(other);
    public static bool IsProperSubsetOf(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.IsProperSubsetOf(other);
    public static bool IsProperSupersetOf(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.IsProperSupersetOf(other);
    public static bool Overlaps(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.Overlaps(other);
    public static bool SetEquals(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.SetEquals(other);
    public static void SymmetricExceptWith(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.SymmetricExceptWith(other);
    public static void UnionWith(global::Seamless.Collections.ManagedHashSet<string> set, global::Seamless.Collections.ManagedHashSet<string> other) => set.UnionWith(other);
    public static bool IsDisposed(global::Seamless.Collections.ManagedHashSet<string> set) => set.IsDisposed;
}

public readonly struct UnmanagedIntHashSetStrategy : IHashSetTestStrategy<global::Seamless.Collections.UnmanagedHashSet<int>, int>
{
    public static int Elem(int i) => i;
    public static HashSetHandle<global::Seamless.Collections.UnmanagedHashSet<int>, int> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0)
    {
        var store = Factory.Create(MemorySize: memorySize, MutableFraction: mutableFraction).CreateUnmanagedHashSet<int>();
        return new HashSetHandle<global::Seamless.Collections.UnmanagedHashSet<int>, int>(store, store.Create());
    }
    public static bool Add(global::Seamless.Collections.UnmanagedHashSet<int> set, int item) => set.Add(item);
    public static void ExceptWith(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.ExceptWith(other);
    public static void IntersectWith(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.IntersectWith(other);
    public static bool IsSubsetOf(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.IsSubsetOf(other);
    public static bool IsSupersetOf(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.IsSupersetOf(other);
    public static bool IsProperSubsetOf(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.IsProperSubsetOf(other);
    public static bool IsProperSupersetOf(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.IsProperSupersetOf(other);
    public static bool Overlaps(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.Overlaps(other);
    public static bool SetEquals(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.SetEquals(other);
    public static void SymmetricExceptWith(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.SymmetricExceptWith(other);
    public static void UnionWith(global::Seamless.Collections.UnmanagedHashSet<int> set, global::Seamless.Collections.UnmanagedHashSet<int> other) => set.UnionWith(other);
    public static bool IsDisposed(global::Seamless.Collections.UnmanagedHashSet<int> set) => set.IsDisposed;
}
