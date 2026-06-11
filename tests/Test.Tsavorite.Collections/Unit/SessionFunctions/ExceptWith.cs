using Tsavorite.Collections;
using Tsavorite.Stores;

namespace Test.Tsavorite.Collections.Unit.SessionFunctions;

public abstract class ExceptWith<TKey, TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : IHashSet<TKey>, IDisposable {
    [Fact, Trait("Category","Unit")]
    public void ExceptWithAB() {
        this.RunWithAB();
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptWithAA() {
        this.RunWithAA();
    }
}

public sealed class ManagedExceptWith:ExceptWith<string,global::Tsavorite.Collections.HashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (global::Tsavorite.Collections.HashSet<string> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<string>(new MemoryPackSerializerStrategy());
        return (store.Create(),store);
    }
    protected override void With(global::Tsavorite.Collections.HashSet<string> set,global::Tsavorite.Collections.HashSet<string> other) => set.ExceptWith(other);
    protected override void ExpectedWith(System.Collections.Generic.HashSet<string> set,System.Collections.Generic.HashSet<string> other) => set.ExceptWith(other);
}

public sealed class UnmanagedExceptWith:ExceptWith<int,global::Tsavorite.Collections.HashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (global::Tsavorite.Collections.HashSet<int> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(global::Tsavorite.Collections.HashSet<int> set,global::Tsavorite.Collections.HashSet<int> other) => set.ExceptWith(other);
    protected override void ExpectedWith(System.Collections.Generic.HashSet<int> set,System.Collections.Generic.HashSet<int> other) => set.ExceptWith(other);
}
