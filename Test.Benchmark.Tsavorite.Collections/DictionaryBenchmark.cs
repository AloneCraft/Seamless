using Tsavorite.Stores;
using Tsavorite.core;

namespace Test.Benchmark.Tsavorite.Collections;

/// <summary>
/// Dictionary ベンチマークの共通土台。
/// <see cref="global::Tsavorite.Collections.IDictionary{TKey,TValue}"/> /
/// <see cref="IDictionary{TKey,TValue}"/> /
/// <see cref="ICollection{T}"/> (KeyValuePair) /
/// <see cref="IReadOnlyCollection{T}"/> /
/// <see cref="IReadOnlyDictionary{TKey,TValue}"/> のメソッドを計測する。
/// </summary>
public abstract class DictionaryBenchmark<TKey, TValue>(ITestOutputHelper output):Benchmark(output)
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    protected abstract TKey CreateKey(int seed);
    protected abstract TValue CreateValue(int seed);

    /// <summary>1 スレッド = 1 Store = 1 Dictionary のスコープ。呼び出し側で using する。</summary>
    protected abstract Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits);

    protected abstract class Handle:IDisposable {
        public abstract global::Tsavorite.Collections.IDictionary<TKey,TValue> Dict { get; }
        public abstract void Dispose();
    }

    // ---- 各スレッドが自身で Setup して計測部へ入る共通ドライバ ----
    private void Drive(
        int parallel,
        double mutableFraction,
        long indexSize,
        long segmentSize,
        int memorySizeBits,
        int pageSizeBits,
        long opCount,
        Action<global::Tsavorite.Collections.IDictionary<TKey,TValue>> prefill,
        Action<global::Tsavorite.Collections.IDictionary<TKey,TValue>> measured,
        string label
    ) {
        var handles = new Handle?[parallel];
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
                    handles[id]=this.CreateHandle(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
                    prefill(handles[id]!.Dict);
                },
                measured: id => measured(handles[id]!.Dict),
                label: label
            );
        }
        finally {
            foreach(var h in handles) h?.Dispose();
        }
    }

    private static void PrefillNone<TKey2, TValue2>(global::Tsavorite.Collections.IDictionary<TKey2,TValue2> _)
        where TKey2 : IEquatable<TKey2>
        where TValue2 : IEquatable<TValue2> { }

    private void PrefillAll(global::Tsavorite.Collections.IDictionary<TKey,TValue> dict) {
        var n = Scaled;
        for(var i = 0; i<n; i++) dict.Add(this.CreateKey(i),this.CreateValue(i));
    }

    // =============================================================
    // ITsavoriteDictionary / IDictionary<TKey,TValue> (直接 API)
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Add(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            PrefillNone,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict.Add(this.CreateKey(i),this.CreateValue(i)); },
            nameof(Dictionary_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Remove_Key(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict.Remove(this.CreateKey(i)); },
            nameof(Dictionary_Remove_Key));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Remove_Key_Value(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict.Remove(this.CreateKey(i),out _); },
            nameof(Dictionary_Remove_Key_Value));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Clear(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => dict.Clear(),
            nameof(Dictionary_Clear));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_TryGetValue(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) _=dict.TryGetValue(this.CreateKey(i),out _); },
            nameof(Dictionary_TryGetValue));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Contains_KeyValuePair(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Contains(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(Dictionary_Contains_KeyValuePair));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_ContainsKey(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) _=dict.ContainsKey(this.CreateKey(i)); },
            nameof(Dictionary_ContainsKey));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Foreach(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => { foreach(var _ in dict) { } },
            nameof(Dictionary_Foreach));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Count(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=dict.Count; },
            nameof(Dictionary_Count));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Keys(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=dict.Keys; },
            nameof(Dictionary_Keys));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Values(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=dict.Values; },
            nameof(Dictionary_Values));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_ToList(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => {
                var list = new List<KeyValuePair<TKey,TValue>>((int)Scaled);
                foreach(var kv in dict) list.Add(kv);
            },
            nameof(Dictionary_ToList));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Indexer_Get(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) _=dict[this.CreateKey(i)]; },
            nameof(Dictionary_Indexer_Get));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Indexer_Set(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict[this.CreateKey(i)]=this.CreateValue(i); },
            nameof(Dictionary_Indexer_Set));

    // =============================================================
    // IDictionary<TKey,TValue> 明示実装経由
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Add(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            PrefillNone,
            dict => {
                var id = (global::Tsavorite.Collections.IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) id.Add(this.CreateKey(i),this.CreateValue(i));
            },
            nameof(IDictionary_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_ContainsKey(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var id = (global::Tsavorite.Collections.IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=id.ContainsKey(this.CreateKey(i));
            },
            nameof(IDictionary_ContainsKey));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Remove(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var id = (global::Tsavorite.Collections.IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=id.Remove(this.CreateKey(i));
            },
            nameof(IDictionary_Remove));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_TryGetValue(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var id = (IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=id.TryGetValue(this.CreateKey(i),out _);
            },
            nameof(IDictionary_TryGetValue));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Keys(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=((global::Tsavorite.Collections.IDictionary<TKey,TValue>)dict).Keys; },
            nameof(IDictionary_Keys));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Values(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=((global::Tsavorite.Collections.IDictionary<TKey,TValue>)dict).Values; },
            nameof(IDictionary_Values));

    // =============================================================
    // ICollection<KeyValuePair<TKey,TValue>> 経由
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Add(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            PrefillNone,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) c.Add(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(ICollection_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Clear(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => ((ICollection<KeyValuePair<TKey,TValue>>)dict).Clear(),
            nameof(ICollection_Clear));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Contains(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Contains(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(ICollection_Contains));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_CopyTo(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var buf = new KeyValuePair<TKey,TValue>[(int)Scaled];
                c.CopyTo(buf,0);
            },
            nameof(ICollection_CopyTo));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Remove(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Remove(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(ICollection_Remove));

    // =============================================================
    // IReadOnlyCollection / IReadOnlyDictionary
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyCollection_Count(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyCollection<KeyValuePair<TKey,TValue>>)dict).Count; },
            nameof(IReadOnlyCollection_Count));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_ContainsKey(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var ro = (IReadOnlyDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=ro.ContainsKey(this.CreateKey(i));
            },
            nameof(IReadOnlyDictionary_ContainsKey));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_TryGetValue(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var ro = (IReadOnlyDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=ro.TryGetValue(this.CreateKey(i),out _);
            },
            nameof(IReadOnlyDictionary_TryGetValue));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Indexer(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,
            this.PrefillAll,
            dict => {
                var ro = (IReadOnlyDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=ro[this.CreateKey(i)];
            },
            nameof(IReadOnlyDictionary_Indexer));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Keys(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyDictionary<TKey,TValue>)dict).Keys; },
            nameof(IReadOnlyDictionary_Keys));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Values(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyDictionary<TKey,TValue>)dict).Values; },
            nameof(IReadOnlyDictionary_Values));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Count(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyDictionary<TKey,TValue>)dict).Count; },
            nameof(IReadOnlyDictionary_Count));
}

