using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;

namespace BenchmarkExe.Benchmarks;

public enum CollectionImplKind {
    Managed,
    Unmanaged,
}

public enum RegionKind {
    Mutable,
    Immutable,
    Storage,
}

public enum DictionaryOperation {
    Add,
    Remove,
    ContainsKey,
    TryGetValue,
    IndexerGet,
    IndexerSet,
    TryAdd,
    RemoveWithValue,
    ICollectionContains,
    ICollectionCopyTo,
    IEnumerableEnumerate,
    KeysCount,
    ValuesCount,
    Count,
    EntryCount,
    Save,
    Load,
    Clear,
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 1,iterationCount: 5)]
public class DictionaryApiSurfaceBenchmark {
    private int[] intKeys = null!;
    private string[] stringKeys = null!;
    private string[] stringValues = null!;
    private string checkpointRoot = null!;

    private Seamless.Collections.IDictionary<int,int>? unmanagedDict;
    private Seamless.Collections.IDictionary<string,string>? managedDict;
    private UnmanagedDictionaryStore<int,int>? unmanagedStore;
    private ManagedDictionaryStore<string,string>? managedStore;
    private IDevice? logDevice;
    private IDevice? objDevice;
    private string? loadCheckpointDir;
    private Guid loadToken;

    [Params(10_000)]
    public int CountValue { get; set; }

    [Params(CollectionImplKind.Managed,CollectionImplKind.Unmanaged)]
    public CollectionImplKind Impl { get; set; }

    [Params(RegionKind.Mutable,RegionKind.Immutable,RegionKind.Storage)]
    public RegionKind Region { get; set; }

    [Params(
        DictionaryOperation.Add,
        DictionaryOperation.Remove,
        DictionaryOperation.ContainsKey,
        DictionaryOperation.TryGetValue,
        DictionaryOperation.IndexerGet,
        DictionaryOperation.IndexerSet,
        DictionaryOperation.TryAdd,
        DictionaryOperation.RemoveWithValue,
        DictionaryOperation.ICollectionContains,
        DictionaryOperation.ICollectionCopyTo,
        DictionaryOperation.IEnumerableEnumerate,
        DictionaryOperation.KeysCount,
        DictionaryOperation.ValuesCount,
        DictionaryOperation.Count,
        DictionaryOperation.EntryCount,
        DictionaryOperation.Save,
        DictionaryOperation.Load,
        DictionaryOperation.Clear)]
    public DictionaryOperation Operation { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        this.intKeys=new int[this.CountValue];
        this.stringKeys=new string[this.CountValue];
        this.stringValues=new string[this.CountValue];
        for(var i = 0; i<this.CountValue; i++) {
            this.intKeys[i]=i;
            this.stringKeys[i]="k"+i;
            this.stringValues[i]="v"+i;
        }

        this.checkpointRoot=BenchmarkOutputLayout.CreateCategoryWorkspaceRoot("Dictionary");
        Directory.CreateDirectory(this.checkpointRoot);
    }

    [IterationSetup]
    public void IterationSetup() {
        this.DisposeIterationResources();

        var indexSize = BenchmarkHashStoreSizing.GetIndexSize(this.CountValue);
        var memorySize = BenchmarkHashStoreSizing.GetMemorySize(this.CountValue*128L);
        var mutableFraction = this.Region==RegionKind.Immutable ? 0.2 : 1.0;

        var useStorage = this.Region==RegionKind.Storage||this.Operation is DictionaryOperation.Save or DictionaryOperation.Load;
        string? checkpointDir = null;
        if(useStorage) {
            checkpointDir=Path.Combine(this.checkpointRoot,"iter-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(checkpointDir);
            var logPath = Path.Combine(checkpointDir,"log");
            var objPath = Path.Combine(checkpointDir,"obj");
            this.logDevice=Devices.CreateLogDevice(logPath);
            this.objDevice=Devices.CreateLogDevice(objPath);
        }

        var factory = HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: mutableFraction,CheckpointDir: checkpointDir);

        if(this.Impl==CollectionImplKind.Unmanaged) {
            this.unmanagedStore=useStorage
                ? factory.Create<int,int>(this.logDevice!,this.objDevice!)
                : factory.Create<int,int>();
            this.unmanagedDict=this.unmanagedStore.Create();
            this.PrepareUnmanagedData();
        }
        else {
            this.managedStore=useStorage
                ? factory.Create<string,string>(this.logDevice!,this.objDevice!,new ManagedTag(),new ManagedTag())
                : factory.Create<string,string>(new ManagedTag(),new ManagedTag());
            this.managedDict=this.managedStore.Create();
            this.PrepareManagedData();
        }

        if(this.Region==RegionKind.Immutable) {
            if(this.unmanagedStore is not null) {
                this.unmanagedStore.Store.Log.FlushAndEvict(wait: true);
            }
            if(this.managedStore is not null) {
                this.managedStore.Store.Log.FlushAndEvict(wait: true);
            }
        }

        if(this.Operation==DictionaryOperation.Load) {
            this.PrepareLoadCheckpoint();
        }
    }

