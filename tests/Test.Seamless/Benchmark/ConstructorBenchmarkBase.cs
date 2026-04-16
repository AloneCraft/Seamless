using System.Collections;
using System.Diagnostics;

namespace Benchmarks;

public sealed class ConstructorBenchmarkHandle<TCollection>(IDisposable store,TCollection collection)
    :IDisposable {
    public TCollection Collection { get; } = collection;

    public void Dispose() {
        if(Collection is IDisposable disposable)
            disposable.Dispose();
        store.Dispose();
    }
}

public abstract class DictionaryConstructorBenchmarkBase<TKey, TValue, TDictionary>(ITestOutputHelper output)
    :Benchmark<TKey,TValue>(output)
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TDictionary : IEnumerable {
    protected abstract string BenchmarkName { get; }
    protected abstract ConstructorBenchmarkHandle<TDictionary> CreateHandle();
    protected abstract void AddItem(TDictionary dict,TKey key,TValue value);
    protected abstract bool TryGetItem(TDictionary dict,TKey key,out TValue value);
    protected abstract bool ContainsItem(TDictionary dict,TKey key);
    protected abstract bool TryAddItem(TDictionary dict,TKey key,TValue value);
    protected abstract void SetItem(TDictionary dict,TKey key,TValue value);
    protected abstract bool RemoveItem(TDictionary dict,TKey key);
    protected abstract long GetItemCount(TDictionary dict);
    protected abstract void ClearItems(TDictionary dict);

    private void Seed(TDictionary dict) {
        for(var i = 0; i<Count; i++)
            this.AddItem(dict,this.CreateKey(i),this.CreateValue(i));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Add_TryGet_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            this.AddItem(dict,this.CreateKey(i),this.CreateValue(i));
        for(var i = 0; i<Count; i++)
            _=this.TryGetItem(dict,this.CreateKey(i),out _);
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor] {Count:N0}件x2: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/(Count*2):F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(dict));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_ContainsKey_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;
        this.Seed(dict);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.ContainsItem(dict,this.CreateKey(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor ContainsKey] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(dict));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_TryAdd_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.TryAddItem(dict,this.CreateKey(i),this.CreateValue(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor TryAdd] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(dict));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_IndexerSet_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;
        this.Seed(dict);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            this.SetItem(dict,this.CreateKey(i),this.CreateValue(i+Count));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Indexer set] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(dict));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Remove_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;
        this.Seed(dict);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.RemoveItem(dict,this.CreateKey(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Remove] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(0,this.GetItemCount(dict));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_GetEnumerator_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;
        this.Seed(dict);

        var sw = Stopwatch.StartNew();
        var count = 0;
        foreach(var _ in dict)
            count++;
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor GetEnumerator] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,count);
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Mixed_Add_Get_Remove_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            this.AddItem(dict,this.CreateKey(i),this.CreateValue(i));
        for(var i = 0; i<Count; i++)
            _=this.TryGetItem(dict,this.CreateKey(i),out _);
        for(var i = 0; i<Count; i++)
            _=this.RemoveItem(dict,this.CreateKey(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Mixed(Add+Get+Remove)] {Count*3:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/(Count*3):F2}us/op)");
        Assert.Equal(0,this.GetItemCount(dict));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Clear_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;
        this.Seed(dict);

        var sw = Stopwatch.StartNew();
        this.ClearItems(dict);
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Clear] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(0,this.GetItemCount(dict));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Count_10万件を計測する() {
        using var handle = this.CreateHandle();
        var dict = handle.Collection;
        this.Seed(dict);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.GetItemCount(dict);
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Count] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(dict));
    }
}

public abstract class HashSetConstructorBenchmarkBase<TKey, TSet>(ITestOutputHelper output)
    :HashSetBenchmark<TKey>(output)
    where TKey : IEquatable<TKey>
    where TSet : IEnumerable {
    protected abstract string BenchmarkName { get; }
    protected abstract ConstructorBenchmarkHandle<TSet> CreateHandle();
    protected abstract bool AddItem(TSet set,TKey key);
    protected abstract bool TryAddItem(TSet set,TKey key);
    protected abstract bool ContainsItem(TSet set,TKey key);
    protected abstract bool RemoveItem(TSet set,TKey key);
    protected abstract long GetItemCount(TSet set);
    protected abstract void ClearItems(TSet set);

    private void Seed(TSet set) {
        for(var i = 0; i<Count; i++)
            _=this.AddItem(set,this.CreateKey(i));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Add_Contains_10万件を計測する() {
        using var handle = this.CreateHandle();
        var set = handle.Collection;

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.AddItem(set,this.CreateKey(i));
        for(var i = 0; i<Count; i++)
            _=this.ContainsItem(set,this.CreateKey(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor] {Count:N0}件x2: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/(Count*2):F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(set));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_TryAdd_10万件を計測する() {
        using var handle = this.CreateHandle();
        var set = handle.Collection;

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.TryAddItem(set,this.CreateKey(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor TryAdd] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(set));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Remove_10万件を計測する() {
        using var handle = this.CreateHandle();
        var set = handle.Collection;
        this.Seed(set);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.RemoveItem(set,this.CreateKey(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Remove] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(0,this.GetItemCount(set));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_GetEnumerator_10万件を計測する() {
        using var handle = this.CreateHandle();
        var set = handle.Collection;
        this.Seed(set);

        var sw = Stopwatch.StartNew();
        var count = 0;
        foreach(var _ in set)
            count++;
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor GetEnumerator] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,count);
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Mixed_Add_Contains_Remove_10万件を計測する() {
        using var handle = this.CreateHandle();
        var set = handle.Collection;

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.AddItem(set,this.CreateKey(i));
        for(var i = 0; i<Count; i++)
            _=this.ContainsItem(set,this.CreateKey(i));
        for(var i = 0; i<Count; i++)
            _=this.RemoveItem(set,this.CreateKey(i));
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Mixed(Add+Contains+Remove)] {Count*3:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/(Count*3):F2}us/op)");
        Assert.Equal(0,this.GetItemCount(set));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Clear_10万件を計測する() {
        using var handle = this.CreateHandle();
        var set = handle.Collection;
        this.Seed(set);

        var sw = Stopwatch.StartNew();
        this.ClearItems(set);
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Clear] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(0,this.GetItemCount(set));
    }

    [Fact]
    [Trait("Category","")]
    public void _Ctor_Count_10万件を計測する() {
        using var handle = this.CreateHandle();
        var set = handle.Collection;
        this.Seed(set);

        var sw = Stopwatch.StartNew();
        for(var i = 0; i<Count; i++)
            _=this.GetItemCount(set);
        sw.Stop();

        this.Write($"[{this.BenchmarkName} ctor Count] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,this.GetItemCount(set));
    }
}