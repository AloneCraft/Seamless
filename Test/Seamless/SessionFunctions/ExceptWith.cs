using Seamless.Collections.Tsavorite;
using Seamless.Collections.Tsavorite.Stores;

namespace Test.Seamless.SessionFunctions;

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

public sealed class ManagedExceptWith:ExceptWith<string,global::Seamless.Collections.Tsavorite.HashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (global::Seamless.Collections.Tsavorite.HashSet<string> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<string>(new MemoryPackSerializerStrategy());
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Collections.Tsavorite.HashSet<string> set,global::Seamless.Collections.Tsavorite.HashSet<string> other) => set.ExceptWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<string> set,global::System.Collections.Generic.HashSet<string> other) => set.ExceptWith(other);
}

public sealed class UnmanagedExceptWith:ExceptWith<int,global::Seamless.Collections.Tsavorite.HashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (global::Seamless.Collections.Tsavorite.HashSet<int> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Collections.Tsavorite.HashSet<int> set,global::Seamless.Collections.Tsavorite.HashSet<int> other) => set.ExceptWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<int> set,global::System.Collections.Generic.HashSet<int> other) => set.ExceptWith(other);
}