    [IterationCleanup]
    public void IterationCleanup() {
        this.DisposeIterationResources();
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        this.DisposeIterationResources();
        if(Directory.Exists(this.checkpointRoot)) {
            Directory.Delete(this.checkpointRoot,recursive: true);
        }
    }

    [Benchmark]
    public long Run() {
        return this.Impl==CollectionImplKind.Unmanaged
            ? this.RunUnmanaged()
            : this.RunManaged();
    }

    private void PrepareUnmanagedData() {
        if(this.unmanagedDict is null) {
            return;
        }

        if(this.Operation is DictionaryOperation.Add) {
            return;
        }

        for(var i = 0; i<this.intKeys.Length; i++) {
            this.unmanagedDict[this.intKeys[i]]=this.intKeys[i]*2;
        }
    }

    private void PrepareManagedData() {
        if(this.managedDict is null) {
            return;
        }

        if(this.Operation is DictionaryOperation.Add) {
            return;
        }

        for(var i = 0; i<this.stringKeys.Length; i++) {
            this.managedDict[this.stringKeys[i]]=this.stringValues[i];
        }
    }

    private void PrepareLoadCheckpoint() {
        this.loadCheckpointDir=Path.Combine(this.checkpointRoot,"load-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.loadCheckpointDir);

        var logPath = Path.Combine(this.loadCheckpointDir,"log");
        var objPath = Path.Combine(this.loadCheckpointDir,"obj");
        using var log = Devices.CreateLogDevice(logPath);
        using var obj = Devices.CreateLogDevice(objPath);

        var indexSize = BenchmarkHashStoreSizing.GetIndexSize(this.CountValue);
        var memorySize = BenchmarkHashStoreSizing.GetMemorySize(this.CountValue*128L);
        var factory = HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: 1.0,CheckpointDir: this.loadCheckpointDir);

        if(this.Impl==CollectionImplKind.Unmanaged) {
            using var store = factory.Create<int,int>(log,obj);
            using var dict = store.Create();
            foreach(var t in this.intKeys){
                dict[t]=t*2;
            }
            this.loadToken=store.Save();
        }
        else {
            using var store = factory.Create<string,string>(log,obj,new ManagedTag(),new ManagedTag());
            using var dict = store.Create();
            for(var i = 0; i<this.stringKeys.Length; i++) {
                dict[this.stringKeys[i]]=this.stringValues[i];
            }
            this.loadToken=store.Save();
        }
    }