// ============================================================================
// 具体派生: Managed と Unmanaged の 2 パターン
// ============================================================================

[Trait("Category","Benchmark"), Trait("Kind","Managed"), Trait("Collection","Dictionary")]
public sealed class ManagedDictionaryBenchmark(ITestOutputHelper output):DictionaryBenchmark<string,string>(output) {
    protected override string CreateKey(int seed) => $"K{seed}";
    protected override string CreateValue(int seed) => $"V{seed}";
    protected override Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
        return new H(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
    }
    private sealed class H:Handle {
        private readonly DictionaryStore<string,string> store;
        private readonly global::Tsavorite.Collections.IDictionary<string,string> dict;
        private readonly long indexSize;
        private readonly long segmentSize;
        private readonly int memorySizeBits;
        private readonly int pageSizeBits;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        private static int directorySequence;
        public H(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
            this.indexSize=indexSize;
            this.segmentSize=segmentSize;
            this.memorySizeBits=memorySizeBits;
            this.pageSizeBits=pageSizeBits;
            var directoryId = Interlocked.Increment(ref directorySequence);
            this.rootDir=BenchmarkStorePath.Create<ManagedDictionaryBenchmark>($"h{directoryId:D4}");
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
            ).Create<string,string>(this.logDevice,this.objDevice,new MemoryPackSerializerStrategy(),new MemoryPackSerializerStrategy());
            this.dict=this.store.Create();
        }
        public override global::Tsavorite.Collections.IDictionary<string,string> Dict => this.dict;
        public override void Dispose() {
            try {
                this.dict.Dispose();
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
    }
}

[Trait("Category","Benchmark"), Trait("Kind","Unmanaged"), Trait("Collection","Dictionary")]
public sealed class UnmanagedDictionaryBenchmark(ITestOutputHelper output):DictionaryBenchmark<int,int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed;
    protected override Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
        return new H(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
    }
    private sealed class H:Handle {
        private readonly DictionaryStore<int,int> store;
        private readonly global::Tsavorite.Collections.IDictionary<int,int> dict;
        private readonly long indexSize;
        private readonly long segmentSize;
        private readonly int memorySizeBits;
        private readonly int pageSizeBits;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        private static int directorySequence;
        public H(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
            this.indexSize=indexSize;
            this.segmentSize=segmentSize;
            this.memorySizeBits=memorySizeBits;
            this.pageSizeBits=pageSizeBits;
            var directoryId = Interlocked.Increment(ref directorySequence);
            this.rootDir=BenchmarkStorePath.Create<UnmanagedDictionaryBenchmark>($"h{directoryId:D4}");
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
            ).Create<int,int>(this.logDevice,this.objDevice,new BlittableStrategy(),new BlittableStrategy());
            this.dict=this.store.Create();
        }
        public override global::Tsavorite.Collections.IDictionary<int,int> Dict => this.dict;
        public override void Dispose() {
            try {
                this.dict.Dispose();
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
    }
}
