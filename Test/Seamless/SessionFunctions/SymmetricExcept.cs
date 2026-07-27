namespace Test.Seamless.SessionFunctions;

public abstract class SymmetricExceptWith<TKey, TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : IHashSet<TKey>, IDisposable {
    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWithAB() => this.RunWithAB();

    [Fact, Trait("Category","Unit")]
    public void SymmetricExceptWithAA() => this.RunWithAA();
}

public sealed class ManagedSymmetricExceptWith:SymmetricExceptWith<string,global::Seamless.Collections.Tsavorite.HashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (global::Seamless.Collections.Tsavorite.HashSet<string> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<string>(new MemoryPackSerializerStrategy());
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Collections.Tsavorite.HashSet<string> set,global::Seamless.Collections.Tsavorite.HashSet<string> other) => set.SymmetricExceptWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<string> set,global::System.Collections.Generic.HashSet<string> other) => set.SymmetricExceptWith(other);
}

public sealed class UnmanagedSymmetricExceptWith:SymmetricExceptWith<int,global::Seamless.Collections.Tsavorite.HashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (global::Seamless.Collections.Tsavorite.HashSet<int> Set,IDisposable Store) CreateSet() {
        var store = Store.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(global::Seamless.Collections.Tsavorite.HashSet<int> set,global::Seamless.Collections.Tsavorite.HashSet<int> other) => set.SymmetricExceptWith(other);
    protected override void ExpectedWith(global::System.Collections.Generic.HashSet<int> set,global::System.Collections.Generic.HashSet<int> other) => set.SymmetricExceptWith(other);
}
