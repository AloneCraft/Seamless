using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.Benchmarks;

/// <summary>
/// HashSet ベンチマークの共通土台。
/// <see cref="ManagedHashSet{T}"/> / <see cref="UnmanagedHashSet{T}"/> が
/// 提供する集合演算および <see cref="ICollection{T}"/> / <see cref="IReadOnlyCollection{T}"/>
/// 経由の操作を計測する。
/// 集合演算 (IntersectWith/SymmetricExceptWith/IsXxxOf/Overlaps/SetEquals) は
/// 各 HashSet 具象型固有のオーバーロードを計測するため、ハンドル経由で委譲する。
/// </summary>
public abstract class HashSetBenchmark<TKey>(ITestOutputHelper output):Benchmark(output)
    where TKey : IEquatable<TKey> {
    protected abstract TKey CreateKey(int seed);

    /// <summary>
    /// Drive2 用 Matrix。
    /// Tsavorite の LightEpoch インスタンス上限 (MaxInstances=16) により
    /// Drive2 は primary + sibling = 2×parallel 個のストアを同時保持するため
    /// parallel ≤ 8 (2×8=16 ≤ MaxInstances) に制限する。
    /// </summary>
    public static TheoryData<int,double> MatrixPairs {
        get {
            var data = new TheoryData<int,double>();
            foreach(var p in new[] { 1,2,4,8 })
                foreach(var mf in new[] { 0.0,0.5,1.0 })
                    data.Add(p,mf);
            return data;
        }
    }

    /// <summary>1 スレッド = 1 Store = 1 HashSet のスコープ。呼び出し側で using する。</summary>
    protected abstract Handle CreateHandle(double mutableFraction);

    protected abstract class Handle:IDisposable {
        public abstract ISeamlessHashSet<TKey> Set { get; }
        public abstract void Dispose();
        public abstract Handle CreateSibling();
        public abstract void IntersectWith(Handle other);
        public abstract void SymmetricExceptWith(Handle other);
        public abstract bool IsSubsetOf(Handle other);
        public abstract bool IsSupersetOf(Handle other);
        public abstract bool IsProperSubsetOf(Handle other);
        public abstract bool IsProperSupersetOf(Handle other);
        public abstract bool Overlaps(Handle other);
        public abstract bool SetEquals(Handle other);
    }

    private void Drive(
        int parallel,
        double mutableFraction,
        long opCount,
        bool prefill,
        Action<Handle> measured,
        string label
    ) {
        var primary = new Handle?[parallel];
        try {
            this.RunMeasured(
                parallel,
                mutableFraction,
                opCount,
                setup: id => {
                    var h = this.CreateHandle(mutableFraction);
                    primary[id]=h;
                    if(prefill) {
                        var n = Scaled;
                        for(var i = 0; i<n; i++) h.Set.Add(this.CreateKey(i));
                    }
                },
                measured: id => measured(primary[id]!),
                label: label);
        }
        finally {
            foreach(var h in primary) h?.Dispose();
        }
    }

    private void Drive2(
        int parallel,
        double mutableFraction,
        long opCount,
        bool prefillPrimary,
        bool prefillSibling,
        Action<Handle,Handle> measured,
        string label
    ) {
        var primary = new Handle?[parallel];
        var sibling = new Handle?[parallel];
        try {
            this.RunMeasured(
                parallel,
                mutableFraction,
                opCount,
                setup: id => {
                    var h = this.CreateHandle(mutableFraction);
                    primary[id]=h;
                    if(prefillPrimary) {
                        var n = Scaled;
                        for(var i = 0; i<n; i++) h.Set.Add(this.CreateKey(i));
                    }
                    var s = h.CreateSibling();
                    sibling[id]=s;
                    if(prefillSibling) {
                        var n = Scaled;
                        for(var i = 0; i<n; i++) s.Set.Add(this.CreateKey(i));
                    }
                },
                measured: id => measured(primary[id]!,sibling[id]!),
                label: label);
        }
        finally {
            foreach(var h in primary) h?.Dispose();
            foreach(var h in sibling) h?.Dispose();
        }
    }

    // =============================================================
    // 基本操作 (HashSet API)
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_Add(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,prefill: false,
            h => { var n = Scaled; for(var i = 0; i<n; i++) h.Set.Add(this.CreateKey(i)); },
            nameof(this.HashSet_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_Remove(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,prefill: true,
            h => { var n = Scaled; for(var i = 0; i<n; i++) h.Set.Remove(this.CreateKey(i)); },
            nameof(this.HashSet_Remove));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_Contains(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,prefill: true,
            h => { var n = Scaled; for(var i = 0; i<n; i++) _=h.Set.Contains(this.CreateKey(i)); },
            nameof(this.HashSet_Contains));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_CopyTo(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,prefill: true,
            h => { var buf = new TKey[(int)Scaled]; h.Set.CopyTo(buf,0); },
            nameof(this.HashSet_CopyTo));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_Foreach(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,prefill: true,
            h => { foreach(var _ in h.Set) { } },
            nameof(this.HashSet_Foreach));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IntersectWith(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => h.IntersectWith(other),
            nameof(this.HashSet_IntersectWith));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_SymmetricExceptWith(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => h.SymmetricExceptWith(other),
            nameof(this.HashSet_SymmetricExceptWith));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsProperSubsetOf(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsProperSubsetOf(other); },
            nameof(this.HashSet_IsProperSubsetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsProperSupersetOf(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsProperSupersetOf(other); },
            nameof(this.HashSet_IsProperSupersetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsSubsetOf(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsSubsetOf(other); },
            nameof(this.HashSet_IsSubsetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsSupersetOf(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsSupersetOf(other); },
            nameof(this.HashSet_IsSupersetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_Overlaps(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.Overlaps(other); },
            nameof(this.HashSet_Overlaps));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_SetEquals(int parallel,double mutableFraction)
        => this.Drive2(parallel,mutableFraction,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.SetEquals(other); },
            nameof(this.HashSet_SetEquals));

    // =============================================================
    // ICollection<T>
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Add(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,prefill: false,
            h => {
                var c = (ICollection<TKey>)h.Set;
                var n = Scaled;
                for(var i = 0; i<n; i++) c.Add(this.CreateKey(i));
            },
            nameof(this.ICollection_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Clear(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,prefill: true,
            h => ((ICollection<TKey>)h.Set).Clear(),
            nameof(this.ICollection_Clear));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Contains(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,prefill: true,
            h => {
                var c = (ICollection<TKey>)h.Set;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Contains(this.CreateKey(i));
            },
            nameof(this.ICollection_Contains));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_CopyTo(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,prefill: true,
            h => {
                var buf = new TKey[(int)Scaled];
                ((ICollection<TKey>)h.Set).CopyTo(buf,0);
            },
            nameof(this.ICollection_CopyTo));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Remove(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,prefill: true,
            h => {
                var c = (ICollection<TKey>)h.Set;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Remove(this.CreateKey(i));
            },
            nameof(this.ICollection_Remove));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Count(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,prefill: true,
            h => { _=((ICollection<TKey>)h.Set).Count; },
            nameof(this.ICollection_Count));

    // =============================================================
    // IReadOnlyCollection<T>
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyCollection_Count(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,prefill: true,
            h => {
                if(h.Set is IReadOnlyCollection<TKey> ro) _=ro.Count;
                else _=((ICollection<TKey>)h.Set).Count;
            },
            nameof(this.IReadOnlyCollection_Count));
}

// ============================================================================
// 具体派生: Managed / Unmanaged HashSet
// ============================================================================

[Trait("Kind","Managed"), Trait("Collection","HashSet")]
public sealed class ManagedHashSetBenchmark(ITestOutputHelper output):HashSetBenchmark<string>(output) {
    protected override string CreateKey(int seed) => $"K{seed}";
    protected override Handle CreateHandle(double mutableFraction) {
        return new H(mutableFraction);
    }
    private sealed class H:Handle {
        private readonly ManagedUnmanagedHashSetStore<string> store;
        private readonly ManagedUnmanagedHashSet<string> set;
        private readonly double mf;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        public H(double mutableFraction) {
            this.mf=mutableFraction;
            this.rootDir=Path.Combine(Path.GetTempPath(),"Seamless-Benchmark-Test",Guid.NewGuid().ToString("N"));
            var checkpointDir = Path.Combine(this.rootDir,"checkpoint");
            var logPath = Path.Combine(this.rootDir,"log");
            var objPath = Path.Combine(this.rootDir,"obj");
            Directory.CreateDirectory(checkpointDir);
            this.logDevice=Devices.CreateLogDevice(logPath);
            this.objDevice=Devices.CreateLogDevice(objPath);
            this.store=Factory.Create(
                IndexSize: IndexSize,
                MutableFraction: mutableFraction,
                MemorySize: global::Seamless.Const.LogMemorySize<<12,
                CheckpointDir: checkpointDir
            ).Create<string>(this.logDevice,this.objDevice,new ManagedTag());
            this.set=this.store.Create();
        }
        public override ISeamlessHashSet<string> Set => this.set;
        public override void Dispose() {
            this.set.Dispose();
            this.store.Store.Log.FlushAndEvict(wait: true);
            this.store.Dispose();
            this.logDevice.Dispose();
            this.objDevice.Dispose();
            try { Directory.Delete(this.rootDir,true); } catch { }
        }
        public override Handle CreateSibling() => new H(this.mf);
        private static ManagedHashSet<string> C(Handle h) => ((H)h).set;
        public override void IntersectWith(Handle other) => this.set.IntersectWith(C(other));
        public override void SymmetricExceptWith(Handle other) => this.set.SymmetricExceptWith(C(other));
        public override bool IsSubsetOf(Handle other) => this.set.IsSubsetOf(C(other));
        public override bool IsSupersetOf(Handle other) => this.set.IsSupersetOf(C(other));
        public override bool IsProperSubsetOf(Handle other) => this.set.IsProperSubsetOf(C(other));
        public override bool IsProperSupersetOf(Handle other) => this.set.IsProperSupersetOf(C(other));
        public override bool Overlaps(Handle other) => this.set.Overlaps(C(other));
        public override bool SetEquals(Handle other) => this.set.SetEquals(C(other));
    }
}

[Trait("Kind","Unmanaged"), Trait("Collection","HashSet")]
public sealed class UnmanagedHashSetBenchmark(ITestOutputHelper output):HashSetBenchmark<int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override Handle CreateHandle(double mutableFraction) {
        return new H(mutableFraction);
    }
    private sealed class H:Handle {
        private readonly UnmanagedUnmanagedHashSetStore<int> store;
        private readonly UnmanagedUnmanagedHashSet<int> set;
        private readonly double mf;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        public H(double mutableFraction) {
            this.mf=mutableFraction;
            this.rootDir=Path.Combine(Path.GetTempPath(),"Seamless-Benchmark-Test",Guid.NewGuid().ToString("N"));
            var checkpointDir = Path.Combine(this.rootDir,"checkpoint");
            var logPath = Path.Combine(this.rootDir,"log");
            var objPath = Path.Combine(this.rootDir,"obj");
            Directory.CreateDirectory(checkpointDir);
            this.logDevice=Devices.CreateLogDevice(logPath);
            this.objDevice=Devices.CreateLogDevice(objPath);
            this.store=Factory.Create(
                IndexSize: IndexSize,
                MutableFraction: mutableFraction,
                MemorySize: global::Seamless.Const.LogMemorySize<<12,
                CheckpointDir: checkpointDir
            ).Create<int>(this.logDevice,this.objDevice,new UnmanagedTag());
            this.set=this.store.Create();
        }
        public override ISeamlessHashSet<int> Set => this.set;
        public override void Dispose() {
            this.set.Dispose();
            this.store.Store.Log.FlushAndEvict(wait: true);
            this.store.Dispose();
            this.logDevice.Dispose();
            this.objDevice.Dispose();
            try { Directory.Delete(this.rootDir,true); } catch { }
        }
        public override Handle CreateSibling() => new H(this.mf);
        private static UnmanagedHashSet<int> C(Handle h) => ((H)h).set;
        public override void IntersectWith(Handle other) => this.set.IntersectWith(C(other));
        public override void SymmetricExceptWith(Handle other) => this.set.SymmetricExceptWith(C(other));
        public override bool IsSubsetOf(Handle other) => this.set.IsSubsetOf(C(other));
        public override bool IsSupersetOf(Handle other) => this.set.IsSupersetOf(C(other));
        public override bool IsProperSubsetOf(Handle other) => this.set.IsProperSubsetOf(C(other));
        public override bool IsProperSupersetOf(Handle other) => this.set.IsProperSupersetOf(C(other));
        public override bool Overlaps(Handle other) => this.set.Overlaps(C(other));
        public override bool SetEquals(Handle other) => this.set.SetEquals(C(other));
    }
}
