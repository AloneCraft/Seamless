using System.Collections;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Seamless.Collections.Benchmarks;

/// <summary>
/// Dictionary 用の (Store, Dictionary, チE��イス, 一時ディレクトリ) をまとめた破棁E��能ハンドル、E
/// </summary>
public sealed class DictHandle<TKey,TValue> : IDisposable
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    public required IDisposable Store { get; init; }
    public required ISeamlessDictionary<TKey,TValue> Dict { get; init; }
    public string? Dir { get; init; }
    public IDevice? Log { get; init; }
    public IDevice? Obj { get; init; }
    public void Dispose() {
        this.Dict.Dispose();
        this.Store.Dispose();
        this.Log?.Dispose();
        this.Obj?.Dispose();
        if(this.Dir is not null && Directory.Exists(this.Dir)) {
            try { Directory.Delete(this.Dir,recursive: true); } catch { }
        }
    }
}

/// <summary>
/// Dictionary ベンチ�Eークの generic 基底、EPI メソチE��ごとに <c>[Theory]</c> を定義する、E
/// 派生型で Managed/Unmanaged を型引数で刁E��替え、Esee cref="CreateStoreAndDict"/> を実裁E��る、E
/// </summary>
/// <remarks>
/// 吁E��スト�E <c>Count</c> 件のキーで Store めE1 つ作り、Parallel 並列度刁E�EスレチE��で同時に計測する、E
/// <c>IndexSize=Count</c>、Ec>MutableFraction&lt;1.0</c> 時�E一時ログチE��イスに書き込む、E
/// </remarks>
public abstract class BenchmarkDictionary<TKey,TValue> : BenchmarkBase
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue> {
    protected BenchmarkDictionary(ITestOutputHelper output) : base(output) { }

    protected abstract TKey CreateKey(int seed);
    protected abstract TValue CreateValue(int seed);

    /// <summary>Store+Dictionary を作る。派生型ぁEManaged/Unmanaged のどちらかを実裁E��る、E/summary>
    protected abstract DictHandle<TKey,TValue> CreateStoreAndDict(double mutableFraction,int elementCount);

    /// <summary>dict を空にしてから <paramref name="count"/> 件を投入する、E/summary>
    protected void Populate(ISeamlessDictionary<TKey,TValue> dict,int count) {
        for(var i = 0; i<count; i++) {
            dict[this.CreateKey(i)]=this.CreateValue(i);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // ISeamlessDictionary (= IDictionary<TKey,TValue>) 直接呼び出ぁE
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Add"),MemberData(nameof(Matrix))]
    public void Add(int parallel,double mutableFraction) {
        const int Count = 2_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => this.CreateStoreAndDict(mutableFraction,Count),
            measure: (t,h) => {
                for(var i = 0; i<Count; i++) h.Dict.Add(this.CreateKey(i),this.CreateValue(i));
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.Add",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Remove_key"),MemberData(nameof(Matrix))]
    public void Remove_key(int parallel,double mutableFraction) {
        const int Count = 2_300_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                for(var i = 0; i<Count; i++) h.Dict.Remove(this.CreateKey(i));
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.Remove(key)",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Remove_key_value"),MemberData(nameof(Matrix))]
    public void Remove_key_value(int parallel,double mutableFraction) {
        const int Count = 2_300_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                for(var i = 0; i<Count; i++) h.Dict.Remove(this.CreateKey(i),out _);
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.Remove(key,value)",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Clear"),MemberData(nameof(Matrix))]
    public void Clear(int parallel,double mutableFraction) {
        const int Count = 5_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => h.Dict.Clear(),
            teardown: h => h.Dispose(),
            label: "Dictionary.Clear",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.TryGetValue"),MemberData(nameof(Matrix))]
    public void TryGetValue(int parallel,double mutableFraction) {
        const int Count = 2_300_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                for(var i = 0; i<Count; i++) h.Dict.TryGetValue(this.CreateKey(i),out _);
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.TryGetValue",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Contains"),MemberData(nameof(Matrix))]
    public void Contains(int parallel,double mutableFraction) {
        const int Count = 2_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var coll = (ICollection<KeyValuePair<TKey,TValue>>)h.Dict;
                for(var i = 0; i<Count; i++) _ = coll.Contains(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.Contains(KVP)",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.ContainsKey"),MemberData(nameof(Matrix))]
    public void ContainsKey(int parallel,double mutableFraction) {
        const int Count = 2_400_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                for(var i = 0; i<Count; i++) _ = h.Dict.ContainsKey(this.CreateKey(i));
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.ContainsKey",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.foreach"),MemberData(nameof(Matrix))]
    public void Enumerate_foreach(int parallel,double mutableFraction) {
        const int Count = 1_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { var n = 0; foreach(var kv in h.Dict) n++; },
            teardown: h => h.Dispose(),
            label: "Dictionary.foreach",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Count"),MemberData(nameof(Matrix))]
    public void Count_(int parallel,double mutableFraction) {
        const int Count = 1_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { _ = h.Dict.Count; },
            teardown: h => h.Dispose(),
            label: "Dictionary.Count",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Keys"),MemberData(nameof(Matrix))]
    public void Keys_(int parallel,double mutableFraction) {
        const int Count = 1_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { _ = h.Dict.Keys; },
            teardown: h => h.Dispose(),
            label: "Dictionary.Keys",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.Values"),MemberData(nameof(Matrix))]
    public void Values_(int parallel,double mutableFraction) {
        const int Count = 1_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { _ = h.Dict.Values; },
            teardown: h => h.Dispose(),
            label: "Dictionary.Values",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.ToList"),MemberData(nameof(Matrix))]
    public void ToList_(int parallel,double mutableFraction) {
        const int Count = 700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { _ = System.Linq.Enumerable.ToList(h.Dict); },
            teardown: h => h.Dispose(),
            label: "Dictionary.ToList",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.get_Item"),MemberData(nameof(Matrix))]
    public void Indexer_Get(int parallel,double mutableFraction) {
        const int Count = 2_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                for(var i = 0; i<Count; i++) _ = h.Dict[this.CreateKey(i)];
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.get_Item",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","Dictionary.set_Item"),MemberData(nameof(Matrix))]
    public void Indexer_Set(int parallel,double mutableFraction) {
        const int Count = 2_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                for(var i = 0; i<Count; i++) h.Dict[this.CreateKey(i)]=this.CreateValue(i+1);
            },
            teardown: h => h.Dispose(),
            label: "Dictionary.set_Item",
            measuredOpsTotal: (long)parallel*Count);
    }

    // ──────────────────────────────────────────────────────────────────
    // IDictionary<TKey,TValue> 経由
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","IDictionary.Add"),MemberData(nameof(Matrix))]
    public void IDictionary_Add(int parallel,double mutableFraction) {
        const int Count = 2_400_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => this.CreateStoreAndDict(mutableFraction,Count),
            measure: (t,h) => {
                var d = (IDictionary<TKey,TValue>)h.Dict;
                for(var i = 0; i<Count; i++) d.Add(this.CreateKey(i),this.CreateValue(i));
            },
            teardown: h => h.Dispose(),
            label: "IDictionary.Add",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IDictionary.ContainsKey"),MemberData(nameof(Matrix))]
    public void IDictionary_ContainsKey(int parallel,double mutableFraction) {
        const int Count = 2_700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var d = (IDictionary<TKey,TValue>)h.Dict;
                for(var i = 0; i<Count; i++) _ = d.ContainsKey(this.CreateKey(i));
            },
            teardown: h => h.Dispose(),
            label: "IDictionary.ContainsKey",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IDictionary.Remove"),MemberData(nameof(Matrix))]
    public void IDictionary_Remove(int parallel,double mutableFraction) {
        const int Count = 2_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var d = (IDictionary<TKey,TValue>)h.Dict;
                for(var i = 0; i<Count; i++) _ = d.Remove(this.CreateKey(i));
            },
            teardown: h => h.Dispose(),
            label: "IDictionary.Remove",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IDictionary.TryGetValue"),MemberData(nameof(Matrix))]
    public void IDictionary_TryGetValue(int parallel,double mutableFraction) {
        const int Count = 2_700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var d = (IDictionary<TKey,TValue>)h.Dict;
                for(var i = 0; i<Count; i++) d.TryGetValue(this.CreateKey(i),out _);
            },
            teardown: h => h.Dispose(),
            label: "IDictionary.TryGetValue",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IDictionary.Keys"),MemberData(nameof(Matrix))]
    public void IDictionary_Keys(int parallel,double mutableFraction) {
        const int Count = 1_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { var d = (IDictionary<TKey,TValue>)h.Dict; _ = d.Keys; },
            teardown: h => h.Dispose(),
            label: "IDictionary.Keys",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IDictionary.Values"),MemberData(nameof(Matrix))]
    public void IDictionary_Values(int parallel,double mutableFraction) {
        const int Count = 1_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { var d = (IDictionary<TKey,TValue>)h.Dict; _ = d.Values; },
            teardown: h => h.Dispose(),
            label: "IDictionary.Values",
            measuredOpsTotal: parallel);
    }

    // ──────────────────────────────────────────────────────────────────
    // ICollection<KVP> 経由
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Add"),MemberData(nameof(Matrix))]
    public void ICollection_Add(int parallel,double mutableFraction) {
        const int Count = 2_700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => this.CreateStoreAndDict(mutableFraction,Count),
            measure: (t,h) => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)h.Dict;
                for(var i = 0; i<Count; i++) c.Add(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            teardown: h => h.Dispose(),
            label: "ICollection.Add",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Clear"),MemberData(nameof(Matrix))]
    public void ICollection_Clear(int parallel,double mutableFraction) {
        const int Count = 5_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { var c = (ICollection<KeyValuePair<TKey,TValue>>)h.Dict; c.Clear(); },
            teardown: h => h.Dispose(),
            label: "ICollection.Clear",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Contains"),MemberData(nameof(Matrix))]
    public void ICollection_Contains(int parallel,double mutableFraction) {
        const int Count = 2_800_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)h.Dict;
                for(var i = 0; i<Count; i++) _ = c.Contains(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            teardown: h => h.Dispose(),
            label: "ICollection.Contains",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.CopyTo"),MemberData(nameof(Matrix))]
    public void ICollection_CopyTo(int parallel,double mutableFraction) {
        const int Count = 700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)h.Dict;
                var arr = new KeyValuePair<TKey,TValue>[c.Count];
                c.CopyTo(arr,0);
            },
            teardown: h => h.Dispose(),
            label: "ICollection.CopyTo",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Remove"),MemberData(nameof(Matrix))]
    public void ICollection_Remove(int parallel,double mutableFraction) {
        const int Count = 2_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)h.Dict;
                for(var i = 0; i<Count; i++) _ = c.Remove(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            teardown: h => h.Dispose(),
            label: "ICollection.Remove",
            measuredOpsTotal: (long)parallel*Count);
    }

    // ──────────────────────────────────────────────────────────────────
    // IReadOnlyCollection<KVP>.Count / IReadOnlyDictionary メンチE
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","IReadOnlyCollection.Count"),MemberData(nameof(Matrix))]
    public void IReadOnlyCollection_Count(int parallel,double mutableFraction) {
        const int Count = 1_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { var c = (IReadOnlyCollection<KeyValuePair<TKey,TValue>>)h.Dict; _ = c.Count; },
            teardown: h => h.Dispose(),
            label: "IReadOnlyCollection.Count",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IReadOnlyDictionary.get_Item"),MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Indexer_Get(int parallel,double mutableFraction) {
        const int Count = 2_600_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var d = (IReadOnlyDictionary<TKey,TValue>)h.Dict;
                for(var i = 0; i<Count; i++) _ = d[this.CreateKey(i)];
            },
            teardown: h => h.Dispose(),
            label: "IReadOnlyDictionary.get_Item",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IReadOnlyDictionary.ContainsKey"),MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_ContainsKey(int parallel,double mutableFraction) {
        const int Count = 2_600_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var d = (IReadOnlyDictionary<TKey,TValue>)h.Dict;
                for(var i = 0; i<Count; i++) _ = d.ContainsKey(this.CreateKey(i));
            },
            teardown: h => h.Dispose(),
            label: "IReadOnlyDictionary.ContainsKey",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IReadOnlyDictionary.TryGetValue"),MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_TryGetValue(int parallel,double mutableFraction) {
        const int Count = 2_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var d = (IReadOnlyDictionary<TKey,TValue>)h.Dict;
                for(var i = 0; i<Count; i++) d.TryGetValue(this.CreateKey(i),out _);
            },
            teardown: h => h.Dispose(),
            label: "IReadOnlyDictionary.TryGetValue",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IReadOnlyDictionary.Keys"),MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Keys(int parallel,double mutableFraction) {
        const int Count = 5_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { var d = (IReadOnlyDictionary<TKey,TValue>)h.Dict; _ = d.Keys; },
            teardown: h => h.Dispose(),
            label: "IReadOnlyDictionary.Keys",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IReadOnlyDictionary.Values"),MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Values(int parallel,double mutableFraction) {
        const int Count = 5_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => { var d = (IReadOnlyDictionary<TKey,TValue>)h.Dict; _ = d.Values; },
            teardown: h => h.Dispose(),
            label: "IReadOnlyDictionary.Values",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","IEnumerable.GetEnumerator"),MemberData(nameof(Matrix))]
    public void IEnumerable_GetEnumerator(int parallel,double mutableFraction) {
        const int Count = 1_400_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndDict(mutableFraction,Count); this.Populate(h.Dict,Count); return h; },
            measure: (t,h) => {
                var e = ((IEnumerable)h.Dict).GetEnumerator();
                try { while(e.MoveNext()) { } } finally { if(e is IDisposable d) d.Dispose(); }
            },
            teardown: h => h.Dispose(),
            label: "IEnumerable.GetEnumerator",
            measuredOpsTotal: (long)parallel*Count);
    }
}
