using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace Test.Seamless.Benchmarks;

/// <summary>
/// Dictionary ベンチマークの共通土台。
/// <see cref="ISeamlessDictionary{TKey,TValue}"/> /
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
    protected abstract Handle CreateHandle(double mutableFraction);

    protected abstract class Handle:IDisposable {
        public abstract ISeamlessDictionary<TKey,TValue> Dict { get; }
        public abstract void Dispose();
    }

    // ---- 各スレッドが自身で Setup して計測部へ入る共通ドライバ ----
    private void Drive(
        int parallel,
        double mutableFraction,
        long opCount,
        Action<ISeamlessDictionary<TKey,TValue>> prefill,
        Action<ISeamlessDictionary<TKey,TValue>> measured,
        string label
    ) {
        var handles = new Handle?[parallel];
        try {
            this.RunMeasured(
                parallel,
                mutableFraction,
                opCount,
                setup: id => {
                    handles[id]=this.CreateHandle(mutableFraction);
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

    private static void PrefillNone<TKey2, TValue2>(ISeamlessDictionary<TKey2,TValue2> _)
        where TKey2 : IEquatable<TKey2>
        where TValue2 : IEquatable<TValue2> { }

    private void PrefillAll(ISeamlessDictionary<TKey,TValue> dict) {
        var n = Scaled;
        for(var i = 0; i<n; i++) dict.Add(this.CreateKey(i),this.CreateValue(i));
    }

    // =============================================================
    // ISeamlessDictionary / IDictionary<TKey,TValue> (直接 API)
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Add(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            PrefillNone,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict.Add(this.CreateKey(i),this.CreateValue(i)); },
            nameof(this.Dictionary_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Remove_Key(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict.Remove(this.CreateKey(i)); },
            nameof(this.Dictionary_Remove_Key));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Remove_Key_Value(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict.Remove(this.CreateKey(i),out _); },
            nameof(this.Dictionary_Remove_Key_Value));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Clear(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => dict.Clear(),
            nameof(this.Dictionary_Clear));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_TryGetValue(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) _=dict.TryGetValue(this.CreateKey(i),out _); },
            nameof(this.Dictionary_TryGetValue));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Contains_KeyValuePair(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Contains(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(this.Dictionary_Contains_KeyValuePair));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_ContainsKey(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) _=dict.ContainsKey(this.CreateKey(i)); },
            nameof(this.Dictionary_ContainsKey));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Foreach(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => { foreach(var _ in dict) { } },
            nameof(this.Dictionary_Foreach));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Count(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=dict.Count; },
            nameof(this.Dictionary_Count));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Keys(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=dict.Keys; },
            nameof(this.Dictionary_Keys));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Values(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=dict.Values; },
            nameof(this.Dictionary_Values));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_ToList(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => {
                var list = new List<KeyValuePair<TKey,TValue>>((int)Scaled);
                foreach(var kv in dict) list.Add(kv);
            },
            nameof(this.Dictionary_ToList));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Indexer_Get(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) _=dict[this.CreateKey(i)]; },
            nameof(this.Dictionary_Indexer_Get));

    [Theory, MemberData(nameof(Matrix))]
    public void Dictionary_Indexer_Set(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => { var n = Scaled; for(var i = 0; i<n; i++) dict[this.CreateKey(i)]=this.CreateValue(i); },
            nameof(this.Dictionary_Indexer_Set));

    // =============================================================
    // IDictionary<TKey,TValue> 明示実装経由
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Add(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            PrefillNone,
            dict => {
                var id = (IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) id.Add(this.CreateKey(i),this.CreateValue(i));
            },
            nameof(this.IDictionary_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_ContainsKey(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var id = (IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=id.ContainsKey(this.CreateKey(i));
            },
            nameof(this.IDictionary_ContainsKey));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Remove(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var id = (IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=id.Remove(this.CreateKey(i));
            },
            nameof(this.IDictionary_Remove));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_TryGetValue(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var id = (IDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=id.TryGetValue(this.CreateKey(i),out _);
            },
            nameof(this.IDictionary_TryGetValue));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Keys(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=((IDictionary<TKey,TValue>)dict).Keys; },
            nameof(this.IDictionary_Keys));

    [Theory, MemberData(nameof(Matrix))]
    public void IDictionary_Values(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=((IDictionary<TKey,TValue>)dict).Values; },
            nameof(this.IDictionary_Values));

    // =============================================================
    // ICollection<KeyValuePair<TKey,TValue>> 経由
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Add(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            PrefillNone,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) c.Add(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(this.ICollection_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Clear(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => ((ICollection<KeyValuePair<TKey,TValue>>)dict).Clear(),
            nameof(this.ICollection_Clear));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Contains(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Contains(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(this.ICollection_Contains));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_CopyTo(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var buf = new KeyValuePair<TKey,TValue>[(int)Scaled];
                c.CopyTo(buf,0);
            },
            nameof(this.ICollection_CopyTo));

    [Theory, MemberData(nameof(Matrix))]
    public void ICollection_Remove(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var c = (ICollection<KeyValuePair<TKey,TValue>>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=c.Remove(new KeyValuePair<TKey,TValue>(this.CreateKey(i),this.CreateValue(i)));
            },
            nameof(this.ICollection_Remove));

    // =============================================================
    // IReadOnlyCollection / IReadOnlyDictionary
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyCollection_Count(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyCollection<KeyValuePair<TKey,TValue>>)dict).Count; },
            nameof(this.IReadOnlyCollection_Count));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_ContainsKey(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var ro = (IReadOnlyDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=ro.ContainsKey(this.CreateKey(i));
            },
            nameof(this.IReadOnlyDictionary_ContainsKey));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_TryGetValue(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var ro = (IReadOnlyDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=ro.TryGetValue(this.CreateKey(i),out _);
            },
            nameof(this.IReadOnlyDictionary_TryGetValue));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Indexer(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,Scaled,
            this.PrefillAll,
            dict => {
                var ro = (IReadOnlyDictionary<TKey,TValue>)dict;
                var n = Scaled;
                for(var i = 0; i<n; i++) _=ro[this.CreateKey(i)];
            },
            nameof(this.IReadOnlyDictionary_Indexer));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Keys(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyDictionary<TKey,TValue>)dict).Keys; },
            nameof(this.IReadOnlyDictionary_Keys));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Values(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyDictionary<TKey,TValue>)dict).Values; },
            nameof(this.IReadOnlyDictionary_Values));

    [Theory, MemberData(nameof(Matrix))]
    public void IReadOnlyDictionary_Count(int parallel,double mutableFraction)
        => this.Drive(parallel,mutableFraction,1,
            this.PrefillAll,
            dict => { _=((IReadOnlyDictionary<TKey,TValue>)dict).Count; },
            nameof(this.IReadOnlyDictionary_Count));
}

