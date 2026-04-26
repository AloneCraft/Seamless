using Seamless.Collections;
using Seamless.Stores;

namespace Test.Seamless.Unit.SessionFunctions;

public abstract class ExceptWith<TKey, TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : ISeamlessHashSet<TKey>, IDisposable {
    [Fact, Trait("Category","Unit")]
    public void ExceptWithAB() {
        this.RunWithAB();
    }

    [Fact, Trait("Category","Unit")]
    public void ExceptWithAA() {
        this.RunWithAA();
    }
}

public sealed class ManagedExceptWith:ExceptWith<string,ManagedHashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (ManagedHashSet<string> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<string>(new ManagedTag());
        return (store.Create(),store);
    }
    protected override void With(ManagedHashSet<string> set,ManagedHashSet<string> other) => set.ExceptWith(other);
    protected override void ExpectedWith(HashSet<string> set,HashSet<string> other) => set.ExceptWith(other);
}

public sealed class UnmanagedExceptWith:ExceptWith<int,UnmanagedHashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (UnmanagedHashSet<int> Set,IDisposable Store) CreateSet() {
        var store = HashStore.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(UnmanagedHashSet<int> set,UnmanagedHashSet<int> other) => set.ExceptWith(other);
    protected override void ExpectedWith(HashSet<int> set,HashSet<int> other) => set.ExceptWith(other);
}
