using Seamless.Collections;
using Seamless.Stores;

namespace Benchmarks;

public abstract class ManagedHashSetCtorBenchmark<TKey>(ITestOutputHelper output)
    :HashSetConstructorBenchmarkBase<TKey,ManagedHashSet<TKey>>(output)
    where TKey : IEquatable<TKey> {
    private const long MemorySize = Seamless.Const.LogMemorySize<<11;

    protected override string BenchmarkName => "ManagedHashSet";

    protected override ConstructorBenchmarkHandle<ManagedHashSet<TKey>> CreateHandle(){
        var store = Factory.Create(IndexSize: this.IndexSize,MemorySize: MemorySize)
            .Create<TKey>(new ManagedTag());
        var set = new ManagedHashSet<TKey>(store);
        return new(store,set);
    }

    protected override bool AddItem(ManagedHashSet<TKey> set,TKey key) => set.Add(key);
    protected override bool TryAddItem(ManagedHashSet<TKey> set,TKey key) => set.TryAdd(key,new EqualityEmpty())==TryAddResult.Success;
    protected override bool ContainsItem(ManagedHashSet<TKey> set,TKey key) => set.Contains(key);
    protected override bool RemoveItem(ManagedHashSet<TKey> set,TKey key) => set.Remove(key);
    protected override long GetItemCount(ManagedHashSet<TKey> set) => set.GetLongCount();
    protected override void ClearItems(ManagedHashSet<TKey> set) => set.Clear();
}

public abstract class UnmanagedHashSetCtorBenchmark<TKey>(ITestOutputHelper output)
    :HashSetConstructorBenchmarkBase<TKey,UnmanagedHashSet<TKey>>(output)
    where TKey : unmanaged, IEquatable<TKey> {
    private const long MemorySize = Seamless.Const.LogMemorySize<<11;

    protected override string BenchmarkName => "UnmanagedHashSet";

    protected override ConstructorBenchmarkHandle<UnmanagedHashSet<TKey>> CreateHandle(){
        var store = Factory.Create(IndexSize: this.IndexSize,MemorySize: MemorySize)
            .Create<TKey>(new UnmanagedTag());
        var set = new UnmanagedHashSet<TKey>(store);
        return new(store,set);
    }

    protected override bool AddItem(UnmanagedHashSet<TKey> set,TKey key) => set.Add(key);
    protected override bool TryAddItem(UnmanagedHashSet<TKey> set,TKey key) => set.TryAdd(key,new EqualityEmpty())==TryAddResult.Success;
    protected override bool ContainsItem(UnmanagedHashSet<TKey> set,TKey key) => set.Contains(key);
    protected override bool RemoveItem(UnmanagedHashSet<TKey> set,TKey key) => set.Remove(key);
    protected override long GetItemCount(UnmanagedHashSet<TKey> set) => set.GetLongCount();
    protected override void ClearItems(UnmanagedHashSet<TKey> set) => set.Clear();
}

[Trait("TKey","int")]
public sealed class ManagedHashSetCtor_Int(ITestOutputHelper output)
    :ManagedHashSetCtorBenchmark<int>(output){
    protected override int CreateKey(int seed) => seed;
}

[Trait("TKey","string")]
public sealed class ManagedHashSetCtor_String(ITestOutputHelper output)
    :ManagedHashSetCtorBenchmark<string>(output){
    protected override string CreateKey(int seed) => seed.ToString();
}

[Trait("TKey","int")]
public sealed class UnmanagedHashSetCtor_Int(ITestOutputHelper output)
    :UnmanagedHashSetCtorBenchmark<int>(output){
    protected override int CreateKey(int seed) => seed;
}

[Trait("TKey","long")]
public sealed class UnmanagedHashSetCtor_Long(ITestOutputHelper output)
    :UnmanagedHashSetCtorBenchmark<long>(output){
    protected override long CreateKey(int seed) => seed;
}