    private long RunUnmanaged() {
        var dict = this.unmanagedDict!;
        var iDict = (System.Collections.Generic.IDictionary<int,int>)dict;
        var ro = (IReadOnlyDictionary<int,int>)dict;
        var coll = (ICollection<KeyValuePair<int,int>>)dict;

        return this.Operation switch {
            DictionaryOperation.Add => this.RunLoop(() => {
                dict.Clear();
                foreach(var t in this.intKeys){
                    dict.Add(t,t);
                }
                return dict.Count;
            }),
            DictionaryOperation.Remove => this.RunLoop(() => {
                var removed = 0;
                foreach(var t in this.intKeys){
                    if(dict.Remove(t)) {
                        removed++;
                    }
                }
                return removed;
            }),
            DictionaryOperation.ContainsKey => this.RunLoop(() => this.CountTrue(this.intKeys,dict.ContainsKey)),
            DictionaryOperation.TryGetValue => this.RunLoop(() => this.CountTryGet(this.intKeys,key => dict.TryGetValue(key,out _))),
            DictionaryOperation.IndexerGet => this.RunLoop(() => this.SumInt(this.intKeys,key => dict[key])),
            DictionaryOperation.IndexerSet => this.RunLoop(() =>{
                foreach(var t in this.intKeys){
                    iDict[t]=t+1;
                }
                return iDict.Count;
            }),
            DictionaryOperation.TryAdd => this.RunLoop(() => {
                var ok = 0;
                dict.Clear();
                foreach(var t in this.intKeys){
                    if(dict.TryAdd(t,t)==TryAddResult.Success) {
                        ok++;
                    }
                }
                return ok;
            }),
            DictionaryOperation.RemoveWithValue => this.RunLoop(() => {
                var ok = 0;
                foreach(var t in this.intKeys){
                    if(dict.Remove(t,out _)) {
                        ok++;
                    }
                }
                return ok;
            }),
            DictionaryOperation.ICollectionContains => this.RunLoop(() => this.CountContainsKv(coll,this.intKeys)),
            DictionaryOperation.ICollectionCopyTo => this.RunLoop(() => {
                var dst = new KeyValuePair<int,int>[dict.Count];
                coll.CopyTo(dst,0);
                return dst.Length;
            }),
            DictionaryOperation.IEnumerableEnumerate => this.RunLoop(() => this.EnumerateCount(dict)),
            DictionaryOperation.KeysCount => this.RunLoop(() => iDict.Keys.Count),
            DictionaryOperation.ValuesCount => this.RunLoop(() => iDict.Values.Count),
            DictionaryOperation.Count => this.RunLoop(() => ro.Count),
            DictionaryOperation.EntryCount => this.RunLoop(() => (int)dict.EntryCount),
            DictionaryOperation.Save => this.RunLoop(() => this.SaveCurrent()),
            DictionaryOperation.Load => this.RunLoop(() => this.LoadCurrent()),
            DictionaryOperation.Clear => this.RunLoop(() => {
                dict.Clear();
                return dict.Count;
            }),
            _ => 0,
        };
    }

    private long RunManaged() {
        var dict = this.managedDict!;
        var iDict = (System.Collections.Generic.IDictionary<string,string>)dict;
        var ro = (IReadOnlyDictionary<string,string>)dict;
        var coll = (ICollection<KeyValuePair<string,string>>)dict;

        return this.Operation switch {
            DictionaryOperation.Add => this.RunLoop(() => {
                dict.Clear();
                for(var i = 0; i<this.stringKeys.Length; i++) {
                    dict.Add(this.stringKeys[i],this.stringValues[i]);
                }
                return dict.Count;
            }),
            DictionaryOperation.Remove => this.RunLoop(() => {
                var removed = 0;
                for(var i = 0; i<this.stringKeys.Length; i++) {
                    if(dict.Remove(this.stringKeys[i])) {
                        removed++;
                    }
                }
                return removed;
            }),
            DictionaryOperation.ContainsKey => this.RunLoop(() => this.CountTrue(this.stringKeys,dict.ContainsKey)),
            DictionaryOperation.TryGetValue => this.RunLoop(() => this.CountTryGet(this.stringKeys,key => dict.TryGetValue(key,out _))),
            DictionaryOperation.IndexerGet => this.RunLoop(() => this.SumInt(this.stringKeys,key => dict[key].Length)),
            DictionaryOperation.IndexerSet => this.RunLoop(() => {
                for(var i = 0; i<this.stringKeys.Length; i++) {
                    iDict[this.stringKeys[i]]=this.stringValues[i]+"x";
                }
                return iDict.Count;
            }),
            DictionaryOperation.TryAdd => this.RunLoop(() => {
                var ok = 0;
                dict.Clear();
                for(var i = 0; i<this.stringKeys.Length; i++) {
                    if(dict.TryAdd(this.stringKeys[i],this.stringValues[i])==TryAddResult.Success) {
                        ok++;
                    }
                }
                return ok;
            }),
            DictionaryOperation.RemoveWithValue => this.RunLoop(() => {
                var ok = 0;
                for(var i = 0; i<this.stringKeys.Length; i++) {
                    if(dict.Remove(this.stringKeys[i],out _)) {
                        ok++;
                    }
                }
                return ok;
            }),
            DictionaryOperation.ICollectionContains => this.RunLoop(() => this.CountContainsKv(coll,this.stringKeys,this.stringValues)),
            DictionaryOperation.ICollectionCopyTo => this.RunLoop(() => {
                var dst = new KeyValuePair<string,string>[dict.Count];
                coll.CopyTo(dst,0);
                return dst.Length;
            }),
            DictionaryOperation.IEnumerableEnumerate => this.RunLoop(() => this.EnumerateCount(dict)),
            DictionaryOperation.KeysCount => this.RunLoop(() => iDict.Keys.Count),
            DictionaryOperation.ValuesCount => this.RunLoop(() => iDict.Values.Count),
            DictionaryOperation.Count => this.RunLoop(() => ro.Count),
            DictionaryOperation.EntryCount => this.RunLoop(() => (int)dict.EntryCount),
            DictionaryOperation.Save => this.RunLoop(() => this.SaveCurrent()),
            DictionaryOperation.Load => this.RunLoop(() => this.LoadCurrent()),
            DictionaryOperation.Clear => this.RunLoop(() => {
                dict.Clear();
                return dict.Count;
            }),
            _ => 0,
        };
    }

