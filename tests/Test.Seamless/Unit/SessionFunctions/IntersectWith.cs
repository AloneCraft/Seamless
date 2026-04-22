using Azure;

using Seamless;
using Seamless.Collections;
using Seamless.Stores;

using Tsavorite.core;
namespace Test.Seamless.Unit.SessionFunctions;

public abstract class IntersectWith<TKey,TSet>:CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : ISeamlessHashSet<TKey>,IDisposable {
    [Fact, Trait("Category","Unit")]
    public void IntersectWithAB() => this.RunWithAB();

    [Fact, Trait("Category","Unit")]
    public void IntersectWithAA() => this.RunWithAA();
}

public sealed class ManagedIntersectWith:IntersectWith<string,ManagedHashSet<string>> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override (ManagedHashSet<string> Set,IDisposable Store) CreateSet() {
        var store = Factory.Create().Create<string>(new ManagedTag());
        return (store.Create(),store);
    }
    protected override void With(ManagedHashSet<string> set,ManagedHashSet<string> other) => set.IntersectWith(other);
    protected override void ExpectedWith(HashSet<string> set,HashSet<string> other) => set.IntersectWith(other);
}

public sealed class UnmanagedIntersectWith:IntersectWith<int,UnmanagedHashSet<int>> {
    protected override int CreateKey(int seed) => seed;
    protected override (UnmanagedHashSet<int> Set,IDisposable Store) CreateSet() {
        var store = Factory.Create().Create<int>();
        return (store.Create(),store);
    }
    protected override void With(UnmanagedHashSet<int> set,UnmanagedHashSet<int> other) => set.IntersectWith(other);
    protected override void ExpectedWith(HashSet<int> set,HashSet<int> other) => set.IntersectWith(other);
}
