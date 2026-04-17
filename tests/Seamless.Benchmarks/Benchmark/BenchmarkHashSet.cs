using System.Collections;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Seamless.Collections.Benchmarks;

/// <summary>
/// HashSet 用の (Store, HashSet, 他�E HashSet, チE��イス, 一時ディレクトリ) をまとめたハンドル、E
/// 雁E��演算用に補助 HashSet <see cref="Other"/> を持つ、E
/// </summary>
public sealed class HashSetHandle<T> : IDisposable where T:IEquatable<T> {
    public required IDisposable Store { get; init; }
    public required ISeamlessHashSet<T> Set { get; init; }
    public IDisposable? OtherStore { get; init; }
    public ISeamlessHashSet<T>? Other { get; init; }
    public string? Dir { get; init; }
    public IDevice? Log { get; init; }
    public IDevice? Obj { get; init; }
    public IDevice? LogOther { get; init; }
    public IDevice? ObjOther { get; init; }
    public void Dispose() {
        this.Set.Dispose();
        this.Store.Dispose();
        this.Other?.Dispose();
        this.OtherStore?.Dispose();
        this.Log?.Dispose();
        this.Obj?.Dispose();
        this.LogOther?.Dispose();
        this.ObjOther?.Dispose();
        if(this.Dir is not null && Directory.Exists(this.Dir)) {
            try { Directory.Delete(this.Dir,recursive: true); } catch { }
        }
    }
}

/// <summary>
/// HashSet ベンチ�Eークの generic 基底、EPI メソチE��ごとに <c>[Theory]</c> を定義する、E
/// 派生型で Managed/Unmanaged を型引数で刁E��替え、Esee cref="CreateStoreAndSet"/> を実裁E��る、E
/// </summary>
public abstract class BenchmarkHashSet<T> : BenchmarkBase where T:IEquatable<T> {
    protected BenchmarkHashSet(ITestOutputHelper output) : base(output) { }

    protected abstract T CreateKey(int seed);

    /// <summary>雁E��演算用に 2 つ目の set が忁E��か (雁E��演算メソチE��のみ true)、E/summary>
    protected abstract HashSetHandle<T> CreateStoreAndSet(double mutableFraction,int elementCount,bool needsOther);

    protected void Populate(ISeamlessHashSet<T> set,int count,int offset = 0) {
        for(var i = 0; i<count; i++) set.Add(this.CreateKey(i+offset));
    }