// ============================================================================
// 具体派生: Managed と Unmanaged の 2 パターン
// ============================================================================

[Trait("Kind","Managed"), Trait("Collection","Dictionary")]
public sealed class ManagedDictionaryBenchmark(ITestOutputHelper output):DictionaryBenchmark<string,string>(output) {
    protected override string CreateKey(int seed) => $"K{seed}";
    protected override string CreateValue(int seed) => $"V{seed}";
    protected override Handle CreateHandle(double mutableFraction) {
        return new H(mutableFraction);
    }
    private sealed class H:Handle {
        private readonly ManagedManagedDictionaryStore<string,string> store;
        private readonly ISeamlessDictionary<string,string> dict;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        public H(double mutableFraction) {
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
            ).Create<string,string>(this.logDevice,this.objDevice,new ManagedTag(),new ManagedTag());
            this.dict=this.store.Create();
        }
        public override ISeamlessDictionary<string,string> Dict => this.dict;
        public override void Dispose() {
            this.dict.Dispose();
            this.store.Store.Log.FlushAndEvict(wait: true);
            this.store.Dispose();
            this.logDevice.Dispose();
            this.objDevice.Dispose();
            try { Directory.Delete(this.rootDir,true); } catch { }
        }
    }
}

[Trait("Kind","Unmanaged"), Trait("Collection","Dictionary")]
public sealed class UnmanagedDictionaryBenchmark(ITestOutputHelper output):DictionaryBenchmark<int,int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed;
    protected override Handle CreateHandle(double mutableFraction) {
        return new H(mutableFraction);
    }
    private sealed class H:Handle {
        private readonly UnmanagedUnmanagedDictionaryStore<int,int> store;
        private readonly ISeamlessDictionary<int,int> dict;
        private readonly IDevice logDevice;
        private readonly IDevice objDevice;
        private readonly string rootDir;
        public H(double mutableFraction) {
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
            ).Create<int,int>(this.logDevice,this.objDevice,new UnmanagedTag(),new UnmanagedTag());
            this.dict=this.store.Create();
        }
        public override ISeamlessDictionary<int,int> Dict => this.dict;
        public override void Dispose() {
            this.dict.Dispose();
            this.store.Store.Log.FlushAndEvict(wait: true);
            this.store.Dispose();
            this.logDevice.Dispose();
            this.objDevice.Dispose();
            try { Directory.Delete(this.rootDir,true); } catch { }
        }
    }
}
