using Tsavorite.Collections;
using Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Benchmark.Tsavorite.Collections;

/// <summary>
/// HashSet ベンチマークの共通土台。
/// <see cref="HashSet{T}"/> / <see cref="UnmanagedHashSet{T}"/> が
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
    /// parallel ? 8 (2×8=16 ? MaxInstances) に制限する。
    /// </summary>
    public static TheoryData<int,double,long,long,int,int> MatrixPairs {
        get {
            var data = new TheoryData<int,double,long,long,int,int>();
            data.Add(1,0.5,64,1L<<12,14,12);
            if(BenchmarkRunSettings.RunFullMatrix) {
                data.Add(1,1.0,64,1L<<12,14,12);
                data.Add(4,0.5,64,1L<<12,14,12);
                data.Add(1,0.5,1L<<10,1L<<20,16,12);
            }
            return data;
        }
    }

    /// <summary>1 スレッド = 1 Store = 1 HashSet のスコープ。呼び出し側で using する。</summary>
    protected abstract Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits);

    protected abstract class Handle:IDisposable {
        public abstract IHashSet<TKey> Set { get; }
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
        long indexSize,
        long segmentSize,
        int memorySizeBits,
        int pageSizeBits,
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
                indexSize,
                segmentSize,
                memorySizeBits,
                pageSizeBits,
                opCount,
                setup: id => {
                    var h = this.CreateHandle(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
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
        long indexSize,
        long segmentSize,
        int memorySizeBits,
        int pageSizeBits,
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
                indexSize,
                segmentSize,
                memorySizeBits,
                pageSizeBits,
                opCount,
                setup: id => {
                    var h = this.CreateHandle(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
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
    public void HashSet_Add(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: false,
            h => { var n = Scaled; for(var i = 0; i<n; i++) h.Set.Add(this.CreateKey(i)); },
            nameof(HashSet_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_Remove(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: true,
            h => { var n = Scaled; for(var i = 0; i<n; i++) h.Set.Remove(this.CreateKey(i)); },
            nameof(HashSet_Remove));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_Contains(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: true,
            h => { var n = Scaled; for(var i = 0; i<n; i++) _=h.Set.Contains(this.CreateKey(i)); },
            nameof(HashSet_Contains));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_CopyTo(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefill: true,
            h => { var buf = new TKey[(int)Scaled]; h.Set.CopyTo(buf,0); },
            nameof(HashSet_CopyTo));

    [Theory, MemberData(nameof(Matrix))]
    public void HashSet_Foreach(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: true,
            h => { foreach(var _ in h.Set) { } },
            nameof(HashSet_Foreach));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IntersectWith(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => h.IntersectWith(other),
            nameof(HashSet_IntersectWith));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_SymmetricExceptWith(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => h.SymmetricExceptWith(other),
            nameof(HashSet_SymmetricExceptWith));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsProperSubsetOf(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsProperSubsetOf(other); },
            nameof(HashSet_IsProperSubsetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsProperSupersetOf(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsProperSupersetOf(other); },
            nameof(HashSet_IsProperSupersetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsSubsetOf(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsSubsetOf(other); },
            nameof(HashSet_IsSubsetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_IsSupersetOf(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.IsSupersetOf(other); },
            nameof(HashSet_IsSupersetOf));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_Overlaps(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.Overlaps(other); },
            nameof(HashSet_Overlaps));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void HashSet_SetEquals(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (h,other) => { _=h.SetEquals(other); },
            nameof(HashSet_SetEquals));

    // =============================================================
    // ICollection<T>
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Add(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: false,
            h => {
                var c = (ICollection<TKey>)h.Set;
                var n = Scaled;
                for(var i = 0; i<n; i++) c.Add(this.CreateKey(i));
            },
            nameof(ICollection_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Clear(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefill: true,
            h => ((ICollection<TKey>)h.Set).Clear(),
            nameof(ICollection_Clear));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Contains(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: true,
            h => {
                var c = (ICollection<TKey>)h.Set;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Contains(this.CreateKey(i));
            },
            nameof(ICollection_Contains));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_CopyTo(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefill: true,
            h => {
                var buf = new TKey[(int)Scaled];
                ((ICollection<TKey>)h.Set).CopyTo(buf,0);
            },
            nameof(ICollection_CopyTo));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Remove(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: true,
            h => {
                var c = (ICollection<TKey>)h.Set;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Remove(this.CreateKey(i));
            },
            nameof(ICollection_Remove));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Count(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefill: true,
            h => { _=((ICollection<TKey>)h.Set).Count; },
            nameof(ICollection_Count));

    // =============================================================
    // IReadOnlyCollection<T>
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyCollection_Count(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefill: true,
            h => {
                if(h.Set is IReadOnlyCollection<TKey> ro) _=ro.Count;
                else _=((ICollection<TKey>)h.Set).Count;
            },
            nameof(IReadOnlyCollection_Count));
}

// ============================================================================
// 具体派生: Managed / Unmanaged HashSet
// ============================================================================

[Trait("Category","Benchmark"), Trait("Kind","Managed"), Trait("Collection","HashSet")]
public sealed class ManagedHashSetBenchmark(ITestOutputHelper output):HashSetBenchmark<string>(output) {
    protected override string CreateKey(int seed) => $"K{seed}";
    protected override Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
        return new H(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
    }
    private sealed class H:Handle {
        private readonly HashSetStore<string> store;
        private readonly global::Tsavorite.Collections.HashSet<string> set;
        private readonly double mf;
        private readonly long indexSize;
        private readonly long segmentSize;
        private readonly int memorySizeBits;
        private readonly int pageSizeBits;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        private static int directorySequence;
        public H(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
            this.mf=mutableFraction;
            this.indexSize=indexSize;
            this.segmentSize=segmentSize;
            this.memorySizeBits=memorySizeBits;
            this.pageSizeBits=pageSizeBits;
            var directoryId = Interlocked.Increment(ref directorySequence);
            this.rootDir=BenchmarkStorePath.Create<ManagedHashSetBenchmark>($"h{directoryId:D4}");
            var checkpointDir = Path.Combine(this.rootDir,"checkpoint");
            var logPath = Path.Combine(this.rootDir,"log");
            var objPath = Path.Combine(this.rootDir,"obj");
            Directory.CreateDirectory(checkpointDir);
            this.logDevice=Devices.CreateLogDevice(logPath);
            this.objDevice=Devices.CreateLogDevice(objPath);
            this.store=HashStore.Create(
                IndexSize: indexSize,
                PageSize: 1L<<pageSizeBits,
                SegmentSize: segmentSize,
                MemorySize: 1L<<memorySizeBits,
                MutableFraction: mutableFraction,
                CheckpointDir: checkpointDir
            ).Create<string>(this.logDevice,this.objDevice,new MemoryPackSerializerStrategy());
            this.set=this.store.Create();
        }
        public override IHashSet<string> Set => this.set;
        public override void Dispose() {
            try {
                this.set.Dispose();
            }
            finally {
                try {
                    this.store.Store.Log.FlushAndEvict(wait: true);
                }
                finally {
                    try {
                        this.store.Dispose();
                    }
                    finally {
                        try {
                            this.logDevice.Dispose();
                        }
                        finally {
                            try {
                                this.objDevice.Dispose();
                            }
                            finally {
                                try { Directory.Delete(this.rootDir,true); } catch { }
                            }
                        }
                    }
                }
            }
        }
        public override Handle CreateSibling() => new H(this.mf,this.indexSize,this.segmentSize,this.memorySizeBits,this.pageSizeBits);
        private static global::Tsavorite.Collections.HashSet<string> C(Handle h) => ((H)h).set;
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

[Trait("Category","Benchmark"), Trait("Kind","Unmanaged"), Trait("Collection","HashSet")]
public sealed class UnmanagedHashSetBenchmark(ITestOutputHelper output):HashSetBenchmark<int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
        return new H(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
    }
    private sealed class H:Handle {
        private readonly HashSetStore<int> store;
        private readonly global::Tsavorite.Collections.HashSet<int> set;
        private readonly double mf;
        private readonly long indexSize;
        private readonly long segmentSize;
        private readonly int memorySizeBits;
        private readonly int pageSizeBits;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        private static int directorySequence;
        public H(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
            this.mf=mutableFraction;
            this.indexSize=indexSize;
            this.segmentSize=segmentSize;
            this.memorySizeBits=memorySizeBits;
            this.pageSizeBits=pageSizeBits;
            var directoryId = Interlocked.Increment(ref directorySequence);
            this.rootDir=BenchmarkStorePath.Create<UnmanagedHashSetBenchmark>($"h{directoryId:D4}");
            var checkpointDir = Path.Combine(this.rootDir,"checkpoint");
            var logPath = Path.Combine(this.rootDir,"log");
            var objPath = Path.Combine(this.rootDir,"obj");
            Directory.CreateDirectory(checkpointDir);
            this.logDevice=Devices.CreateLogDevice(logPath);
            this.objDevice=Devices.CreateLogDevice(objPath);
            this.store=HashStore.Create(
                IndexSize: indexSize,
                PageSize: 1L<<pageSizeBits,
                SegmentSize: segmentSize,
                MemorySize: 1L<<memorySizeBits,
                MutableFraction: mutableFraction,
                CheckpointDir: checkpointDir
            ).Create<int>(this.logDevice,this.objDevice,new BlittableStrategy());
            this.set=this.store.Create();
        }
        public override IHashSet<int> Set => this.set;
        public override void Dispose() {
            try {
                this.set.Dispose();
            }
            finally {
                try {
                    this.store.Store.Log.FlushAndEvict(wait: true);
                }
                finally {
                    try {
                        this.store.Dispose();
                    }
                    finally {
                        try {
                            this.logDevice.Dispose();
                        }
                        finally {
                            try {
                                this.objDevice.Dispose();
                            }
                            finally {
                                try { Directory.Delete(this.rootDir,true); } catch { }
                            }
                        }
                    }
                }
            }
        }
        public override Handle CreateSibling() => new H(this.mf,this.indexSize,this.segmentSize,this.memorySizeBits,this.pageSizeBits);
        private static global::Tsavorite.Collections.HashSet<int> C(Handle h) => ((H)h).set;
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