    // ──────────────────────────────────────────────────────────────────
    // ISeamlessHashSet (= ICollection<T>) 直接呼び出ぁE
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.Add"),MemberData(nameof(Matrix))]
    public void Add(int parallel,double mutableFraction) {
        const int Count = 3_600_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => this.CreateStoreAndSet(mutableFraction,Count,needsOther: false),
            measure: (t,h) => { for(var i = 0; i<Count; i++) h.Set.Add(this.CreateKey(i)); },
            teardown: h => h.Dispose(),
            label: "HashSet.Add",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.Remove"),MemberData(nameof(Matrix))]
    public void Remove(int parallel,double mutableFraction) {
        const int Count = 3_200_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { for(var i = 0; i<Count; i++) h.Set.Remove(this.CreateKey(i)); },
            teardown: h => h.Dispose(),
            label: "HashSet.Remove",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.Contains"),MemberData(nameof(Matrix))]
    public void Contains(int parallel,double mutableFraction) {
        const int Count = 3_500_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { for(var i = 0; i<Count; i++) _ = h.Set.Contains(this.CreateKey(i)); },
            teardown: h => h.Dispose(),
            label: "HashSet.Contains",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.CopyTo"),MemberData(nameof(Matrix))]
    public void CopyTo(int parallel,double mutableFraction) {
        const int Count = 800_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { var arr = new T[h.Set.Count]; h.Set.CopyTo(arr,0); },
            teardown: h => h.Dispose(),
            label: "HashSet.CopyTo",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.foreach"),MemberData(nameof(Matrix))]
    public void Enumerate_foreach(int parallel,double mutableFraction) {
        const int Count = 1_600_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { var n = 0; foreach(var v in h.Set) n++; },
            teardown: h => h.Dispose(),
            label: "HashSet.foreach",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.Count"),MemberData(nameof(Matrix))]
    public void Count_(int parallel,double mutableFraction) {
        const int Count = 1_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { _ = h.Set.Count; },
            teardown: h => h.Dispose(),
            label: "HashSet.Count",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.Clear"),MemberData(nameof(Matrix))]
    public void Clear(int parallel,double mutableFraction) {
        const int Count = 5_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => h.Set.Clear(),
            teardown: h => h.Dispose(),
            label: "HashSet.Clear",
            measuredOpsTotal: (long)parallel*Count);
    }

    // ──────────────────────────────────────────────────────────────────
    // 雁E��演箁E呼び出し�E 1 回、要素数は Count)
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.IntersectWith"),MemberData(nameof(Matrix))]
    public void IntersectWith(int parallel,double mutableFraction) {
        const int Count = 640_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,needsOther: true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => h.Set.IntersectWith(h.Other!),
            teardown: h => h.Dispose(),
            label: "HashSet.IntersectWith",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.SymmetricExceptWith"),MemberData(nameof(Matrix))]
    public void SymmetricExceptWith(int parallel,double mutableFraction) {
        const int Count = 740_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => h.Set.SymmetricExceptWith(h.Other!),
            teardown: h => h.Dispose(),
            label: "HashSet.SymmetricExceptWith",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.UnionWith"),MemberData(nameof(Matrix))]
    public void UnionWith(int parallel,double mutableFraction) {
        const int Count = 900_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => h.Set.UnionWith(h.Other!),
            teardown: h => h.Dispose(),
            label: "HashSet.UnionWith",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.ExceptWith"),MemberData(nameof(Matrix))]
    public void ExceptWith(int parallel,double mutableFraction) {
        const int Count = 700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => h.Set.ExceptWith(h.Other!),
            teardown: h => h.Dispose(),
            label: "HashSet.ExceptWith",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.IsProperSubsetOf"),MemberData(nameof(Matrix))]
    public void IsProperSubsetOf(int parallel,double mutableFraction) {
        const int Count = 1_700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => { _ = h.Set.IsProperSubsetOf(h.Other!); },
            teardown: h => h.Dispose(),
            label: "HashSet.IsProperSubsetOf",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.IsProperSupersetOf"),MemberData(nameof(Matrix))]
    public void IsProperSupersetOf(int parallel,double mutableFraction) {
        const int Count = 1_700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => { _ = h.Set.IsProperSupersetOf(h.Other!); },
            teardown: h => h.Dispose(),
            label: "HashSet.IsProperSupersetOf",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.IsSubsetOf"),MemberData(nameof(Matrix))]
    public void IsSubsetOf(int parallel,double mutableFraction) {
        const int Count = 2_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => { _ = h.Set.IsSubsetOf(h.Other!); },
            teardown: h => h.Dispose(),
            label: "HashSet.IsSubsetOf",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.IsSupersetOf"),MemberData(nameof(Matrix))]
    public void IsSupersetOf(int parallel,double mutableFraction) {
        const int Count = 1_700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => { _ = h.Set.IsSupersetOf(h.Other!); },
            teardown: h => h.Dispose(),
            label: "HashSet.IsSupersetOf",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.Overlaps"),MemberData(nameof(Matrix))]
    public void Overlaps(int parallel,double mutableFraction) {
        const int Count = 2_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => { _ = h.Set.Overlaps(h.Other!); },
            teardown: h => h.Dispose(),
            label: "HashSet.Overlaps",
            measuredOpsTotal: parallel);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","HashSet.SetEquals"),MemberData(nameof(Matrix))]
    public void SetEquals(int parallel,double mutableFraction) {
        const int Count = 1_700_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => {
                var h = this.CreateStoreAndSet(mutableFraction,Count,true);
                this.Populate(h.Set,Count);
                this.Populate(h.Other!,Count,offset: Count/2);
                return h;
            },
            measure: (t,h) => { _ = h.Set.SetEquals(h.Other!); },
            teardown: h => h.Dispose(),
            label: "HashSet.SetEquals",
            measuredOpsTotal: parallel);
    }

    // ──────────────────────────────────────────────────────────────────
    // ICollection<T> 経由
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Add"),MemberData(nameof(Matrix))]
    public void ICollection_Add(int parallel,double mutableFraction) {
        const int Count = 3_800_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => this.CreateStoreAndSet(mutableFraction,Count,false),
            measure: (t,h) => { var c = (ICollection<T>)h.Set; for(var i = 0; i<Count; i++) c.Add(this.CreateKey(i)); },
            teardown: h => h.Dispose(),
            label: "HashSet.ICollection.Add",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Clear"),MemberData(nameof(Matrix))]
    public void ICollection_Clear(int parallel,double mutableFraction) {
        const int Count = 5_000_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { var c = (ICollection<T>)h.Set; c.Clear(); },
            teardown: h => h.Dispose(),
            label: "HashSet.ICollection.Clear",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Contains"),MemberData(nameof(Matrix))]
    public void ICollection_Contains(int parallel,double mutableFraction) {
        const int Count = 3_100_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { var c = (ICollection<T>)h.Set; for(var i = 0; i<Count; i++) _ = c.Contains(this.CreateKey(i)); },
            teardown: h => h.Dispose(),
            label: "HashSet.ICollection.Contains",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.CopyTo"),MemberData(nameof(Matrix))]
    public void ICollection_CopyTo(int parallel,double mutableFraction) {
        const int Count = 800_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { var c = (ICollection<T>)h.Set; var arr = new T[c.Count]; c.CopyTo(arr,0); },
            teardown: h => h.Dispose(),
            label: "HashSet.ICollection.CopyTo",
            measuredOpsTotal: (long)parallel*Count);
    }

    [Theory,Trait("Category","Benchmark"),Trait("Api","ICollection.Remove"),MemberData(nameof(Matrix))]
    public void ICollection_Remove(int parallel,double mutableFraction) {
        const int Count = 3_300_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => { var c = (ICollection<T>)h.Set; for(var i = 0; i<Count; i++) _ = c.Remove(this.CreateKey(i)); },
            teardown: h => h.Dispose(),
            label: "HashSet.ICollection.Remove",
            measuredOpsTotal: (long)parallel*Count);
    }

    // ──────────────────────────────────────────────────────────────────
    // IReadOnlyCollection<T>.Count / IEnumerable
    // ──────────────────────────────────────────────────────────────────

    [Theory,Trait("Category","Benchmark"),Trait("Api","IEnumerable.GetEnumerator"),MemberData(nameof(Matrix))]
    public void IEnumerable_GetEnumerator(int parallel,double mutableFraction) {
        const int Count = 1_600_000 * BenchmarkScale.Scale;
        this.RunParallel(parallel,
            setupPerThread: t => { var h = this.CreateStoreAndSet(mutableFraction,Count,false); this.Populate(h.Set,Count); return h; },
            measure: (t,h) => {
                var e = ((IEnumerable)h.Set).GetEnumerator();
                try { while(e.MoveNext()) { } } finally { if(e is IDisposable d) d.Dispose(); }
            },
            teardown: h => h.Dispose(),
            label: "HashSet.IEnumerable.GetEnumerator",
            measuredOpsTotal: (long)parallel*Count);
    }
}