    private long RunLoop(Func<int> action) {
        return action();
    }

    private int CountTrue<T>(T[] keys,Func<T,bool> predicate) {
        var c = 0;
        for(var i = 0; i<keys.Length; i++) {
            if(predicate(keys[i])) {
                c++;
            }
        }
        return c;
    }

    private int CountTryGet<T>(T[] keys,Func<T,bool> tryGet) {
        var c = 0;
        for(var i = 0; i<keys.Length; i++) {
            if(tryGet(keys[i])) {
                c++;
            }
        }
        return c;
    }

    private int SumInt<T>(T[] keys,Func<T,int> getter) {
        var sum = 0;
        for(var i = 0; i<keys.Length; i++) {
            sum+=getter(keys[i]);
        }
        return sum;
    }

    private int EnumerateCount<TKey, TValue>(IEnumerable<KeyValuePair<TKey,TValue>> e) {
        var c = 0;
        foreach(var _ in e) {
            c++;
        }
        return c;
    }

    private int CountContainsKv(ICollection<KeyValuePair<int,int>> coll,int[] keys) {
        var c = 0;
        for(var i = 0; i<keys.Length; i++) {
            if(coll.Contains(new KeyValuePair<int,int>(keys[i],keys[i]*2))) {
                c++;
            }
        }
        return c;
    }

    private int CountContainsKv(ICollection<KeyValuePair<string,string>> coll,string[] keys,string[] values) {
        var c = 0;
        for(var i = 0; i<keys.Length; i++) {
            if(coll.Contains(new KeyValuePair<string,string>(keys[i],values[i]))) {
                c++;
            }
        }
        return c;
    }

    private int SaveCurrent() {
        if(this.unmanagedStore is not null) {
            this.unmanagedStore.Save();
            return this.unmanagedDict?.Count??0;
        }
        if(this.managedStore is not null) {
            this.managedStore.Save();
            return this.managedDict?.Count??0;
        }
        return 0;
    }

    private int LoadCurrent() {
        if(this.loadCheckpointDir is null||this.loadToken==Guid.Empty) {
            return 0;
        }

        var logPath = Path.Combine(this.loadCheckpointDir,"log");
        var objPath = Path.Combine(this.loadCheckpointDir,"obj");
        using var log = Devices.CreateLogDevice(logPath);
        using var obj = Devices.CreateLogDevice(objPath);

        var indexSize = BenchmarkHashStoreSizing.GetIndexSize(this.CountValue);
        var memorySize = BenchmarkHashStoreSizing.GetMemorySize(this.CountValue*128L);
        var factory = HashStore.Create(IndexSize: indexSize,MemorySize: memorySize,MutableFraction: 1.0,CheckpointDir: this.loadCheckpointDir)
            .WithRecoveryIndexSize(this.loadToken);

        if(this.Impl==CollectionImplKind.Unmanaged) {
            using var store = factory.Create<int,int>(log,obj);
            store.Load(this.loadToken);
            using var dict = store.Create();
            return dict.Count;
        }
        using var mstore = factory.Create<string,string>(log,obj,new ManagedTag(),new ManagedTag());
        {
            mstore.Load(this.loadToken);
            using var dict = mstore.Create();
            return dict.Count;
        }
    }

    private void DisposeIterationResources() {
        this.unmanagedDict?.Dispose();
        this.unmanagedDict=null;
        this.managedDict?.Dispose();
        this.managedDict=null;

        this.unmanagedStore?.Dispose();
        this.unmanagedStore=null;
        this.managedStore?.Dispose();
        this.managedStore=null;

        this.logDevice?.Dispose();
        this.logDevice=null;
        this.objDevice?.Dispose();
        this.objDevice=null;

        if(this.loadCheckpointDir is not null&&Directory.Exists(this.loadCheckpointDir)) {
            Directory.Delete(this.loadCheckpointDir,recursive: true);
        }
        this.loadCheckpointDir=null;
        this.loadToken=Guid.Empty;
    }
}
