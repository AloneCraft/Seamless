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

public sealed class ManagedExceptWith:ExceptWith<string,global::Seamless.Tsavorite.Collections.HashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (global::Seamless.Tsavorite.Collections.HashSet<string> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<string>(new MemoryPackSerializerStrategy());
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Tsavorite.Collections.HashSet<string> set,global::Seamless.Tsavorite.Collections.HashSet<string> other) => set.ExceptWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<string> set,global::System.Collections.Generic.HashSet<string> other) => set.ExceptWith(other);
}

public sealed class UnmanagedExceptWith:ExceptWith<int,global::Seamless.Tsavorite.Collections.HashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (global::Seamless.Tsavorite.Collections.HashSet<int> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Tsavorite.Collections.HashSet<int> set,global::Seamless.Tsavorite.Collections.HashSet<int> other) => set.ExceptWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<int> set,global::System.Collections.Generic.HashSet<int> other) => set.ExceptWith(other);
}
